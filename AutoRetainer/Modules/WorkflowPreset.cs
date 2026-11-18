namespace AutoRetainer.Modules;

[Flags]
public enum WorkflowParts
{
    None=0, Ventures=1, Gil=2, Entrust=4, Market=8, Collection=16,
    DresserCleanup=32, Disposal=64, OtherPostprocessors=128, All=255
}

public sealed class WorkflowPreset
{
    public string Name = "New preset";
    public WorkflowParts Parts = WorkflowParts.None;
    public static List<WorkflowPreset> Defaults() =>
    [
        new(){Name="All enabled",Parts=WorkflowParts.All},
        new(){Name="AutoDuty",Parts=WorkflowParts.Ventures|WorkflowParts.Gil|WorkflowParts.Entrust},
        new(){Name="Ventures and gil",Parts=WorkflowParts.Ventures|WorkflowParts.Gil},
        new(){Name="Storage",Parts=WorkflowParts.Entrust},
        new(){Name="Market",Parts=WorkflowParts.Market},
        new(){Name="Glamour collection",Parts=WorkflowParts.Collection|WorkflowParts.Entrust},
        new(){Name="Disposal",Parts=WorkflowParts.Disposal},
    ];
    public static bool AllowsAction(WorkflowParts parts,string action)=>action switch
    {
        "Gil"=>parts.HasFlag(WorkflowParts.Gil),
        "Entrust" or "Store"=>parts.HasFlag(WorkflowParts.Entrust),
        "Market"=>parts.HasFlag(WorkflowParts.Market),
        "Dresser" or "Armoire"=>parts.HasFlag(WorkflowParts.Collection),
        "Vendor" or "Desynth" or "Gc" or "Discard"=>parts.HasFlag(WorkflowParts.Disposal),
        _=>true
    };
}
