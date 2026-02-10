using System.Collections.Generic;
using Dalamud.Configuration;
using JobAutoSwitcher.Services;

namespace JobAutoSwitcher;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public int RetryCount { get; set; } = 5;
    public int RetryDelayFirst { get; set; } = 500;
    public int RetryDelayLoop { get; set; } = 2000;

    /// <summary>
    /// Per-job gearset preferences. Key = job ID, Value = preferred gearset ID (null = use highest ilvl).
    /// </summary>
    public Dictionary<uint, byte?> GearsetPreferences { get; set; } = [];

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
