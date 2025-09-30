// TasePlugin.Main.cs — TASE core + titles/roles + help commands (fixed DataPath + SayTo)
using System;
using System.Collections.Generic;
using System.IO;
using CounterStrikeSharp.API;                 // RegisterListener<...>
using CounterStrikeSharp.API.Core;            // BasePlugin, CCSPlayerController
using CounterStrikeSharp.API.Modules.Commands;

namespace Tase
{
    // Roles used by TASE (matches TitleClass.MinRole)
    public enum TaseRole { None = 0, BlockMaker = 1, BlockBuster = 2, Admin = 3, GoD = 4 }

    public sealed class TaseConfig
    {
        public int  PhysMaxDistance { get; set; } = 1200;
        public bool PhysSafeMode    { get; set; } = true;
    }

    public partial class TasePlugin : BasePlugin
    {
        public override string ModuleName    => "TASE";
        public override string ModuleVersion => "0.6.1";
        public override string ModuleAuthor  => "you + assistant";

        internal static TaseConfig Config = new();

        // ---- Data paths (portable across CSS# versions) ----
        // Root: <server>/addons/counterstrikesharp/data/tase
        private static string DataRoot =>
            Path.Combine(AppContext.BaseDirectory, "addons", "counterstrikesharp", "data", "tase");

        private static string TitlesMapPath =>
            Path.Combine(DataRoot, "titles.json");

        public override void Load(bool hotReload)
        {
            // Ensure titles DB exists and is loaded
            Directory.CreateDirectory(DataRoot);
            TitlesManager.Load(TitlesMapPath);

            // ===== Commands (current) =====
            AddCommand("tase",           "Show TASE help",                CmdTaseHelp);
            AddCommand("tase_help",      "Show TASE help",                CmdTaseHelp);
            AddCommand("tase_version",   "Show TASE version",             CmdTaseVersion);

            // Title admin: set/clear/list/me
            AddCommand("tase_make_god",   "Make a player GoD: tase_make_god <steamid64>",     CmdMakeGod);
            AddCommand("tase_make_admin", "Make a player Admin: tase_make_admin <steamid64>", CmdMakeAdmin);
            AddCommand("tase_title_set",  "Set title: tase_title_set <steamid64> <title>",    CmdTitleSet);
            AddCommand("tase_title_clear","Clear title: tase_title_clear <steamid64>",        CmdTitleClear);
            AddCommand("tase_titles_list","List all titles",                                   CmdTitlesList);
            AddCommand("tase_me",         "Show my title/privileges",                          CmdTaseMe);
			AddCommand("tase_jail",   "tase_jail <steamid64> <seconds>", CmdJail);
			AddCommand("tase_unjail", "tase_unjail <steamid64>",         CmdUnjail);
			AddCommand("tase_ui", "Open TASE UI (shows status; admins see advanced)", CmdTaseUi);
			
			
            // Tools
            AddCommand("grab",           "Toggle Physics Gun (requires UsePhysGun or GoD)", CmdGrab);

            // ===== Listeners =====
            RegisterListener<Listeners.OnTick>(() => Phys_OnTick());
            RegisterListener<Listeners.OnMapEnd>(() => Phys_OnMapEnd(Server.MapName ?? "<unknown>"));
            // Optional disconnect cleanup if available:
            // RegisterListener<Listeners.OnClientDisconnectPost>((CCSPlayerController player) => Phys_OnPlayerDisconnected(player));
        }

        // -------------------- HELP COMMANDS --------------------
		
		private void CmdTaseUi(CCSPlayerController? caller, CommandInfo _)
		{
			if (caller == null) { Console.WriteLine("[TASE] tase_ui is player-only."); return; }
		
			var sid = caller.SteamID;
			var (role, privs, _) = TitlesManager.ResolveFor(sid);
			var title = TitlesManager.Get(sid) ?? role.ToString();
		
			// Define what "elevated" means in UI (admins/mods/god etc)
			bool isElevated = role >= TaseRole.Admin || (privs & TasePrivilege.Operator) != 0 || role >= TaseRole.GoD;
		
			// Always tell the player their status (safe for normals)
			SayTo(caller, $"[TASE] v{ModuleVersion} — Role: {role} {(title!=role.ToString() ? $"[{title}]" : "")}");
		
			// Try to push client cvars (only if your CSS# build supports ExecuteClientCommand).
			// If it doesn't, just leave this compiled out; the UI still shows minimal bar.
		#if TASE_HAS_CLIENTCMD
			TryPushRoleToClient(caller, title, role.ToString(), isElevated, ModuleVersion);
		#endif
		
			// Tip to open the local UI overlay
			SayTo(caller, "[TASE] UI requested. If you don’t see it, install the Panorama addon and use ui_reload_panorama.");
		}
		
