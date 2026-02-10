# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JobAutoSwitcher is a **Dalamud plugin** for Final Fantasy XIV. It automatically switches the player's gearset to match the job they queued with when clicking "Commence" in the Duty Finder, bypassing the "Class/Job is different" error.

## Build Commands

```bash
# Restore dependencies (requires DALAMUD_HOME env var pointing to a Dalamud installation)
dotnet restore JobAutoSwitcher.slnx

# Build (Release configuration, x64 platform)
dotnet build --configuration Release JobAutoSwitcher/JobAutoSwitcher.csproj
```

The project uses `Dalamud.NET.Sdk/14.0.1` which handles Dalamud dependency resolution. The `DALAMUD_HOME` environment variable must point to an extracted Dalamud distribution. CI downloads it from `https://goatcorp.github.io/dalamud-distrib/latest.zip`.

Target framework: `net10.0-windows`. Solution file is `JobAutoSwitcher.slnx` (XML-based solution format). There are no tests.

## Architecture

The plugin has 4 source files in `JobAutoSwitcher/`:

- **Plugin.cs** — Entry point implementing `IDalamudPlugin`. Registers an `AddonLifecycle` listener on the `ContentsFinderConfirm` addon (the duty commence popup). Filters for event type 25 (button click) and delegates to `JobManager`.

- **JobManager.cs** — Core logic. `OnCommenceWindow` reads the job icon ID from `AtkValues[24]` of the addon, computes the target job ID (`iconId - 62100`), and compares it to the player's current job. If different, it equips the best gearset (highest item level) via `RaptureGearsetModule` and starts an async retry loop (`StartCommenceLoop`) that re-clicks Commence up to 5 times with delays, handling animation locks.

- **Services/Service.cs** — Static service locator using Dalamud's `[PluginService]` IoC injection. All Dalamud services (Chat, ObjectTable, Framework, AddonLifecycle, etc.) are accessed via `Service.*`.

- **UI/PluginUI.cs** — Minimal ImGui status window with no configuration options.

## Key Dalamud APIs Used

- `IAddonLifecycle` — hooks into addon events
- `RaptureGearsetModule` — reads/equips gearsets via client structs
- `IFramework.RunOnFrameworkThread` — marshals calls to the game's main thread
- `AtkUnitBase` / `AtkValue` — reads addon memory for job icon data

## Release Process

Releases are triggered by pushing a `v*.*.*` tag or via manual workflow dispatch. CI builds and creates a GitHub release with `latest.zip`.
