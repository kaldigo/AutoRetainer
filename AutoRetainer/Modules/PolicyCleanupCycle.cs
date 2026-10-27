namespace AutoRetainer.Modules;

internal enum CleanupPhase { Idle, Bell, Withdraw, Gc, Desynth, Return, Vendor, Review, Complete, Stopped }

// Coordinates existing actions; never grants an item permission or submits a game action.
internal sealed class PolicyCleanupCycle
{
    internal ulong Character { get; private set; }
    internal CleanupPhase Phase { get; private set; }
    internal int Pass { get; private set; }
    internal long Withdrawn { get; private set; }
    internal long Disposed { get; private set; }
    internal long TotalWithdrawn { get; private set; }
    internal long TotalDisposed { get; private set; }
    internal int Visited { get; set; }
    internal string Message { get; private set; }="Not running";
    internal bool Active=>Phase is not (CleanupPhase.Idle or CleanupPhase.Complete or CleanupPhase.Stopped);
    internal void Start(ulong character)
    {
        if(character==0||Active)throw new InvalidOperationException("Cleanup cannot start");
        Character=character;Pass=1;Withdrawn=Disposed=TotalWithdrawn=TotalDisposed=0;Visited=0;Phase=CleanupPhase.Bell;Message="Running";
    }
    internal void ConfirmWithdrawal(uint quantity) { if(Phase==CleanupPhase.Withdraw){Withdrawn+=quantity;TotalWithdrawn+=quantity;} }
    internal void ConfirmDisposal(uint quantity) { if(Active){Disposed+=quantity;TotalDisposed+=quantity;} }
    internal void Advance()=>Phase=Phase switch
    {
        CleanupPhase.Bell=>CleanupPhase.Withdraw, CleanupPhase.Withdraw=>CleanupPhase.Gc,
        CleanupPhase.Gc=>CleanupPhase.Desynth, CleanupPhase.Desynth=>CleanupPhase.Return,
        CleanupPhase.Return=>CleanupPhase.Vendor, CleanupPhase.Vendor=>CleanupPhase.Review,
        _=>throw new InvalidOperationException("Unexpected cleanup transition")
    };
    internal void Review(bool hasSpace,bool gcRemaining)
    {
        if(Phase!=CleanupPhase.Review)throw new InvalidOperationException("Cleanup is not ready for review");
        if(gcRemaining){Stop("GC candidates remain undelivered; review GC eligibility or seal capacity before continuing");return;}
        if(Visited==0){Stop("No included retainers were visited");return;}
        if(Withdrawn==0&&Disposed==0)
        {
            if(!hasSpace){Stop("Inventory space prevented another withdrawal batch");return;}
            Phase=CleanupPhase.Complete;Message="Finished: no further approved disposal items were withdrawn";return;
        }
        if(Disposed==0){Stop("Items were withdrawn but no disposal was confirmed; review enabled methods and requirements");return;}
        if(Pass>=100){Stop("Batch limit reached; review progress before starting another run");return;}
        Pass++;Withdrawn=Disposed=0;Visited=0;Phase=CleanupPhase.Withdraw;
    }
    internal void Stop(string reason){Phase=CleanupPhase.Stopped;Message=reason;}
}
