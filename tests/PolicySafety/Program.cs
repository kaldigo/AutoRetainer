using AutoRetainer.Modules;

var pending=new PolicyConfirmation(12,42,"Inventory1/0/42/1/None",1000);
var total=0;
void Check(bool result,string name){total++;if(!result)throw new Exception(name);}
{
    void ToReview(PolicyCleanupCycle c,uint withdrawn,uint disposed)
    {
        if(c.Phase==CleanupPhase.Bell)c.Advance();
        c.Visited=2;c.ConfirmWithdrawal(withdrawn);c.Advance();
        c.ConfirmDisposal(disposed);c.Advance();c.Advance();c.Advance();c.Advance();
        Check(c.Phase==CleanupPhase.Review,"Cleanup uses withdrawal, GC, desynthesis, return and vendor stages");
    }
    var cycle=new PolicyCleanupCycle();
    Check(!cycle.Active,"Cleanup does not start on load");
    cycle.ConfirmDisposal(3);Check(cycle.Disposed==0,"Unrelated disposal while idle is not batch progress");
    cycle.Start(12);ToReview(cycle,5,5);cycle.Review(true,false);
    Check(cycle.Active&&cycle.Phase==CleanupPhase.Withdraw&&cycle.Pass==2,"Confirmed batch returns for another retainer pass");
    Check(cycle.Withdrawn==0&&cycle.Disposed==0,"Each batch requires new confirmed progress");
    ToReview(cycle,0,0);cycle.Review(true,false);
    Check(!cycle.Active&&cycle.Phase==CleanupPhase.Complete,"An empty completed sweep ends the cycle");
    Check(cycle.TotalDisposed==5&&cycle.TotalWithdrawn==5,"The final empty sweep preserves actual run totals");
    cycle.Start(12);ToReview(cycle,4,0);cycle.Review(true,false);
    Check(cycle.Phase==CleanupPhase.Stopped,"Withdrawals without confirmed disposal cannot loop");
    cycle.Start(12);ToReview(cycle,0,0);cycle.Review(false,false);
    Check(cycle.Phase==CleanupPhase.Stopped,"Full bags are a blocked batch, not successful completion");
    cycle.Start(12);ToReview(cycle,4,2);cycle.Review(true,true);
    Check(cycle.Phase==CleanupPhase.Stopped,"Remaining GC candidates stop further withdrawals");
    cycle.Start(12);ToReview(cycle,0,3);cycle.Review(true,false);
    Check(cycle.Active&&cycle.Pass==2,"Disposing carried items can free room for the first withdrawal batch");
    cycle.Stop("cancelled");Check(!cycle.Active,"Cancellation stops future batch phases");
    cycle.Start(12);ToReview(cycle,0,0);cycle.Visited=0;cycle.Review(true,false);
    Check(cycle.Phase==CleanupPhase.Stopped,"No included retainers cannot masquerade as a completed sweep");
    cycle.Start(12);
    for(var pass=0;pass<100;pass++){ToReview(cycle,1,1);cycle.Review(true,false);}
    Check(cycle.Phase==CleanupPhase.Stopped,"Bounded batch count prevents an endless changing inventory loop");
    Check(!new PolicyCleanupCycle().Active,"Plugin restart never resumes batch automation implicitly");
}
Check(pending.StillValid(12,42,pending.Fingerprint,1001,true),"Unchanged permitted confirmation");
Check(!pending.StillValid(12,42,pending.Fingerprint,1001,false),"Revoked protection or unavailable policy blocks final confirmation");
Check(!pending.StillValid(13,42,pending.Fingerprint,1001,true),"Character changed");
Check(!pending.StillValid(12,43,pending.Fingerprint,1001,true),"Selected item changed");
Check(!pending.StillValid(12,42,"Inventory1/1/42/1/None",1001,true),"Slot changed");
Check(!pending.StillValid(12,42,"Inventory1/0/42/2/None",1001,true),"Quantity changed");
Check(!pending.StillValid(12,42,"Inventory1/0/42/1/HighQuality",1001,true),"Quality changed");
Check(!pending.StillValid(12,42,null,1001,true),"Inventory unavailable");
Check(!pending.StillValid(12,42,pending.Fingerprint,11001,true),"Confirmation expired");
Check(!pending.StillValid(12,42,pending.Fingerprint,999,true),"Invalid clock order");
Check(!new PolicyConfirmation(0,42,"slot",1000).StillValid(0,42,"slot",1001,true),"No character");
Check(!new PolicyConfirmation(12,0,"slot",1000).StillValid(12,0,"slot",1001,true),"No item");
Check(!new PolicyConfirmation(12,42,"",1000).StillValid(12,42,"",1001,true),"Empty inventory evidence");
var endpoint="Steward.InventoryPolicy.Evaluate";
Check(PolicyDialog.MatchesItemName("Boots of the Divine Hero","Boots of the Divine Hero"),"Recorded item 8240 reward keeps internal spaces on both sides");
Check(PolicyDialog.MatchesItemName("Boots of the Divine Hero","  Boots of the Divine Hero\n"),"Outer dialog padding does not change item identity");
Check(!PolicyDialog.MatchesItemName("Boots of the Divine Hero","BootsoftheDivineHero"),"Do not strip meaningful internal spaces from only the reward");
Check(!PolicyDialog.MatchesItemName("Boots of the Divine Hero","Boots of the Divine Light"),"Another reward name is rejected");
Check(!PolicyDialog.MatchesItemName("Boots of the Divine Hero",null),"Missing reward name is rejected");
Check(!PolicyDialog.MatchesItemName("", ""),"Empty names cannot establish item identity");
Check(!PolicyDialog.MatchesItemName("Boots of the Divine Hero","Deliver Boots of the Divine Hero"),"A substring is not an exact reward identity");
// Reproduce the observed GC modal transition: approval survives the hidden list,
// but a matching dialog alone never overrides live inventory/policy evidence.
Check(pending.DialogStillValid(12,true,pending.Fingerprint,1047,true),"GC reward uses captured approval after its selection list disappears");
Check(!pending.DialogStillValid(12,false,pending.Fingerprint,1047,true),"Different or unidentified GC reward is denied");
Check(!pending.DialogStillValid(13,true,pending.Fingerprint,1047,true),"GC character change is denied");
Check(!pending.DialogStillValid(12,true,null,1047,true),"GC unloaded inventory is denied");
Check(!pending.DialogStillValid(12,true,"Inventory1/1/42/1/None",1047,true),"GC moved source is denied");
Check(!pending.DialogStillValid(12,true,"Inventory1/0/42/2/None",1047,true),"GC changed quantity is denied");
Check(!pending.DialogStillValid(12,true,"Inventory1/0/42/1/HighQuality",1047,true),"GC changed quality is denied");
Check(!pending.DialogStillValid(12,true,pending.Fingerprint,1047,false),"New protection or lost Steward permission denies GC confirmation");
Check(!pending.DialogStillValid(12,true,pending.Fingerprint,11001,true),"GC expired approval is denied even with a matching reward");
Check(!pending.DialogStillValid(12,true,pending.Fingerprint,999,true),"GC invalid approval clock is denied");
Check(!pending.DialogStillValid(12,PolicyDialog.Matches(5,6,7),pending.Fingerprint,1050,true),"Unrelated HQ dialog is denied");
Check(pending.DialogStillValid(12,PolicyDialog.Matches(5,5,7),pending.Fingerprint,1050,true),"Owned HQ follow-up uses the same approval without the hidden list");
Check(HandoffLease.Owns(12,12,12,endpoint,1000,1001,true),"Current Steward lease owns its handoff");
Check(!HandoffLease.Owns(0,12,12,endpoint,1000,1001,true),"A locked handoff alone is not Steward ownership");
Check(!HandoffLease.Owns(12,13,12,endpoint,1000,1001,true),"Different connected character cannot own handoff");
Check(!HandoffLease.Owns(12,12,13,endpoint,1000,1001,true),"Character change revokes handoff");
Check(!HandoffLease.Owns(12,12,12,endpoint,1000,6001,true),"Expired heartbeat revokes handoff");
Check(!HandoffLease.Owns(12,12,12,null,1000,1001,true),"Missing companion revokes handoff");
Check(!HandoffLease.Owns(12,12,12,"OtherPlugin",1000,1001,true),"Another endpoint cannot own Steward handoff");
Check(!HandoffLease.Owns(12,12,12,endpoint,1000,1001,false),"Released native lock revokes handoff");
Check(!HandoffLease.Owns(12,12,12,endpoint,1000,999,true),"Invalid heartbeat clock revokes handoff");
Console.WriteLine($"{total} confirmation safety checks passed.");
var control=new PolicyControl();
Check(!control.Acquire(0,true,false,false),"Anonymous control rejected");
Check(!control.Acquire(12,false,false,false),"Disconnected control rejected");
Check(!control.Acquire(12,true,true,false),"Busy AutoRetainer cannot be acquired");
Check(!control.Acquire(12,true,false,true),"Another plugin's suppression remains respected");
Check(control.Acquire(12,true,false,false),"Connected idle character acquires its own control");
Check(!control.Acquire(13,true,false,false)&&control.Owner==12,"A second claimant cannot steal control");
Check(!control.Release(13)&&control.Owner==12,"Wrong owner cannot release control");
Check(control.Release(12)&&!control.Active,"Owner releases its control");
Check(control.Release(12),"Release acknowledgement is idempotent after expiry/reload");
Check(control.Acquire(13,true,false,false)&&!control.Release(12)&&control.Owner==13,"Old character cannot release a new character's control");
Check(!PolicyDialog.Matches(0,0,0),"No owner does not match default confirmation IDs");
Check(PolicyDialog.Matches(12,12,0)&&PolicyDialog.Matches(12,0,12),"Owned parent or host accepted");
Check(!PolicyDialog.Matches(12,13,14),"Unrelated HQ confirmation rejected");
Console.WriteLine($"{total} confirmation/control safety checks passed.");
var gate=new PolicyCommitGate();
Check(!gate.Submit(false)&&!gate.Submitted,"Revoked permission does not submit a confirmation");
Check(gate.Submit(true)&&gate.Submitted,"Approved confirmation submits once");
Check(!gate.Submit(true),"Unacknowledged confirmation cannot be submitted again");
var hq=new PolicyCommitGate();Check(hq.Submit(true)&&!hq.Submit(true),"HQ confirmation is also one-shot");
Console.WriteLine($"{total} safety checks passed including one-shot native commits.");
var rollback=new PolicyControl();
try{rollback.Acquire(12,true,false,false,()=>throw new Exception("Pause unavailable"));throw new Exception("Failure swallowed");}
catch(Exception ex) when(ex.Message=="Pause unavailable"){Check(!rollback.Active,"Failed pause initialization rolls back untracked control");}
Console.WriteLine($"{total} safety checks passed including acquisition rollback.");
Check(PolicyAcknowledgement.Check(2,2,false,1000,1001)==PolicyAck.Waiting,"Closing a dialog alone does not acknowledge GC delivery");
Check(PolicyAcknowledgement.Check(2,1,true,1000,1001)==PolicyAck.Waiting,"Inventory change with a pending dialog remains held");
Check(PolicyAcknowledgement.Check(2,1,false,1000,1001)==PolicyAck.Completed,"Exact one-item reduction with closed dialogs acknowledges GC delivery");
Check(PolicyAcknowledgement.Check(1,0,false,1000,1001)==PolicyAck.Completed,"Last source copy removed is valid acknowledgement");
Check(PolicyAcknowledgement.Check(2,0,false,1000,1001)==PolicyAck.Failed,"Unexpected multiple-item removal stops GC work");
Check(PolicyAcknowledgement.Check(2,null,false,1000,1001)==PolicyAck.Failed,"Unavailable inventory cannot acknowledge GC delivery");
Check(PolicyAcknowledgement.Check(2,2,false,1000,11001)==PolicyAck.Failed,"Unchanged inventory cannot reopen/retry a timed-out GC transaction");
Console.WriteLine($"{total} safety checks passed including GC acknowledgement fencing.");
var faults=new HashSet<ulong>{12};var journal=new HashSet<ulong>{12};var resets=0;
var reviewedGate=new PolicyCommitGate();reviewedGate.Submit(true);
try{PolicyPersistence.ClearReview(faults,journal,12,()=>throw new System.IO.IOException("disk"),()=>resets++);throw new Exception("Save failure swallowed");}
catch(System.IO.IOException){Check(faults.Contains(12)&&journal.Contains(12)&&resets==0,"Failed review save restores both guards and does not reset a live transaction");}
PolicyPersistence.ClearReview(faults,journal,12,()=>{},()=>{resets++;reviewedGate.Reset();});
Check(!faults.Contains(12)&&!journal.Contains(12)&&resets==1,"Successful explicit review clears both markers and resets transaction state exactly once");
Check(reviewedGate.Submit(true),"A successfully reviewed transaction can submit afresh without re-latching its old commit state");
var owners=new Dictionary<ulong,string>{{12,"Steward"}};
try{PolicyPersistence.Relax(()=>owners.Remove(12),()=>throw new System.IO.IOException("disk"),()=>owners[12]="Steward");throw new Exception("Detach save failure swallowed");}
catch(System.IO.IOException){Check(owners.ContainsKey(12),"Failed detach cannot restore unmanaged disposal in memory");}
journal.Add(12);
try{PolicyPersistence.Relax(()=>journal.Remove(12),()=>throw new System.IO.IOException("disk"),()=>journal.Add(12));throw new Exception("Ack save failure swallowed");}
catch(System.IO.IOException){Check(journal.Contains(12),"Failed acknowledgement save retains the pending transaction marker");}
var identity=new PolicyItemIdentity(42,1,0,3,0);
Check(identity.Matches(new(42,1,0,3,0)),"Selected desynthesis identity matches its authorization");
Check(!identity.Matches(new(43,1,0,3,0)),"Changed selected item is blocked");
Check(!identity.Matches(new(42,2,0,3,0)),"Changed selected quantity is blocked");
Check(!identity.Matches(new(42,1,1,3,0)),"Changed selected container is blocked");
Check(!identity.Matches(new(42,1,0,4,0)),"Changed selected slot is blocked");
Check(!identity.Matches(new(42,1,0,3,1)),"Changed selected quality/flags are blocked");
Check(!new PolicyItemIdentity(0,1,0,3,0).Matches(new(0,1,0,3,0)),"Empty selected identity never authorizes destruction");
Console.WriteLine($"{total} safety checks passed including recovery persistence and desynthesis target binding.");
var order=new List<string>();var nativeCalls=0;var held=0;
Check(PolicyPersistence.Submit(()=>order.Add("journal"),()=>{order.Add("identity");return true;},()=>{order.Add("native");nativeCalls++;},_=>held++),"Valid submission runs");
Check(order.SequenceEqual(new[]{"journal","identity","native"})&&nativeCalls==1&&held==0,"Persistence and final identity precede the native call");
Check(!PolicyPersistence.Submit(()=>throw new System.IO.IOException("disk"),()=>true,()=>nativeCalls++,_=>held++),"Failed journal save holds");
Check(nativeCalls==1&&held==1,"No native action after journal save failure");
Check(!PolicyPersistence.Submit(()=>{},()=>false,()=>nativeCalls++,_=>held++),"Changed source after persistence holds");
Check(nativeCalls==1&&held==2,"No native action after final source mismatch");
Check(!PolicyPersistence.Submit(()=>{},()=>true,()=>throw new Exception("native"),_=>held++),"Native submission failure holds");
Check(held==3,"Possible partial native action is escalated for review");
for(uint quantity=1;quantity<=5;quantity++)
for(uint removed=1;removed<=quantity;removed++)
{
    var expected=new PolicyItemIdentity(42,quantity,0,3,0);
    var afterSource=quantity==removed?new PolicyItemIdentity(0,0,0,3,0):expected with{Quantity=quantity-removed};
    Check(PolicySlotAcknowledgement.Check(expected,afterSource,quantity+4,quantity+4-removed,removed,false,1000,1001)==PolicyAck.Completed,"Exact source and total reduction acknowledge once");
    Check(PolicySlotAcknowledgement.Check(expected,expected,quantity+4,quantity+4,removed,false,1000,1001)==PolicyAck.Waiting,"Unchanged inventory waits without retrying");
    Check(PolicySlotAcknowledgement.Check(expected,afterSource,quantity+4,quantity+4-removed,removed,true,1000,1001)==PolicyAck.Waiting,"Busy native action or missing cabinet ownership waits");
    Check(PolicySlotAcknowledgement.Check(expected,afterSource,quantity+4,quantity+3-removed,removed,false,1000,1001)==PolicyAck.Failed,"Unexpected total loss fails");
    Check(PolicySlotAcknowledgement.Check(expected,afterSource with{Slot=4},quantity+4,quantity+4-removed,removed,false,1000,1001)==PolicyAck.Failed,"Another slot cannot acknowledge this source");
    Check(PolicySlotAcknowledgement.Check(expected,null,quantity+4,null,removed,false,1000,1001)==PolicyAck.Failed,"Unavailable source and inventory cannot acknowledge");
    Check(PolicySlotAcknowledgement.Check(expected,afterSource,quantity+4,quantity+4-removed,removed,false,1000,11001)==PolicyAck.Failed,"Expired submission stays for review even after late inventory change");
}
Check(PolicySlotAcknowledgement.Check(identity,identity,1,1,0,false,1000,1001)==PolicyAck.Failed,"Zero expected removal is invalid");
Console.WriteLine($"{total} safety checks passed including durable submissions and source acknowledgements.");
var folder=Path.Combine(Path.GetTempPath(),"ar-policy-safety-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
var statePath=Path.Combine(folder,"state.json");
try
{
    var state=new PolicyLedger{Owners=new(){{12,"Steward.InventoryPolicy.Evaluate"}},Pending=[12]};
    AtomicState.Write(statePath,state);
    var restored=AtomicState.ReadOrMigrate<PolicyLedger>(statePath,()=>throw new Exception("Incorrect migration"));restored.Validate();
    Check(restored.Owners.ContainsKey(12)&&restored.Pending.Contains(12),"Real filesystem reload retains management and an unacknowledged submission");
    restored.Pending.Clear();restored.Owners.Clear();AtomicState.Write(statePath,restored);
    Check(AtomicState.ReadOrMigrate<PolicyLedger>(statePath,()=>throw new Exception()).Owners.Count==0,"Explicit persisted detach remains detached after reload");
    File.WriteAllText(statePath,"{}");
    try{AtomicState.ReadOrMigrate(statePath,()=>new PolicyLedger());throw new Exception("Incomplete ledger accepted");}
    catch(System.Text.Json.JsonException){Check(true,"Missing ledger fields cannot silently erase managed owners");}
    File.WriteAllText(statePath,"{");
    try{AtomicState.ReadOrMigrate(statePath,()=>new PolicyLedger());throw new Exception("Corrupt ledger accepted");}
    catch(System.Text.Json.JsonException){Check(true,"Corrupt safety ledger cannot fall back to unmanaged configuration");}
    var blocked=Path.Combine(folder,"blocked");Directory.CreateDirectory(blocked);
    try{AtomicState.Write(blocked,state);throw new Exception("Filesystem error swallowed");}
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Check(true,"Actual write/replace failure reaches the pre-submit guard");}
    Check(Directory.GetFiles(folder,"*.tmp").Length==0,"Failed native ledger write cleans up only its own temporary file");
}
finally{foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(Path.Combine(folder,"blocked"));Directory.Delete(folder);}
Console.WriteLine($"{total} safety checks passed including real ledger I/O and reload failures.");

