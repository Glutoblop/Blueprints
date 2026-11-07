using UnityEngine;

namespace Blueprints
{
    // Keys while the hammer is out: save / clear with the selection tool, rename / delete with a blueprint chosen.
    // Also keeps the marked pieces tinted.
    internal class BlueprintInput : MonoBehaviour
    {
        private const float DeleteConfirmSeconds = 3f;

        private Blueprint _pendingDelete;
        private float _pendingDeleteUntil;

        private void Update()
        {
            Player player = Player.m_localPlayer;
            bool building = player != null && !player.IsDead() && player.InPlaceMode() && player.m_buildPieces == BlueprintPieces.HammerTable();
            Piece selected = building ? player.m_buildPieces.GetSelectedPiece() : null;
            bool toolOut = BlueprintPieces.IsTool(selected);

            Selection.UpdateHighlights(building);

            if (!building || TextInput.IsVisible() || Hud.IsPieceSelectionVisible() || Console.IsVisible()
                || (Chat.instance != null && Chat.instance.HasFocus()) || Menu.IsVisible() || InventoryGui.IsVisible())
                return;

            if (toolOut)
            {
                if (Plugin.SaveKey.Value.IsDown())
                {
                    if (Selection.Count == 0)
                        player.Message(MessageHud.MessageType.Center, "Mark some pieces first");
                    else
                        NamePrompt.Ask("Name this blueprint", "", OnSaveName);
                }
                else if (Plugin.ClearKey.Value.IsDown() && Selection.Count > 0)
                {
                    Selection.Clear();
                    player.Message(MessageHud.MessageType.TopLeft, "Blueprint marks cleared");
                }
                return;
            }

            if (!BlueprintPieces.TryGet(selected, out var rt)) return;
            Blueprint bp = rt.Blueprint;

            if (Plugin.SaveKey.Value.IsDown())
            {
                NamePrompt.Ask("Rename blueprint", bp.name, name =>
                {
                    BlueprintStore.Rename(bp, BlueprintStore.UniqueName(name, bp));
                    BlueprintPieces.Refresh();
                    Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Renamed to " + bp.name);
                });
            }
            else if (Plugin.DeleteKey.Value.IsDown())
            {
                if (_pendingDelete == bp && Time.time < _pendingDeleteUntil)
                {
                    _pendingDelete = null;
                    BlueprintStore.Delete(bp);
                    BlueprintPieces.Refresh();
                    player.Message(MessageHud.MessageType.Center, "Deleted blueprint " + bp.name);
                }
                else
                {
                    _pendingDelete = bp;
                    _pendingDeleteUntil = Time.time + DeleteConfirmSeconds;
                    player.Message(MessageHud.MessageType.Center, $"Press {Plugin.DeleteKey.Value} again to delete '{bp.name}'");
                }
            }
        }

        private static void OnSaveName(string name)
        {
            Blueprint bp = Selection.ToBlueprint(BlueprintStore.UniqueName(name));
            if (bp == null) return;
            BlueprintStore.Add(bp);
            Selection.Clear();
            BlueprintPieces.Refresh();
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, $"Saved blueprint '{bp.name}' ({bp.pieces.Count} pieces)");
        }
    }

    // Vanilla text prompt (the one signs use).
    internal class NamePrompt : TextReceiver
    {
        private readonly string _initial;
        private readonly System.Action<string> _done;

        private NamePrompt(string initial, System.Action<string> done)
        {
            _initial = initial;
            _done = done;
        }

        internal static void Ask(string topic, string initial, System.Action<string> done)
        {
            if (TextInput.instance == null) return;
            TextInput.instance.RequestText(new NamePrompt(initial, done), topic, 40);
        }

        public string GetText() => _initial;

        public void SetText(string text)
        {
            text = text?.Trim();
            if (!string.IsNullOrEmpty(text)) _done(text);
        }
    }
}
