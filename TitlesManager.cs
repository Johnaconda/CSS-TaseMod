// TitlesManager.cs — player→title map + title class files
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Tase
{
    public static class TitlesManager
    {
        private static readonly object _lock = new();

        // player → title
        private static Dictionary<ulong, string> _titles = new();

        private static string _mapPath = "";     // .../data/tase/titles.json
        private static string _classesDir = "";  // .../data/tase/titles/
        private static Dictionary<string, TitleClass> _classes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Load titles from a map file path. Classes dir is derived as {dir}/titles.
        /// </summary>
        public static void Load(string mapPath)
        {
            lock (_lock)
            {
                _mapPath = mapPath;
                var root = Path.GetDirectoryName(_mapPath) ?? ".";
                _classesDir = Path.Combine(root, "titles");

                Directory.CreateDirectory(root);
                Directory.CreateDirectory(_classesDir);

                if (!File.Exists(_mapPath))
                    File.WriteAllText(_mapPath, "{}");

                try
                {
                    _titles = JsonConvert.DeserializeObject<Dictionary<ulong, string>>(File.ReadAllText(_mapPath)) ?? new();
                }
                catch
                {
                    _titles = new();
                    Save();
                }

                _classes = new(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(_classesDir, "*.json"))
                {
                    try
                    {
                        var cls = JsonConvert.DeserializeObject<TitleClass>(File.ReadAllText(file));
                        if (cls != null && !string.IsNullOrWhiteSpace(cls.Name))
                            _classes[cls.Name] = cls;
                    }
                    catch { /* ignore malformed class file */ }
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_mapPath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(_mapPath)!);
                File.WriteAllText(_mapPath, JsonConvert.SerializeObject(_titles, Formatting.Indented));
            }
        }

        public static string Get(ulong steamId)
            => _titles.TryGetValue(steamId, out var t) ? t : string.Empty;

        public static bool TryGet(ulong steamId, out string title)
            => _titles.TryGetValue(steamId, out title!);

        public static void Set(ulong steamId, string title)
        {
            _titles[steamId] = title.Trim();
            Save();
            EnsureClass(title); // make sure a default class file exists
        }

        public static bool Clear(ulong steamId)
        {
            var had = _titles.Remove(steamId);
            if (had) Save();
            return had;
        }

        public static IEnumerable<(ulong steamId, string title)> All()
        {
            foreach (var kv in _titles)
                yield return (kv.Key, kv.Value);
        }

        // ---- Classes API ----
        public static TitleClass EnsureClass(string name)
        {
            Directory.CreateDirectory(_classesDir);
            if (!_classes.TryGetValue(name, out var cls))
            {
                cls = new TitleClass { Name = name };
                _classes[name] = cls;
                File.WriteAllText(Path.Combine(_classesDir, $"{name}.json"),
                    JsonConvert.SerializeObject(cls, Formatting.Indented));
            }
            return cls;
        }

        public static IEnumerable<TitleClass> Classes() => _classes.Values;

        public static TitleClass? GetClass(string name)
            => _classes.TryGetValue(name, out var c) ? c : null;

        public static void SaveClass(TitleClass cls)
        {
            _classes[cls.Name] = cls;
            Directory.CreateDirectory(_classesDir);
            File.WriteAllText(Path.Combine(_classesDir, $"{cls.Name}.json"),
                JsonConvert.SerializeObject(cls, Formatting.Indented));
        }

        public static (TaseRole minRole, TasePrivilege privs, Dictionary<string, string> vars) ResolveFor(string title)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            TasePrivilege privs = TasePrivilege.None;
            var minRole = TaseRole.None;

            void Merge(string t)
            {
                if (string.IsNullOrWhiteSpace(t) || !seen.Add(t)) return;
                if (!_classes.TryGetValue(t, out var cls)) return;

                foreach (var baseName in cls.Inherits) Merge(baseName);

                foreach (var kv in cls.Vars) if (!vars.ContainsKey(kv.Key)) vars[kv.Key] = kv.Value;
                privs |= cls.Privileges;
                if (cls.MinRole > minRole) minRole = cls.MinRole;
            }

            Merge(title);
            return (minRole, privs, vars);
        }

        public static (TaseRole minRole, TasePrivilege privs, Dictionary<string, string> vars) ResolveFor(ulong steamId)
            => ResolveFor(Get(steamId));
    }
}
