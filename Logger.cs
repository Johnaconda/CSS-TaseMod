// Logger.cs
using System;
using System.IO;
using System.Collections.Generic;

public static class Logger
{
    private static string _path;
    private static object _lock = new();

    public static void Init(string path) { _path = path; if (!File.Exists(_path)) File.WriteAllText(_path, ""); }

    public static void LogAction(string action, ulong actorSteam, ulong? targetSteam, string details)
    {
        var line = $"{DateTime.UtcNow:o}\t{action}\tactor={actorSteam}\ttarget={(targetSteam?.ToString() ?? "")}\t{details}";
        lock(_lock) File.AppendAllText(_path, line + Environment.NewLine);
    }

    public static IEnumerable<string> ReadLastLines(int n = 200)
    {
        var lines = File.ReadAllLines(_path);
        int start = Math.Max(0, lines.Length - n);
        for (int i=start;i<lines.Length;i++) yield return lines[i];
    }
}