		#if TASE_HAS_CLIENTCMD
		private static void TryPushRoleToClient(CCSPlayerController p, string title, string role, bool elevated, string version)
		{
			void Exec(string cmd) { try { p.ExecuteClientCommand(cmd); } catch { /* ignore if not supported */ } }
			// Quote-safe
			string q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
		
			Exec($"tase_ui_version {q(version)}");
			Exec($"tase_ui_role {q(role)}");
			Exec($"tase_ui_title {q(title)}");
			Exec($"tase_ui_elevated {(elevated ? 1 : 0)}");
			Exec("tase_ui_open 1");
		}
		#endif
		
		private void CmdJail(CCSPlayerController? caller, CommandInfo info)
		{
			if (info.ArgCount < 3) { SayTo(caller, "Usage: tase_jail <steamid64> <seconds>"); return; }
			if (!CanEditTitles(caller)) { SayTo(caller, "No permission."); return; }
			if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Bad steamid64"); return; }
			if (!int.TryParse(info.GetArg(2), out var secs)) secs = 60;
			// TODO: call your JailManager here
			SayTo(caller, $"[TASE] (stub) Jailed {sid} for {secs}s.");
		}
		
		private void CmdUnjail(CCSPlayerController? caller, CommandInfo info)
		{
			if (info.ArgCount < 2) { SayTo(caller, "Usage: tase_unjail <steamid64>"); return; }
			if (!CanEditTitles(caller)) { SayTo(caller, "No permission."); return; }
			if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Bad steamid64"); return; }
			// TODO: call your JailManager here
			SayTo(caller, $"[TASE] (stub) Unjailed {sid}.");
		}
		
        private void CmdTaseHelp(CCSPlayerController? caller, CommandInfo _)
            => SendLines(caller, GetHelpLines());

        private void CmdTaseVersion(CCSPlayerController? caller, CommandInfo _)
            => SendLines(caller, new[]
            {
                $"[TASE] {ModuleName} v{ModuleVersion} by {ModuleAuthor}",
                $"[TASE] PhysMaxDistance={Config.PhysMaxDistance}, PhysSafeMode={Config.PhysSafeMode}"
            });

        private string[] GetHelpLines()
        {
            var lines = new List<string>
            {
                $"[TASE] {ModuleName} — Admin Toolbox",
                $"[TASE] Version: {ModuleVersion}",
                "[TASE] Commands:",
                "  tase / tase_help                   — Show this help",
                "  tase_version                       — Show plugin version & config",
                "  tase_me                            — Show your title/privileges",
                "  grab                               — Toggle Physics Gun (needs UsePhysGun or GoD)",
                "  tase_titles_list                   — List current SteamID64 → Title mappings",
                "  tase_title_set <steamid64> <title> — Assign a title to a player",
                "  tase_title_clear <steamid64>       — Clear a player's title",
                "  tase_make_admin <steamid64>        — Create/ensure 'Admin' class and assign it",
                "  tase_make_god <steamid64>          — Create/ensure 'GoD' class and assign it",
                "[TASE] Files:",
                $"  • Titles DB: {TitlesMapPath}",
                $"  • Title classes: {Path.Combine(DataRoot, "titles", "<TitleName>.json")}",
            };
            return lines.ToArray();
        }

        private static void SendLines(CCSPlayerController? to, IEnumerable<string> lines)
        {
            if (to == null) { foreach (var l in lines) Console.WriteLine(l); return; }
            foreach (var l in lines) { try { to.PrintToChat(l); } catch { } }
        }

        // Simple chat/console helper used across commands
        internal static void SayTo(CCSPlayerController? p, string text)
        {
            if (p == null) { Console.WriteLine(text); return; }
            try { p.PrintToChat(text); } catch { /* ignore */ }
        }

