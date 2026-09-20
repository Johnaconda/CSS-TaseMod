# TASE — CS2 Admin + BlockMaker

TASE is a CounterStrikeSharp administration and in-game construction toolkit.

## Current development branch

`feature/admin-blockmaker-v1` begins the resurrection of the original prototype.

The old repository mixed placeholder APIs, duplicate command systems and root-level C# files while the project file compiled only `src/**/*.cs`. The new implementation deliberately uses `src/` as the canonical compiled source tree. Root-level legacy files remain temporarily as reference material and are not runtime authority.

## Implemented in the current v0.7.0-dev slice

- One canonical CounterStrikeSharp plugin entry point.
- JSON-backed TASE roles: `None`, `BlockMaker`, `BlockBuster`, `Admin`, `GoD`.
- Native `CenterHtmlMenu` admin shell.
- Live player list backed by SteamID64.
- Persistent per-admin multi-target selections.
- Checkbox display for selected players.
- Hold **Shift** while selecting a player to toggle that player into/out of the current multi-selection.
- Select without Shift to replace the current selection with that player.
- Disconnected-player pruning from selection sets.
- Reusable selected-target action screen.
- BlockMaker per-admin session state.
- Initial BlockMaker shapes: Box, Slab, Pillar, Wedge, Inverted Wedge, Inner Corner, Outer Corner.
- Initial size, RGB, grid snap and preview settings.

CounterStrikeSharp's native menu provides paging for long player lists. A later optional Panorama frontend will map mouse-wheel scrolling directly while preserving the same server-authoritative selection state.

## Commands

- `!tase` / `css_tase` — open TASE.
- `!bm` / `css_bm` — open BlockMaker.
- `css_tase_select <steamid64>` — toggle a target in the current selection.
- `css_tase_clear` — clear the current selection.

## Architecture

```
src/
  TasePlugin.cs
  Admin/
    AdminMenu.cs
    TargetSelectionManager.cs
  Auth/
    RoleStore.cs
  BlockMaker/
    BlockMakerService.cs
```

All privileged actions must be verified server-side. UI state is never permission authority.

Player targeting uses SteamID64 rather than player names or transient slots.

## Build

The repository targets .NET 8 and references the CounterStrikeSharp API.

```powershell
PowerShell -ExecutionPolicy Bypass -File .\compile.ps1
```

The existing compiler script stages `CounterStrikeSharp.API.dll` from the configured CS2 server and builds `Release`.

## Next implementation slices

- Real moderation actions applied safely to one or many selected players.
- Role/permission editor using the same multi-target selector.
- BlockMaker world ray trace and ghost preview.
- Server-authoritative block placement and stable block IDs.
- Rotation, move, duplicate, delete, undo/redo and grouped selections.
- Save/load per-map BlockMaker JSON.
- Modular block model library for wedges/corners and predictable collision.
- Shift multi-select for world blocks/entities.
- Draw tools for walls, floors, ramps, stairs and arrays.
- Optional Panorama frontend with literal mouse-wheel scrolling and richer visual controls.
- Reconcile/delete the legacy root-level prototype once the new implementation reaches feature parity.

## Validation rule

A successful `dotnet build` must compile the actual `src/**/*.cs` runtime implementation. A small DLL produced from an empty source glob is not considered a valid build.