Check(PolicyTransferAcknowledgement.Check(PolicyAck.Completed,0,1,1)==PolicyAck.Completed,"Store acknowledgement requires destination gain");
Check(PolicyTransferAcknowledgement.Check(PolicyAck.Completed,0,0,1)==PolicyAck.Waiting,"Removed source with missing destination remains pending");
Check(PolicyTransferAcknowledgement.Check(PolicyAck.Waiting,0,1,1)==PolicyAck.Waiting,"Destination gain alone cannot acknowledge storage");
Check(PolicyTransferAcknowledgement.Check(PolicyAck.Completed,0,2,1)==PolicyAck.Failed,"Unexpected destination gain holds for review");
Check(PolicyTransferAcknowledgement.Check(PolicyAck.Completed,0,null,1)==PolicyAck.Failed,"Unavailable destination cannot release transaction");
Console.WriteLine($"{total} checks passed including retainer destination acknowledgements.");

foreach(var action in new[]{"Gc","Desynth","Vendor"})
foreach(var gc in new[]{false,true})
foreach(var desynth in new[]{false,true})
{
    Check(!PolicyDisposal.Allows(action,true,true,gc,desynth),"Demo mode blocks every disposal method");
    Check(PolicyDisposal.Allows(action,false,true,gc,desynth)==(action==(gc?"Gc":desynth?"Desynth":"Vendor")),"GC-first method priority");
    Check(PolicyDisposal.Allows(action,false,false,gc,desynth),"Opt-out preserves existing method behavior");
}
Check(PolicyDisposal.Allows("Store",true,true,true,true),"Disposal demo mode does not block collection/storage");
Console.WriteLine($"{total} checks passed including disposal demo mode and priority.");
foreach(var unlocked in new[]{false,true})
foreach(var enabled in new[]{false,true})
foreach(var supported in new[]{false,true})
foreach(var level in new[]{0,29,30,100})
{
    var eligible=PolicyDisposal.CanDesynth(unlocked,enabled,supported,level);
    Check(eligible==(unlocked&&enabled&&supported&&level>=30),"Desynthesis requires unlock, enabled method, supported item and crafting level");
    Check(PolicyDisposal.Allows("Vendor",false,true,false,eligible)==!eligible,"Vendor route and desynthesis reservation agree, including locked quest");
}
foreach(var marketable in new[]{false,true})
{
    Check(PolicyStorageRouting.Allows(2,10,false,marketable,44)==marketable,"Medicine follows assigned plan when marketable, currency role otherwise");
    Check(PolicyStorageRouting.Allows(2,2,false,marketable,44)==!marketable,"Currency role does not collect marketable medicine");
    foreach(var category in new uint[]{1,11,35,43})
    {
        Check(PolicyStorageRouting.Allows(2,10,true,marketable,category),"Gear stays with native plan regardless of marketability");
        Check(!PolicyStorageRouting.Allows(2,2,true,marketable,category),"Currency role cannot collect gear");
    }
    foreach(var category in new uint[]{57,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,82,95})
    {
        Check(PolicyStorageRouting.Allows(2,12,false,marketable,category),"Housing stays with native plan regardless of marketability");
        Check(!PolicyStorageRouting.Allows(2,2,false,marketable,category),"Currency role cannot collect housing");
    }
    Check(PolicyStorageRouting.Allows(2,2,false,marketable,100),"Currencies use configured native storage role");
    Check(!PolicyStorageRouting.Allows(2,10,false,marketable,100),"Currencies do not leak to another native plan");
    Check(PolicyStorageRouting.Allows(0,10,false,marketable,44),"Disabling special role leaves native plans authoritative");
}
Check(PolicyStorageRouting.Selects(false,0,0,30,false,false,null,999),"Medicine category selected by native plan");
Check(!PolicyStorageRouting.Selects(true,30,0,30,false,false,null,999),"Explicit item reserve overrides category selection");
Check(PolicyStorageRouting.Selects(true,29,null,30,false,false,null,999),"Native item list permits only quantity above reserve");
Check(!PolicyStorageRouting.Selects(false,0,null,30,false,false,null,999),"Unassigned item is not an overflow destination");
Check(!PolicyStorageRouting.Selects(false,0,null,30,true,false,null,999),"Unknown retainer contents cannot authorize duplicate overflow");
Check(PolicyStorageRouting.Selects(false,0,null,30,true,false,5,999),"Duplicate-only plan requires an existing live stack");
Check(!PolicyStorageRouting.Selects(false,0,null,30,true,false,999,999),"Full duplicate stack stays held");
Check(PolicyStorageRouting.Selects(false,0,null,30,true,true,999,999),"Multi-stack duplicate option is respected");
Check(!PolicyStorageRouting.UniqueFits(true,1),"Already stored unique item is skipped before an entrust command");
Check(!PolicyStorageRouting.UniqueFits(true,null),"Unknown destination contents cannot approve a unique deposit");
Check(PolicyStorageRouting.UniqueFits(true,0),"Missing unique item can be entrusted");
Check(PolicyStorageRouting.UniqueFits(false,1),"Ordinary duplicates retain native stacking behavior");
foreach(var action in new[]{"Entrust","Retrieve","Gil"})
    Check(PolicyStandalone.Allows(action),"Standalone keeps native inventory transfer and gil handling: "+action);
