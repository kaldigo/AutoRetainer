namespace AutoRetainer.Modules;

internal static class PolicyDisposal
{
    internal static bool Allows(string action,bool dry,bool preferGc,bool gcCandidate,bool desynthEligible)
    {
        if(action is not ("Gc" or "Desynth" or "Vendor"))return true;
        if(dry)return false;
        if(!preferGc)return true;
        return action==(gcCandidate?"Gc":desynthEligible?"Desynth":"Vendor");
    }
}
