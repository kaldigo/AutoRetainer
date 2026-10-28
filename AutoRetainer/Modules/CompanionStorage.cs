using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using AutoRetainer.Internal.InventoryManagement;
using AutoRetainer.Scheduler.Tasks;
using AutoRetainer.Scheduler.Handlers;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
    private static ulong storageSchedulerOwner;
    private static long storageSchedulerRevision;
    private static bool FinishStorage(ulong cid)
    {
        if(!Connected(cid)||HasFault||ledger.Pending.Contains(cid)||ControlActive||SchedulerMain.RetainerPostProcessLocked||P.TaskManager.IsBusy||AutoGCHandin.Operation||IPC.Suppressed)return false;
        if(storageSchedulerOwner==cid)
        {
            if(PolicyStorageRouting.OwnsCompletedRun(cid,storageSchedulerOwner,storageSchedulerRevision,SchedulerMain.EnableRevision,SchedulerMain.PluginEnabled))
                SchedulerMain.DisablePlugin();
            storageSchedulerOwner=0;
        }
        Svc.Log.Information("Steward storage handoff completed; only the captured scheduler run may be disabled");
        return true;
    }
    private static unsafe bool? CloseBellForCollection(ulong cid)
    {
        if(!Connected(cid)||HasFault)return null;
        if(TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("RetainerList",out var list)&&list->IsVisible)
        {
            if(SchedulerMain.PluginEnabled&&(storageSchedulerOwner!=cid||storageSchedulerRevision!=SchedulerMain.EnableRevision))
            {storageSchedulerOwner=cid;storageSchedulerRevision=SchedulerMain.EnableRevision;}
            RetainerListHandlers.CloseRetainerList();
            return false; // Wait for the actual close before AutoRetainer approaches storage.
        }
        return !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell];
    }

    private static bool EnqueueCollectionVisit(ulong cid,bool armoire)
    {
        P.TaskManager.Enqueue(()=>CloseBellForCollection(cid),"Close bell before collection storage");
        P.TaskManager.Enqueue(() =>
        {
            if(!Connected(cid)||HasFault)return;
            // The upstream route inserts inn travel at the front of the queue. Construct it only after the bell has closed.
            Svc.Log.Information("Steward collection route: opening {Storage}",armoire?"armoire":"glamour dresser");
            if(armoire)S.CabinetManager.EnqueueGoToInnAndDeliverEverything();
            else S.MirageManager.EnqueueGoToInnAndDeliverEverything(true);
        },"Start collection route after closing bell");
        return true;
    }

    private static unsafe long? CountStored(uint id,bool hq)
    {
        var manager=InventoryManager.Instance();if(manager==null)return null;
        long count=0;
        foreach(var type in Utils.RetainerInventories)
        {
            var inv=manager->GetInventoryContainer(type);if(inv==null||!inv->IsLoaded)return null;
            for(var i=0;i<inv->Size;i++)
            {
                var item=inv->GetInventorySlot(i);if(item==null)return null;
                if(item->ItemId==id && item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)==hq)count+=item->Quantity;
            }
        }
        return count;
    }
    private static unsafe bool Store(ulong cid,string request)
    {
        if(!OwnsHandoff(cid)||SubmissionPending||ledger.Pending.Contains(cid)||HasFault)return false;
        if(!StoragePlanAllows(cid,request))return false;
        var expected=JsonConvert.DeserializeObject<Retrieval>(request);
        if(expected==null||expected.Quantity!=1||!PolicyPlayerInventories.Contains((InventoryType)expected.Container))return false;
        var rm=RetainerManager.Instance();var im=InventoryManager.Instance();
        if(rm==null||im==null||expected.Retainer==0||rm->LastSelectedRetainerId!=expected.Retainer)return false;
        var identity=ReadIdentity((InventoryType)expected.Container,expected.Slot);
        if(identity==null||identity.ItemId!=expected.ItemId||identity.Quantity!=1||
            ((InventoryItem.ItemFlags)identity.Flags).HasFlag(InventoryItem.ItemFlags.HighQuality)!=expected.Hq)return false;
        var data=Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if(!data.TryGetRow(expected.ItemId,out var facts)||facts.EquipSlotCategory.RowId==0)return false;
        // Equipment is unstackable. Require every page live, an empty slot and no unique-item conflict.
        var empty=false;
        foreach(var type in Utils.RetainerInventories)
        {
            var inv=im->GetInventoryContainer(type);if(inv==null||!inv->IsLoaded)return false;
            for(var i=0;i<inv->Size;i++)
            {
                var item=inv->GetInventorySlot(i);if(item==null)return false;
                if(item->ItemId==0)empty=true;
                if(facts.IsUnique && item->ItemId==expected.ItemId)return false;
            }
        }
        if(!empty||CountStored(expected.ItemId,expected.Hq)==null||!OwnsHandoff(cid))return false;
        return SubmitSlot("Store",(InventoryType)expected.Container,expected.Slot,
            ()=>P.Memory.RetainerItemCommandDetour(InventorySpaceManager.AgentRetainerItemCommandModule,
                (uint)expected.Slot,(InventoryType)expected.Container,0,RetainerItemCommand.EntrustToRetainer),1);
    }
    private static unsafe bool EnqueueOverflow(ulong cid,bool collection=false,bool disposalOnly=false,bool entrustOnly=false)
    {
        if(!Connected(cid)||HasFault||Utils.GetReachableRetainerBell(true)==null||!ECommons.GameHelpers.Player.IsInHomeWorld)return false;
        P.SkipNextEnable=true;
        TaskInteractWithNearestBell.Enqueue(true);
        P.TaskManager.Enqueue(()=>TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("RetainerList",out var list)&&IsAddonReady(list),"Wait for retainer list");
        P.TaskManager.Enqueue(() =>
        {
            if(!Connected(cid)||HasFault)return true;
            P.TaskManager.InsertStack(() => EnqueueRetainers(cid,collection,disposalOnly,entrustOnly));
            return true;
        },"Steward protected overflow retainers");
        P.TaskManager.Enqueue(RetainerListHandlers.CloseRetainerList);
        return true;
    }
    private static unsafe void EnqueueRetainers(ulong cid,bool collection,bool disposalOnly=false,bool entrustOnly=false)
    {
        var rm=RetainerManager.Instance();if(rm==null||!rm->IsReady)return;
        foreach(var retainer in Internal.GameRetainerManager.Retainers)
        {
            if(!retainer.Handle.Available||C.CompanionGilOnlyRetainers.Contains(retainer.RetainerID))continue;
            var name=retainer.Name;
            var retainerId=retainer.RetainerID;
            if(string.IsNullOrWhiteSpace(name))continue;
            P.TaskManager.Enqueue(()=>
            {
                if(!Connected(cid)||HasFault)return true;
                var extra=C.AdditionalData.GetValueOrDefault(Utils.GetAdditionalDataKey(cid,name,false));
                var plan=C.EntrustPlans.FirstOrDefault(p=>p.Guid==extra?.EntrustPlan&&!p.ManualPlan);
                if(entrustOnly&&(!C.EnableEntrustManager||plan==null||C.CompanionGilOnlyRetainers.Contains(retainerId)))return true;
                if(disposalOnly)
                {
                    if(!cleanup.Active||!CleanupRetainerAllowed(cid,retainerId))return true;
                    cleanup.Visited++;
                    if(Utils.GetInventoryFreeSlotCount()<2)return true;
                }
                else if(!entrustOnly&&!(collection?NeedsCollection(cid):NeedsOverflow(cid)))return true;
                Svc.Log.Information("Steward {Operation}: selecting retainer {RetainerName} ({RetainerId})",
                    entrustOnly?"post-dresser entrust":collection?"collection":"overflow",name,retainerId);
                P.TaskManager.InsertStack(()=>
                {
                    TaskSelectRetainer.Enqueue(name);
                    if(entrustOnly)TaskEntrustDuplicates.EnqueueNew(plan!);
                    else TaskPostprocessRetainerIPC.Enqueue(name,"Steward");
                    P.TaskManager.Enqueue(RetainerHandlers.SelectQuit);
                    P.TaskManager.Enqueue(RetainerHandlers.ConfirmCantBuyback);
                });
                return true;
            },(collection?"Steward collection: ":"Steward overflow: ")+name);
        }
    }
    private static bool NeedsCollection(ulong cid)
    {
        if(!Connected(cid)||HasFault)return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,bool>("Steward.InventoryPolicy.CollectionNeeded").InvokeFunc(cid);}
        catch{return false;}
    }
    private static bool NeedsOverflow(ulong cid)
    {
        if(!Connected(cid)||HasFault)return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,bool>("Steward.InventoryPolicy.OverflowNeeded").InvokeFunc(cid);}
        catch{return false;}
    }
}
