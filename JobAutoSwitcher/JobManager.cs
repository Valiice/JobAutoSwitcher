using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JobAutoSwitcher.Services;
using Lumina.Excel.Sheets;

namespace JobAutoSwitcher;

public class JobManager(Configuration config) : IDisposable
{
    private const int AtkValuesMinCount = 25;
    private const int JobIconIndex = 24;
    private const uint IconBaseOffset = 62100;
    private const uint MinJobIconId = 62100;
    private const uint MaxJobIconId = 63000;
    private const int MaxGearsetSlots = 100;
    private const int CommenceButtonId = 8;

    private readonly Dictionary<uint, string> _jobNameCache = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;

    public void OnCommenceWindow(nint addonAddress)
    {
        if (!config.Enabled) return;

        uint? targetJobId;
        unsafe { targetJobId = ParseTargetJobId((AtkUnitBase*)addonAddress); }
        if (targetJobId == null) return;

        if (IsAlreadyOnJob(targetJobId.Value)) return;

        SwitchAndCommence(targetJobId.Value);
    }

    private static unsafe uint? ParseTargetJobId(AtkUnitBase* addon)
    {
        if (addon->AtkValuesCount < AtkValuesMinCount) return null;

        var iconId = addon->AtkValues[JobIconIndex].UInt;
        if (iconId < MinJobIconId || iconId > MaxJobIconId) return null;

        return iconId - IconBaseOffset;
    }

    private static bool IsAlreadyOnJob(uint targetJobId)
    {
        var localPlayer = Service.ObjectTable.LocalPlayer;
        return localPlayer == null || localPlayer.ClassJob.Value.RowId == targetJobId;
    }

    private void SwitchAndCommence(uint targetJobId)
    {
        PrintChat($"Switching to {GetJobName(targetJobId)}...");

        if (!EquipGearset(targetJobId)) return;

        StartCommenceLoop(targetJobId);
    }

    private unsafe bool EquipGearset(uint targetJobId)
    {
        var rapture = RaptureGearsetModule.Instance();
        if (rapture == null) return false;

        if (TryEquipPreferredGearset(rapture, targetJobId))
            return true;

        return TryEquipHighestIlvlGearset(rapture, targetJobId);
    }

    private unsafe bool TryEquipPreferredGearset(RaptureGearsetModule* rapture, uint targetJobId)
    {
        if (!config.GearsetPreferences.TryGetValue(targetJobId, out var preferredId) || !preferredId.HasValue)
            return false;

        var gearset = rapture->GetGearset(preferredId.Value);
        if (gearset != null &&
            gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) &&
            gearset->ClassJob == targetJobId)
        {
            rapture->EquipGearset(preferredId.Value);
            return true;
        }

        Service.PluginLog.Warning(
            $"[JobAutoSwitcher] Preferred gearset {preferredId.Value + 1} is invalid for job {targetJobId}, falling back to highest ilvl.");
        return false;
    }

    private static unsafe bool TryEquipHighestIlvlGearset(RaptureGearsetModule* rapture, uint targetJobId)
    {
        short bestItemLevel = -1;
        byte? bestGearsetId = null;

        for (int i = 0; i < MaxGearsetSlots; i++)
        {
            var gearset = rapture->GetGearset(i);
            if (gearset != null &&
                gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) &&
                gearset->ClassJob == targetJobId &&
                gearset->ItemLevel > bestItemLevel)
            {
                bestItemLevel = gearset->ItemLevel;
                bestGearsetId = gearset->Id;
            }
        }

        if (!bestGearsetId.HasValue) return false;

        rapture->EquipGearset(bestGearsetId.Value);
        return true;
    }

    private void StartCommenceLoop(uint targetJobId)
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        var token = _cts!.Token;

        _ = Task.Run(() => CommenceLoopAsync(targetJobId, token), token);
    }

    private async Task CommenceLoopAsync(uint targetJobId, CancellationToken token)
    {
        try
        {
            for (int attempt = 0; attempt < config.RetryCount; attempt++)
            {
                var delay = attempt == 0 ? config.RetryDelayFirst : config.RetryDelayLoop;
                await Task.Delay(delay, token);

                var shouldContinue = await TryClickCommenceAsync(targetJobId, attempt, token);
                if (!shouldContinue) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "[JobAutoSwitcher] Error in commence loop.");
        }
    }

    private Task<bool> TryClickCommenceAsync(uint targetJobId, int attempt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        return Service.Framework.RunOnFrameworkThread(() =>
        {
            unsafe
            {
                var addon = (AtkUnitBase*)Service.Gui.GetAddonByName(Plugin.AddonName).Address;
                if (addon == null || !addon->IsVisible)
                    return false;

                RetryGearSwitchIfNeeded(targetJobId);

                Service.PluginLog.Info($"[JobAutoSwitcher] Attempt {attempt + 1}: Clicking Commence...");
                ClickCommenceButton(addon);

                return true;
            }
        });
    }

    private void RetryGearSwitchIfNeeded(uint targetJobId)
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null || player.ClassJob.Value.RowId == targetJobId) return;

        Service.PluginLog.Info("[JobAutoSwitcher] Retrying gear switch...");
        EquipGearset(targetJobId);
    }

    private static unsafe void ClickCommenceButton(AtkUnitBase* addon)
    {
        addon->Focus();

        var values = stackalloc AtkValue[1];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
        values[0].Int = CommenceButtonId;
        addon->FireCallback(1, values);
    }

    private string GetJobName(uint jobId)
    {
        if (_jobNameCache.TryGetValue(jobId, out var cached))
            return cached;

        var jobRow = Service.Data.GetExcelSheet<ClassJob>()?.GetRow(jobId);
        var name = jobRow.HasValue ? jobRow.Value.Name.ToString() : "Unknown Job";
        _jobNameCache[jobId] = name;
        return name;
    }

    private static void PrintChat(string message)
    {
        Service.Chat.Print(new SeString(new TextPayload($"[JobAutoSwitcher] {message}")));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        GC.SuppressFinalize(this);
    }
}
