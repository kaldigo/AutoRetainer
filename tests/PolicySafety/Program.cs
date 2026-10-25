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
