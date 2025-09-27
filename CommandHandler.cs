// CommandHandler.cs
using System;

public static class CommandHandler
{
    public static void RegisterCommands()
    {
        Server.AddConsoleCommand("tase_mod", OnConsoleTaseMod);
        Server.AddChatCommand("/tase", OnChatTase);
        Server.AddChatCommand("/bmsave", OnChatBMSave);
        Server.AddChatCommand("/jail", OnChatJail);
        Server.AddChatCommand("/unjail", OnChatUnjail);
        Server.AddChatCommand("/grab", OnChatGrabToggle);
        Server.AddChatCommand("/appeal", OnChatAppeal); // for banned spectators
        Server.AddConsoleCommand("tase_roles_reload", (args) => RolesManager.Load(TasePlugin.RolesPath));
        Server.ConsoleWriteLine("[TASE] Commands registered.");
    }

    // Console: tase_mod add <steam64> <role>
    static void OnConsoleTaseMod(CommandArgs args)
    {
        if (args.ArgCount < 1) { Server.ConsoleWriteLine("Usage: tase_mod add/del/list ..."); return; }
        var sub = args[0].ToLower();
        if (sub == "add" && args.ArgCount >= 3)
        {
            var sid = ulong.Parse(args[1]);
            var role = Enum.Parse<TaseRole>(args[2], true);
            RolesManager.SetRole(sid, role);
            Server.ConsoleWriteLine($"[TASE] {sid} -> {role}");
        }
        else if (sub == "del" && args.ArgCount >= 2)
        {
            var sid = ulong.Parse(args[1]);
            RolesManager.RemoveRole(sid);
            Server.ConsoleWriteLine($"[TASE] removed {sid}");
        }
        else if (sub == "list")
        {
            foreach (var (steam, role) in RolesManager.ListAll()) Server.ConsoleWriteLine($"{steam} : {role}");
        }
    }

    // In-game chat command examples (simplified)
    static void OnChatTase(PlayerInvoker inv, string[] args)
    {
        var role = RolesManager.GetRole(inv.SteamId);
        HUD.OpenTasePanel(inv, role); // UI presenter (you implement panorama or simple chat output)
    }

    static void OnChatBMSave(PlayerInvoker inv, string[] args)
    {
        if (!RolesManager.HasAtLeast(inv.SteamId, TaseRole.BlockMaker)) { inv.Reply("No permission."); return; }
        BlockMaker.SaveCurrentMap(inv);
        inv.Reply("BlockMaker: map saved.");
    }

    static void OnChatJail(PlayerInvoker inv, string[] args)
    {
        if (!RolesManager.HasAtLeast(inv.SteamId, TaseRole.BlockBuster)) { inv.Reply("No permission."); return; }
        if (args.Length == 0) { inv.Reply("Usage: /jail <targetplayer>"); return; }
        var target = Server.FindPlayerByNameOrId(args[0]);
        if (target == null) { inv.Reply("Target not found."); return; }
        JailManager.JailPlayer(target, inv.SteamId);
        inv.Reply($"Jailed {target.Name}");
    }

    static void OnChatUnjail(PlayerInvoker inv, string[] args)
    {
        if (!RolesManager.HasAtLeast(inv.SteamId, TaseRole.BlockBuster)) { inv.Reply("No permission."); return; }
        if (args.Length==0) { inv.Reply("Usage: /unjail <target>"); return; }
        var target = Server.FindPlayerByNameOrId(args[0]);
        if (target == null) { inv.Reply("Target not found."); return; }
        JailManager.UnjailPlayer(target, inv.SteamId);
        inv.Reply($"Unjailed {target.Name}");
    }

    static void OnChatGrabToggle(PlayerInvoker inv, string[] args)
    {
        if (!RolesManager.HasAtLeast(inv.SteamId, TaseRole.GoD)) { inv.Reply("Only GoD may use physgun."); return; }
        PhysGun.ToggleForPlayer(inv);
    }

    static void OnChatAppeal(PlayerInvoker inv, string[] args)
    {
        // Called by a banned spectator — forwards to BanAppeal subsystem
        BanAppeal.SubmitAppeal(inv, args.JoinToString(" "));
    }
}
