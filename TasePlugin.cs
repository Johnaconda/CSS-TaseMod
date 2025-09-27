// TasePlugin.cs — core (roles, config, commands, logging, temp share, backups)
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Newtonsoft.Json;

namespace Tase
{
    // Keep the canonical role enum here so other modules can reference it.
    public enum TaseRole { None = 0, BlockMaker = 1, BlockBuster = 2, Admin = 3, GoD = 4 }

    // Config: superset for compatibility with older fields + new ones
    public class TaseConfig
    {
        // Titles / visuals
        public bool UseClanTagForTitles { get; set; } = true;

        // PhysGun
        public int  PhysMaxDistance { get; set; } = 1600;
        public bool PhysSafeMode    { get; set; } = true;

        // Jail
        public int JailDefaultRadius   { get; set; } = 128;
        public int JailDefaultHeight   { get; set; } = 96;
        public int JailDefaultDuration { get; set; } = 300;

        // Blockmaker / misc
        public int TomatoCount { get; set; } = 10;
        public int AutosaveBlocksSeconds { get; set; } = 60;

        // Backups
        public int BackupIntervalMinutes { get; set; } = 30;
        public int BackupKeep            { get; set; } = 10;
    }

    public partial class TasePlugin : BasePlugin
    {
        public override string ModuleName    => "TASE";
        public override string ModuleVersion => "0.4.2";
        public override string ModuleAuthor  => "you + assistant";

        // Store all data under <module>/data so it works on all hosts/paths.
        internal string DataPath       => Path.Combine(ModuleDirectory, "data");
        internal string RolesPath      => Path.Combine(DataPath, "roles.json");
        internal string ConfigPath     => Path.Combine(DataPath, "tase_config.json");
        internal string LogPath        => Path.Combine(DataPath, "tase_actions.log");
        internal string AppealsPath    => Path.Combine(DataPath, "appeals.json");
        internal string BackupPath     => Path.Combine(DataPath, "backups");
        internal string TitlesMapPath  => Path.Combine(DataPath, "titles.json"); // player → title
        internal string TitlesDirPath  => Path.Combine(DataPath, "titles");      // title classes

        // Session-only temp shares for BlockMaker
        private readonly HashSet<ulong> _tempSharedBlockMakers = new();

        internal TaseConfig Config = new();

