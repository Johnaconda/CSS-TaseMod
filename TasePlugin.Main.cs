// TasePlugin.Main.cs — Canonical “Mainfile” (Authoritative, wired-up)
//
// Keep this file as the authoritative map of the plugin. Implementations of
// subsystems can live in partials (e.g., Tase.PhysGun.cs), referenced via
// partial methods below so the project compiles even when a subsystem is
// temporarily absent.

using System;
using System.IO;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Newtonsoft.Json;

namespace Tase
{
    public partial class TasePlugin : BasePlugin
    {
        public override string ModuleName => "TASE";
        public override string ModuleAuthor => "You + ChatGPT";
        public override string ModuleVersion => "0.2-mainfile";

        // ==== Paths =========================================================
        public static readonly string DataRoot      = Path.Combine(Server.DataPath, "tase");
        public static readonly string LogsDir       = Path.Combine(DataRoot, "logs");
        public static readonly string TitlesMapPath = Path.Combine(DataRoot, "titles.json");
        public static readonly string TitlesDir     = Path.Combine(DataRoot, "titles");
        public static readonly string RolesPath     = Path.Combine(DataRoot, "roles.json");
        public static readonly string ConfigPath    = Path.Combine(DataRoot, "tase_config.json");
        public static readonly string AppealsPath   = Path.Combine(DataRoot, "appeals.json");
        public static readonly string JailDir       = Path.Combine(DataRoot, "jail");
        public static readonly string BlocksDir     = Path.Combine(DataRoot, "blocks");

        // ==== Config ========================================================
        public class TaseConfig
        {
            public bool UseClanTagForTitles { get; set; } = true;
            public bool PhysSafeMode         { get; set; } = true;
            public int  PhysMaxDistance      { get; set; } = 800;
            public int  JailDefaultDuration  { get; set; } = 300; // seconds
            public int  AutosaveBlocksSeconds{ get; set; } = 60;
        }

        public static TaseConfig Config = new();

        // ==== Lifecycle =====================================================
        public override void Load(bool hotReload)
        {
            try
            {
                EnsureDirectories();
                LoadConfig();
                RolesManager.Load(RolesPath);
                TitlesManager.Load(TitlesMapPath); // also loads classes in TitlesDir
                Logger.Init(LogsDir);

                RegisterCommands();
                RegisterEventHooks();

                Logger.Info($"[TASE] Loaded (hotReload={hotReload}).");
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(LogsDir);
                    File.AppendAllText(Path.Combine(LogsDir, "fatal.log"), DateTime.UtcNow + " " + ex + "\n");
                }
                catch { /* ignore */ }
                throw;
            }
        }

        public override void Unload(bool hotReload)
        {
            SaveAll();
            Logger.Info($"[TASE] Unloaded (hotReload={hotReload}).");
        }

        // ==== Boot Helpers ==================================================
        private void EnsureDirectories()
        {
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(LogsDir);
            Directory.CreateDirectory(TitlesDir);
            Directory.CreateDirectory(JailDir);
            Directory.CreateDirectory(BlocksDir);
            EnsureFile(ConfigPath, "{\n  \"UseClanTagForTitles\": true\n}\n");
            EnsureFile(TitlesMapPath, "{}\n");
        }

