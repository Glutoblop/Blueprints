using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Blueprints
{
    // Runtime Piece objects for the hammer: the selection tool and one per blueprint. They live under an inactive,
    // never-destroyed holder so none of their components ever run; vanilla only instantiates them as the placement
    // ghost (with ZNetView.m_forceDisableInit set), and we intercept the actual placing.
    internal static class BlueprintPieces
    {
        internal class Runtime
        {
            public Blueprint Blueprint;
            public Piece Piece;
            public List<CraftingStation> Stations = new List<CraftingStation>();
        }

        private const string ToolName = "Blueprints_SelectTool";

        private static GameObject _holder;
        private static Piece _tool;
        private static readonly List<Runtime> _runtimes = new List<Runtime>();
        private static readonly Dictionary<Piece, Runtime> _byPiece = new Dictionary<Piece, Runtime>();
        private static PieceTable _table;
        private static bool _dirty = true;

        internal static Piece Tool => _tool;
        internal static IReadOnlyList<Runtime> Runtimes => _runtimes;
        internal static Piece.PieceCategory Category { get; private set; } = Piece.PieceCategory.Max;

        internal static bool IsTool(Piece p) => p != null && _tool != null && p == _tool;
        internal static bool IsOurs(Piece p) => p != null && (IsTool(p) || _byPiece.ContainsKey(p));
        internal static bool TryGet(Piece p, out Runtime rt)
        {
            rt = null;
            return p != null && _byPiece.TryGetValue(p, out rt);
        }

        internal static PieceTable HammerTable()
        {
            if (ObjectDB.instance == null) return null;
            GameObject hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            return hammer != null ? hammer.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
        }

        internal static void MarkDirty() => _dirty = true;

        // Called before the hammer table works out its available pieces.
        internal static void EnsureRegistered(PieceTable table)
        {
            if (!_dirty && _table == table && table.m_pieces.Contains(_tool ? _tool.gameObject : null)) return;
            if (ZNetScene.instance == null) return;

            _table = table;
            _dirty = false;
            if (_holder == null)
            {
                _holder = new GameObject("Blueprints_Templates");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            ChooseCategory(table);
            table.m_pieces.RemoveAll(go => go == null || go.transform.parent == _holder.transform);
            BuildTool(table);
            BuildBlueprints();

            if (_tool != null) table.m_pieces.Add(_tool.gameObject);
            foreach (var rt in _runtimes) table.m_pieces.Add(rt.Piece.gameObject);
        }

        // Our pieces need one of the 9 vanilla category slots for PieceTable's bookkeeping (selection index, lookup).
        // Use one the hammer doesn't, so they never show up in a vanilla category.
        private static void ChooseCategory(PieceTable table)
        {
            var used = new HashSet<Piece.PieceCategory>(table.m_categories);
            foreach (GameObject go in table.m_pieces)
            {
                Piece p = go ? go.GetComponent<Piece>() : null;
                if (p != null && !IsOurs(p)) used.Add(p.m_category);
            }
            Piece.PieceCategory[] candidates =
            {
                Piece.PieceCategory.Meads, Piece.PieceCategory.Food, Piece.PieceCategory.Feasts,
                Piece.PieceCategory.DeepNorth, Piece.PieceCategory.Furniture, Piece.PieceCategory.Crafting,
            };
            Category = candidates.FirstOrDefault(c => !used.Contains(c));
            if (used.Contains(Category)) Plugin.Warn("No free piece category; blueprints share one with vanilla pieces");
        }

        private static void BuildTool(PieceTable table)
        {
            if (_tool != null)
            {
                _tool.m_category = Category;
                return;
            }
            Piece repair = table.m_pieces.Select(go => go ? go.GetComponent<Piece>() : null)
                                         .FirstOrDefault(p => p != null && p.m_repairPiece && !IsOurs(p));
            if (repair == null) { Plugin.Warn("Hammer has no repair piece to copy"); return; }

            GameObject go2 = Object.Instantiate(repair.gameObject, _holder.transform, false);
            go2.name = ToolName;
            _tool = go2.GetComponent<Piece>();
            _tool.m_name = "Blueprint selection";
            _tool.m_description = DescribeTool();
            _tool.m_category = Category;
            _tool.m_resources = new Piece.Requirement[0];
            _tool.m_craftingStation = null;
        }

        internal static string DescribeTool()
        {
            return "Click a built piece to mark or unmark it.\n" +
                   $"{Plugin.ConnectedModifier.Value} + click: mark everything connected to it.\n" +
                   $"{Plugin.SaveKey.Value}: save the marked pieces as a blueprint.\n" +
                   $"{Plugin.ClearKey.Value}: clear the marks.";
        }

        private static void BuildBlueprints()
        {
            foreach (var rt in _runtimes)
                if (rt.Piece != null) Object.Destroy(rt.Piece.gameObject);
            _runtimes.Clear();
            _byPiece.Clear();

            foreach (Blueprint bp in BlueprintStore.All)
            {
                Runtime rt = BuildBlueprint(bp);
                if (rt == null) continue;
                _runtimes.Add(rt);
                _byPiece[rt.Piece] = rt;
            }
        }

        private static Runtime BuildBlueprint(Blueprint bp)
        {
            var root = new GameObject("Blueprint_" + bp.Id);
            root.transform.SetParent(_holder.transform, false);
            Piece piece = root.AddComponent<Piece>();
            var rt = new Runtime { Blueprint = bp, Piece = piece };

            var costs = new Dictionary<string, Piece.Requirement>();
            var counts = new Dictionary<string, int>();
            var icons = new Dictionary<string, Sprite>();
            int placed = 0, missing = 0;

            foreach (BlueprintEntry e in bp.pieces)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(e.prefab);
                Piece src = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (src == null) { missing++; continue; }
                placed++;

                GameObject child = Object.Instantiate(prefab, root.transform, false);
                child.transform.localPosition = e.pos;
                child.transform.localRotation = e.rot;

                foreach (Piece.Requirement req in src.m_resources)
                {
                    if (req.m_resItem == null || req.m_amount <= 0) continue;
                    string key = req.m_resItem.name;
                    if (!costs.TryGetValue(key, out var sum))
                        costs[key] = sum = new Piece.Requirement { m_resItem = req.m_resItem, m_amount = 0, m_recover = true };
                    sum.m_amount += req.m_amount;
                }
                if (src.m_craftingStation != null && !rt.Stations.Any(s => s.m_name == src.m_craftingStation.m_name))
                    rt.Stations.Add(src.m_craftingStation);

                counts[src.m_name] = (counts.TryGetValue(src.m_name, out int c) ? c : 0) + 1;
                if (src.m_icon != null) icons[src.m_name] = src.m_icon;
            }

            if (placed == 0)
            {
                Plugin.Warn($"Blueprint '{bp.name}' has no pieces this game knows; skipped");
                Object.Destroy(root);
                return null;
            }

            string most = counts.OrderByDescending(kv => kv.Value).First().Key;
            piece.m_name = bp.name;
            piece.m_icon = icons.TryGetValue(most, out var icon) ? icon : (_tool != null ? _tool.m_icon : null);
            piece.m_category = Category;
            piece.m_resources = costs.Values.OrderByDescending(r => r.m_amount).ToArray();
            piece.m_craftingStation = null; // every station is checked in the HaveRequirements patch
            piece.m_canRotate = true;
            piece.m_description = Describe(bp, placed, missing, counts, rt.Stations);
            return rt;
        }

        private static string Describe(Blueprint bp, int placed, int missing, Dictionary<string, int> counts, List<CraftingStation> stations)
        {
            var lines = new List<string> { $"Blueprint: {placed} pieces" };
            foreach (var kv in counts.OrderByDescending(kv => kv.Value).Take(6))
                lines.Add($"  {kv.Value} x {kv.Key}");
            if (counts.Count > 6) lines.Add("  ...");
            if (stations.Count > 0) lines.Add("Needs: " + string.Join(", ", stations.Select(s => s.m_name)));
            if (missing > 0) lines.Add($"<color=orange>{missing} unknown piece(s) will be skipped</color>");
            lines.Add($"{Plugin.SaveKey.Value}: rename   {Plugin.DeleteKey.Value} twice: delete");
            return string.Join("\n", lines);
        }

        // Rebuild after a save / rename / delete and refresh the hammer menu.
        internal static void Refresh()
        {
            _dirty = true;
            Player player = Player.m_localPlayer;
            PieceTable table = HammerTable();
            if (table != null) EnsureRegistered(table);
            if (player != null && player.m_buildPieces != null) player.UpdateAvailablePiecesList();
            BuildMenuTab.ForceRefresh();
        }
    }
}
