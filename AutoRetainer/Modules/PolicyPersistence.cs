namespace AutoRetainer.Modules;

internal static class PolicyPersistence
{
    internal static void ClearReview(System.Collections.Generic.ISet<ulong> faults,System.Collections.Generic.ISet<ulong> pending,
        ulong cid,System.Action save,System.Action reset)
    {
        var hadFault=faults.Contains(cid);var hadPending=pending.Contains(cid);
        Relax(()=>{faults.Remove(cid);pending.Remove(cid);},save,
            ()=>{if(hadFault)faults.Add(cid);if(hadPending)pending.Add(cid);});
        reset();
    }
    internal static void Relax(System.Action change,System.Action save,System.Action rollback)
    {
        try{change();save();}
        catch{rollback();throw;}
    }
}

internal sealed record PolicyItemIdentity(uint ItemId,uint Quantity,uint Container,int Slot,int Flags)
{
    internal bool Matches(PolicyItemIdentity other)=>ItemId!=0 && Quantity>0 && this==other;
}
