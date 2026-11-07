namespace AutoRetainer.Scheduler.Tasks;

internal static class TaskPostprocessRetainerIPC
{
    internal static void Enqueue(string retainer, string pluginToProcess = null, string marketAction = null)
    {
        if(!Modules.CompanionPolicy.CompanionAvailable)return; // Standalone visits run native ventures/entrust only.
        if(marketAction != null)
        {
            P.TaskManager.Enqueue(() =>
            {
                if(!Modules.CompanionPolicy.CompanionAvailable) return;
                Modules.CompanionPolicy.BeginHandoff("Steward");
                SchedulerMain.RetainerPostProcessLocked = true;
                try
                {
                    if(Svc.PluginInterface.GetIpcSubscriber<string,string,bool>("Steward.Bell.Market").InvokeFunc(marketAction,retainer)) return;
                }
                catch(Exception ex) { ex.Log(); }
                Modules.CompanionPolicy.EndHandoff();
                SchedulerMain.RetainerPostProcessLocked = false;
            }, "Start Steward market visit");
            P.TaskManager.Enqueue(() => !SchedulerMain.RetainerPostProcessLocked, "Wait for Steward market visit", new(timeLimitMS: int.MaxValue));
            return;
        }
        P.TaskManager.Enqueue(() =>
        {
            if(!Modules.CompanionPolicy.CompanionAvailable)return;
            SchedulerMain.RetainerPostprocess = SchedulerMain.RetainerPostprocess.Clear();
            IPC.FireRetainerPostprocessTaskRequestEvent(retainer);
        }, "TaskRetainerPostprocessIPCEnqueue");
        P.TaskManager.Enqueue(() =>
        {
            if(!Modules.CompanionPolicy.CompanionAvailable)return;
            P.TaskManager.BeginStack();
            try
            {
                DebugLog($"SchedulerMain.RetainerPostprocess contains: {SchedulerMain.RetainerPostprocess.Print()}, pluginToProcess = {pluginToProcess}");
                foreach(var x in SchedulerMain.RetainerPostprocess.Where(x => pluginToProcess == null || x == pluginToProcess))
                {
                    P.TaskManager.Enqueue(() =>
                    {
                        if(!Modules.CompanionPolicy.CompanionAvailable)return;
                        SchedulerMain.RetainerPostprocess = SchedulerMain.RetainerPostprocess.Remove(x);
                        SchedulerMain.RetainerPostProcessLocked = true;
                        IPC.FireRetainerPostprocessEvent(x, retainer);
                    }, $"Retainer Postprocess request from {x}");
                    P.TaskManager.Enqueue(() => !SchedulerMain.RetainerPostProcessLocked, $"Retainer Postprocess task from {x}", new(timeLimitMS: int.MaxValue));
                }
            }
            catch(Exception ex) { ex.Log(); }
            P.TaskManager.InsertStack();
        }, "TaskRetainerPostprocessProcessEntries");
    }
}