foreach(var action in new[]{"Gc","Desynth","Vendor","Discard","Market","Reprice","MarketReturn","Dresser","Armoire","Store","Unknown",""})
    Check(!PolicyStandalone.Allows(action),"Standalone cannot consume, sell or alter collection items: "+action);
foreach(var selected in new[]{false,true})foreach(var due in new[]{false,true})foreach(var gil in new[]{false,true})foreach(var strategy in new[]{false,true})
    Check(PolicyStandalone.VisitManualRetainer(selected,due,gil,strategy)==(selected&&(due||gil||strategy)),"Manual item retainer still visits for due ventures, gil or explicit strategy");
Check(PolicyStandalone.FinishesSoon(240)&&!PolicyStandalone.VentureDue(240,-5,1,false,false),"Five-minute wait includes Skaenwyda without prematurely collecting her venture");
Check(PolicyStandalone.FinishesSoon(-6)&&PolicyStandalone.VentureDue(-6,-5,1,false,false),"Completed Skaenwyda venture is processed instead of waiting forever");
Check(!PolicyStandalone.FinishesSoon(3600)&&!PolicyStandalone.VentureDue(3600,-5,1,false,false),"Reassigned venture no longer keeps the five-minute wait active");
Check(PolicyStandalone.VisitManualRetainer(true,false,true,false),"Gil-only visit does not require an early venture reassignment");
Check(!PolicyStandalone.VentureDue(-10,-5,0,false,false),"Unassigned retainer cannot loop without an assignable venture");
Check(!PolicyStandalone.Allows("Retrieve",false),"Standalone cannot misclassify market listing withdrawal as ordinary retrieval");
SettingsChecks.Run(Check);
Console.WriteLine($"{total} checks passed including live inherited protection without candidate-list cloning.");

