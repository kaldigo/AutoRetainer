using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoRetainer.Modules;

/// <summary>Optional, character-scoped policy. All game actions remain owned by AutoRetainer.</summary>
internal static partial class CompanionPolicy
{
    internal const int Version = 6;
    private static string callback;
    private static ulong character;
    private static long heartbeat;
    private static ulong handoffCharacter;
    private static readonly PolicyControl control=new();
    private static PolicyLedger ledger=new();
    private static bool ready;
    internal static bool StartupBlocked=>!ready;
    internal static bool ControlActive => control.Active;
    internal static string Status = "No companion connected";
    internal static bool IsManaged => ledger.Owners.ContainsKey(Svc.ClientState.LocalContentId);
    internal static bool HasFault => IsManaged && ledger.Faults.Contains(Svc.ClientState.LocalContentId);

    internal static void Init()
    {
        var migrated=false;
        var saved=AtomicState.ReadOrMigrate(PolicyPath,()=>{migrated=true;return new PolicyLedger{Owners=new(C.CompanionPolicyOwners),Faults=new(C.CompanionPolicyFaults),Pending=new(C.CompanionPolicyPending)};});
        saved.Validate();
        ledger=saved;
        ledger.Faults.UnionWith(ledger.Pending);
        if(!migrated || ledger.Owners.Count!=0 || ledger.Faults.Count!=0 || ledger.Pending.Count!=0)SavePolicy();
        ready=true;
        Svc.PluginInterface.GetIpcProvider<uint,uint,bool>("AutoRetainer.InventoryPolicy.DisposalSelected").RegisterFunc((id,quantity)=>
            Utils.IsItemSelectedByHardList(id,quantity)&&
            (Data.GetIMSettings().IMEnableAutoVendor||Data.GetIMSettings().IMEnableNpcSell||Scheduler.Tasks.TaskDesynthItems.DesynthEligible(id)));
        Svc.Framework.Update+=Tick;
        Svc.PluginInterface.GetIpcProvider<int>("AutoRetainer.InventoryPolicy.Version").RegisterFunc(() => Version);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Connect").RegisterFunc(Connect);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Heartbeat").RegisterFunc(Pulse);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Detach").RegisterFunc(Detach);
        Svc.PluginInterface.GetIpcProvider<string>("AutoRetainer.InventoryPolicy.Status").RegisterFunc(() => Status);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Run").RegisterFunc(Run);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Menu").RegisterFunc(Menu);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Retrieve").RegisterFunc(Retrieve);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Store").RegisterFunc(Store);
        Svc.PluginInterface.GetIpcProvider<bool>("AutoRetainer.InventoryPolicy.SubmissionPending").RegisterFunc(()=>SubmissionPending);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsHandoff").RegisterFunc(OwnsHandoff);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.AcquireControl").RegisterFunc(AcquireControl);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.ReleaseControl").RegisterFunc(control.Release);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsControl").RegisterFunc(OwnsControl);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.HasFault").RegisterFunc(cid=>ledger.Owners.ContainsKey(cid)&&ledger.Faults.Contains(cid));
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.ClearFault").RegisterFunc(ClearFault);
        Svc.PluginInterface.GetIpcProvider<ulong,string,object>("AutoRetainer.InventoryPolicy.Hold").RegisterAction((cid,reason)=>{if(Connected(cid))ReportFault(reason,false);});
    }
    private static string PolicyPath=>System.IO.Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(),"steward-policy.json");
    private static PolicyLedger CapturePolicy()=>new(){Owners=new(ledger.Owners),Faults=new(ledger.Faults),Pending=new(ledger.Pending)};
    private static void SavePolicy()=>AtomicState.Write(PolicyPath,CapturePolicy());

    internal static void ReportFault(string reason,bool notify=true)
    {
        if(!ready)return;
        if(!IsManaged)return;
        var cid=Svc.ClientState.LocalContentId;
        if(ledger.Faults.Add(cid))
            try{SavePolicy();}catch(Exception ex){Svc.Log.Error(ex,"Could not persist companion review hold");}
        Status="Managed item actions held for review: "+reason;
        try{MaintainPauses();}catch(Exception ex){Svc.Log.Error(ex,"Could not maintain dialog pauses; item actions remain held");}
        if(!notify)return;
        try{Svc.PluginInterface.GetIpcSubscriber<ulong,string,object>("Steward.InventoryPolicy.Fault").InvokeAction(cid,reason);}
        catch(Exception ex){Svc.Log.Debug(ex,Status);}
    }
    internal static void BeginTransaction()
    {
        ledger.Pending.Add(Svc.ClientState.LocalContentId);
        SavePolicy(); // Must succeed before the native commit.
    }
    internal static void CompleteTransaction()
    {
        var cid=Svc.ClientState.LocalContentId;
        var pending=ledger.Pending.Contains(cid);
        PolicyPersistence.Relax(()=>ledger.Pending.Remove(cid),()=>SavePolicy(),
            ()=>{if(pending)ledger.Pending.Add(cid);});
    }
    private static unsafe bool ClearFault(ulong cid)
    {
        if(!Connected(cid) || ControlActive || SchedulerMain.RetainerPostProcessLocked || P.TaskManager.IsBusy || AutoGCHandin.Operation)return false;
        if(ledger.Pending.Contains(cid) && !ledger.Faults.Contains(cid))return false;
        foreach(var name in new[]{"SelectYesno","GrandCompanySupplyReward","RetainerSell","MiragePrismMiragePrismSetConvert","MiragePrismPrismSetConvert"})
            if(TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(name,out var addon) && addon->IsVisible)return false;
        PolicyPersistence.ClearReview(ledger.Faults,ledger.Pending,cid,
            ()=>SavePolicy(),()=>{AutoGCHandin.ClearCompanionTransaction();submission=null;});
        return true;
    }

    private static bool Connected(ulong cid) => HandoffLease.Owns(cid,character,Svc.ClientState.LocalContentId,
        callback,heartbeat,Environment.TickCount64,true);
    private static bool AcquireControl(ulong cid)
    {
        return control.Acquire(cid,Connected(cid)&&!HasFault&&!ledger.Pending.Contains(cid),Utils.IsBusy,IPC.Suppressed,MaintainPauses);
    }
    internal static void MaintainPauses()
    {
        NewYesAlreadyManager.Tick();TextAdvanceManager.Tick();
        NewYesAlreadyManager.Lock();TextAdvanceManager.LockTA();
    }
    private static bool OwnsControl(ulong cid) => cid!=0 && control.Owner==cid && Connected(cid);

    internal static void BeginHandoff(string plugin) => handoffCharacter=plugin=="Steward"?Svc.ClientState.LocalContentId:0;
    internal static void EndHandoff() => handoffCharacter=0;
    private static bool OwnsHandoff(ulong cid) => cid==handoffCharacter && HandoffLease.Owns(handoffCharacter,character,
        Svc.ClientState.LocalContentId,callback,heartbeat,Environment.TickCount64,SchedulerMain.RetainerPostProcessLocked);
    private static void Tick(Dalamud.Plugin.Services.IFramework _)
    {
        CheckSubmission();
        foreach(var cid in ledger.Pending.Where(id=>id!=Svc.ClientState.LocalContentId).ToArray())
            if(ledger.Faults.Add(cid))try{SavePolicy();}catch(Exception ex){Svc.Log.Error(ex,"Could not persist interrupted transaction hold");}
        if(submission!=null && submission.Character!=Svc.ClientState.LocalContentId)submission=null;
        if(control.Active && !OwnsControl(control.Owner))control.Release(control.Owner);
        if(handoffCharacter!=0 && !OwnsHandoff(handoffCharacter))
        {
            handoffCharacter=0;
            SchedulerMain.RetainerPostProcessLocked=false;
            Status="Steward handoff released: its lease or character became unavailable";
        }
    }

    private static bool Connect(ulong cid, string endpoint)
    {
        if(cid == 0 || cid != Svc.ClientState.LocalContentId || endpoint != "Steward.InventoryPolicy.Evaluate") return false;
        if(ledger.Owners.TryGetValue(cid, out var existing) && existing != endpoint) return false;
        ledger.Owners[cid]=endpoint;
        // A previous failed save may have left only an in-memory owner. Every connection must persist before activating.
        SavePolicy();
        character = cid;
        callback = endpoint;
        heartbeat = Environment.TickCount64;
        Status = "Steward policy active";
        return true;
    }

    private static bool Pulse(ulong cid, string endpoint)
    {
        if(character != cid || callback != endpoint || cid != Svc.ClientState.LocalContentId) return false;
        heartbeat = Environment.TickCount64;
        return true;
    }

    private static bool Detach(ulong cid, string endpoint)
    {
        if(cid != Svc.ClientState.LocalContentId || !ledger.Owners.TryGetValue(cid, out var owner) || owner != endpoint) return false;
        if(ControlActive || SchedulerMain.RetainerPostProcessLocked || P.TaskManager.IsBusy || AutoGCHandin.Operation ||
            ledger.Pending.Contains(cid) || ledger.Faults.Contains(cid))return false;
        PolicyPersistence.Relax(()=>ledger.Owners.Remove(cid),()=>SavePolicy(),
            ()=>ledger.Owners[cid]=owner);
        character = 0;
        callback = null;
        Status = "Companion detached; original AutoRetainer rules apply";
        return true;
    }

    /// <summary>Checked at both candidate selection and submission. Explicit upstream protections win.</summary>
    internal static bool Allows(string action, uint itemId, uint quantity = 1, int container = -1, int slot = -1,bool ownSubmission=false)
    {
        if(!ready || ControlActive || HasFault || submission!=null&&!ownSubmission)return false;
        var cid = Svc.ClientState.LocalContentId;
        if(cid == 0) return false;
        if(!ledger.Owners.TryGetValue(cid, out var owner)) return true;
        if(ledger.Pending.Contains(cid) && action!="Gc" && !ownSubmission)return false;
        if(character != cid || callback != owner || Environment.TickCount64 - heartbeat > 5000)
        {
            Status = "Managed item actions paused: companion unavailable";
            return false;
        }
        try
        {
            var request = JsonConvert.SerializeObject(new { Version, Character = cid, Action = action, ItemId = itemId, Quantity = quantity, Container = container, Slot = slot });
            var result = Svc.PluginInterface.GetIpcSubscriber<string, string>(callback).InvokeFunc(request);
            // Only an explicit allow authorizes an action. Invalid/unknown responses hold the item.
            return result == "Allow" && Connected(cid) && callback==owner && IsManaged && !HasFault && !ControlActive;
        }
        catch(Exception ex)
        {
            Status = "Managed item actions paused: policy call failed";
            Svc.Log.Debug(ex, Status);
            return false;
        }
    }

    internal static unsafe bool AllowsSlot(string action, InventoryType container, int slot,bool ownSubmission=false)
    {
        var manager = InventoryManager.Instance();
        if(manager == null) return false;
        var inv = manager->GetInventoryContainer(container);
        if(inv == null || !inv->IsLoaded || slot < 0 || slot >= inv->Size) return false;
        var item = inv->GetInventorySlot(slot);
        if(item==null || item->ItemId==0 || item->Quantity<=0)return false;
        var expected=new PolicyItemIdentity(item->ItemId,(uint)item->Quantity,(uint)container,slot,(int)item->Flags);
        if(Data.GetIMSettings().IMProtectList.Contains(expected.ItemId))return false;
        if(!Allows(action,expected.ItemId,expected.Quantity,(int)container,slot,ownSubmission))return false;
        inv=manager->GetInventoryContainer(container);
        if(inv==null || !inv->IsLoaded || slot>=inv->Size)return false;
        item=inv->GetInventorySlot(slot);
        return item!=null && !Data.GetIMSettings().IMProtectList.Contains(expected.ItemId) &&
            expected.Matches(new(item->ItemId,(uint)item->Quantity,(uint)container,slot,(int)item->Flags));
    }

    private static bool Run(ulong cid, string operation)
    {
        if(!Connected(cid) || HasFault || ledger.Pending.Contains(cid) || Utils.IsBusy) return false;
        switch(operation)
        {
            case "Overflow": return EnqueueOverflow(cid);
            case "Collect": return EnqueueOverflow(cid,true);
            case "Armoire": S.CabinetManager.EnqueueGoToInnAndDeliverEverything(); return true;
            case "Dresser": S.MirageManager.EnqueueGoToInnAndDeliverEverything(true); return true;
            case "Vendor": Scheduler.Tasks.TaskVendorItems.EnqueueFromCommand(); return true;
            case "Desynth": if(!Data.GetIMSettings().IMEnableItemDesynthesis) return false; Scheduler.Tasks.TaskDesynthItems.Enqueue(); return true;
            case "Gc": GCContinuation.EnqueueInitiation(true); return true;
            default: return false;
        }
    }

    private static unsafe bool Menu(ulong cid, string menu)
    {
        if(!OwnsHandoff(cid)) return false;
        if(menu=="Inventory")
        {
            if(Internal.InventoryManagement.InventorySpaceManager.IsRetainerInventoryLoaded()) return true;
            Scheduler.Handlers.RetainerHandlers.SelectEntrustItems();
        }
        if(menu=="Market")
        {
            if(TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("RetainerSellList",out var sell) && IsAddonReady(sell))return true;
            var sheet=Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Addon>();
            // Resolve the known English label to its localized Addon row, without a fixed menu position.
            var label=Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Addon>(Dalamud.Game.ClientLanguage.English)
                .FirstOrDefault(row=>row.Text.ToString().StartsWith("Sell items",StringComparison.Ordinal)
                    && row.Text.ToString().Contains("your inventory",StringComparison.Ordinal)
                    && row.Text.ToString().Contains("market",StringComparison.Ordinal)
                    && !row.Text.ToString().Contains("retainer",StringComparison.OrdinalIgnoreCase));
            if(label.RowId!=0) Utils.TrySelectSpecificEntry(sheet.GetRow(label.RowId).Text.ToString());
        }
        return false;
    }

    private static unsafe bool Retrieve(ulong cid,string request)
    {
        if(!OwnsHandoff(cid)) return false;
        var expected=JsonConvert.DeserializeObject<Retrieval>(request);
        if(expected==null || !Enum.IsDefined((InventoryType)expected.Container) || !Utils.RetainerInventories.Contains((InventoryType)expected.Container))return false;
        var retainers=RetainerManager.Instance();
        if(retainers==null || expected.Retainer==0 || retainers->LastSelectedRetainerId!=expected.Retainer)return false;
        var type=(InventoryType)expected.Container;
        var inv=InventoryManager.Instance()->GetInventoryContainer(type);
        if(inv==null||!inv->IsLoaded||expected.Slot<0||expected.Slot>=inv->Size)return false;
        var item=inv->GetInventorySlot(expected.Slot);
        if(item==null || item->ItemId!=expected.ItemId || item->Quantity!=expected.Quantity ||
            item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)!=expected.Hq || !AllowsSlot("Retrieve",type,expected.Slot))return false;
        if(Utils.GetInventoryFreeSlotCount()<2)return false;
        if(retainers->LastSelectedRetainerId!=expected.Retainer || !OwnsHandoff(cid))return false;
        P.Memory.RetainerItemCommandDetour(Internal.InventoryManagement.InventorySpaceManager.AgentRetainerItemCommandModule,(uint)expected.Slot,type,0,Internal.InventoryManagement.RetainerItemCommand.RetrieveFromRetainer);
        return true;
    }

    private sealed class Retrieval
    {
        public uint Container;
        public int Slot;
        public uint ItemId;
        public uint Quantity;
        public bool Hq;
        public ulong Retainer;
    }

    internal static unsafe bool AllowsDresserBatch()
    {
        if(!ready)return false;
        if(!ledger.Owners.ContainsKey(Svc.ClientState.LocalContentId)) return true;
        // The bulk IPC has no final per-item policy callback, and can outlive the companion lease.
        Status = "Managed bulk dresser storage held: use individually validated transfers";
        return false;
    }

    internal static void Shutdown()
    {
        if(control.Active)control.Release(control.Owner);
        Svc.PluginInterface.GetIpcProvider<uint,uint,bool>("AutoRetainer.InventoryPolicy.DisposalSelected").UnregisterFunc();
        Svc.Framework.Update-=Tick;
        if(handoffCharacter!=0){handoffCharacter=0;SchedulerMain.RetainerPostProcessLocked=false;}
        Svc.PluginInterface.GetIpcProvider<int>("AutoRetainer.InventoryPolicy.Version").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Connect").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Heartbeat").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Detach").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<string>("AutoRetainer.InventoryPolicy.Status").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Run").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Menu").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Retrieve").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Store").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<bool>("AutoRetainer.InventoryPolicy.SubmissionPending").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsHandoff").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.AcquireControl").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.ReleaseControl").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsControl").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.HasFault").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.ClearFault").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<ulong,string,object>("AutoRetainer.InventoryPolicy.Hold").UnregisterAction();
        callback = null;
        character = 0;
    }
}
