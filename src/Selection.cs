using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Blueprints
{
    // Pieces marked with the "Blueprint selection" tool, tinted with MaterialMan while marked.
    internal static class Selection
    {
        private static readonly HashSet<Piece> Marked = new HashSet<Piece>();
        private static readonly List<Piece> Order = new List<Piece>(); // click order: the first piece sets the blueprint's facing
        private static int _pieceMask;

        internal static int Count => Marked.Count;

        private static int PieceMask => _pieceMask != 0 ? _pieceMask : (_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid"));

        // Player.Repair with our tool selected.
        internal static void OnClick(Player player)
        {
            Piece hovered = player.GetHoveringPiece();
            if (hovered == null) return;
            if (!CanUse(hovered))
            {
                player.Message(MessageHud.MessageType.Center, "That can't go in a blueprint");
                return;
            }

            if (ZInput.GetKey(Plugin.ConnectedModifier.Value, false))
            {
                int added = AddConnected(hovered);
                player.Message(MessageHud.MessageType.TopLeft, $"Marked {added} connected piece(s), {Marked.Count} total");
            }
            else if (Marked.Contains(hovered))
            {
                Remove(hovered);
                player.Message(MessageHud.MessageType.TopLeft, $"Unmarked {hovered.m_name}, {Marked.Count} marked");
            }
            else
            {
                Add(hovered);
                player.Message(MessageHud.MessageType.TopLeft, $"Marked {hovered.m_name}, {Marked.Count} marked");
            }
            hovered.m_placeEffect.Create(hovered.transform.position, hovered.transform.rotation);
        }

        private static bool CanUse(Piece p)
        {
            if (p == null || p.GetComponent<ZNetView>() == null || BlueprintPieces.IsOurs(p)) return false;
            return ZNetScene.instance != null && ZNetScene.instance.GetPrefab(Utils.GetPrefabName(p.gameObject)) != null;
        }

        private static void Add(Piece p)
        {
            if (Marked.Add(p)) Order.Add(p);
        }

        private static void Remove(Piece p)
        {
            if (!Marked.Remove(p)) return;
            Order.Remove(p);
            Unhighlight(p);
        }

        internal static void Clear()
        {
            foreach (Piece p in Marked) Unhighlight(p);
            Marked.Clear();
            Order.Clear();
        }

        // Flood fill through pieces whose collider bounds touch.
        private static int AddConnected(Piece start)
        {
            int before = Marked.Count;
            int limit = Plugin.MaxConnected.Value;
            var queue = new Queue<Piece>();
            var seen = new HashSet<Piece> { start };
            queue.Enqueue(start);
            Add(start);
            var hits = new Collider[256];

            while (queue.Count > 0 && Marked.Count - before < limit)
            {
                Piece p = queue.Dequeue();
                foreach (Collider col in p.GetComponentsInChildren<Collider>())
                {
                    if (!col.enabled || col.isTrigger) continue;
                    Bounds b = col.bounds;
                    int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.05f, hits, Quaternion.identity, PieceMask);
                    for (int i = 0; i < n; i++)
                    {
                        Piece other = hits[i].GetComponentInParent<Piece>();
                        if (other == null || !seen.Add(other) || !CanUse(other)) continue;
                        Add(other);
                        queue.Enqueue(other);
                        if (Marked.Count - before >= limit) break;
                    }
                }
            }
            return Marked.Count - before;
        }

        // Re-applied every frame: vanilla's repair-mode hover highlight resets the tint on the hovered piece.
        internal static void UpdateHighlights(bool show)
        {
            if (Marked.Count == 0) return;
            Marked.RemoveWhere(p => p == null);
            Order.RemoveAll(p => p == null);
            Color c = Plugin.HighlightColor.Value;
            foreach (Piece p in Marked)
            {
                if (show)
                {
                    MaterialMan.instance.SetValue(p.gameObject, ShaderProps._EmissionColor, c * 0.5f);
                    MaterialMan.instance.SetValue(p.gameObject, ShaderProps._Color, c);
                }
                else Unhighlight(p);
            }
        }

        private static void Unhighlight(Piece p)
        {
            if (p == null || MaterialMan.instance == null) return;
            MaterialMan.instance.ResetValue(p.gameObject, ShaderProps._EmissionColor);
            MaterialMan.instance.ResetValue(p.gameObject, ShaderProps._Color);
        }

        // Anchor = bottom centre of the marked pieces, facing the way the first marked piece faces.
        internal static Blueprint ToBlueprint(string name)
        {
            List<Piece> pieces = Order.Where(p => p != null).ToList();
            if (pieces.Count == 0) return null;

            Bounds bounds = new Bounds(pieces[0].transform.position, Vector3.zero);
            foreach (Piece p in pieces)
            {
                bounds.Encapsulate(p.transform.position);
                foreach (Collider col in p.GetComponentsInChildren<Collider>())
                    if (col.enabled && !col.isTrigger) bounds.Encapsulate(col.bounds.min);
            }
            Vector3 anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Quaternion facing = Quaternion.Euler(0f, pieces[0].transform.rotation.eulerAngles.y, 0f);
            Quaternion inv = Quaternion.Inverse(facing);

            var bp = new Blueprint { name = name };
            foreach (Piece p in pieces.OrderBy(p => p.transform.position.y)) // lowest first, so supports go down first
            {
                bp.pieces.Add(new BlueprintEntry
                {
                    prefab = Utils.GetPrefabName(p.gameObject),
                    pos = inv * (p.transform.position - anchor),
                    rot = inv * p.transform.rotation,
                });
            }
            return bp;
        }
    }
}
