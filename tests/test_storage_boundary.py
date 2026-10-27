import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'AutoRetainer'
class StorageBoundary(unittest.TestCase):
 def test_storage_checks_candidates_and_numeric_confirmation(self):
  text=(ROOT/'Scheduler/Tasks/TaskEntrustDuplicates.cs').read_text()
  self.assertGreaterEqual(text.count('CompanionPolicy.AllowsSlot'),6)
  self.assertEqual(text.count('if(!StorageConfirmationAllowed())'),2)
  self.assertIn('slot->ItemId==pendingStorageItem&&slot->Quantity==pendingStorageQuantity',text)
 def test_gil_only_branch_skips_ventures_and_postprocess(self):
  text=(ROOT/'Scheduler/SchedulerMain.cs').read_text()
  branch=text.split('if(C.CompanionGilOnlyRetainers.Contains(ret.RetainerID))')[1].split('VentureOverride = 0')[0]
  self.assertIn('TaskWithdrawGil.Enqueue(100)',branch)
  self.assertIn('return;',branch)
  self.assertIn('TaskPostprocessRetainerIPC.Enqueue(retainer,"Steward")',branch)
  self.assertIn('CompanionPolicy.StrategyDue(ret.RetainerID)',branch)
  self.assertNotIn('TaskReassignVenture',branch)
  self.assertIn('CompanionGilOnlyRetainers.Contains(retainer.RetainerID)',(ROOT/'Modules/CompanionStorage.cs').read_text())
 def test_gil_confirmation_rechecks_policy(self):
  self.assertIn('ProcessBankOrCancel(!global::AutoRetainer.Modules.CompanionPolicy.Allows("Gil",0))',(ROOT/'Scheduler/Tasks/TaskWithdrawGil.cs').read_text())

 def test_retainer_selection_decodes_native_utf8_name(self):
  text=(ROOT/'Modules/CompanionStorage.cs').read_text()
  self.assertIn('foreach(var retainer in Internal.GameRetainerManager.Retainers)',text)
  self.assertIn('var name=retainer.Name;',text)
  self.assertNotIn('retainer.Name.ToString()',text)
  self.assertIn('TaskSelectRetainer.Enqueue(name);',text)
  self.assertIn('string.IsNullOrWhiteSpace(name)',text)
