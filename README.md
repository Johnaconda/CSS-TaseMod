# TASE Plugin (CS2 / CounterStrikeSharp)

## Build
- Requires .NET 7 (or match your server runtime) and CounterStrikeSharp server SDK.
- Add Newtonsoft.Json package.
- Compile TasePlugin.cs + all files into a single plugin DLL.

Example (dotnet):
dotnet new classlib -n TasePlugin
# add files
dotnet add package Newtonsoft.Json
dotnet build -c Release

## Install
1. Copy plugin DLL to server `addons/plugins/` or your server's plugin folder.
2. Create folder `<server>/tase` (plugin auto-creates on first run).
3. Put `tase_config.json` and `roles.json` in `<server>/tase/config/` or adjust TasePlugin paths.
4. Restart server or `plugin_load tase`.

## Commands (console)
- `tase_mod add <steam64> <GoD|Admin|BlockBuster|BlockMaker>`
- `tase_mod del <steam64>`
- `tase_mod list`
- `tase_roles_reload`

## Chat commands (in-game)
- `/tase` -> opens admin panel (role-aware)
- `/bmsave` -> BlockMaker saves map
- `/jail <player>`
- `/unjail <player>`
- `/grab` -> toggle physgun (GoD)
- `/appeal <text>` -> banned spectating players submit apology

## Notes
- Map entity APIs and networked UI need wiring for Panorama if you want fancy right-click menus.
- Replace placeholder calls (`Server.SpawnEntity`, `Physics.Raycast`, `Player.GiveWeapon`, etc.) with your server's exact methods.
