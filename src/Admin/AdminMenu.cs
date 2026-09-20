using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Tase.Auth;
using Tase.BlockMaker;

namespace Tase.Admin;

internal sealed class AdminMenu
{
    private readonly TasePlugin _plugin;
    private readonly RoleStore _roles;
    private readonly TargetSelectionManager _selections;
    private readonly BlockMakerService _blockMaker;

    internal AdminMenu(
        TasePlugin plugin,
        RoleStore roles,
        TargetSelectionManager selections,
        BlockMakerService blockMaker)
    {
        _plugin = plugin;
        _roles = roles;
        _selections = selections;
        _blockMaker = blockMaker;
    }

    internal void OpenRoot(CCSPlayerController player)
    {
        var role = _roles.Get(player.SteamID);
        var menu = NewMenu($"TASE Admin — {role}");

        menu.AddMenuOption("Players / Multi-select", (p, _) => OpenPlayers(p));

        if (role >= TaseRole.BlockMaker)
            menu.AddMenuOption("BlockMaker", (p, _) => OpenBlockMaker(p));

        if (role >= TaseRole.BlockBuster)
            menu.AddMenuOption("Moderation", (p, _) => OpenModeration(p));

        if (role >= TaseRole.Admin)
            menu.AddMenuOption("Roles / Permissions", (p, _) => OpenRoles(p));

        menu.AddMenuOption("Clear target selection", (p, _) =>
        {
            _selections.Clear(p.SteamID);
            p.PrintToChat("[TASE] Target selection cleared.");
            OpenRoot(p);
        });

        menu.Open(player);
    }

    internal void OpenPlayers(CCSPlayerController actor)
    {
        _selections.PruneDisconnectedPlayers();

        var selected = _selections.Get(actor.SteamID);
        var menu = NewMenu($"Players — {selected.Count} selected");

        foreach (var target in Utilities.GetPlayers()
                     .Where(p => p.IsValid && !p.IsBot)
                     .OrderBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase))
        {
            var targetSteamId = target.SteamID;
            var checkedMark = _selections.Contains(actor.SteamID, targetSteamId) ? "☑" : "☐";
            var display = $"{checkedMark} {target.PlayerName}";

            menu.AddMenuOption(display, (p, _) =>
            {
                var shiftHeld = (p.Buttons & PlayerButtons.Speed) != 0;

                if (shiftHeld)
                    _selections.Toggle(p.SteamID, targetSteamId);
                else
                    _selections.ReplaceWith(p.SteamID, targetSteamId);

                OpenPlayers(p);
            });
        }

        menu.AddMenuOption($"Actions for selected ({selected.Count})", (p, _) => OpenSelectedActions(p),
            disabled: selected.Count == 0);

        menu.AddMenuOption("Clear selection", (p, _) =>
        {
            _selections.Clear(p.SteamID);
            OpenPlayers(p);
        });

        menu.AddMenuOption("Back", (p, _) => OpenRoot(p));
        menu.Open(actor);
    }

    private void OpenSelectedActions(CCSPlayerController actor)
    {
        var targets = _selections.ResolvePlayers(actor.SteamID);
        var menu = NewMenu($"Selected targets — {targets.Count}");

        menu.AddMenuOption("Show selection in chat", (p, _) =>
        {
            var names = _selections.ResolvePlayers(p.SteamID)
                .Select(target => target.PlayerName)
                .ToArray();

            p.PrintToChat(names.Length == 0
                ? "[TASE] No selected players."
                : $"[TASE] Selected: {string.Join(", ", names)}");

            OpenSelectedActions(p);
        });

        // Moderation actions are intentionally wired as separate server-authoritative
        // handlers in later slices. The selector itself is already reusable by all tools.
        menu.AddMenuOption("Back to players", (p, _) => OpenPlayers(p));
        menu.Open(actor);
    }

    internal void OpenBlockMaker(CCSPlayerController player)
    {
        if (!_roles.HasAtLeast(player.SteamID, TaseRole.BlockMaker))
        {
            player.PrintToChat("[TASE] No BlockMaker permission.");
            return;
        }

        var session = _blockMaker.GetSession(player.SteamID);
        var menu = NewMenu("TASE BlockMaker");

        menu.AddMenuOption($"Shape: {session.Shape}", (p, _) =>
        {
            session.Shape = NextShape(session.Shape);
            OpenBlockMaker(p);
        });

        menu.AddMenuOption($"Size: {session.SizeX:0} × {session.SizeY:0} × {session.SizeZ:0}",
            (p, _) =>
            {
                CycleSize(session);
                OpenBlockMaker(p);
            });

        menu.AddMenuOption($"RGB: {session.Red}, {session.Green}, {session.Blue}", (p, _) =>
        {
            CycleColor(session);
            OpenBlockMaker(p);
        });

        menu.AddMenuOption($"Grid snap: {session.GridSnap:0}", (p, _) =>
        {
            session.GridSnap = session.GridSnap switch
            {
                1f => 8f,
                8f => 16f,
                16f => 32f,
                32f => 64f,
                _ => 1f
            };
            OpenBlockMaker(p);
        });

        menu.AddMenuOption($"Preview: {(session.PreviewEnabled ? "ON" : "OFF")}", (p, _) =>
        {
            session.PreviewEnabled = !session.PreviewEnabled;
            OpenBlockMaker(p);
        });

        menu.AddMenuOption("Reset builder settings", (p, _) =>
        {
            _blockMaker.ResetSession(p.SteamID);
            OpenBlockMaker(p);
        });

        menu.AddMenuOption("Back", (p, _) => OpenRoot(p));
        menu.Open(player);
    }

    private void OpenModeration(CCSPlayerController player)
    {
        var selectedCount = _selections.Get(player.SteamID).Count;
        var menu = NewMenu($"Moderation — {selectedCount} selected");
        menu.AddMenuOption("Select players", (p, _) => OpenPlayers(p));
        menu.AddMenuOption("Back", (p, _) => OpenRoot(p));
        menu.Open(player);
    }

    private void OpenRoles(CCSPlayerController player)
    {
        var menu = NewMenu("Roles / Permissions");
        menu.AddMenuOption("Role editing will use the same player selector", (p, _) => OpenPlayers(p));
        menu.AddMenuOption("Back", (p, _) => OpenRoot(p));
        menu.Open(player);
    }

    private CenterHtmlMenu NewMenu(string title)
    {
        var menu = new CenterHtmlMenu(title, _plugin)
        {
            ExitButton = true
        };
        return menu;
    }

    private static BlockShape NextShape(BlockShape shape)
    {
        var values = Enum.GetValues<BlockShape>();
        var next = (Array.IndexOf(values, shape) + 1) % values.Length;
        return values[next];
    }

    private static void CycleSize(BlockMakerSession session)
    {
        var next = session.SizeX switch
        {
            32f => 64f,
            64f => 128f,
            128f => 256f,
            _ => 32f
        };

        session.SizeX = next;
        session.SizeY = next;
        session.SizeZ = next;
    }

    private static void CycleColor(BlockMakerSession session)
    {
        (session.Red, session.Green, session.Blue) =
            (session.Red, session.Green, session.Blue) switch
            {
                (128, 128, 128) => (255, 255, 255),
                (255, 255, 255) => (255, 64, 64),
                (255, 64, 64) => (64, 255, 64),
                (64, 255, 64) => (64, 128, 255),
                _ => (128, 128, 128)
            };
    }
}
