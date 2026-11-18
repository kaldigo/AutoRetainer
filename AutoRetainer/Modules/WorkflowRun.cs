namespace AutoRetainer.Modules;

// Per-run snapshot, separate from mutable saved presets.
internal sealed class WorkflowRun
{
    internal bool Active { get; private set; }
    internal ulong Owner { get; private set; }
    internal WorkflowParts Parts { get; private set; }=WorkflowParts.All;
    private readonly HashSet<ulong> visited=[];
    internal void Begin(bool normal,ulong owner,WorkflowParts selected)
    {
        if(normal&&Active&&Owner==owner)return;
        Active=normal;Owner=owner;Parts=selected&WorkflowParts.All;visited.Clear();
    }
    internal bool Refresh(ulong owner,bool schedulerEnabled,bool paused,bool busy,bool followupPending)
    {
        if(Active&&(Owner!=owner||!schedulerEnabled&&!paused&&!busy&&!followupPending))
        {Active=false;visited.Clear();}
        return Active;
    }
    internal bool Visited(ulong id)=>Active&&visited.Contains(id);
    internal void Visit(ulong id){if(Active)visited.Add(id);}
    internal static bool ShouldVisit(WorkflowParts parts,bool manual,bool ventureDue,bool gilConfigured,bool strategyDue)
        =>parts.HasFlag(WorkflowParts.Ventures)&&ventureDue
        ||parts.HasFlag(WorkflowParts.Gil)&&gilConfigured
        ||!manual&&(parts&~(WorkflowParts.Ventures|WorkflowParts.Gil))!=0
        ||manual&&parts.HasFlag(WorkflowParts.Market)&&strategyDue;
}
