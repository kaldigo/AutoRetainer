using AutoRetainer.Modules.Voyage;
using AutoRetainer.Scheduler.Handlers;
using ECommons.ExcelServices;
using ECommons.GameHelpers;

namespace AutoRetainer.Scheduler.Tasks;

internal static unsafe class TaskInteractWithNearestBell
{
    internal static void Enqueue(bool interact = true, bool suppressScheduler = false)
    {
        P.TaskManager.Enqueue(NewYesAlreadyManager.WaitForYesAlreadyDisabledTask);
        P.TaskManager.Enqueue(() =>
        {
            if((VoyageUtils.Workshops.Contains(Svc.ClientState.TerritoryType) || Player.TerritoryIntendedUse == TerritoryIntendedUseEnum.Inn) && Utils.GetReachableRetainerBell(false) == null)
            {
                var bell = Utils.GetNearestRetainerBell(out var distance);
                if(distance < 20f)
                {
                    P.TaskManager.BeginStack();
                    P.TaskManager.Enqueue(HouseEnterTask.LockonBell);
                    P.TaskManager.Enqueue(HouseEnterTask.Approach);
                    P.TaskManager.Enqueue(HouseEnterTask.AutorunOffBell);
                    P.TaskManager.InsertStack();
                }
            }
        }, "ApproachWorkshopBell");
        P.TaskManager.Enqueue(PlayerWorldHandlers.SelectNearestBell);
        if(interact)
        {
            P.TaskManager.Enqueue(() =>
            {
                P.IsInteractionAutomatic = !suppressScheduler;
                if(suppressScheduler) P.SkipNextEnable = true;
            }, "Set bell startup behavior");
            P.TaskManager.Enqueue(PlayerWorldHandlers.InteractWithTargetedBell);
        }
    }
}
