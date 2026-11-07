using AutoRetainer.Scheduler.Tasks;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using AutoRetainer.Internal.InventoryManagement;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
    private static readonly PolicyCleanupCycle cleanup=new();
    private static bool cleanupAwaiting;
    private static bool cleanupTaskFinished;
    private static long cleanupStarted;
    private static long cleanupIdleSince;
    private static bool cleanupGcStarted;
    private static bool cleanupGcFinished;
    internal static bool CleanupActive=>cleanup.Active;
    internal static void CleanupDisposalConfirmed()=>cleanup.ConfirmDisposal(1);
    private static string CleanupStatus=>$"{cleanup.Message}; batch {cleanup.Pass}, {cleanup.Phase}; total withdrawn {cleanup.TotalWithdrawn}, confirmed disposal {cleanup.TotalDisposed}";
    private static bool CleanupAllowed(ulong cid)
    {
        if(!Connected(cid)||HasFault||Data.GetIMSettings().IMDry)return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,bool>("Steward.InventoryPolicy.CleanupAllowed").InvokeFunc(cid);}
        catch{return false;}
    }
    internal static bool CleanupRetainerAllowed(ulong cid,ulong retainer)
    {
        if(!CleanupAllowed(cid)||C.CompanionGilOnlyRetainers.Contains(retainer))return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,ulong,bool>("Steward.InventoryPolicy.CleanupRetainerAllowed").InvokeFunc(cid,retainer);}
        catch{return false;}
    }
    private static string cleanupMethod="All";
    private static bool cleanupCarried;
    private static bool MethodInScope(string method)=>!cleanup.Active||cleanupMethod=="All"||method==cleanupMethod;
    internal static bool MethodEnabled(string method) => method switch
    {
        "Gc"=>Data.GCDeliveryType!=AutoRetainerAPI.Configuration.GCDeliveryType.Disabled,
        "Desynth"=>Data.GetIMSettings().IMEnableItemDesynthesis,
        "Vendor"=>Data.GetIMSettings().IMEnableAutoVendor||Data.GetIMSettings().IMEnableNpcSell,
        "All"=>MethodEnabled("Gc")||MethodEnabled("Desynth")||MethodEnabled("Vendor"),
        _=>false
    };
    private static bool StartCleanup(ulong cid)=>StartScopedCleanup(cid,"RetainersAll");
    private static bool StartScopedCleanup(ulong cid,string scope)
    {
        if(cleanup.Active)return false;
        bool Decline(string reason){cleanup.Stop(reason);return false;}
        var carried=scope.StartsWith("Carried",StringComparison.Ordinal);
        if(!carried&&!scope.StartsWith("Retainers",StringComparison.Ordinal))return Decline("Unknown inventory scope");
        var method=scope[(carried?7:9)..];
        if(!MethodEnabled(method))return Decline("Requested disposal method disabled");
        if(!CleanupAllowed(cid))return Decline("Enable Steward disposal/automation, turn off Demo mode and resolve any review hold");
        if(Utils.IsBusy||SchedulerMain.PluginEnabled||MultiMode.Active)return Decline("Wait for or stop AutoRetainer's existing run before starting full cleanup");
        if(IsOccupied())return Decline("Finish the current interaction and close retainer, shop and confirmation windows before starting full cleanup");
        if(!Player.IsInHomeWorld)return Decline("Return to your home world before starting full cleanup");
        if(!carried && (method is "All" or "Gc") && MethodEnabled("Gc") && !Data.GetIsTeleportEnabledForRetainers())
            return Decline("Enable AutoRetainer's retainer teleport/return travel so the GC batch can return to a bell");
        cleanupMethod=method;cleanupCarried=carried;cleanup.Start(cid);cleanupAwaiting=false;cleanupTaskFinished=false;cleanupIdleSince=0;
        Svc.Log.Information("[Steward cleanup] Started for character {Character}",cid);
        return true;
    }
    private static bool StopCleanup(ulong cid)
    {
        if(!cleanup.Active||cleanup.Character!=cid)return false;
        EndCleanup("Stopped by user",false);return true;
    }
    private static void EndCleanup(string reason,bool fault)
    {
        if(!cleanup.Active)return;
        var gc=cleanup.Phase==CleanupPhase.Gc;
        var travel=cleanup.Phase is CleanupPhase.Bell or CleanupPhase.Return or CleanupPhase.Gc;
        cleanup.Stop(reason);cleanupAwaiting=false;
        P.TaskManager.Abort();
        if(travel)
            try{if(Lifestream.IsBusy())Lifestream.Abort();}
            catch(Exception ex){Svc.Log.Warning(ex,"Could not cancel cleanup travel; item actions remain stopped");}
        if(gc)AutoGCHandin.Operation=false;
        Svc.Log.Warning("[Steward cleanup] {Reason}",reason);
        if(fault)ReportFault("Batch cleanup: "+reason);
    }
    internal static void CleanupGcCompleted(bool capacityBlocked=false)
    {
        if(!cleanup.Active||cleanup.Phase!=CleanupPhase.Gc)return;
        if(capacityBlocked){EndCleanup("GC seal capacity reached; spend seals or configure AutoRetainer continuation",false);return;}
        cleanupGcFinished=true;
    }
    private static bool CleanupWithdrawalConfirmed(ulong cid,ulong retainer,uint quantity)
    {
        if(!cleanup.Active||cleanup.Character!=cid||cleanup.Phase!=CleanupPhase.Withdraw||!OwnsHandoff(cid)||!CleanupRetainerAllowed(cid,retainer)||quantity==0)return false;
        cleanup.ConfirmWithdrawal(quantity);return true;
    }
    private static void TickCleanup()
    {
        if(!cleanup.Active)return;
        if(cleanup.Character!=Svc.ClientState.LocalContentId||!CleanupAllowed(cleanup.Character))
        {EndCleanup("Execution disabled, demo mode enabled, character changed or Steward permission unavailable",false);return;}
        if(cleanupAwaiting)
        {
            if(Environment.TickCount64-cleanupStarted>15*60*1000){EndCleanup("Timed out waiting for AutoRetainer's current action",true);return;}
            if(Utils.IsBusy){cleanupIdleSince=0;return;}
            if(!cleanupTaskFinished)
            {
                if(cleanupIdleSince==0)cleanupIdleSince=Environment.TickCount64;
                if(Environment.TickCount64-cleanupIdleSince>1000)EndCleanup("AutoRetainer's queued action was interrupted",true);
                return;
            }
            if(cleanup.Phase==CleanupPhase.Gc&&cleanupGcStarted&&!cleanupGcFinished)
            {EndCleanup("GC delivery ended without a completion acknowledgement",true);return;}
            cleanupAwaiting=false;cleanup.Advance();
        }
        if(Utils.IsBusy||SchedulerMain.RetainerPostProcessLocked)return;
        if(cleanup.Phase==CleanupPhase.Review)
        {
            if(OtherDisposalRemaining())
            {EndCleanup("Approved desynthesis or sale items remain; check the enabled method, unlocks and vendor availability",false);return;}
            if(cleanupCarried)
            {
                cleanup.Stop(MethodInScope("Gc") && GCContinuation.DoesInventoryHaveDeliverableItem()
                    ? "GC candidates remain; review eligibility or seal capacity" : "Carried inventory pass finished");
                return;
            }
            cleanup.Review(Utils.GetInventoryFreeSlotCount()>=2,MethodInScope("Gc") && GCContinuation.DoesInventoryHaveDeliverableItem());
            Svc.Log.Information("[Steward cleanup] {Status}",CleanupStatus);
            if(!cleanup.Active)return;
        }
        cleanupAwaiting=true;cleanupTaskFinished=false;cleanupIdleSince=0;cleanupStarted=Environment.TickCount64;
        Svc.Log.Information("[Steward cleanup] {Status}",CleanupStatus);
        try
        {
            switch(cleanup.Phase)
            {
                case CleanupPhase.Bell:
                case CleanupPhase.Return:
                    if(cleanupCarried && (cleanup.Phase==CleanupPhase.Bell || !MethodInScope("Vendor") || !cleanupGcStarted))break;
                    if(Utils.GetReachableRetainerBell(true)==null&&!TaskTeleportToProperty.EnqueueIfNeededAndPossible(false))
                    {EndCleanup("No bell is reachable and AutoRetainer has no enabled return route",false);return;}
                    break;
                case CleanupPhase.Withdraw:
                    if(cleanupCarried)break;
                    if(!EnqueueOverflow(cleanup.Character,disposalOnly:true)){EndCleanup("Could not begin the retainer withdrawal pass",false);return;}
                    break;
                case CleanupPhase.Gc:
                    cleanupGcStarted=false;cleanupGcFinished=false;
                    if(MethodInScope("Gc") && GCContinuation.DoesInventoryHaveDeliverableItem())
                    {
                        if(!TaskDeliverItems.Enqueue()){EndCleanup("AutoRetainer could not begin GC delivery",false);return;}
                        cleanupGcStarted=true;
                    }
                    break;
                case CleanupPhase.Desynth:
                    if(MethodInScope("Desynth") && Data.GetIMSettings().IMEnableItemDesynthesis)TaskDesynthItems.Enqueue();
                    break;
                case CleanupPhase.Vendor:
                    if(MethodInScope("Vendor"))TaskVendorItems.EnqueueFromCommand();
                    break;
            }
            P.TaskManager.Enqueue(()=>{cleanupTaskFinished=true;},"Steward cleanup stage submitted");
        }
        catch(Exception ex){EndCleanup(ex.Message,true);}
    }
    private static unsafe bool OtherDisposalRemaining()
    {
        var manager=InventoryManager.Instance();if(manager==null)return true;
        var settings=Data.GetIMSettings();
        foreach(var type in PolicyPlayerInventories)
        {
            var inventory=manager->GetInventoryContainer(type);if(inventory==null||!inventory->IsLoaded)return true;
            for(var slot=0;slot<inventory->Size;slot++)
            {
                var item=inventory->GetInventorySlot(slot);
                if(item==null)return true;
                if(item->ItemId==0||!Utils.IsItemSelectedByHardList(item->ItemId,item->Quantity))continue;
                if((Utils.PlayerInvetories.Contains(type)||settings.IMEnableItemDesynthesisFromArmory)&&
                    DesynthDisposalEligible(item->ItemId)&&AllowsSlot("Desynth",type,slot))return true;
                if((settings.IMEnableAutoVendor||settings.IMEnableNpcSell)&&InventorySpaceManager.GetAllowedToSellInventoryTypes().Contains(type)&&
                    AllowsSlot("Vendor",type,slot))return true;
            }
        }
        return false;
    }
}
