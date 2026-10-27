using AutoRetainer.Modules;

var pending=new PolicyConfirmation(12,42,"Inventory1/0/42/1/None",1000);
var total=0;
void Check(bool result,string name){total++;if(!result)throw new Exception(name);}
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
SettingsChecks.Run(Check);
Console.WriteLine($"{total} checks passed including live inherited protection without candidate-list cloning.");
