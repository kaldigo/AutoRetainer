using AutoRetainer.Internal;
using AutoRetainer.Scheduler.Handlers;
using AutoRetainer.Scheduler.Tasks;
using AutoRetainerAPI;
using AutoRetainerAPI.Configuration;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoRetainer.UI.Overlays;

internal unsafe class RetainerListOverlay : Window
{
    private float height;
    internal volatile string PluginToProcess = null;

    public RetainerListOverlay() : base("AutoRetainer retainerlist overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing, true)
    {
        P.WindowSystem.AddWindow(this);
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public override bool DrawConditions()
    {
        if(!C.UIBar) return false;
        if(Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell] && TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) && IsAddonReady(addon))
        {
            Position = new(addon->X, addon->Y - height);
            return true;
        }
        return false;
    }

    public override void PreDraw()
    {
        //ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void Draw()
    {
        var e = SchedulerMain.PluginEnabled;
        var disabled = MultiMode.Active && !ImGui.GetIO().KeyCtrl;
        if(disabled)
        {
            ImGui.BeginDisabled();
        }
        var running=e || Utils.IsBusy || Modules.CompanionPolicy.CleanupActive;
        if(ImGui.Button(running ? "Stop" : "Run all enabled"))
        {
            P.WasEnabled = false;
            if(!running)
            {
                SchedulerMain.EnablePlugin(PluginEnableReason.Manual);
            }
            else
            {
                SchedulerMain.DisablePlugin();
                var stop=Svc.PluginInterface.GetIpcSubscriber<object>("Steward.Bell.Stop");
                if(stop.HasAction)stop.InvokeAction();
                P.TaskManager.Abort();
            }
        }
        if(disabled)
        {
            ImGui.EndDisabled();
            ImGuiComponents.HelpMarker($"MultiMode controls this option. Hold CTRL to override.");
        }
        if(P.WasEnabled)
        {
            ImGui.SameLine();
            ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudGrey, ImGuiColors.DalamudGrey3, 500), $"Paused");
        }
        if(C.MultiModeUIBar)
        {
            ImGui.SameLine();
            if(ImGui.Checkbox("MultiMode", ref MultiMode.Enabled))
            {
                MultiMode.OnMultiModeEnabled();
                if(MultiMode.Active)
                {
                    SchedulerMain.EnablePlugin(PluginEnableReason.MultiMode);
                }
            }
        }

        Svc.PluginInterface.GetIpcProvider<object>(ApiConsts.OnMainControlsDraw).SendMessage();

        ImGui.SameLine();

