using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
    // Evidence lasts for this batch only and is tied to each carried source slot.
    private static readonly Dictionary<(InventoryType,int),PolicyGcExclusion> gcDiscardEvidence=[];
    private static readonly HashSet<(InventoryType,int)> discardAttempted=[];
    private static PolicyItemIdentity discardSource;
    private static long discardOpened;
    internal static bool SurplusDiscardCandidate(uint id)
    {
        if(!IsManaged||!Connected(Svc.ClientState.LocalContentId)||Data.GetIMSettings().IMProtectList.Contains(id)||
            !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id,out var item)||
            item.EquipSlotCategory.RowId==0||!PolicyDisposal.DiscardOnlyRoute(item.PriceLow,item.Desynth>0))return false;
        try{return Svc.PluginInterface.GetIpcSubscriber<ulong,uint,bool>("Steward.InventoryPolicy.SurplusDiscardSelected").InvokeFunc(Svc.ClientState.LocalContentId,id);}
        catch{return false;}
    }
    private static unsafe int? NativeGcReward(uint id)
    {
        var agent=AgentGrandCompanySupply.Instance();
        if(agent==null||!agent->IsAgentActive()||agent->SelectedTab!=2||agent->NumItems<0||agent->NumItems>1000||
            agent->NumItems>0&&agent->ItemArray==null)return null;
        var reward=0;
        for(var i=0;i<agent->NumItems;i++)
            if(agent->ItemArray[i].ItemId==id&&agent->ItemArray[i].SealReward>reward)reward=agent->ItemArray[i].SealReward;
        return reward;
    }
    internal static unsafe bool SurplusNeedsGcInspection()
    {
        if(!cleanup.Active||cleanupMethod!="All"||!MethodEnabled("Gc"))return false;
        var manager=InventoryManager.Instance();if(manager==null)return false;
        foreach(var type in Utils.PlayerInvetories)
        {
            var bag=manager->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)continue;
            for(var slot=0;slot<bag->Size;slot++)
            {
                var item=bag->GetInventorySlot(slot);
                if(item!=null&&item->Quantity==1&&SurplusDiscardCandidate(item->ItemId))return true;
            }
        }
        return false;
    }
    // Called only at completion of the ready, correctly filtered expert delivery list.
    internal static unsafe void ObserveSurplusGcList(IReadOnlyList<(uint ItemID,uint Seals)> rows)
    {
        if(!cleanup.Active||cleanup.Phase!=CleanupPhase.Gc||cleanupMethod!="All")return;
        gcDiscardEvidence.Clear();
        var agent=AgentGrandCompanySupply.Instance();
        if(agent==null||!agent->IsAgentActive()||agent->SelectedTab!=2||agent->NumItems!=rows.Count)
        {
            Svc.Log.Information("[Steward discard] Expert delivery list could not be verified; no discard evidence recorded");
            return;
        }
        var manager=InventoryManager.Instance();if(manager==null)return;
        foreach(var type in Utils.PlayerInvetories)
        {
            var bag=manager->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)return;
            for(var slot=0;slot<bag->Size;slot++)
            {
                var identity=ReadIdentity(type,slot);
                if(identity==null||identity.Quantity!=1||!SurplusDiscardCandidate(identity.ItemId))continue;
                var reward=(int)rows.Where(row=>row.ItemID==identity.ItemId).Select(row=>row.Seals).DefaultIfEmpty().Max();
                Svc.Log.Information("[Steward discard] Expert delivery observation: item={Item}, seals={Seals}, container={Container}, slot={Slot}",identity.ItemId,reward,type,slot);
                if(PolicyGcExclusion.Capture(Svc.ClientState.LocalContentId,identity,reward) is {} proof)gcDiscardEvidence[(type,slot)]=proof;
            }
        }
    }
    private static bool GcRejectedSource(InventoryType type,int slot)=>
        gcDiscardEvidence.TryGetValue((type,slot),out var observed)&&observed.Matches(Svc.ClientState.LocalContentId,ReadIdentity(type,slot));
    private static unsafe bool MatchesDiscardDialog()
    {
        var context=AgentInventoryContext.Instance();
        if(context==null||discardSource==null||context->DialogType!=1)return false;
        var item=context->DiscardDummyItem;
        return item.ItemId==discardSource.ItemId&&item.Quantity==discardSource.Quantity&&
            (int)item.Flags==discardSource.Flags;
    }
    private static unsafe void CancelSurplusDiscard()
    {
        if(discardSource!=null&&MatchesDiscardDialog())
        {
            var addon=Utils.GetSpecificYesno(Lang.DiscardItem);
            if(addon!=null&&addon->IsReady())new AddonMaster.SelectYesno(addon).No();
        }
        discardSource=null;
    }
    private static unsafe bool RunSurplusDiscard()
    {
        if(!cleanup.Active||cleanupMethod!="All"||HasFault||Data.GetIMSettings().IMDry)
        {CancelSurplusDiscard();return true;}
        if(SubmissionPending)return false;
        if(discardSource!=null)
        {
            var type=(InventoryType)discardSource.Container;var slot=discardSource.Slot;
            var addon=Utils.GetSpecificYesno(Lang.DiscardItem);
            if(!discardSource.Matches(ReadIdentity(type,slot))||!GcRejectedSource(type,slot)||
                !AllowsSlot("Discard",type,slot)||Environment.TickCount64-discardOpened>5000)
            {
                Svc.Log.Warning("[Steward discard] Skipped item {Item}: source, stored copy, permission or confirmation changed",discardSource.ItemId);
                CancelSurplusDiscard();return false;
            }
            if(addon==null||!addon->IsReady())return false;
            if(!MatchesDiscardDialog())
            {
                Svc.Log.Warning("[Steward discard] Confirmation does not match item {Item}; no confirmation submitted",discardSource.ItemId);
                CancelSurplusDiscard();return true;
            }
            var address=(nint)addon;
            // Journal immediately before Yes. Opening the dialog does not remove an item.
            var submitted=SubmitSlot("Discard",type,slot,()=>
            {
                if(!MatchesDiscardDialog()||!GcRejectedSource(type,slot))
                    throw new InvalidOperationException("Discard identity changed at confirmation");
                new AddonMaster.SelectYesno((AtkUnitBase*)address).Yes();
            },1);
            if(submitted)discardSource=null;
            else CancelSurplusDiscard();
            return false;
        }
        if(!SurplusNeedsGcInspection())return true;
        if(IsOccupied()||Utils.AnimationLock!=0)return false;
        if(TryGetAddonByName<AtkUnitBase>("SelectYesno",out var existing)&&existing->IsVisible)return false;
        var manager=InventoryManager.Instance();if(manager==null)return false;
        foreach(var type in Utils.PlayerInvetories)
        {
            var bag=manager->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)return false;
            for(var slot=0;slot<bag->Size;slot++)
            {
                var identity=ReadIdentity(type,slot);
                if(identity==null||identity.Quantity!=1||discardAttempted.Contains((type,slot))||
                    !SurplusDiscardCandidate(identity.ItemId))continue;
                discardAttempted.Add((type,slot));
                if(!GcRejectedSource(type,slot))
                {
                    Svc.Log.Information("[Steward discard] Held item {Item}: no matching completed GC inspection for this carried copy",identity.ItemId);
                    continue;
                }
                if(!AllowsSlot("Discard",type,slot))continue;
                discardSource=identity;discardOpened=Environment.TickCount64;
                MaintainPauses();
                Svc.Log.Information("[Steward discard] Opening confirmation for verified surplus item {Item}",identity.ItemId);
                Utils.ExecuteDiscardSafely(type,slot,identity.ItemId);
                return false;
            }
        }
        return true;
    }
}
