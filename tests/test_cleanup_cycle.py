import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]/'AutoRetainer'
class CleanupCycleWiring(unittest.TestCase):
 def test_reuses_upstream_actions_and_has_no_native_callbacks(self):
  text=(ROOT/'Modules/CompanionCleanup.cs').read_text()
  for action in ['TaskDeliverItems.Enqueue()','TaskDesynthItems.Enqueue()','TaskVendorItems.EnqueueFromCommand()','TaskTeleportToProperty.EnqueueIfNeededAndPossible(false)','EnqueueOverflow(cleanup.Character,disposalOnly:true)']:
   self.assertIn(action,text)
  self.assertNotIn('Callback.Fire',text)
  self.assertIn('Data.GetIMSettings().IMDry',text)
  self.assertIn('SchedulerMain.PluginEnabled||MultiMode.Active',text)
  self.assertIn('!Data.GetIsTeleportEnabledForRetainers()',text)
  self.assertIn('OtherDisposalRemaining()',text)
  self.assertIn('cleanupGcStarted&&!cleanupGcFinished',text)
  self.assertIn('cleanupTaskFinished',text)
 def test_progress_is_counted_after_acknowledgement(self):
  submission=(ROOT/'Modules/CompanionSubmission.cs').read_text()
  ack=submission.split('if(result==PolicyAck.Completed)')[1]
  self.assertLess(ack.index('CompleteTransaction()'),ack.index('cleanup.ConfirmDisposal'))
  gc=(ROOT/'Modules/GcHandin/AutoGCHandin.cs').read_text()
  self.assertLess(gc.index('CompanionPolicy.CompleteTransaction()'),gc.index('CompanionPolicy.CleanupDisposalConfirmed()'))
 def test_retainer_exclusions_and_handoffs_are_preserved(self):
  cycle=(ROOT/'Modules/CompanionCleanup.cs').read_text()
  self.assertIn('C.CompanionGilOnlyRetainers.Contains(retainer)',cycle)
  self.assertIn('Steward.InventoryPolicy.CleanupRetainerAllowed',cycle)
  self.assertIn('!OwnsHandoff(cid)',cycle)
  storage=(ROOT/'Modules/CompanionStorage.cs').read_text()
  self.assertIn('!cleanup.Active||!CleanupRetainerAllowed(cid,retainerId)',storage)
  self.assertIn('TaskPostprocessRetainerIPC.Enqueue(name,"Steward")',storage)

