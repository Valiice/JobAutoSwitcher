using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using JobAutoSwitcher.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace JobAutoSwitcher.UI;

public class PluginUI(Configuration config) : IDisposable
{
    private bool _isVisible;
    private float _kofiButtonWidth;

    private readonly List<GearsetJobEntry> _gearsetEntries = [];
    private bool _needsRefresh = true;

    public void Toggle() => _isVisible = !_isVisible;

    public void Draw()
    {
        if (!_isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(500, 0), new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin("Job Auto Switcher", ref _isVisible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            DrawKofiButton();
            DrawGeneralSection();
            ImGui.Spacing();
            DrawTimingSection();
            ImGui.Spacing();
            DrawGearsetPreferencesSection();
        }

        ImGui.End();
    }

    private void DrawKofiButton()
    {
        var startPos = ImGui.GetCursorPos();
        if (_kofiButtonWidth > 0)
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - _kofiButtonWidth);

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Coffee, "Support",
            new Vector4(1.0f, 0.35f, 0.35f, 0.9f),
            new Vector4(1.0f, 0.25f, 0.25f, 1.0f),
            new Vector4(1.0f, 0.35f, 0.35f, 0.75f)))
        {
            Util.OpenLink("https://ko-fi.com/valiice");
        }

        _kofiButtonWidth = ImGui.GetItemRectSize().X;
        ImGui.SetCursorPos(startPos);
    }

    private static void DrawJobIcon(uint iconId)
    {
        var lookup = new GameIconLookup(iconId);
        var texture = Service.TextureProvider.GetFromGameIcon(lookup);
        var wrap = texture.GetWrapOrEmpty();
        if (wrap.Handle != nint.Zero)
        {
            ImGui.Image(wrap.Handle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
            ImGui.SameLine();
        }
    }

    private void DrawGeneralSection()
    {
        if (!ImGui.CollapsingHeader("General", ImGuiTreeNodeFlags.DefaultOpen)) return;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            config.Enabled = enabled;
            config.Save();
        }
    }

    private void DrawTimingSection()
    {
        if (!ImGui.CollapsingHeader("Timing", ImGuiTreeNodeFlags.DefaultOpen)) return;

        DrawSlider("Retry Count", config.RetryCount, 1, 15, v => config.RetryCount = v);
        DrawSlider("First Delay (ms)", config.RetryDelayFirst, 100, 3000, v => config.RetryDelayFirst = v);
        DrawSlider("Loop Delay (ms)", config.RetryDelayLoop, 500, 5000, v => config.RetryDelayLoop = v);
    }

    private void DrawSlider(string label, int currentValue, int min, int max, Action<int> setter)
    {
        var value = currentValue;
        if (ImGui.SliderInt(label, ref value, min, max))
        {
            setter(value);
            config.Save();
        }
    }

    private void DrawGearsetPreferencesSection()
    {
        if (!ImGui.CollapsingHeader("Gearset Preferences")) return;

        if (_needsRefresh)
        {
            RefreshGearsetData();
            _needsRefresh = false;
        }

        if (ImGui.Button("Refresh"))
            RefreshGearsetData();

        ImGui.SameLine();
        ImGui.TextDisabled("Select a preferred gearset per job, or leave as Auto.");

        if (_gearsetEntries.Count == 0)
        {
            ImGui.Text("No gearsets found.");
            return;
        }

        DrawGearsetTable();
    }

    private void DrawGearsetTable()
    {
        if (!ImGui.BeginTable("##GearsetPrefs", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg))
            return;

        ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 180);
        ImGui.TableSetupColumn("Preferred Gearset", ImGuiTableColumnFlags.WidthFixed, 250);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableHeadersRow();

        foreach (var entry in _gearsetEntries)
            DrawGearsetRow(entry);

        ImGui.EndTable();
    }

    private void DrawGearsetRow(GearsetJobEntry entry)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        DrawJobIcon(entry.IconId);
        ImGui.Text(entry.JobName);

        ImGui.TableNextColumn();
        DrawGearsetCombo(entry);

        ImGui.TableNextColumn();
        DrawResetButton(entry);
    }

    private void DrawGearsetCombo(GearsetJobEntry entry)
    {
        var currentIndex = FindSelectedGearsetIndex(entry);
        ImGui.SetNextItemWidth(240);

        if (!ImGui.Combo($"##combo_{entry.JobId}", ref currentIndex, entry.ComboLabels, entry.ComboLabels.Length))
            return;

        if (currentIndex == 0)
            config.GearsetPreferences.Remove(entry.JobId);
        else
            config.GearsetPreferences[entry.JobId] = entry.Gearsets[currentIndex - 1].Id;

        config.Save();
    }

    private void DrawResetButton(GearsetJobEntry entry)
    {
        if (!config.GearsetPreferences.ContainsKey(entry.JobId)) return;

        if (ImGui.SmallButton($"Reset##{entry.JobId}"))
        {
            config.GearsetPreferences.Remove(entry.JobId);
            config.Save();
        }
    }

    private int FindSelectedGearsetIndex(GearsetJobEntry entry)
    {
        if (!config.GearsetPreferences.TryGetValue(entry.JobId, out var preferredId) || !preferredId.HasValue)
            return 0;

        for (int i = 0; i < entry.Gearsets.Count; i++)
        {
            if (entry.Gearsets[i].Id == preferredId.Value)
                return i + 1;
        }

        return 0;
    }

    private void RefreshGearsetData()
    {
        _gearsetEntries.Clear();

        Dictionary<uint, List<GearsetInfo>> jobGearsets;
        unsafe
        {
            var rapture = RaptureGearsetModule.Instance();
            if (rapture == null) return;
            jobGearsets = CollectGearsetsByJob(rapture);
        }

        var jobSheet = Service.Data.GetExcelSheet<ClassJob>();
        if (jobSheet == null) return;

        BuildEntries(jobGearsets, jobSheet);

        _gearsetEntries.Sort((a, b) => string.Compare(a.JobName, b.JobName, StringComparison.OrdinalIgnoreCase));
    }

    private static unsafe Dictionary<uint, List<GearsetInfo>> CollectGearsetsByJob(RaptureGearsetModule* rapture)
    {
        var result = new Dictionary<uint, List<GearsetInfo>>();

        for (int i = 0; i < 100; i++)
        {
            var gearset = rapture->GetGearset(i);
            if (gearset == null || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            uint jobId = gearset->ClassJob;
            if (!result.ContainsKey(jobId))
                result[jobId] = [];

            result[jobId].Add(new GearsetInfo
            {
                Id = gearset->Id,
                Name = gearset->NameString,
                ItemLevel = gearset->ItemLevel,
            });
        }

        return result;
    }

    private void BuildEntries(Dictionary<uint, List<GearsetInfo>> jobGearsets, ExcelSheet<ClassJob> jobSheet)
    {
        foreach (var (jobId, gearsets) in jobGearsets)
        {
            var jobName = jobSheet.GetRow(jobId).Name.ToString();
            if (string.IsNullOrEmpty(jobName)) continue;

            var labels = BuildComboLabels(gearsets);

            _gearsetEntries.Add(new GearsetJobEntry
            {
                JobId = jobId,
                IconId = jobId + 62100,
                JobName = jobName,
                Gearsets = gearsets,
                ComboLabels = labels,
            });
        }
    }

    private static string[] BuildComboLabels(List<GearsetInfo> gearsets)
    {
        var labels = new string[gearsets.Count + 1];
        labels[0] = "Auto (highest ilvl)";

        for (int i = 0; i < gearsets.Count; i++)
            labels[i + 1] = $"#{gearsets[i].Id + 1}: {gearsets[i].Name} (ilvl {gearsets[i].ItemLevel})";

        return labels;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    private struct GearsetInfo
    {
        public byte Id;
        public string Name;
        public short ItemLevel;
    }

    private class GearsetJobEntry
    {
        public uint JobId;
        public uint IconId;
        public string JobName = string.Empty;
        public List<GearsetInfo> Gearsets = [];
        public string[] ComboLabels = [];
    }
}
