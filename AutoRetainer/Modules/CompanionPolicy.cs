using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoRetainer.Modules;

/// <summary>Optional, character-scoped policy. All game actions remain owned by AutoRetainer.</summary>
internal static class CompanionPolicy
{
    internal const int Version = 2;
    private static string callback;
    private static ulong character;
    private static long heartbeat;
    private static ulong handoffCharacter;
    internal static string Status = "No companion connected";
    internal static bool IsManaged => C.CompanionPolicyOwners.ContainsKey(Svc.ClientState.LocalContentId);

    internal static void Init()
    {
        Svc.Framework.Update+=Tick;
        Svc.PluginInterface.GetIpcProvider<int>("AutoRetainer.InventoryPolicy.Version").RegisterFunc(() => Version);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Connect").RegisterFunc(Connect);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Heartbeat").RegisterFunc(Pulse);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Detach").RegisterFunc(Detach);
        Svc.PluginInterface.GetIpcProvider<string>("AutoRetainer.InventoryPolicy.Status").RegisterFunc(() => Status);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Run").RegisterFunc(Run);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Menu").RegisterFunc(Menu);
        Svc.PluginInterface.GetIpcProvider<ulong, string, bool>("AutoRetainer.InventoryPolicy.Retrieve").RegisterFunc(Retrieve);
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsHandoff").RegisterFunc(OwnsHandoff);
    }

    internal static void BeginHandoff(string plugin) => handoffCharacter=plugin=="Steward"?Svc.ClientState.LocalContentId:0;
    internal static void EndHandoff() => handoffCharacter=0;
    private static bool OwnsHandoff(ulong cid) => cid==handoffCharacter && HandoffLease.Owns(handoffCharacter,character,
        Svc.ClientState.LocalContentId,callback,heartbeat,Environment.TickCount64,SchedulerMain.RetainerPostProcessLocked);
    private static void Tick(Dalamud.Plugin.Services.IFramework _)
    {
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
        if(C.CompanionPolicyOwners.TryGetValue(cid, out var existing) && existing != endpoint) return false;
        if(!C.CompanionPolicyOwners.ContainsKey(cid)) { C.CompanionPolicyOwners[cid] = endpoint; ECommons.Configuration.EzConfig.Save(); }
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
        if(cid != Svc.ClientState.LocalContentId || !C.CompanionPolicyOwners.TryGetValue(cid, out var owner) || owner != endpoint) return false;
        C.CompanionPolicyOwners.Remove(cid);
        ECommons.Configuration.EzConfig.Save();
        character = 0;
        callback = null;
        Status = "Companion detached; original AutoRetainer rules apply";
        return true;
    }

    /// <summary>Checked at both candidate selection and submission. Explicit upstream protections win.</summary>
    internal static bool Allows(string action, uint itemId, uint quantity = 1, int container = -1, int slot = -1)
    {
        var cid = Svc.ClientState.LocalContentId;
        if(cid == 0) return false;
        if(!C.CompanionPolicyOwners.TryGetValue(cid, out var owner)) return true;
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
            return result == "Allow";
        }
        catch(Exception ex)
        {
            Status = "Managed item actions paused: policy call failed";
            Svc.Log.Debug(ex, Status);
            return false;
        }
    }

    internal static unsafe bool AllowsSlot(string action, InventoryType container, int slot)
    {
        var manager = InventoryManager.Instance();
        if(manager == null) return false;
        var inv = manager->GetInventoryContainer(container);
        if(inv == null || !inv->IsLoaded || slot < 0 || slot >= inv->Size) return false;
        var item = inv->GetInventorySlot(slot);
        return item != null && item->ItemId != 0 && item->Quantity > 0 && Allows(action, item->ItemId, (uint)item->Quantity, (int)container, slot);
    }

    private static bool Run(ulong cid, string operation)
    {
        if(cid != Svc.ClientState.LocalContentId || character != cid || Environment.TickCount64-heartbeat > 5000 || Utils.IsBusy) return false;
        switch(operation)
        {
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
        var type=(InventoryType)expected.Container;
        var inv=InventoryManager.Instance()->GetInventoryContainer(type);
        if(inv==null||!inv->IsLoaded||expected.Slot<0||expected.Slot>=inv->Size)return false;
        var item=inv->GetInventorySlot(expected.Slot);
        if(item->ItemId!=expected.ItemId || item->Quantity!=expected.Quantity || !AllowsSlot("Retrieve",type,expected.Slot))return false;
        if(Utils.GetInventoryFreeSlotCount()<2)return false;
        P.Memory.RetainerItemCommandDetour(Internal.InventoryManagement.InventorySpaceManager.AgentRetainerItemCommandModule,(uint)expected.Slot,type,0,Internal.InventoryManagement.RetainerItemCommand.RetrieveFromRetainer);
        return true;
    }

    private sealed class Retrieval
    {
        public uint Container;
        public int Slot;
        public uint ItemId;
        public uint Quantity;
    }

    internal static unsafe bool AllowsDresserBatch()
    {
        if(!C.CompanionPolicyOwners.ContainsKey(Svc.ClientState.LocalContentId)) return true;
        // The bulk IPC has no final per-item policy callback, and can outlive the companion lease.
        Status = "Managed bulk dresser storage held: use individually validated transfers";
        return false;
    }

    internal static void Shutdown()
    {
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
        Svc.PluginInterface.GetIpcProvider<ulong, bool>("AutoRetainer.InventoryPolicy.OwnsHandoff").UnregisterFunc();
        callback = null;
        character = 0;
    }
}