        private static void EnsureFile(string path, string defaultContent)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path)) File.WriteAllText(path, defaultContent);
        }

        private void LoadConfig()
        {
            try
            {
                var txt = File.ReadAllText(ConfigPath);
                Config = JsonConvert.DeserializeObject<TaseConfig>(txt) ?? new TaseConfig();
            }
            catch { Config = new TaseConfig(); }
        }

        private void SaveAll()
        {
            // TODO: if any in-memory state needs flushing, do it here
        }

        // ==== Commands ======================================================
        private void RegisterCommands()
        {
            CommandHandler.RegisterCommands(); // existing module keeps all chat/console commands

            // Minimal built-in utility: reload config & titles
            Server.AddConsoleCommand("tase_mod_reload", info =>
            {
                if (!Server.CallerIsRconOrConsole(info)) { Server.ConsoleWriteLine("RCON/console only."); return; }
                LoadConfig();
                TitlesManager.Load(TitlesMapPath);
                Server.ConsoleWriteLine("[TASE] Config & Titles reloaded.");
            });

            // Diagnostic
            Server.AddConsoleCommand("tase_mod_diag", info =>
            {
                if (!Server.CallerIsRconOrConsole(info)) { Server.ConsoleWriteLine("RCON/console only."); return; }
                int count = 0;
                if (Directory.Exists(TitlesDir)) count = Directory.GetFiles(TitlesDir, "*.json").Length;
                Server.ConsoleWriteLine($"[TASE] players={Server.PlayerCount} titles={count}");
            });
        }

        // ==== Events / Hooks ===============================================
        private void RegisterEventHooks()
        {
            Server.HookPlayerConnected(OnPlayerConnected);
            Server.HookPlayerDisconnected(OnPlayerDisconnected);
            Server.HookMapStart(OnMapStart);
            Server.HookMapEnd(OnMapEnd);
            Server.HookTick(OnTick);
        }

        private void OnPlayerConnected(Player player)
        {
            // Resolve title → privileges/vars
            var (minRole, privs, vars) = TitlesManager.ResolveFor(player.SteamId64);
            if (minRole > RolesManager.GetRole(player.SteamId64))
                RolesManager.SetRole(player.SteamId64, minRole);
            if ((privs & TasePrivilege.Operator) != 0)
                RolesManager.SetRole(player.SteamId64, TaseRole.GoD);

            PlayerVarCache.Set(player.SteamId64, vars);

            if (Config.UseClanTagForTitles)
            {
                var title = TitlesManager.Get(player.SteamId64);
                if (!string.IsNullOrEmpty(title)) { try { player.SetClanTag(title); } catch { } }
            }

            // Module hooks (optional; implement in partials)
            Jail_OnPlayerConnected(player);
            Phys_OnPlayerConnected(player);
            Blocks_OnPlayerConnected(player);
        }

        private void OnPlayerDisconnected(Player player)
        {
            Jail_OnPlayerDisconnected(player);
            Phys_OnPlayerDisconnected(player);
            Blocks_OnPlayerDisconnected(player);
        }

        private void OnMapStart(string map)
        {
            Jail_OnMapStart(map);
            Phys_OnMapStart(map);
            Blocks_OnMapStart(map);
        }

        private void OnMapEnd(string map)
        {
            Jail_OnMapEnd(map);
            Phys_OnMapEnd(map);
            Blocks_OnMapEnd(map);
        }

        private void OnTick()
        {
            Phys_OnTick(); // smoothing, safety checks
        }

        // ==== Public Helpers (shared across modules) ========================
        public static string GetPlayerTitle(ulong steamId) => TitlesManager.Get(steamId);

        public static int GetIntVar(ulong sid, string key, int fallback)
        {
            var s = PlayerVarCache.Get(sid, key, fallback.ToString());
            return int.TryParse(s, out var v) ? v : fallback;
        }

        public static bool HasPrivilege(ulong sid, TasePrivilege p)
        {
            var (minRole, privs, _) = TitlesManager.ResolveFor(sid);
            if ((privs & p) != 0) return true;
            // Role-based fallback for legacy gates
            return p == TasePrivilege.Operator && RolesManager.GetRole(sid) >= TaseRole.GoD;
        }

        public static void Say(string msg) => Server.SayToAll(msg);
        public static void SayTo(Player p, string msg) => p.Reply(msg);

        // ==== Partial hooks (optional implementations in other files) =======
        // PhysGun
        partial void Phys_OnTick();
        partial void Phys_OnPlayerConnected(Player p);
        partial void Phys_OnPlayerDisconnected(Player p);
        partial void Phys_OnMapStart(string map);
        partial void Phys_OnMapEnd(string map);

        // Jail
        partial void Jail_OnPlayerConnected(Player p);
        partial void Jail_OnPlayerDisconnected(Player p);
        partial void Jail_OnMapStart(string map);
        partial void Jail_OnMapEnd(string map);

        // Blocks
        partial void Blocks_OnPlayerConnected(Player p);
        partial void Blocks_OnPlayerDisconnected(Player p);
        partial void Blocks_OnMapStart(string map);
        partial void Blocks_OnMapEnd(string map);
    }
}
