import json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class IntegrationContract(unittest.TestCase):
 def test_identity_is_unchanged(self):
  manifest=json.loads((ROOT/'AutoRetainer/AutoRetainer.json').read_text(encoding='utf-8-sig'))
  self.assertEqual(manifest['InternalName'],'AutoRetainer');self.assertEqual(manifest['Name'],'AutoRetainer')
  project=(ROOT/'AutoRetainer/AutoRetainer.csproj').read_text()
  self.assertIn('<AssemblyName>AutoRetainer</AssemblyName>',project)
 def test_every_disposal_route_is_gated(self):
  paths={'Internal/InventoryManagement/InventorySpaceManager.cs':'AllowsSlot("Vendor"','Internal/InventoryManagement/NpcSaleManager.cs':'AllowsSlot("Vendor"','Scheduler/Tasks/TaskDesynthItems.cs':'AllowsSlot("Desynth"','Modules/GcHandin/AutoGCHandin.cs':'Allows("Gc"','Helpers/Utils.cs':'AllowsSlot("Discard"'}
  for path,guard in paths.items():
   with self.subTest(path=path):self.assertIn(guard,(ROOT/'AutoRetainer'/path).read_text())
 def test_opt_in_and_unavailable_provider_holds(self):
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('if(!ledger.Owners.TryGetValue(cid, out var owner))',text)
  self.assertIn('return action is not ("Gc" or "Desynth" or "Vendor")||!C.CompanionRequiredCleanupPlans.Contains(Data.GetIMSettings().GUID);',text)
  self.assertIn('return result == "Allow" && Connected(cid)',text)
  self.assertIn('Environment.TickCount64 - heartbeat > 5000',text)
  self.assertNotIn('WriteOfflineCharacterData',text)
 def test_storage_is_not_conflated_with_disposal(self):
  self.assertIn('AllowsSlot("Armoire"',(ROOT/'AutoRetainer/Services/CabinetManager.cs').read_text())
  self.assertIn('AllowsDresserBatch()',(ROOT/'AutoRetainer/Services/MirageManager.cs').read_text())
 def test_release_and_update_guards(self):
  sync=(ROOT/'.github/workflows/sync-upstream.yml').read_text()
  self.assertIn('PunishXIV/AutoRetainer.git',sync);self.assertIn('upstream master',sync)
  self.assertIn('merge_routine_upstream.py',sync);self.assertIn('dispatch_routine_release.py --recover',sync)
  release=(ROOT/'.github/workflows/release-fork.yml').read_text()
  self.assertIn('publish-release@b8b369ec2b8f45efa2a8eabb094b13226bc815b9',release)
  self.assertIn('origin/master',release)
 def test_pinned_dependencies_are_not_updated_by_build(self):
  self.assertNotIn('submodule update --remote',(ROOT/'tools/build.ps1').read_text())
 def test_managed_discard_never_enters_confirmation(self):
  text=(ROOT/'AutoRetainer/Scheduler/Tasks/TaskRecursiveItemDiscard.cs').read_text()
  self.assertIn('if(CompanionPolicy.IsManaged) return;',text)
  self.assertIn('private static bool ConfirmDiscard()\n    {\n        if(CompanionPolicy.IsManaged) return true;',text)
 def test_final_gc_confirmation_rechecks_live_policy(self):
  text=(ROOT/'AutoRetainer/Modules/GcHandin/AutoGCHandin.cs').read_text()
  self.assertLess(text.index('if(!PendingAllowed(MatchesPendingReward(&addon->AtkUnitBase)))return false;'),text.index('new AddonMaster.GrandCompanySupplyReward(addon).Deliver()'))
  self.assertIn('PendingAllowed(pendingDelivered && PolicyDialog.Matches(pendingRewardId,addon->ParentId,addon->HostId))',text)
  self.assertIn('var pending=pendingConfirmation;',text)
  self.assertIn('InventoryFingerprint(pending.ItemId)',text)
  confirmation=text.split('private static bool PendingAllowed(')[1].split('private static bool MatchesPendingReward')[0]
  self.assertNotIn('GetHandinItems',confirmation)
  self.assertNotIn('items[pendingIndex]',confirmation)
  reward=text.split('private static bool MatchesPendingReward')[1].split('internal static bool IsEnabled')[0]
  self.assertIn('PolicyDialog.MatchesItemName(expected,actual)',reward)
  self.assertNotIn('.Cleanup()',reward)
 def test_gc_approval_is_captured_only_for_an_actual_selection(self):
  text=(ROOT/'AutoRetainer/Modules/GcHandin/AutoGCHandin.cs').read_text().split('internal static void InvokeHandin(')[1].split('internal static bool HasInInventory')[0]
  self.assertLess(text.index('FrameThrottler.Throttle'),text.index('pendingConfirmation=new'))
  self.assertIn('dialog->IsVisible',text)
  self.assertLess(text.index('pendingConfirmation=new'),text.index('Callback.Fire'))
 def test_quick_sell_and_low_level_sales_are_gated(self):
  text=(ROOT/'AutoRetainer/Modules/QuickSellItems.cs').read_text()
  self.assertIn('!CompanionPolicy.AllowsSlot("Vendor"',text)
  self.assertIn('!CompanionPolicy.AllowsSlot("Market"',text)
  text=(ROOT/'AutoRetainer/Internal/Memory.cs').read_text()
  self.assertIn('command == RetainerItemCommand.HaveRetainerSellItem && !CompanionPolicy.AllowsSlot',text)
  self.assertIn('public void SellItemToShop(InventoryType type, int slot)\n    {\n        if(!CompanionPolicy.AllowsSlot',text)
 def test_managed_bulk_cannot_outlive_per_item_policy(self):
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  body=text.split('internal static unsafe bool AllowsDresserBatch()')[1].split('internal static void Shutdown()')[0]
  self.assertIn('if(!ledger.Owners.ContainsKey(Svc.ClientState.LocalContentId)) return true;',body)
  self.assertIn('return false;',body)
  self.assertNotIn('AllowsSlot("Dresser"',body)
 def test_lease_watchdog_only_releases_its_recorded_handoff(self):
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('handoffCharacter=plugin=="Steward"?Svc.ClientState.LocalContentId:0',text)
  self.assertIn('if(handoffCharacter!=0 && !OwnsHandoff(handoffCharacter))',text)
  self.assertEqual(text.count('if(!OwnsHandoff(cid)) return false;'),2)
  self.assertIn('internal const int Version = 6;',text)
  ipc=(ROOT/'AutoRetainer/Modules/IPC.cs').read_text()
  self.assertIn('CompanionPolicy.BeginHandoff(pluginName)',ipc)
  self.assertIn('CompanionPolicy.EndHandoff()',ipc)
 def test_standalone_control_is_owned_and_does_not_mutate_general_suppression(self):
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('if(control.Active && !OwnsControl(control.Owner))control.Release(control.Owner);',text)
  self.assertIn('if(!ready || ControlActive || HasFault || submission!=null&&!ownSubmission)return false;',text)
  self.assertNotIn('IPC.Suppressed=',text)
  self.assertIn('if(CompanionPolicy.StartupBlocked || CompanionPolicy.ControlActive || CompanionPolicy.HasFault){CompanionPolicy.MaintainPauses();return;}',(ROOT/'AutoRetainer/AutoRetainer.cs').read_text(encoding='utf-8-sig'))
 def test_gc_hq_confirmation_is_owned(self):
  text=(ROOT/'AutoRetainer/Modules/GcHandin/AutoGCHandin.cs').read_text()
  self.assertIn('PolicyDialog.Matches(pendingRewardId,addon->ParentId,addon->HostId)',text)
 def test_gc_submits_only_once_and_persists_uncertain_hold(self):
  text=(ROOT/'AutoRetainer/Modules/GcHandin/AutoGCHandin.cs').read_text()
  self.assertLess(text.index('deliveryGate.Submitted'),text.index('new AddonMaster.GrandCompanySupplyReward(addon).Deliver()'))
  self.assertLess(text.index('hqGate.Submitted'),text.index('new AddonMaster.SelectYesno((IntPtr)addon).Yes()'))
  self.assertIn('CompanionPolicy.ReportFault(reason)',text)
  self.assertIn('GC stopped before inventory acknowledgement',text)
  self.assertLess(text.index('CompanionPolicy.BeginTransaction();'),text.index('new AddonMaster.GrandCompanySupplyReward(addon).Deliver()'))
  self.assertIn('CompanionPolicy.CompleteTransaction();',text)
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('ledger.Faults.Add(cid)',text)
  self.assertIn('ledger.Faults.UnionWith(ledger.Pending);',text)
  self.assertIn('Steward.InventoryPolicy.Fault',text)
  self.assertIn('addon->IsVisible)return false;',text)
 def test_existing_dialog_automation_is_paused_before_companion_work(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('NewYesAlreadyManager.Lock();TextAdvanceManager.LockTA();',source)
  self.assertIn('IPC.Suppressed,MaintainPauses)',source)
  for name in ['NewYesAlreadyManager','TextAdvanceManager']:
   source=(ROOT/'AutoRetainer/Modules'/f'{name}.cs').read_text()
   self.assertIn('CompanionPolicy.ControlActive || CompanionPolicy.HasFault',source)
 def test_review_resets_old_gc_state_only_after_persistence(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('PolicyPersistence.ClearReview(',source)
  self.assertIn('AutoGCHandin.ClearCompanionTransaction',source)
  source=(ROOT/'AutoRetainer/Modules/GcHandin/AutoGCHandin.cs').read_text()
  reset=source.split('internal static void ClearCompanionTransaction()')[1].split('private static void HoldPending')[0]
  self.assertIn('pendingConfirmation=null;pendingDelivered=false;',reset)
  self.assertIn('deliveryGate.Reset();hqGate.Reset();',reset)
 def test_native_desynthesis_target_and_post_callback_source_are_bound(self):
  source=(ROOT/'AutoRetainer/Scheduler/Tasks/TaskDesynthItems.cs').read_text()
  self.assertIn('var selected=agent->DesynthItemSlot;',source)
  self.assertLess(source.index('expected.Matches(new(selected.ItemId'),source.index('->AgentInterface.ReceiveEvent'))
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('expected.Matches(new(item->ItemId',source)
  self.assertIn('!Data.GetIMSettings().IMProtectList.Contains(expected.ItemId)',source)
 def test_native_mutations_use_write_ahead_submission_and_acknowledgement(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionSubmission.cs').read_text()
  self.assertIn('PolicyPersistence.Submit(()=>{MaintainPauses();BeginTransaction();}',source)
  self.assertIn('AllowsSlot(action,type,slot,true)',source)
  self.assertIn('PolicySlotAcknowledgement.Check(',source)
  ack=source.split('if(result==PolicyAck.Completed)')[1]
  self.assertIn('CompleteTransaction();submission=null;',ack)
  memory=(ROOT/'AutoRetainer/Internal/Memory.cs').read_text()
  self.assertEqual(memory.count('CompanionPolicy.SubmitSlot("Vendor"'),2)
  self.assertIn('CompanionPolicy.SubmitSlot("Desynth"',(ROOT/'AutoRetainer/Scheduler/Tasks/TaskDesynthItems.cs').read_text())
  self.assertIn('CompanionPolicy.SubmitArmoire(', (ROOT/'AutoRetainer/Services/CabinetManager.cs').read_text())
 def test_recovery_and_detach_cannot_resume_backlogged_actions(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  clear=source.split('private static unsafe bool ClearFault')[1].split('private static bool Connected')[0]
  self.assertIn('P.TaskManager.IsBusy',clear)
  detach=source.split('private static bool Detach')[1].split('/// <summary>')[0]
  self.assertIn('ledger.Pending.Contains(cid)',detach)
  self.assertIn('ledger.Faults.Contains(cid)',detach)
 def test_pending_acknowledgement_waits_in_original_workflows(self):
  for path in ['Internal/InventoryManagement/InventorySpaceManager.cs','Internal/InventoryManagement/NpcSaleManager.cs','Scheduler/Tasks/TaskDesynthItems.cs','Services/CabinetManager.cs']:
   self.assertIn('if(CompanionPolicy.SubmissionPending)return false;',(ROOT/'AutoRetainer'/path).read_text())
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('retainers->LastSelectedRetainerId!=expected.Retainer',source)
  self.assertIn('InventoryItem.ItemFlags.HighQuality)!=expected.Hq',source)
 def test_connection_retry_persists_even_an_existing_memory_owner(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  connect=source.split('private static bool Connect(')[1].split('private static bool Pulse')[0]
  self.assertNotIn('if(!ledger.Owners.ContainsKey(cid))',connect)
  self.assertLess(connect.index('SavePolicy();'),connect.index('character = cid;'))
 def test_safety_writes_do_not_use_exception_swallowing_config_helper(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertNotIn('EzConfig.Save()',source)
  self.assertIn('AtomicState.Write(PolicyPath,CapturePolicy())',source)
  self.assertLess(source.index('saved.Validate();'),source.index('Svc.Framework.Update+=Tick'))
 def test_ordinary_config_imports_cannot_erase_runtime_policy(self):
  source=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  allows=source.split('internal static unsafe bool Allows(')[1].split('internal static unsafe bool AllowsSlot')[0]
  self.assertIn('ledger.Owners.TryGetValue',allows)
  self.assertNotIn('C.CompanionPolicy',allows)
  self.assertIn('internal static bool StartupBlocked=>!ready;',source)
if __name__=='__main__':unittest.main()
