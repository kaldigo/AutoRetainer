namespace AutoRetainer.Modules;

internal sealed class PolicyControl
{
    internal ulong Owner { get; private set; }
    internal bool Active => Owner!=0;
    internal bool Acquire(ulong cid, bool connected, bool busy, bool suppressed, System.Action initialize=null)
    {
        if(cid==0 || !connected || busy || suppressed || Active)return false;
        Owner=cid;
        try{initialize?.Invoke();return true;}
        catch{Owner=0;throw;}
    }
    internal bool Release(ulong cid)
    {
        if(cid==0)return false;
        if(!Active)return true;
        if(Owner!=cid)return false;
        Owner=0;return true;
    }
}
