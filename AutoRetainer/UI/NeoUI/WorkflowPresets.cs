using AutoRetainer.Modules;
using ECommons.Configuration;

namespace AutoRetainer.UI.NeoUI;
public sealed class WorkflowPresets : NeoUIEntry
{
    public override string Path => "Workflow presets";
    public override NuiBuilder Builder { get; init; }
    private string newName="";
    private string error="";
    private static readonly (WorkflowParts Part,string Label)[] Options=
    [
        (WorkflowParts.Ventures,"Collect and assign ventures"),
        (WorkflowParts.Gil,"Withdraw or deposit gil"),
        (WorkflowParts.Entrust,"Entrust inventory and overflow storage"),
        (WorkflowParts.Market,"Market listings, repricing and returns"),
        (WorkflowParts.Collection,"Collect and store glamours / armoire items"),
        (WorkflowParts.DresserCleanup,"Clean dresser during collection visits"),
        (WorkflowParts.Disposal,"Inventory disposal"),
        (WorkflowParts.OtherPostprocessors,"Other retainer postprocessing plugins"),
    ];
    public WorkflowPresets()=>Builder=new NuiBuilder().Section("Normal run workflow").Widget(DrawPresets);
    internal static void Selector()
    {
        if(ImGui.BeginCombo("Workflow",NormalWorkflow.Selected.Name))
        {
            foreach(var p in C.WorkflowPresets)
                if(ImGui.Selectable(p.Name,p.Name==C.SelectedWorkflowPreset))NormalWorkflow.Select(p.Name);
            ImGui.EndCombo();
        }
    }
    private void DrawPresets()
    {
        ImGui.TextWrapped("Choose what the normal enable command and bell Run button may do. AutoDuty uses this same selection. Switching or editing a preset affects the next run and does not start it.");
        Selector();
        var preset=NormalWorkflow.Selected;
        foreach(var (part,label) in Options)
        {
            var enabled=preset.Parts.HasFlag(part);
            if(ImGui.Checkbox(label,ref enabled))
            {
                var old=preset.Parts;
                preset.Parts=enabled?old|part:old&~part;
                try{EzConfig.Save();}catch{preset.Parts=old;throw;}
            }
        }
        ImGui.TextWrapped("Selections only permit existing enabled features; item protections and log-only mode still apply. Explicit workflow buttons are separate. AutoDuty still decides when to visit retainers from venture readiness and defers glamour travel while looping. Disposal uses the normal run's existing disposal stages, not the separate full-cleanup action.");
        ImGui.InputTextWithHint("##newWorkflow","New preset name",ref newName,64);
        ImGui.SameLine();
        if(ImGui.Button("Save as new preset"))
        {
            var name=newName.Trim();
            if(name.Length==0||C.WorkflowPresets.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))error="Choose a unique, nonempty name.";
            else
            {
                var copy=new WorkflowPreset{Name=name,Parts=preset.Parts};
                C.WorkflowPresets.Add(copy);
                try{NormalWorkflow.Select(name);newName="";error="";}catch{C.WorkflowPresets.Remove(copy);throw;}
            }
        }
        if(C.WorkflowPresets.Count>1 && ImGui.Button("Delete selected preset"))
        {
            var index=C.WorkflowPresets.IndexOf(preset);
            if(index>=0)
            {
                C.WorkflowPresets.RemoveAt(index);
                try{NormalWorkflow.Select(C.WorkflowPresets[0].Name);}catch{C.WorkflowPresets.Insert(index,preset);throw;}
            }
        }
        if(error.Length>0)ImGui.TextWrapped(error);
        ImGui.TextWrapped("Macro: /autoretainer preset AutoDuty, then enable AutoDuty. /autoretainer preset lists names.");
    }
}
