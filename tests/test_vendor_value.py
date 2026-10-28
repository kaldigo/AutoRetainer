import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'AutoRetainer'
class VendorValue(unittest.TestCase):
 def test_vendor_value_gates_planning_preview_and_final_permission(self):
  text=(ROOT/'Modules/CompanionPolicy.cs').read_text(encoding='utf-8')
  self.assertIn('item.PriceLow:null',text)
  self.assertIn('(action!="Vendor"||VendorDisposalEligible(id))&&PolicyDisposal.Allows',text)
  selected=text.split('"AutoRetainer.InventoryPolicy.DisposalSelected"')[1].split('DisposalPreview')[0]
  self.assertIn('VendorDisposalEligible(id)',selected)
  preview=text.split('private static string DisposalPreview')[1].split('private static bool GcDisposalCandidate')[0]
  self.assertIn('VendorDisposalEligible(id)',preview)
  self.assertIn('no NPC sale value',preview)
  remaining=(ROOT/'Modules/CompanionCleanup.cs').read_text().split('private static unsafe bool OtherDisposalRemaining')[1]
  self.assertIn('AllowsSlot("Vendor",type,slot)',remaining)
