using AutoRetainerAPI.Configuration;

namespace AutoRetainer.Modules;

internal static class CompanionSettings
{
    // Only for managed plans inheriting protection alone. As with the existing empty-default fast path,
    // unchanged candidate lists remain read-through references. Never mutate either input or cache a
    // resolved protection list: each permission request must see edits immediately.
    internal static InventoryManagementSettings WithInheritedProtection(InventoryManagementSettings plan, InventoryManagementSettings defaults)
    {
        if(defaults.IMProtectList.Count==0)return plan;
        return new()
        {
            GUID=plan.GUID,Name=plan.Name,
            IMEnableCofferAutoOpen=plan.IMEnableCofferAutoOpen,IMEnableAutoVendor=plan.IMEnableAutoVendor,
            IMEnableContextMenu=plan.IMEnableContextMenu,IMSkipVendorIfRetainer=plan.IMSkipVendorIfRetainer,
            IMAutoVendorHard=plan.IMAutoVendorHard,IMAutoVendorHardIgnoreStack=plan.IMAutoVendorHardIgnoreStack,
            IMAutoVendorSoft=plan.IMAutoVendorSoft,IMDiscardList=plan.IMDiscardList,IMDiscardIgnoreStack=plan.IMDiscardIgnoreStack,
            IMDesynth=plan.IMDesynth,IMProtectList=plan.IMProtectList.Concat(defaults.IMProtectList).Distinct().ToList(),
            IMAutoVendorHardStackLimit=plan.IMAutoVendorHardStackLimit,IMDiscardStackLimit=plan.IMDiscardStackLimit,
            IMDry=plan.IMDry,IMEnableItemDesynthesis=plan.IMEnableItemDesynthesis,
            IMEnableItemDesynthesisFromArmory=plan.IMEnableItemDesynthesisFromArmory,IMEnableNpcSell=plan.IMEnableNpcSell,
            AllowSellFromArmory=plan.AllowSellFromArmory,EnableCabinetAutoDelivery=plan.EnableCabinetAutoDelivery,
            EnableMirageAutoDelivery=plan.EnableMirageAutoDelivery,AdditionModeProtectList=plan.AdditionModeProtectList,
            AdditionModeSoftSellList=plan.AdditionModeSoftSellList,AdditionModeHardSellList=plan.AdditionModeHardSellList,
            AdditionModeDiscardList=plan.AdditionModeDiscardList,AdditionModeDesynthList=plan.AdditionModeDesynthList,
            MaxCoffersAtOnce=plan.MaxCoffersAtOnce,
        };
    }
}
