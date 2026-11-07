using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Blueprints
{
    [Serializable]
    internal class BlueprintEntry
    {
        public string prefab;
        public Vector3 pos;    // relative to the blueprint's anchor (bottom centre, anchor yaw)
        public Quaternion rot; // relative to the anchor's rotation
    }

    [Serializable]
    internal class Blueprint
    {
        public string name;
        public List<BlueprintEntry> pieces = new List<BlueprintEntry>();

        [NonSerialized] public string Id; // file name without extension
    }

    // One JSON file per blueprint in BepInEx/config/Blueprints/<id>.json.
    internal static class BlueprintStore
    {
        internal static readonly List<Blueprint> All = new List<Blueprint>();

        private static string Folder => Path.Combine(Paths.ConfigPath, "Blueprints");

        internal static void Load()
        {
            All.Clear();
            if (!Directory.Exists(Folder)) return;
            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var bp = JsonUtility.FromJson<Blueprint>(File.ReadAllText(file));
                    if (bp == null || bp.pieces == null || bp.pieces.Count == 0) continue;
                    bp.Id = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(bp.name)) bp.name = bp.Id;
                    All.Add(bp);
                }
                catch (Exception e)
                {
                    Plugin.Warn("Couldn't read blueprint " + file + ": " + e.Message);
                }
            }
            Sort();
            Plugin.Log($"Loaded {All.Count} blueprint(s)");
        }

        internal static void Add(Blueprint bp)
        {
            bp.Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            All.Add(bp);
            Sort();
            Write(bp);
        }

        internal static void Rename(Blueprint bp, string name)
        {
            bp.name = name;
            Sort();
            Write(bp);
        }

        internal static void Delete(Blueprint bp)
        {
            All.Remove(bp);
            try { File.Delete(PathFor(bp)); }
            catch (Exception e) { Plugin.Warn("Couldn't delete blueprint file: " + e.Message); }
        }

        internal static string UniqueName(string wanted, Blueprint except = null)
        {
            string name = wanted;
            for (int i = 2; All.Any(b => b != except && string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase)); i++)
                name = wanted + " " + i;
            return name;
        }

        private static void Sort() => All.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        private static string PathFor(Blueprint bp) => Path.Combine(Folder, bp.Id + ".json");

        private static void Write(Blueprint bp)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathFor(bp), JsonUtility.ToJson(bp, true));
            }
            catch (Exception e)
            {
                Plugin.Warn("Couldn't save blueprint: " + e.Message);
            }
        }
    }
}
