import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'AutoRetainer'

class StorageAuthority(unittest.TestCase):
    def test_unique_conflict_is_filtered_at_native_candidate_and_submission_boundary(self):
        policy = (ROOT / 'Modules/CompanionPolicy.cs').read_text(encoding='utf-8')
        slot = policy.split('internal static unsafe bool AllowsSlot')[1].split('private static bool Run')[0]
        self.assertLess(slot.index('UniqueEntrustAllowed'), slot.index('if(!Allows(action'))
        plans = (ROOT / 'Modules/CompanionStoragePlans.cs').read_text(encoding='utf-8')
        self.assertIn('CountStored(id,false)', plans)
        self.assertIn('CountStored(id,true)', plans)
        self.assertIn('if(!item.IsUnique)return true;', plans)
        self.assertIn('already owns the unique item. Continuing with other items.', plans)

    def test_overflow_uses_live_native_plan_and_source(self):
        text = (ROOT / 'Modules/CompanionStoragePlans.cs').read_text(encoding='utf-8')
        for gate in ['!C.EnableEntrustManager', 'plan.ManualPlan', 'plan.GetAllowedInventories()', 'identity.Quantity!=source.Quantity', 'PolicyStorageRouting.Selects', 'GetAdditionalDataKey(cid,retainer.Name,false)']:
            self.assertIn(gate, text)
        self.assertIn('!StoragePlanAllows(cid,request)', (ROOT / 'Modules/CompanionStorage.cs').read_text(encoding='utf-8'))

    def test_currency_role_migration_does_not_overwrite_native_choice(self):
        text = (ROOT / 'Modules/CompanionStoragePlans.cs').read_text(encoding='utf-8')
        self.assertLess(text.index('ContainsKey(cid))return true'), text.index('CompanionCurrencyRetainers.Add'))
        self.assertIn('try{EzConfig.Save();return true;}', text)
        self.assertIn('catch{C.CompanionCurrencyRetainers.Remove(cid);throw;}', text)
        self.assertIn('if(!Connected(cid))return false;', text)

    def test_non_equipment_category_selection_is_scoped_and_still_stack_limited(self):
        text = (ROOT / 'Modules/CompanionPolicy.cs').read_text(encoding='utf-8')
        selection = text.split('internal static bool NonEquipmentDisposalSelected')[1].split('private static string DisposalPreview')[0]
        self.assertIn('!UsesStewardCleanupPlan||!Connected(cid)', selection)
        self.assertIn('item.EquipSlotCategory.RowId!=0', selection)
        self.assertIn('catch{return false;}', selection)
        native = (ROOT / 'Internal/InventoryManagement/InventorySpaceManager.cs').read_text(encoding='utf-8')
        self.assertIn('CompanionPolicy.NonEquipmentDisposalSelected(item.ItemId)', native)
        self.assertIn('item.Quantity < Data.GetIMSettings().IMAutoVendorHardStackLimit', native)
        self.assertIn('CompanionPolicy.AllowsSlot("Vendor", invType, i)', native)
        desynth = (ROOT / 'Scheduler/Tasks/TaskDesynthItems.cs').read_text(encoding='utf-8')
        self.assertIn('IsOnIMList(item->ItemId,item->Quantity)', desynth)
        self.assertIn('Utils.IsItemSelectedByHardList(itemID,quantity)', desynth)

    def test_followup_uses_native_entrust_without_companion_withdrawals(self):
        policy=(ROOT/'Modules/CompanionPolicy.cs').read_text()
        self.assertIn('case "Entrust": return EnqueueOverflow(cid,entrustOnly:true);',policy)
        storage=(ROOT/'Modules/CompanionStorage.cs').read_text()
        for gate in ['!Connected(cid)||HasFault','!C.EnableEntrustManager||plan==null','!p.ManualPlan','C.CompanionGilOnlyRetainers.Contains(retainerId)']:
            self.assertIn(gate,storage)
        self.assertIn('if(entrustOnly)TaskEntrustDuplicates.EnqueueNew(plan!);',storage)
        self.assertIn('else TaskPostprocessRetainerIPC.Enqueue(name,"Steward");',storage)