        public override void Load(bool hotReload)
        {
            try
            {
                Directory.CreateDirectory(DataPath);
                Directory.CreateDirectory(BackupPath);
                Directory.CreateDirectory(TitlesDirPath);

                EnsureFile(RolesPath, "{}");
                EnsureFile(ConfigPath, JsonConvert.SerializeObject(Config, Formatting.Indented));
                EnsureFile(LogPath, "");
                EnsureFile(AppealsPath, "{}");
                EnsureFile(TitlesMapPath, "{}");

                LoadConfig();

                // Centralize roles in RolesManager (ensure RolesManager.cs is in the project)
                RolesManager.Load(RolesPath);

                // Load titles (ensure TitlesManager.cs + TitleClass.cs + PlayerVarCache.cs are in the project)
                TitlesManager.Load(TitlesMapPath);

                // Apply titles/privileges to anyone already online
                ApplyTitlesForAllOnline();

                RegisterCommands();

                LogLine($"Loaded (hotReload={hotReload}).");
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogPath, $"{DateTime.UtcNow:o} [TASE] Load EX: {ex}\n"); } catch {}
                Console.WriteLine($"[TASE] Load exception: {ex}");
            }
        }

        public override void Unload(bool hotReload)
        {
            RolesManager.Save();
            SaveAppeals(); // implemented in Appeal partial (safe if no-op)
            LogLine("Unloaded.");
        }

        // ========= Apply titles/privileges =========
        private void ApplyTitlesForAllOnline()
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid) continue;
                ApplyTitleFor(p);
            }
        }

        private void ApplyTitleFor(CCSPlayerController player)
        {
            ulong sid = player.SteamID;

            // Explicit types so deconstruction compiles across toolchains
            (TaseRole minRole, TasePrivilege privs, Dictionary<string, string> vars) = TitlesManager.ResolveFor(sid);

            // Enforce minimum role from title
            if (minRole > RolesManager.GetRole(sid))
                RolesManager.SetRole(sid, minRole);

            // Operator privilege implies GoD within our plugin’s role gates
            if ((privs & TasePrivilege.Operator) != 0)
                RolesManager.SetRole(sid, TaseRole.GoD);

            // Cache per-title vars for quick lookups by systems
            PlayerVarCache.Set(sid, vars);

            // NOTE: Some CS# builds don't expose SetClanTag; if you want it later and your build supports it, we can add it back.
        }

        // ========= Roles (helpers delegating to RolesManager) =========
        private TaseRole GetRole(CCSPlayerController? p)
            => (p?.SteamID is ulong sid) ? RolesManager.GetRole(sid) : TaseRole.None;

        private bool HasAtLeast(CCSPlayerController? p, TaseRole min)
            => GetRole(p) >= min;

        // ========= Config =========
        private void LoadConfig()
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonConvert.DeserializeObject<TaseConfig>(json) ?? new TaseConfig();
            }
            catch { Config = new TaseConfig(); }
        }

        // ========= Commands =========
        private void RegisterCommands()
        {
            // Role management (kept for compatibility; now uses RolesManager under the hood)
            AddCommand("tase_mod", "TASE role management", CmdTaseMod);

            // UI / helpers
            AddCommand("tase",     "Open TASE panel (text)", CmdTasePanel);
            AddCommand("bmsave",   "BlockMaker save",        CmdBMSave);
            AddCommand("sharebm",  "BlockMaker temp share",  CmdShareBM);
            AddCommand("revokebm", "Revoke BM temp share",   CmdRevokeBM);

            // Jail & PhysGun & Appeals are implemented in your partials already,
            // so we keep these registrations pointing to your real handlers:
            AddCommand("jail",          "Jail player",            CmdJail);
            AddCommand("unjail",        "Unjail player",          CmdUnjail);
            AddCommand("setjail",       "Set jail area/marker",   CmdSetJail);
            AddCommand("grab",          "Toggle phys-gun mode",   CmdGrab);
            AddCommand("appeal",        "Submit apology",         CmdAppeal);
            AddCommand("appeals",       "List appeals (Admin+)",  CmdAppealsList);
            AddCommand("appeal_accept", "Accept appeal",          CmdAppealAccept);
            AddCommand("appeal_reject", "Reject appeal",          CmdAppealReject);

            // Utility: re-apply titles/privileges/vars to all current players
            AddCommand("tase_apply_all", "Re-apply titles to all online players", (caller, info) =>
            {
                ApplyTitlesForAllOnline();
                SayTo(caller, "[TASE] Applied titles to all online players.");
            });

            // Quick admin utilities for titles/roles reload
            AddCommand("tase_reload", "Reload config + titles", (caller, info) =>
            {
                LoadConfig();
                TitlesManager.Load(TitlesMapPath);
                SayTo(caller, "[TASE] Config & Titles reloaded.");
                ApplyTitlesForAllOnline();
            });
        }

        private void CmdTaseMod(CCSPlayerController? caller, CommandInfo info)
        {
            if (info.ArgCount < 2)
            {
                SayTo(caller, "Usage: tase_mod add <steam64> <GoD|Admin|BlockBuster|BlockMaker> | del <steam64> | list | reload");
                return;
            }
            var sub = info.GetArg(1).ToLowerInvariant();
            switch (sub)
            {
                case "add":
                    if (info.ArgCount < 4) { SayTo(caller, "Usage: tase_mod add <steam64> <role>"); return; }
                    if (!ulong.TryParse(info.GetArg(2), out var sid)) { SayTo(caller, "Bad steam64"); return; }
                    if (!Enum.TryParse<TaseRole>(info.GetArg(3), true, out var role)) { SayTo(caller, "Bad role"); return; }
                    RolesManager.SetRole(sid, role);
                    LogAction("set_role", caller?.SteamID, sid, $"role={role}");
                    SayTo(caller, $"[TASE] {sid} => {role}");
                    break;

                case "del":
                    if (info.ArgCount < 3) { SayTo(caller, "Usage: tase_mod del <steam64>"); return; }
                    if (!ulong.TryParse(info.GetArg(2), out var sid2)) { SayTo(caller, "Bad steam64"); return; }
                    RolesManager.SetRole(sid2, TaseRole.None);
                    LogAction("remove_role", caller?.SteamID, sid2, "");
                    SayTo(caller, $"[TASE] removed {sid2}");
                    break;

                case "list":
                    foreach (var item in RolesManager.ListAll())
                    {
                        ulong sidL = item.steamId;
                        TaseRole roleL = item.role;
                        SayTo(caller, $"{sidL} : {roleL}");
                    }
                    break;

                case "reload":
                    RolesManager.Load(RolesPath);
                    SayTo(caller, "[TASE] roles reloaded.");
                    break;

                default:
                    SayTo(caller, "Subcommands: add | del | list | reload");
                    break;
            }
        }

        private void CmdTasePanel(CCSPlayerController? caller, CommandInfo info)
        {
            var role = GetRole(caller);
            SayTo(caller, $"[TASE] Your role: {role}");
            if (role >= TaseRole.BlockMaker)
                SayTo(caller, "  • /bmsave — save map (integrates with BM)");
            if (role >= TaseRole.BlockBuster)
                SayTo(caller, "  • /setjail | /jail <name> | /unjail <name>");
            if (role >= TaseRole.Admin)
                SayTo(caller, "  • /appeals — review ban appeals");
            if (role >= TaseRole.GoD)
                SayTo(caller, "  • /grab — phys-gun mode, /tase_backup now|list|restore <id>");
        }

        private void CmdBMSave(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockMaker)) { SayTo(caller, "No permission (BlockMaker+)."); return; }
            // TODO: wire to BM save
            SayTo(caller, "[TASE] Saved (stub).");
            LogAction("bm_save", caller?.SteamID, null, "");
        }

        private void CmdShareBM(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockMaker)) { SayTo(caller, "No permission (BlockMaker+)."); return; }
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /sharebm <playerName>"); return; }
            var target = FindByName(info.GetArg(1));
            if (target == null) { SayTo(caller, "Player not found."); return; }
            _tempSharedBlockMakers.Add(target.SteamID);
            SayTo(caller, $"[TASE] Temporarily shared BlockMaker with {target.PlayerName} (session only).");
            SayTo(target, $"[TASE] You were granted temporary BlockMaker by {caller?.PlayerName}.");
            LogAction("share_bm_temp", caller?.SteamID, target.SteamID, "");
        }

        private void CmdRevokeBM(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockMaker)) { SayTo(caller, "No permission (BlockMaker+)."); return; }
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /revokebm <playerName>"); return; }
            var target = FindByName(info.GetArg(1));
            if (target == null) { SayTo(caller, "Player not found."); return; }
            _tempSharedBlockMakers.Remove(target.SteamID);
            SayTo(caller, $"[TASE] Revoked temporary BlockMaker from {target.PlayerName}.");
            SayTo(target, $"[TASE] Your temporary BlockMaker access was revoked.");
            LogAction("revoke_bm_temp", caller?.SteamID, target.SteamID, "");
        }

        private void CmdBackup(CCSPlayerController? caller, CommandInfo info)
        {
            // GoD via console recommended
            if (!(caller == null || HasAtLeast(caller, TaseRole.GoD))) { SayTo(caller, "GoD or console only."); return; }

            if (info.ArgCount < 2) { SayTo(caller, "Usage: tase_backup now | list | restore <id>"); return; }
            var sub = info.GetArg(1).ToLowerInvariant();
            switch (sub)
            {
                case "now":
                    // TODO: call BM export and write a timestamped file in BackupPath
                    SayTo(caller, $"[Backups] Saved snapshot (stub).");
                    LogAction("backup_now", caller?.SteamID, null, "");
                    break;
                case "list":
                    var files = Directory.GetFiles(BackupPath, "*.json").OrderByDescending(f => f).Take(20).ToArray();
                    if (files.Length == 0) { SayTo(caller, "[Backups] No backups yet."); return; }
                    for (int i = 0; i < files.Length; i++)
                        SayTo(caller, $"[{i}] {Path.GetFileName(files[i])}");
                    break;
                case "restore":
                    if (info.ArgCount < 3) { SayTo(caller, "Usage: tase_backup restore <index>"); return; }
                    if (!int.TryParse(info.GetArg(2), out var idx)) { SayTo(caller, "Bad index."); return; }
                    var list = Directory.GetFiles(BackupPath, "*.json").OrderByDescending(f => f).ToArray();
                    if (idx < 0 || idx >= list.Length) { SayTo(caller, "Out of range."); return; }
                    var file = list[idx];
                    // TODO: feed BM import with this file
                    SayTo(caller, $"[Backups] Restored {Path.GetFileName(file)} (stub).");
                    LogAction("backup_restore", caller?.SteamID, null, $"file={Path.GetFileName(file)}");
                    break;
            }
        }

        // ========= Utilities =========
        internal static void EnsureFile(string path, string initial)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path)) File.WriteAllText(path, initial);
        }

        internal void LogLine(string text)
        {
            var line = $"{DateTime.UtcNow:o} [TASE] {text}";
            Console.WriteLine(line);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { /* ignore */ }
        }

        internal static void LogAction(string action, ulong? actor, ulong? target, string details)
        {
            Console.WriteLine($"{DateTime.UtcNow:o} [TASE] {action}\tactor={actor?.ToString() ?? ""}\ttarget={target?.ToString() ?? ""}\t{details}");
        }

        internal static void SayTo(CCSPlayerController? p, string text)
        {
            if (p == null) { Console.WriteLine(text); return; }
            p.PrintToChat(text);
        }

        internal static CCSPlayerController? FindByName(string name)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid) continue;
                if (p.PlayerName != null &&
                    p.PlayerName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            }
            return null;
        }

        internal bool IsBlockMaker(CCSPlayerController? p)
        {
            if (p == null) return false;
            if (_tempSharedBlockMakers.Contains(p.SteamID)) return true;
            return HasAtLeast(p, TaseRole.BlockMaker);
        }
    }
}
