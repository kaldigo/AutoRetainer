import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'AutoRetainer'
class DisposalBoundary(unittest.TestCase):
 def test_final_policy_checks_disposal_method(self):
  source=(ROOT/'Modules/CompanionPolicy.cs').read_text()
  self.assertIn('return result == "Allow" && Connected(cid) && DisposalMethodAllows(action,itemId)',source)
  self.assertIn('PolicyDisposal.Allows(action,Data.GetIMSettings().IMDry',source)
 def test_prepared_plans_cannot_escape_to_unmanaged_disposal(self):
  source=(ROOT/'Modules/CompanionPolicy.cs').read_text()
  branch=source.split('if(!ledger.Owners.TryGetValue(cid, out var owner))')[1].split('if(character != cid')[0]
  self.assertIn('StandaloneAllows(action,container)',branch)
 def test_demo_blocks_disposal_routes_and_withdrawals(self):
  source=(ROOT/'Modules/CompanionPolicy.cs').read_text()
  self.assertIn('!Data.GetIMSettings().IMDry&&Utils.IsItemSelectedByHardList',source)
  self.assertIn('operation is "Gc" or "Desynth" or "Vendor" && Data.GetIMSettings().IMDry',source)
  self.assertIn('case "Gc": return Scheduler.Tasks.TaskDeliverItems.Enqueue();',source)
 def test_gc_travel_requires_approved_items_before_buffs_or_travel(self):
  source=(ROOT/'Scheduler/Tasks/TaskDeliverItems.cs').read_text()
  self.assertLess(source.index('Data.GetIMSettings().IMDry||!GCContinuation.DoesInventoryHaveDeliverableItem()'),source.index('UsingItemBuff = false;',source.index('public static bool Enqueue')))
  source=(ROOT/'Modules/GcHandin/GCContinuation.cs').read_text()
  self.assertIn('if(!CompanionPolicy.AllowsSlot("Gc",x,i))continue;',source)