        // -------------------- TITLE COMMANDS --------------------
        private void CmdMakeGod(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2) { SayTo(caller, "Usage: tase_make_god <steamid64>"); return; }
            if (!CanEditTitles(caller)) { SayTo(caller, "No permission to edit titles."); return; }

            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }

            var cls = TitlesManager.EnsureClass("GoD");
            cls.MinRole = TaseRole.GoD;
            cls.Privileges = TasePrivilege.BuildBlocks | TasePrivilege.BustBlocks |
                             TasePrivilege.UsePhysGun | TasePrivilege.JailPower  |
                             TasePrivilege.Operator;
            TitlesManager.SaveClass(cls);
            TitlesManager.Set(sid, "GoD");

            SayTo(caller, $"[TASE] {sid} is now GoD.");
        }

        private void CmdMakeAdmin(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2) { SayTo(caller, "Usage: tase_make_admin <steamid64>"); return; }
            if (!CanEditTitles(caller)) { SayTo(caller, "No permission to edit titles."); return; }

            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }

            var cls = TitlesManager.EnsureClass("Admin");
            cls.MinRole = TaseRole.Admin;
            cls.Privileges = TasePrivilege.BuildBlocks | TasePrivilege.BustBlocks |
                             TasePrivilege.UsePhysGun | TasePrivilege.JailPower;
            TitlesManager.SaveClass(cls);
            TitlesManager.Set(sid, "Admin");

            SayTo(caller, $"[TASE] {sid} is now Admin.");
        }

        private void CmdTitleSet(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 3) { SayTo(caller, "Usage: tase_title_set <steamid64> <title>"); return; }
            if (!CanEditTitles(caller)) { SayTo(caller, "No permission to edit titles."); return; }

            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }
            var title = info.GetArg(2);
            TitlesManager.Set(sid, title);
            SayTo(caller, $"[TASE] {sid} → '{title}'");
        }

        private void CmdTitleClear(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2) { SayTo(caller, "Usage: tase_title_clear <steamid64>"); return; }
            if (!CanEditTitles(caller)) { SayTo(caller, "No permission to edit titles."); return; }

            if (!ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Invalid steamid64."); return; }
            var had = TitlesManager.Clear(sid);
            SayTo(caller, had ? $"[TASE] Cleared title for {sid}." : $"[TASE] No title set for {sid}.");
        }

        private void CmdTitlesList(CCSPlayerController? caller, CommandInfo _)
        {
            var list = new List<string> { "[TASE] Current titles:" };
            foreach (var (sid, title) in TitlesManager.All())
                list.Add($"  {sid} → {title}");
            SendLines(caller, list);
        }

        private void CmdTaseMe(CCSPlayerController? caller, CommandInfo _)
        {
            if (caller == null) { SayTo(caller, "[TASE] Run this as a player."); return; }
            var (minRole, privs, _) = TitlesManager.ResolveFor(caller.SteamID);
            SayTo(caller, $"[TASE] You are '{TitlesManager.Get(caller.SteamID)}' (role {minRole}).");
            SayTo(caller, $"[TASE] Privileges: {privs}");
        }

        private static bool CanEditTitles(CCSPlayerController? caller)
        {
            // Console/RCON always allowed
            if (caller == null) return true;
            // Allow GoD or Operator privilege to edit titles
            var (role, privs, _) = TitlesManager.ResolveFor(caller.SteamID);
            return role >= TaseRole.GoD || (privs & TasePrivilege.Operator) != 0;
        }

        // -------------------- Permission helpers --------------------
        internal static bool HasAtLeast(CCSPlayerController? p, TaseRole min)
        {
            if (p == null) return true; // console
            var (role, _, _) = TitlesManager.ResolveFor(p.SteamID);
            return role >= min;
        }

        internal static bool HasPrivilege(CCSPlayerController? p, TasePrivilege need)
        {
            if (p == null) return true; // console
            var (_, privs, _) = TitlesManager.ResolveFor(p.SteamID);
            return (privs & need) != 0;
        }

        // -------------------- Phys hooks (implemented in PhysGun partial) --------------------
        partial void Phys_OnTick();
        partial void Phys_OnPlayerDisconnected(CCSPlayerController p);
        partial void Phys_OnMapEnd(string map);
    }
}
