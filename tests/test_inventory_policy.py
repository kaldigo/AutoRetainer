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
if __name__=='__main__':unittest.main()
