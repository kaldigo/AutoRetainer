namespace AutoRetainer.Modules;

internal static class PolicyPersistence
{
    internal static bool Submit(System.Action persist,System.Func<bool> valid,System.Action native,System.Action<System.Exception> hold)
    {
        try
        {
            persist();
            if(!valid())throw new System.InvalidOperationException("Source changed before submission");
            native();return true;
        }
        catch(System.Exception ex){hold(ex);return false;}
    }
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

internal static class PolicySlotAcknowledgement
{
    internal static PolicyAck Check(PolicyItemIdentity expected,PolicyItemIdentity current,long before,long? after,
        uint removed,bool busy,long submitted,long now)
    {
        if(current==null || after==null || removed==0 || removed>expected.Quantity || before<expected.Quantity ||
            now<submitted || now-submitted>10000)return PolicyAck.Failed;
        if(expected.Matches(current) && after==before)return PolicyAck.Waiting;
        var remaining=expected.Quantity-removed;
        var sourceMatches=current.Container==expected.Container && current.Slot==expected.Slot &&
            (remaining==0 ? current.ItemId==0&&current.Quantity==0 : current.ItemId==expected.ItemId&&current.Quantity==remaining&&current.Flags==expected.Flags);
        if(!sourceMatches || after!=before-removed)return PolicyAck.Failed;
        return busy?PolicyAck.Waiting:PolicyAck.Completed;
    }
}