        if(ImGuiEx.IconButton($"{Lang.IconSettings}##Open plugin interface"))
        {
            Svc.Commands.ProcessCommand("/ays");
        }
        ImGuiEx.Tooltip("Open Plugin Settings");
        var ready = SchedulerMain.ManualVentureControlAvailable && !Utils.IsBusy && !P.WasEnabled;
        var nativeReady=ready && !Modules.CompanionPolicy.HasFault && (!Modules.CompanionPolicy.CompanionAvailable || Reason("Native",ready)=="");
        var nativeReason=Modules.CompanionPolicy.HasFault ? "Resolve the inventory review hold." : !ready ? "Wait for the current run to finish." : "Steward is in log-only mode.";
        Group("Ventures", FontAwesomeIcon.Running.ToIconString(), nativeReady, SchedulerMain.StartQuickVentures, () =>
        {
            if(!nativeReady) ImGui.TextDisabled(nativeReason);
            if(!Data.GetIMSettings().IMEnableCofferAutoOpen) ImGui.TextDisabled("Enable coffer opening in AutoRetainer.");
            if(ImGui.MenuItem("Open venture coffers", "", false, nativeReady && Data.GetIMSettings().IMEnableCofferAutoOpen))
            {
                P.TaskManager.Enqueue(RetainerListHandlers.CloseRetainerList);
                P.TaskManager.Enqueue(() => !IsOccupied());
                TaskOpenAllCoffers.Enqueue(includeDiscard: false);
            }
        });
        Group("Storage", Lang.IconDuplicate, nativeReady, () => Entrust(null, false), () =>
        {
            if(!nativeReady) ImGui.TextDisabled(nativeReason);
            foreach(var reverse in new[]{false, true})
            {
                if(!ImGui.BeginMenu(reverse ? "Withdraw using plan" : "Entrust using plan", nativeReady)) continue;
                foreach(var plan in C.EntrustPlans)
                    if(ImGui.MenuItem($"{plan.Name}##{plan.Guid}")) Entrust(plan.Guid, reverse);
                ImGui.EndMenu();
            }
            if(ImGui.MenuItem("Withdraw all gil", "", false, nativeReady)) TransferGil(true);
            if(ImGui.MenuItem("Run configured gil transfers", "", false, nativeReady)) TransferGil(false);
            StewardEntry("Store protected overflow", "Overflow", ready);
        });
        string[] marketModes = ["Market", "Reprice", "List", "Review"];
        string[] marketLabels = ["Full market pass", "Reprice only", "List only", "Review withdrawals and rotation"];
        void MarketOptions(ulong retainer)
        {
            for(int i=0; i<marketModes.Length; i++)
                StewardEntry(marketLabels[i], marketModes[i], ready, retainer);
        }
        Group("Market", FontAwesomeIcon.Coins.ToIconString(), Available("Market",ready), () => Start("Market"), () =>
        {
            MarketOptions(0);
            if(ImGui.BeginMenu("One retainer"))
            {
                foreach(var ret in GameRetainerManager.Retainers)
                    if(ret.Available && ImGui.BeginMenu(ret.Name))
                    {
                        MarketOptions(ret.RetainerID);
                        ImGui.EndMenu();
                    }
                ImGui.EndMenu();
            }
        });
        Group("Glamour", FontAwesomeIcon.Tshirt.ToIconString(), Available("GlamourStorage",ready), () => Start("GlamourStorage"), () =>
        {
            StewardEntry("Armoire only", "Armoire", ready);
            StewardEntry("Clean dresser", "DresserCleanup", ready);
        });
        Group("Disposal", FontAwesomeIcon.Recycle.ToIconString(), Available("InventoryDisposal",ready), () => Start("InventoryDisposal"), () =>
        {
            foreach(var scope in new[]{"Carried", "Retainers"})
            {
                if(!ImGui.BeginMenu(scope=="Carried" ? "Carried inventory only" : "Include retainer withdrawals")) continue;
                foreach(var method in new[]{"All","Gc","Desynth","Vendor"})
                    StewardEntry(method=="All" ? "Enabled methods" : method=="Gc" ? "GC only" : method+" only", scope+method, ready);
                ImGui.EndMenu();
            }
            if(ImGui.MenuItem("Quick vendor via retainer", "", false, nativeReady && Available("InventoryDisposal",ready) && Data.GetIMSettings().IMEnableAutoVendor))
                Utils.EnqueueVendorItemsByRetainer();
            if(ImGui.MenuItem("Sell quick-venture list", "", false, nativeReady && Available("InventoryDisposal",ready) && Data.GetIMSettings().IMEnableAutoVendor))
            {
                foreach(var ret in GameRetainerManager.Retainers)
                {
                    if(!ret.Available || C.CompanionGilOnlyRetainers.Contains(ret.RetainerID)) continue;
                    TaskSelectRetainer.Enqueue(ret.Name);
                    TaskVendorItems.Enqueue(true);
                    LeaveRetainer();
                    P.TaskManager.Enqueue(RetainerHandlers.ConfirmCantBuyback);
                    break;
                }
            }
        });
        if(!P.TaskManager.IsBusy)
        {
            PluginToProcess = null;
            Svc.PluginInterface.GetIpcProvider<object>(ApiConsts.OnRetainerListTaskButtonsDraw).SendMessage();
            if(PluginToProcess != null)
            {
                for(var i = 0; i < GameRetainerManager.Count; i++)
                {
                    var ret = GameRetainerManager.Retainers[i];
                    if(ret.Available)
                    {
                        P.TaskManager.Enqueue(() => RetainerListHandlers.SelectRetainerByName(ret.Name.ToString()));
                        TaskPostprocessRetainerIPC.Enqueue(ret.Name.ToString(), PluginToProcess);

                        if(C.RetainerMenuDelay > 0)
                        {
                            TaskWaitSelectString.Enqueue(C.RetainerMenuDelay);
                        }
                        P.TaskManager.Enqueue(RetainerHandlers.SelectQuit);
                        P.TaskManager.Enqueue(RetainerHandlers.ConfirmCantBuyback);
                    }
                }
            }
        }
        height = ImGui.GetWindowSize().Y;
    }

    private static void Group(string name, string icon, bool enabled, Action action, Action menu)
    {
        ImGui.SameLine();
        ImGui.PushID(name);
        ImGui.BeginDisabled(!enabled);
        if(ImGuiEx.IconButton(icon+"##action")) action();
        ImGui.EndDisabled();
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(enabled ? name : name+" unavailable. Open options for details.");
        ImGui.SameLine();
        if(ImGui.ArrowButton("options", ImGuiDir.Down)) ImGui.OpenPopup("menu");
        if(ImGui.BeginPopup("menu"))
        {
            menu();
            ImGui.EndPopup();
        }
        ImGui.PopID();
    }

    private static string Reason(string action, bool ready, ulong retainer=0)
    {
        if(!ready) return "Wait for the current run to finish.";
        if(action.StartsWith("Carried") || action.StartsWith("Retainers") || action=="InventoryDisposal")
        {
            var method=action=="InventoryDisposal" ? "All" : action[(action.StartsWith("Carried")?7:9)..];
            if(Data.GetIMSettings().IMDry) return "AutoRetainer Demo mode is on.";
            if(!Modules.CompanionPolicy.MethodEnabled(method)) return "Enable this disposal method in AutoRetainer.";
        }
        try { return Svc.PluginInterface.GetIpcSubscriber<string,ulong,string>("Steward.Bell.Availability").InvokeFunc(action,retainer); }
        catch { return "Update and connect Steward."; }
    }
    private static bool Available(string action,bool ready) => Reason(action,ready)=="";
    private static void StewardEntry(string label,string action,bool ready,ulong retainer=0)
    {
        var reason=Reason(action,ready,retainer);
        if(ImGui.MenuItem(label,"",false,reason=="")) Start(action,retainer);
        if(reason!="" && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reason);
    }
    private static void Start(string action,ulong only=0)
    {
        if(action is "Market" or "List" or "Reprice" or "Review")
        {
            var scope=Reason("LogOnly",true)=="" ? "Preview/"+action : action;
            var visited=0;
            foreach(var ret in GameRetainerManager.Retainers)
            {
                if(!ret.Available || only!=0 && ret.RetainerID!=only || Reason(action,true,ret.RetainerID)!="") continue;
                TaskSelectRetainer.Enqueue(ret.Name);
                visited++;
                TaskPostprocessRetainerIPC.Enqueue(ret.Name,"Steward",scope);
                LeaveRetainer();
            }
            if(visited==0)Notify.Info("No available retainers match the sale settings for this action.");
            return;
        }
        Svc.PluginInterface.GetIpcSubscriber<string,object>("Steward.Bell.Start").InvokeAction(action);
    }
    private static void LeaveRetainer()
    {
        if(C.RetainerMenuDelay>0) TaskWaitSelectString.Enqueue(C.RetainerMenuDelay);
        P.TaskManager.Enqueue(RetainerHandlers.SelectQuit);
    }
    private static void Entrust(Guid? planId,bool reverse)
    {
        foreach(var ret in GameRetainerManager.Retainers)
        {
            if(!ret.Available || C.CompanionGilOnlyRetainers.Contains(ret.RetainerID)) continue;
            var id=planId??Utils.GetAdditionalData(Data.CID,ret.Name).EntrustPlan;
            var plan=C.EntrustPlans.FirstOrDefault(x=>x.Guid==id);
            if(plan==null || !reverse && !plan.DoesInventoryHaveMatchingItems()) continue;
            TaskSelectRetainer.Enqueue(ret.Name);
            if(reverse) TaskEntrustDuplicates.EnqueueNewReverse(plan);
            else TaskEntrustDuplicates.EnqueueNew(plan);
            LeaveRetainer();
        }
    }
    private static void TransferGil(bool all)
    {
        foreach(var ret in GameRetainerManager.Retainers)
        {
            if(!ret.Available) continue;
            var data=Utils.GetAdditionalData(Data.CID,ret.Name);
            if(!all && !data.WithdrawGil) continue;
            TaskSelectRetainer.Enqueue(ret.Name);
            P.TaskManager.Enqueue(() =>
            {
                if(!Modules.CompanionPolicy.Allows("Gil",0))return;
                P.TaskManager.InsertStack(() =>
                {
                    if(!all && data.Deposit) TaskDepositGil.Enqueue(data.WithdrawGilPercent);
                    else TaskWithdrawGil.Enqueue(all ? 100 : data.WithdrawGilPercent);
                });
            });
            LeaveRetainer();
        }
    }

    public override void PostDraw()
    {
        //ImGui.PopStyleVar();
    }
}