foreach(var entry in new[]{(Source:6381u,Output:8143u,Quantity:7u),(Source:6391u,Output:8149u,Quantity:1u)})
{
    var source=new PolicyItemIdentity(entry.Source,1,2,23,0);
    var output=new PolicyItemIdentity(entry.Output,entry.Quantity,2,23,0);
    Check(PolicySlotAcknowledgement.Check(source,output,1,0,1,true,1000,3500,true)==PolicyAck.Waiting,"Desynth output reusing source slot waits for animation");
    Check(PolicySlotAcknowledgement.Check(source,output,1,0,1,false,1000,4000,true)==PolicyAck.Completed,"Exact consumed item and reused output slot acknowledges desynthesis");
    Check(PolicySlotAcknowledgement.Check(source,output,1,0,1,false,1000,4000)==PolicyAck.Failed,"Other actions cannot accept reused source slots");
    Check(PolicySlotAcknowledgement.Check(source,output,2,0,1,false,1000,4000,true)==PolicyAck.Failed,"Extra source loss still fails");
    Check(PolicySlotAcknowledgement.Check(source,output,1,1,1,false,1000,4000,true)==PolicyAck.Failed,"Slot replacement without source loss still fails");
    Check(PolicySlotAcknowledgement.Check(source,output with{Slot=24},1,0,1,false,1000,4000,true)==PolicyAck.Failed,"Another slot cannot acknowledge desynthesis");
    Check(PolicySlotAcknowledgement.Check(source,output,1,null,1,false,1000,4000,true)==PolicyAck.Failed,"Unknown source count remains held");
    Check(PolicySlotAcknowledgement.Check(source,output,1,0,1,false,1000,11001,true)==PolicyAck.Failed,"Late slot reuse cannot clear expired submission");
    Check(PolicySlotAcknowledgement.Check(source with{Quantity=2},output,2,1,1,false,1000,4000,true)==PolicyAck.Failed,"Partial stack cannot be replaced by output");
}
Console.WriteLine($"{total} checks passed including desynthesis output slot reuse.");
