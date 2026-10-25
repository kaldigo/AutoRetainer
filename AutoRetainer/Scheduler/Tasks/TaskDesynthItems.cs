using Dalamud.Game.ClientState.Conditions;
using ECommons.ExcelServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace AutoRetainer.Scheduler.Tasks;
public static unsafe class TaskDesynthItems
{
    private static InventoryType[] DesynthableInventories => [.. Utils.PlayerInvetories, 
        .. (Data.GetIMSettings().IMEnableItemDesynthesisFromArmory ? Utils.PlayerArmory : [])];

    public static void Enqueue()
    {
        P.TaskManager.Enqueue(RecursivelyDesynthItems, new(timeLimitMS: 10 * 60 * 1000, abortOnTimeout: false));
        P.TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Occupied39]);
    }

    private static bool? RecursivelyDesynthItems()
    {
        if(CompanionPolicy.SubmissionPending)return false;
        if(!QuestManager.IsQuestComplete(65688)) return true;

        var eligibleItems = GetDesynthesisEligibleItems();
        if(eligibleItems.Count == 0) return true;

        foreach(var item in eligibleItems)
        {
            // check IsOccupied vs just Occupied39 or else it might trigger when exiting the retainer menu
            if(Utils.AnimationLock == 0 && !IsOccupied())
            {
                if(Utils.GenericThrottle && EzThrottler.Throttle("DesynthingItem", 1000))
                    DesynthItem(item);
            }
            else
                Utils.RethrottleGeneric();
        }
        return false;
    }

    private static List<Pointer<InventoryItem>> GetDesynthesisEligibleItems()
    {
        List<Pointer<InventoryItem>> items = [];
        foreach(var inv in DesynthableInventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for(var i = 0; i < cont->Size; ++i)
            {
                var item = cont->GetInventorySlot(i);
                if(IsOnIMList(item->ItemId) && DesynthEligible(item->ItemId) && !S.CabinetManager.ShouldExcludeItemFromProcessing(item->ItemId))
                {
                    items.Add(item);
                }
            }
        }
        return items;
    }

    public static bool DesynthEligible(uint itemID)
    {
        return Data.GetIMSettings().IMEnableItemDesynthesis
            && ExcelItemHelper.Get(itemID).Value.Desynth > 0
            && PlayerState.Instance()->ClassJobLevels[ExcelItemHelper.Get(itemID).Value.ClassJobRepair.Value.ExpArrayIndex] >= 30;
    }

    private static bool IsOnIMList(uint itemID)
    {
        return !Data.GetIMSettings().IMProtectList.Contains(itemID) && Data.GetIMSettings().IMAutoVendorHard.Contains(itemID) && CompanionPolicy.Allows("Desynth", itemID);
    }

    private static void DesynthItem(InventoryItem* item)
    {
        if(!CompanionPolicy.AllowsSlot("Desynth", item->Container, item->Slot)) return;
        var expected=new PolicyItemIdentity(item->ItemId,(uint)item->Quantity,(uint)item->Container,item->Slot,(int)item->Flags);
        Svc.Log.Info($"Desynthing {ExcelItemHelper.GetName(ExcelItemHelper.Get(item->ItemId), true)} [Container={item->Container},Slot={item->Slot}]");
        var agent=AgentSalvage.Instance();
        if(agent==null)return;
        agent->SalvageItem(item);
        if(!expected.Matches(new(item->ItemId,(uint)item->Quantity,(uint)item->Container,item->Slot,(int)item->Flags)) ||
            !CompanionPolicy.AllowsSlot("Desynth", item->Container, item->Slot)) return;
        var selected=agent->DesynthItemSlot;
        if(CompanionPolicy.IsManaged && !expected.Matches(new(selected.ItemId,(uint)selected.Quantity,(uint)selected.Container,selected.Slot,(int)selected.Flags)))
        {
            CompanionPolicy.ReportFault("Desynthesis selection changed before confirmation");
            return;
        }
        // Stack values cannot be captured in a delegate. Recreate the same native parameters inside the guarded submission.
        var address=(nint)agent;
        CompanionPolicy.SubmitSlot("Desynth",item->Container,item->Slot,()=>ConfirmDesynth(address,expected),1);
    }
    private static void ConfirmDesynth(nint address,PolicyItemIdentity expected)
    {
        var selected=((AgentSalvage*)address)->DesynthItemSlot;
        if(CompanionPolicy.IsManaged && !expected.Matches(new(selected.ItemId,(uint)selected.Quantity,(uint)selected.Container,selected.Slot,(int)selected.Flags)))
            throw new InvalidOperationException("Desynthesis selection could not be verified at submission");
        var retval=new AtkValue();
        Span<AtkValue> param=[new AtkValue{Type=ValueType.Int,Int=0},new AtkValue{Type=ValueType.Bool,Byte=1}];
        ((AgentSalvage*)address)->AgentInterface.ReceiveEvent(&retval,param.GetPointer(0),2,1);
    }
}
