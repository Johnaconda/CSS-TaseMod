// 📄 src/Tase/TasePlugin.Main.cs
using System;
using System.Collections.Generic;
using System.IO;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace Tase
{
    // Roles used by TASE (matches TitleClass.MinRole)
    public enum TaseRole { None = 0, BlockMaker = 1, BlockBuster = 2, Admin = 3, GoD = 4 }

    public sealed class TaseConfig
    {
        public int  PhysMaxDistance { get; set; } = 1200;
        public bool PhysSafeMode    { get; set; } = true;
        public int  AirMove         { get; set; } = 10;
        public int  AirAccelerate   { get; set; } = 100;
        public bool Emulate128Tick  { get; set; } = true;
    }

    public partial class TasePlugin : BasePlugin
    {
        public override string ModuleName    => "TASE";
        public override string ModuleVersion => "0.6.1";
        public override string ModuleAuthor  => "you + assistant";

        internal static TaseConfig Config = new();

        // Data paths (portable across CSS# versions)
        private static string DataRoot      => Path.Combine(AppContext.BaseDirectory, "addons", "counterstrikesharp", "data", "tase");
        private static string TitlesMapPath => Path.Combine(DataRoot, "titles.json");

        public override void Load(bool hotReload)
        {
            // existing init...
            Directory.CreateDirectory(DataRoot);
            TitlesManager.Load(TitlesMapPath);

            CommandRegistry.Setup(this);
            // add /bmtoggle alias explicitly
            AddCommand("bmtoggle", "Toggle BlockMaker UI", CmdBMToggle);

            InputBridge.Register(this);

            RegisterListener<Listeners.OnTick>(() => Phys_OnTick());
            RegisterListener<Listeners.OnMapStart>((string map) => Movement_OnMapStart(map));
            RegisterListener<Listeners.OnMapEnd>(() => Phys_OnMapEnd(Server.MapName ?? "<unknown>"));
        }

        // ------------ Command Handlers ------------
        private void CmdTaseHelp(CCSPlayerController? caller, CommandInfo _) 
            => SendLines(caller, GetHelpLines(caller));

        private void CmdTaseVersion(CCSPlayerController? caller, CommandInfo _)
        {
            var lines = new List<string>
            {
                $"[TASE] {ModuleName} v{ModuleVersion} by {ModuleAuthor}",
                $"[TASE] PhysMaxDistance={Config.PhysMaxDistance}, PhysSafeMode={Config.PhysSafeMode}, AirMove={Config.AirMove}, AirAccelerate={Config.AirAccelerate}"
            };
            SendLines(caller, lines);
        }

        private void CmdBMToggle(CCSPlayerController? caller, CommandInfo _)
        {
            if (caller == null) return;
            if (!HasPrivilege(caller, TasePrivilege.BuildBlocks))
            {
                SayTo(caller, "No permission.");
                return;
            }
            HUD.OpenBlockMakerPanel(caller);
        }
		
		public override void Load(bool hotReload)
        {
            // existing init...
            Directory.CreateDirectory(DataRoot);
            TitlesManager.Load(TitlesMapPath);

            CommandRegistry.Setup(this);
            // add /bmtoggle alias explicitly
            AddCommand("bmtoggle", "Toggle BlockMaker UI", CmdBMToggle);

            InputBridge.Register(this);

            RegisterListener<Listeners.OnTick>(() => Phys_OnTick());
            RegisterListener<Listeners.OnMapStart>((string map) => Movement_OnMapStart(map));
            RegisterListener<Listeners.OnMapEnd>(() => Phys_OnMapEnd(Server.MapName ?? "<unknown>"));
        }

        private void CmdJail(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /jail <steamid64> [seconds]"); return; }
            if (!HasAtLeast(caller, TaseRole.BlockBuster))
            {
                SayTo(caller, "No permission.");
                return;
            }
            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }
            int secs = 60;
            if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var secVal)) secs = secVal;
            // TODO: integrate JailManager
            SayTo(caller, $"[TASE] (stub) Jailed {sid} for {secs}s.");
        }

        private void CmdUnjail(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /unjail <steamid64>"); return; }
            if (!HasAtLeast(caller, TaseRole.BlockBuster))
            {
                SayTo(caller, "No permission.");
                return;
            }
            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }
            // TODO: integrate JailManager
            SayTo(caller, $"[TASE] (stub) Unjailed {sid}.");
        }

        private void CmdTaseUi(CCSPlayerController? caller, CommandInfo _)
        {
            if (caller == null)
            {
                Console.WriteLine("[TASE] tase_ui is player-only.");
                return;
            }
            var (role, privs, _) = TitlesManager.ResolveFor(caller.SteamID);
            var title = TitlesManager.Get(caller.SteamID) ?? role.ToString();
            bool isElevated = role >= TaseRole.Admin || (privs & TasePrivilege.Operator) != 0 || role >= TaseRole.GoD;
            SayTo(caller, $"[TASE] v{ModuleVersion} — Role: {role} {(title != role.ToString() ? $"[{title}]" : "")}");
#if TASE_HAS_CLIENTCMD
            TryPushRoleToClient(caller, title, role.ToString(), isElevated, ModuleVersion);
#endif
            SayTo(caller, "[TASE] UI requested. If you don’t see it, install the Panorama addon and use ui_reload_panorama.");
        }

