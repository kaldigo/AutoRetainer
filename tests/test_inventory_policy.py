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
  self.assertIn('if(!C.CompanionPolicyOwners.TryGetValue(cid, out var owner)) return true;',text)
  self.assertIn('return result == "Allow";',text)
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
  self.assertLess(text.index('if(!PendingAllowed() || !MatchesPendingReward'),text.index('new AddonMaster.GrandCompanySupplyReward(addon).Deliver()'))
  self.assertIn('!pendingDelivered || !PendingAllowed()',text)
  self.assertIn('InventoryFingerprint(pendingConfirmation.ItemId)',text)
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
  self.assertIn('if(!C.CompanionPolicyOwners.ContainsKey(Svc.ClientState.LocalContentId)) return true;',body)
  self.assertIn('return false;',body)
  self.assertNotIn('AllowsSlot("Dresser"',body)
 def test_lease_watchdog_only_releases_its_recorded_handoff(self):
  text=(ROOT/'AutoRetainer/Modules/CompanionPolicy.cs').read_text()
  self.assertIn('handoffCharacter=plugin=="Steward"?Svc.ClientState.LocalContentId:0',text)
  self.assertIn('if(handoffCharacter!=0 && !OwnsHandoff(handoffCharacter))',text)
  self.assertEqual(text.count('if(!OwnsHandoff(cid)) return false;'),2)
  self.assertIn('internal const int Version = 2;',text)
  ipc=(ROOT/'AutoRetainer/Modules/IPC.cs').read_text()
  self.assertIn('CompanionPolicy.BeginHandoff(pluginName)',ipc)
  self.assertIn('CompanionPolicy.EndHandoff()',ipc)
if __name__=='__main__':unittest.main()
