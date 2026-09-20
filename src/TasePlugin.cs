using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using Tase.Admin;
using Tase.Auth;
using Tase.BlockMaker;

namespace Tase;

[MinimumApiVersion(80)]
public sealed class TasePlugin : BasePlugin
{
    public override string ModuleName => "TASE";
    public override string ModuleVersion => "0.7.0-dev";
    public override string ModuleAuthor => "Johnaconda";
    public override string ModuleDescription => "Role-aware CS2 administration and BlockMaker toolkit.";

    internal RoleStore Roles { get; private set; } = null!;
    internal TargetSelectionManager Selections { get; private set; } = null!;
    internal BlockMakerService BlockMaker { get; private set; } = null!;
    internal AdminMenu AdminMenu { get; private set; } = null!;

    public override void Load(bool hotReload)
    {
        Roles = new RoleStore(Path.Combine(ModuleDirectory, "data", "roles.json"));
        Selections = new TargetSelectionManager();
        BlockMaker = new BlockMakerService();
        AdminMenu = new AdminMenu(this, Roles, Selections, BlockMaker);

        Roles.Load();

        AddCommand("css_tase", "Open the TASE administration menu.", CmdTase);
        AddCommand("css_bm", "Open the TASE BlockMaker menu.", CmdBlockMaker);
        AddCommand("css_tase_select", "Toggle a SteamID in your TASE multi-selection.", CmdSelect);
        AddCommand("css_tase_clear", "Clear your TASE target selection.", CmdClearSelection);

        Logger.LogInformation(
            "TASE {Version} loaded. Native admin menu and BlockMaker session layer are active.",
            ModuleVersion);
    }

    private void CmdTase(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryRequirePlayer(caller, command, out var player))
            return;

        AdminMenu.OpenRoot(player);
    }

    private void CmdBlockMaker(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryRequirePlayer(caller, command, out var player))
            return;

        if (!Roles.HasAtLeast(player.SteamID, TaseRole.BlockMaker))
        {
            command.ReplyToCommand("[TASE] No permission.");
            return;
        }

        AdminMenu.OpenBlockMaker(player);
    }

    private void CmdSelect(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryRequirePlayer(caller, command, out var player))
            return;

        if (command.ArgCount < 2 || !ulong.TryParse(command.GetArg(1), out var targetSteamId))
        {
            command.ReplyToCommand("[TASE] Usage: css_tase_select <steamid64>");
            return;
        }

        Selections.PruneDisconnectedPlayers();
        var selected = Selections.Toggle(player.SteamID, targetSteamId);
        command.ReplyToCommand(selected
            ? $"[TASE] Selected {targetSteamId}."
            : $"[TASE] Unselected {targetSteamId}.");
    }

    private void CmdClearSelection(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryRequirePlayer(caller, command, out var player))
            return;

        Selections.Clear(player.SteamID);
        command.ReplyToCommand("[TASE] Selection cleared.");
    }

    private static bool TryRequirePlayer(
        CCSPlayerController? caller,
        CommandInfo command,
        out CCSPlayerController player)
    {
        if (caller is null || !caller.IsValid)
        {
            command.ReplyToCommand("[TASE] This command is player-only.");
            player = null!;
            return false;
        }

        player = caller;
        return true;
    }
}
