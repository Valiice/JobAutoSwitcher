using System;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using JobAutoSwitcher.Services;

namespace JobAutoSwitcher.Commands;

public class CommandHandler : IDisposable
{
    private const string CommandName = "/jas";

    private readonly Configuration _config;
    private readonly Action _toggleConfigUi;

    public CommandHandler(Configuration config, Action toggleConfigUi)
    {
        _config = config;
        _toggleConfigUi = toggleConfigUi;

        Service.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Job Auto Switcher — /jas [on|off|config]",
        });
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                SetEnabled(true);
                break;

            case "off":
                SetEnabled(false);
                break;

            case "config":
            case "settings":
                _toggleConfigUi();
                break;

            default:
                SetEnabled(!_config.Enabled);
                break;
        }
    }

    private void SetEnabled(bool enabled)
    {
        _config.Enabled = enabled;
        _config.Save();
        PrintChat(enabled ? "Enabled." : "Disabled.");
    }

    private static void PrintChat(string message)
    {
        Service.Chat.Print(new SeString(new TextPayload($"[JobAutoSwitcher] {message}")));
    }

    public void Dispose()
    {
        Service.Commands.RemoveHandler(CommandName);
        GC.SuppressFinalize(this);
    }
}
