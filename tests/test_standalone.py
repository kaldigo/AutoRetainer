import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'AutoRetainer'

class Standalone(unittest.TestCase):
    def test_missing_registration_heartbeat_and_callback_share_safe_fallback(self):
        policy = (ROOT/'Modules/CompanionPolicy.cs').read_text(encoding='utf-8')
        body = policy.split('internal static unsafe bool Allows(')[1].split('internal static unsafe bool AllowsSlot')[0]
        self.assertEqual(body.count('StandaloneAllows(action,container)'), 3)
        self.assertLess(body.index('if(!ready || ControlActive || HasFault'), body.index('if(!ledger.Owners.TryGetValue'))
        self.assertLess(body.index('if(ledger.Pending.Contains'), body.index('if(!ledger.Owners.TryGetValue'))
        self.assertIn('return result == "Allow" && Connected(cid)', body)
        self.assertIn('!HasFault&&!ControlActive&&!ledger.Pending.Contains(cid)', body)
        self.assertIn('Utils.RetainerInventoriesWithCrystals.Contains', policy)
        self.assertIn('GetIpcSubscriber<string,string>(callback).HasFunction', policy)
        self.assertIn('|| !ProviderAvailable', body)

    def test_final_native_and_glamour_entrypoints_do_not_bypass_absent_steward(self):
        submit = (ROOT/'Modules/CompanionSubmission.cs').read_text(encoding='utf-8')
        self.assertIn('if(!IsManaged){if(!AllowsSlot(action,type,slot))return false;native();return true;}', submit)
        armoire = submit.split('internal static unsafe bool SubmitArmoire')[1].split('private static unsafe void CheckSubmission')[0]
        self.assertIn('if(!CompanionAvailable)return false;', armoire)
        gc = (ROOT/'Modules/GcHandin/AutoGCHandin.cs').read_text(encoding='utf-8')
        self.assertIn('if(!CompanionPolicy.CompanionAvailable)return false;', gc)
        self.assertNotIn('if(!CompanionPolicy.IsManaged)return true;', gc)
        for file,entry in [('CabinetManager.cs','public void EnqueueGoToInnAndDeliverEverything()'),('MirageManager.cs','public void EnqueueGoToInnAndDeliverEverything(bool leaveOpen)')]:
            source=(ROOT/'Services'/file).read_text(encoding='utf-8').split(entry)[1]
            self.assertIn('if(!CompanionPolicy.CompanionAvailable)return;', source[:170])
        post=(ROOT/'Scheduler/Tasks/TaskPostprocessRetainerIPC.cs').read_text(encoding='utf-8')
        self.assertIn('if(!Modules.CompanionPolicy.CompanionAvailable)return;', post)
        character=(ROOT/'Scheduler/Tasks/TaskPostprocessCharacterIPC.cs').read_text(encoding='utf-8')
        self.assertIn('if(!Modules.CompanionPolicy.CompanionAvailable)return;', character)

    def test_manual_retainer_runs_native_ventures_but_not_ordinary_item_automation(self):
        scheduler=(ROOT/'Scheduler/SchedulerMain.cs').read_text(encoding='utf-8')
        self.assertIn('if(manualItems&&!VentureDue(ret,adata))', scheduler)
        self.assertIn('if(!manualItems&&C.EnableEntrustManager && selectedPlan != null)', scheduler)
        self.assertEqual(scheduler.count('if(!manualItems&&Data.GetIMSettings().IMEnableAutoVendor)'), 2)
        self.assertIn('if(!manualItems)TaskPostprocessRetainerIPC.Enqueue(retainer);', scheduler)
        self.assertIn('Modules.PolicyStandalone.VisitManualRetainer', scheduler)
        self.assertIn('TaskReassignVenture.Enqueue();', scheduler)
        wait=(ROOT/'Helpers/Utils.cs').read_text(encoding='utf-8').split('internal static bool IsAllCurrentCharacterRetainersHaveMoreThan5Mins()')[1].split('internal static string GetActivePlayerInventoryName')[0]
        self.assertIn('PolicyStandalone.FinishesSoon', wait)
        self.assertNotIn('CompanionGilOnlyRetainers', wait)
