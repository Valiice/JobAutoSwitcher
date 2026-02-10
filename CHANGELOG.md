# Changelog

## Unreleased

### Added

- **Configuration system** — New `Configuration` class with persistent settings: Enabled toggle, RetryCount, RetryDelayFirst, RetryDelayLoop, and per-job GearsetPreferences
- **Slash commands** — `/jas` command with `on`, `off`, `config`/`settings` subcommands; bare `/jas` toggles enabled state
- **Settings UI** — Full ImGui configuration window with General (enable/disable), Timing (retry count and delay sliders), and Gearset Preferences sections
- **Gearset preferences** — Per-job gearset selection via dropdown, with Auto (highest ilvl) as default and manual override per job
- **Job icons** — Game job icons displayed inline next to job names in the Gearset Preferences table
- **Ko-fi support button** — Support button in the top-right corner of the settings window

### Changed

- **JobManager overhaul** — Extracted constants, added config-driven retry timing, split gearset equip into preferred vs highest-ilvl paths, improved error handling
- **Plugin.cs** — Integrated CommandHandler and Configuration loading; registered OpenConfigUi and OpenMainUi callbacks
- **Service.cs** — Added ITextureProvider for game icon rendering

### Code Quality

- Extracted magic numbers into named constants (AtkValuesMinCount, JobIconIndex, IconBaseOffset, etc.)
- Split monolithic methods into focused single-responsibility helpers
- Added job name caching in JobManager to avoid repeated Excel sheet lookups
- Moved command handling to dedicated `Commands/CommandHandler.cs`
