using Dalamud.Bindings.ImGui;
using System;

namespace JobAutoSwitcher.UI;

public class PluginUI : IDisposable
{
    private bool _isVisible;

    public void Toggle() => _isVisible = !_isVisible;

    public void Draw()
    {
        if (!_isVisible) return;

        if (ImGui.Begin("Job Auto Switcher", ref _isVisible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("The Job Auto Switcher is running.");

            ImGui.Spacing();

            ImGui.TextDisabled("This window is just for status.");
            ImGui.TextDisabled("You can close it safely.");
        }

        ImGui.End();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}