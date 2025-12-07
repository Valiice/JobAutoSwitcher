using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JobAutoSwitcher.Services;
using Lumina.Excel.Sheets;

namespace JobAutoSwitcher;

public class JobManager
{
    // --- CONSTANTS ---
    private const string _addonName = "ContentsFinderConfirm";

    // Memory Offsets & Indexes
    private const int _atkValuesMinCount = 25;       // Window must have at least 25 values
    private const int _jobIconIndex = 24;            // Index [24] holds the Job Icon ID

    // Job Math
    private const uint _iconBaseOffset = 62100;
    private const uint _minJobIconId = 62100;
    private const uint _maxJobIconId = 63000;

    // Gearset Logic
    private const int _maxGearsetSlots = 100;

    // Automation
    private const int _commenceButtonId = 8;
    private const int _retryCount = 5;
    private const int _retryDelayFirst = 500;
    private const int _retryDelayLoop = 2000;
    private const int _callbackArgCount = 1;

    public unsafe void OnCommenceWindow(AtkUnitBase* addon)
    {
        if (addon->AtkValuesCount < _atkValuesMinCount) return;

        var targetIconId = addon->AtkValues[_jobIconIndex].UInt;
        if (targetIconId < _minJobIconId || targetIconId > _maxJobIconId) return;

        var calculatedJobId = targetIconId - _iconBaseOffset;

        var localPlayer = Service.ObjectTable[0] as IPlayerCharacter;
        if (localPlayer == null || localPlayer.ClassJob.Value.RowId == calculatedJobId)
            return;

        SwitchAndCommence(calculatedJobId);
    }

    private void SwitchAndCommence(uint targetJobId)
    {
        if (!EquipBestGearset(targetJobId)) return;

        StartCommenceLoop(targetJobId);
    }

    private unsafe bool EquipBestGearset(uint targetJobId)
    {
        var rapture = RaptureGearsetModule.Instance();
        if (rapture == null) return false;

        short bestItemLevel = -1;
        byte? bestGearsetId = null;

        for (int i = 0; i < _maxGearsetSlots; i++)
        {
            var gearset = rapture->GetGearset(i);
            if (gearset != null &&
                gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) &&
                gearset->ClassJob == targetJobId)
            {
                if (gearset->ItemLevel > bestItemLevel)
                {
                    bestItemLevel = gearset->ItemLevel;
                    bestGearsetId = gearset->Id;
                }
            }
        }

        if (bestGearsetId.HasValue)
        {
            var jobRow = Service.Data.GetExcelSheet<ClassJob>()?.GetRow(targetJobId);
            var jobName = jobRow.HasValue ? jobRow.Value.Name.ToString() : "Unknown Job";

            Service.Chat.Print(new SeString(new TextPayload($"[JobAutoSwitcher] Switching to {jobName}...")));
            rapture->EquipGearset(bestGearsetId.Value);
            return true;
        }

        return false;
    }

    private void StartCommenceLoop(uint targetJobId)
    {
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < _retryCount; i++)
            {
                await Task.Delay(i == 0 ? _retryDelayFirst : _retryDelayLoop);

                bool keepRetrying = true;

                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    unsafe
                    {
                        var wrapper = Service.Gui.GetAddonByName(_addonName, 1);
                        var addonPtr = (AtkUnitBase*)wrapper.Address;

                        if (addonPtr == null || !addonPtr->IsVisible)
                        {
                            keepRetrying = false;
                            return;
                        }

                        var player = Service.ObjectTable[0] as IPlayerCharacter;
                        if (player != null && player.ClassJob.Value.RowId != targetJobId)
                        {
                            Service.PluginLog.Info("[JobAutoSwitcher] Retrying gear switch...");
                            EquipBestGearset(targetJobId);
                        }

                        Service.PluginLog.Info($"[JobAutoSwitcher] Attempt {i + 1}: Clicking Commence...");
                        addonPtr->Focus();

                        var values = stackalloc AtkValue[_callbackArgCount];

                        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
                        values[0].Int = _commenceButtonId;

                        addonPtr->FireCallback(_callbackArgCount, values);
                    }
                });

                if (!keepRetrying) break;
            }
        });
    }
}