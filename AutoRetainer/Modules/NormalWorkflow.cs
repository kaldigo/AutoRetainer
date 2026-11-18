using AutoRetainerAPI.Configuration;
using ECommons.Configuration;

namespace AutoRetainer.Modules;

internal static class NormalWorkflow
{
    private static readonly WorkflowRun run=new();
    private static ulong completedOwner;
    private static long completedAt;
    internal static WorkflowPreset Selected => C.WorkflowPresets?.FirstOrDefault(p=>p.Name==C.SelectedWorkflowPreset)
        ?? new(){Name="Unavailable preset",Parts=WorkflowParts.None};
    internal static bool StewardPending
    {
        get
        {
            try
            {
                var api=Svc.PluginInterface.GetIpcSubscriber<bool>("Steward.Workflow.Pending");
                return api.HasFunction && api.InvokeFunc();
            }
            catch { return true; }
        }
    }
    internal static bool Active
    {
        get
        {
            return run.Refresh(Svc.ClientState.LocalContentId,SchedulerMain.PluginEnabledInternal,P.WasEnabled,
                Utils.IsBusy,run.Active&&StewardPending);
        }
    }
    internal static bool SinglePass => Active && run.Parts!=WorkflowParts.All;
    internal static WorkflowParts Parts => Active?run.Parts:WorkflowParts.All;
    internal static bool Allows(WorkflowParts part)=>Parts.HasFlag(part);
    internal static bool AllowsAction(string action)=>WorkflowPreset.AllowsAction(Parts,action);
    internal static bool Visited(ulong retainer)=>Active&&run.Visited(retainer);
    internal static void Visit(ulong retainer){if(Active)run.Visit(retainer);}
    internal static void Begin(PluginEnableReason reason)
    {
        var normal=reason is PluginEnableReason.Auto or PluginEnableReason.Manual or PluginEnableReason.Access;
        if(Active)return; // Repeated enable, bell resumes and other callers cannot broaden queued work.
        run.Begin(normal,Svc.ClientState.LocalContentId,Selected.Parts);
        if(normal){completedOwner=0;Svc.Log.Information("Normal AutoRetainer workflow: {Preset}",Selected.Name);}
    }
    internal static void Completed()
    {
        if(SinglePass&&!run.Parts.HasFlag(WorkflowParts.Ventures)){completedOwner=run.Owner;completedAt=Environment.TickCount64;}
    }
    // AutoDuty checks venture readiness again after a pass. Without ventures, an unchanged
    // completed venture must not cause an immediate endless repetition of that same pass.
    internal static bool JustCompleted(ulong cid)=>cid!=0&&cid==completedOwner&&Environment.TickCount64-completedAt<60000;
    internal static bool StewardCompatible
    {
        get
        {
            if(Parts==WorkflowParts.All)return true;
            try{return Svc.PluginInterface.GetIpcSubscriber<int>("Steward.Workflow.Version").InvokeFunc()>=1;}
            catch{return false;}
        }
    }
    internal static bool Select(string name)
    {
        var preset=C.WorkflowPresets?.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));
        if(preset==null)return false;
        var previous=C.SelectedWorkflowPreset;
        C.SelectedWorkflowPreset=preset.Name;
        try{EzConfig.Save();}catch{C.SelectedWorkflowPreset=previous;throw;}
        return true;
    }
}
