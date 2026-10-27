using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
    private sealed record Submission(ulong Character,string Action,PolicyItemIdentity Source,long Before,uint Removed,
        ulong Retainer,uint CabinetRow,long Started,long? DestinationBefore=null);
    private static Submission submission;
    internal static bool SubmissionPending=>submission!=null && submission.Character==Svc.ClientState.LocalContentId;
    // The removed waist slot remains in the upstream enum/list, but is not a live inventory.
    internal static InventoryType[] PolicyPlayerInventories=>Utils.PlayerInventoryWithArmory.Where(t=>t!=InventoryType.ArmoryWaist).ToArray();

    private static unsafe PolicyItemIdentity ReadIdentity(InventoryType type,int slot)
    {
        var manager=InventoryManager.Instance();if(manager==null)return null;
        var inventory=manager->GetInventoryContainer(type);
        if(inventory==null || !inventory->IsLoaded || slot<0 || slot>=inventory->Size)return null;
        var item=inventory->GetInventorySlot(slot);
        return item==null?null:new(item->ItemId,(uint)item->Quantity,(uint)type,slot,(int)item->Flags);
    }
    private static unsafe long? CountSource(PolicyItemIdentity source)
    {
        var manager=InventoryManager.Instance();if(manager==null)return null;
        var types=Utils.RetainerInventories.Contains((InventoryType)source.Container)?Utils.RetainerInventories:PolicyPlayerInventories;
        long total=0;
        foreach(var type in types)
        {
            var inventory=manager->GetInventoryContainer(type);
            if(inventory==null || !inventory->IsLoaded)return null;
            for(var slot=0;slot<inventory->Size;slot++)
            {
                var item=inventory->GetInventorySlot(slot);if(item==null)return null;
                if(item->ItemId==source.ItemId && (int)item->Flags==source.Flags)total+=item->Quantity;
            }
        }
        return total;
    }
    internal static unsafe bool SubmitSlot(string action,InventoryType type,int slot,Action native,uint removed=0,uint cabinetRow=0)
    {
        if(!IsManaged){native();return true;}
        if(submission!=null || ledger.Pending.Contains(Svc.ClientState.LocalContentId) || !AllowsSlot(action,type,slot))return false;
        var source=ReadIdentity(type,slot);if(source==null || source.ItemId==0 || source.Quantity==0)return false;
        var before=CountSource(source);if(before==null)return false;
        var manager=RetainerManager.Instance();
        var retainer=(Utils.RetainerInventories.Contains(type)||action=="Store")&&manager!=null?manager->LastSelectedRetainerId:0;
        var destination=action=="Store"?CountStored(source.ItemId,((InventoryItem.ItemFlags)source.Flags).HasFlag(InventoryItem.ItemFlags.HighQuality)):null;
        if(action=="Store"&&(retainer==0||destination==null))return false;
        if(Utils.RetainerInventories.Contains(type)&&retainer==0)return false;
        submission=new(Svc.ClientState.LocalContentId,action,source,before.Value,removed==0?source.Quantity:removed,retainer,cabinetRow,Environment.TickCount64,destination);
        return PolicyPersistence.Submit(()=>{MaintainPauses();BeginTransaction();},()=>source.Matches(ReadIdentity(type,slot)) && AllowsSlot(action,type,slot,true) &&
            source.Matches(ReadIdentity(type,slot)),native,
            ex=>ReportFault(action+" submission could not be verified: "+ex.Message));
    }
    internal static unsafe bool SubmitArmoire(uint id,uint row,Action native)
    {
        if(!IsManaged){native();return true;}
        foreach(var type in PolicyPlayerInventories)
        {
            var inventory=InventoryManager.Instance()->GetInventoryContainer(type);
            if(inventory==null || !inventory->IsLoaded)return false;
            for(var slot=0;slot<inventory->Size;slot++)
                if(inventory->GetInventorySlot(slot)->ItemId==id)return SubmitSlot("Armoire",type,slot,native,1,row);
        }
        return false;
    }
    private static unsafe void CheckSubmission()
    {
        var pending=submission;if(pending==null || HasFault)return;
        if(pending.Character!=Svc.ClientState.LocalContentId)return; // Tick persists the old character's review hold.
        try
        {
            if(pending.Retainer!=0 && (RetainerManager.Instance()==null || RetainerManager.Instance()->LastSelectedRetainerId!=pending.Retainer))
                throw new InvalidOperationException("Retainer changed before acknowledgement");
            var visible=false;
            foreach(var name in new[]{"SelectYesno","GrandCompanySupplyReward"})
                if(TryGetAddonByName<AtkUnitBase>(name,out var addon)&&addon->IsVisible)visible=true;
            if(pending.Action=="Desynth" && Svc.Condition[ConditionFlag.Occupied39])visible=true;
            if(pending.CabinetRow!=0 && (UIState.Instance()==null || !UIState.Instance()->Cabinet.IsCabinetLoaded() ||
                !UIState.Instance()->Cabinet.IsItemInCabinet(pending.CabinetRow)))visible=true;
            var result=PolicySlotAcknowledgement.Check(pending.Source,ReadIdentity((InventoryType)pending.Source.Container,pending.Source.Slot),
                pending.Before,CountSource(pending.Source),pending.Removed,visible,pending.Started,Environment.TickCount64);
            if(result==PolicyAck.Failed)throw new InvalidOperationException("Source/quantity acknowledgement is missing or unexpected");
            if(pending.Action=="Store")
            {
                var stored=CountStored(pending.Source.ItemId,((InventoryItem.ItemFlags)pending.Source.Flags).HasFlag(InventoryItem.ItemFlags.HighQuality));
                result=PolicyTransferAcknowledgement.Check(result,pending.DestinationBefore,stored,pending.Removed);
                if(result==PolicyAck.Failed)throw new InvalidOperationException("Retainer destination acknowledgement is missing or unexpected");
            }
            if(result==PolicyAck.Completed)
            {
                CompleteTransaction();submission=null;
                if(pending.Action is "Vendor" or "Desynth")cleanup.ConfirmDisposal(pending.Removed);
            }
        }
        catch(Exception ex){ReportFault(pending.Action+" requires inventory review: "+ex.Message);}
    }
}
