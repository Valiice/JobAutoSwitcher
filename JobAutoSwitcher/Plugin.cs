using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JobAutoSwitcher.Services;
using JobAutoSwitcher.UI;

namespace JobAutoSwitcher;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
    public static string Name => "Job Auto Switcher";
    private const string _addonName = "ContentsFinderConfirm";

    private readonly PluginUI _ui;
    private readonly JobManager _jobManager;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);

        _ui = new PluginUI();
        _jobManager = new JobManager();

        pluginInterface.UiBuilder.Draw += _ui.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += _ui.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += _ui.Toggle;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, _addonName, OnAddonEvent);

        Service.PluginLog.Info("[JobAutoSwitcher] Ready.");
    }

    private unsafe void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs receiveArgs) return;
        if ((int)receiveArgs.AtkEventType != 25) return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null) return;

        _jobManager.OnCommenceWindow(addon);
    }

    public void Dispose()
    {
        Service.PluginInterface.UiBuilder.Draw -= _ui.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= _ui.Toggle;
        Service.PluginInterface.UiBuilder.OpenMainUi -= _ui.Toggle;
        Service.AddonLifecycle.UnregisterListener(OnAddonEvent);

        _ui.Dispose();
    }
}