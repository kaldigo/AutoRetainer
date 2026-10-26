using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using AutoRetainer.Internal.InventoryManagement;
using AutoRetainer.Scheduler.Tasks;
using AutoRetainer.Scheduler.Handlers;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
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
    private static unsafe bool EnqueueOverflow(ulong cid)
    {
        if(!Connected(cid)||HasFault||Utils.GetReachableRetainerBell(true)==null||!ECommons.GameHelpers.Player.IsInHomeWorld)return false;
        P.SkipNextEnable=true;
        TaskInteractWithNearestBell.Enqueue(true);
        P.TaskManager.Enqueue(()=>TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("RetainerList",out var list)&&IsAddonReady(list),"Wait for retainer list");
        P.TaskManager.Enqueue(() =>
        {
            if(!Connected(cid)||HasFault)return true;
            P.TaskManager.InsertStack(() => EnqueueRetainers(cid));
            return true;
        },"Steward protected overflow retainers");
        P.TaskManager.Enqueue(RetainerListHandlers.CloseRetainerList);
        return true;
    }
    private static unsafe void EnqueueRetainers(ulong cid)
    {
        var rm=RetainerManager.Instance();if(rm==null||!rm->IsReady)return;
        foreach(var retainer in rm->Retainers)
        {
            if(!retainer.Available||retainer.RetainerId==0)continue;
            var name=retainer.Name.ToString();
            P.TaskManager.Enqueue(()=>
            {
                if(!NeedsOverflow(cid))return true;
                P.TaskManager.InsertStack(()=>
                {
                    TaskSelectRetainer.Enqueue(name);
                    TaskPostprocessRetainerIPC.Enqueue(name,"Steward");
                    P.TaskManager.Enqueue(RetainerHandlers.SelectQuit);
                    P.TaskManager.Enqueue(RetainerHandlers.ConfirmCantBuyback);
                });
                return true;
            },"Protected overflow: "+name);
        }
    }
    private static bool NeedsOverflow(ulong cid)
    {
        if(!Connected(cid)||HasFault)return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,bool>("Steward.InventoryPolicy.OverflowNeeded").InvokeFunc(cid);}
        catch{return false;}
    }
}
