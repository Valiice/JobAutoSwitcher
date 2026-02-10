using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using JobAutoSwitcher.Commands;
using JobAutoSwitcher.Services;
using JobAutoSwitcher.UI;

namespace JobAutoSwitcher;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
    public static string Name => "Job Auto Switcher";
    internal const string AddonName = "ContentsFinderConfirm";

    // AtkEventType for button click
    private const int ButtonClickEventType = 25;

    private readonly Configuration _config;
    private readonly PluginUI _ui;
    private readonly JobManager _jobManager;
    private readonly CommandHandler _commandHandler;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);

        _config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _ui = new PluginUI(_config);
        _jobManager = new JobManager(_config);
        _commandHandler = new CommandHandler(_config, _ui.Toggle);

        pluginInterface.UiBuilder.Draw += _ui.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += _ui.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += _ui.Toggle;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, AddonName, OnAddonEvent);

        Service.PluginLog.Info("[JobAutoSwitcher] Ready.");
    }

    private void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs receiveArgs) return;
        if ((int)receiveArgs.AtkEventType != ButtonClickEventType) return;
        if (args.Addon.Address == nint.Zero) return;

        _jobManager.OnCommenceWindow(args.Addon.Address);
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonEvent);

        Service.PluginInterface.UiBuilder.Draw -= _ui.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= _ui.Toggle;
        Service.PluginInterface.UiBuilder.OpenMainUi -= _ui.Toggle;

        _commandHandler.Dispose();
        _jobManager.Dispose();
        _ui.Dispose();
    }
}