#if TASE_HAS_CLIENTCMD
        private static void TryPushRoleToClient(CCSPlayerController player, string title, string role, bool elevated, string version)
        {
            void Exec(string cmd) { try { player.ExecuteClientCommand(cmd); } catch { } }
            string q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
            Exec($"tase_ui_version {q(version)}");
            Exec($"tase_ui_role {q(role)}");
            Exec($"tase_ui_title {q(title)}");
            Exec($"tase_ui_elevated {(elevated ? 1 : 0)}");
            Exec("tase_ui_open 1");
        }
#endif

        // -------------- Help Text Generation --------------
        private string[] GetHelpLines(CCSPlayerController? caller)
        {
            var lines = new List<string>();
            lines.Add($"[TASE] {ModuleName} — Admin Toolbox");
            lines.Add($"[TASE] Version: {ModuleVersion}");
            lines.Add("[TASE] Commands:");
            foreach (var cmd in CommandRegistry.Commands)
            {
                // Only list commands allowed for this caller
                if (HasAtLeast(caller, cmd.MinRole) || 
                    (cmd.RequiredPriv != TasePrivilege.None && HasPrivilege(caller, cmd.RequiredPriv)))
                {
                    lines.Add($"  {cmd.DisplayName.PadRight(35)} — {cmd.Description}");
                }
            }
            // Show file paths to admins (or console)
            if (caller == null || HasAtLeast(caller, TaseRole.Admin))
            {
                lines.Add("[TASE] Files:");
                lines.Add("  • Titles DB: " + TitlesMapPath);
                lines.Add("  • Title classes: " + Path.Combine(DataRoot, "titles", "<TitleName>.json"));
            }
            return lines.ToArray();
        }

        // Utility: send list of lines to a player or console
        private static void SendLines(CCSPlayerController? to, IEnumerable<string> lines)
        {
            if (to == null)
            {
                foreach (var line in lines) Console.WriteLine(line);
            }
            else
            {
                foreach (var line in lines) { try { to.PrintToChat(line); } catch { } }
            }
        }

        // Utility: print single message to player or console
        internal static void SayTo(CCSPlayerController? player, string text)
        {
            if (player == null) Console.WriteLine(text);
            else { try { player.PrintToChat(text); } catch { } }
        }

        // Permission helpers (using TitlesManager)
        internal static bool HasAtLeast(CCSPlayerController? player, TaseRole minRole)
        {
            if (player == null) return true;
            var (role, _, _) = TitlesManager.ResolveFor(player.SteamID);
            return role >= minRole;
        }
        internal static bool HasPrivilege(CCSPlayerController? player, TasePrivilege needed)
        {
            if (player == null) return true;
            var (_, privs, _) = TitlesManager.ResolveFor(player.SteamID);
            return (privs & needed) != 0;
        }

        // PhysGun & Movement partial implementations...
        partial void Phys_OnTick();
        partial void Phys_OnMapEnd(string map);
        partial void Phys_OnPlayerDisconnected(CCSPlayerController player);
        partial void Movement_OnMapStart(string map);
    }
}
