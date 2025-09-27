// RolesManager.cs — simple JSON role store
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Tase
{
    public static class RolesManager
    {
        private static Dictionary<ulong, TaseRole> _roles = new();
        private static string _path = ""; // set by Load(path)

        public static void Load(string path)
        {
            _path = path;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (!File.Exists(_path)) { Save(); return; }
                var json = File.ReadAllText(_path);
                _roles = JsonConvert.DeserializeObject<Dictionary<ulong, TaseRole>>(json) ?? new();
            }
            catch
            {
                _roles = new();
                Save();
            }
        }

        public static void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonConvert.SerializeObject(_roles, Formatting.Indented));
        }

        public static TaseRole GetRole(ulong steamId)
            => _roles.TryGetValue(steamId, out var r) ? r : TaseRole.None;

        public static void SetRole(ulong steamId, TaseRole role)
        {
            _roles[steamId] = role;
            Save();
        }

        public static bool HasAtLeast(ulong steamId, TaseRole min)
            => GetRole(steamId) >= min;

        public static IEnumerable<(ulong steamId, TaseRole role)> ListAll()
        {
            foreach (var kv in _roles) yield return (kv.Key, kv.Value);
        }
    }
}
