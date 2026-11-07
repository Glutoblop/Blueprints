using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Blueprints
{
    // A "Blueprints" tab in the hammer menu (BuildUi). BuildUi shows one IPieceList per tab; we add our own list and
    // a copy of a vanilla tab button wired to it. Our tool is a repair-style piece, so BuildUi shows it in the
    // special-button slot like vanilla Repair.
    internal class BlueprintPieceList : IPieceList
    {
        public string DisplayName => "Blueprints";
        public bool ShowTags => true; // vanilla only shows the special (repair-style) button for lists with tags
        public bool CanCustomizeTags => false;
        public int TagCount => 0;
        public int TagSeparatorIndex => -1;
        public string GetTagDisplayName(int index) => "";
        public int GetTagIdByIndex(int index) => -1;
        public void UpdateAvailableTags(PieceTable pieceTable) { }

        public void GetAvailablePiecesWithTag(int tagId, PieceTable pieceTable, IList<Piece> resultOut)
        {
            if (BlueprintPieces.Tool != null && pieceTable.m_availablePieces.Contains(BlueprintPieces.Tool))
                resultOut.Add(BlueprintPieces.Tool);
            foreach (var rt in BlueprintPieces.Runtimes)
                if (pieceTable.m_availablePieces.Contains(rt.Piece)) resultOut.Add(rt.Piece);
        }
    }

    internal static class BuildMenuTab
    {
        private static BuildUi _ui;

        internal static void Attach(BuildUi ui)
        {
            _ui = ui;
            if (ui.m_pieceLists.Any(l => l is BlueprintPieceList)) return;

            ui.m_pieceLists.Add(new BlueprintPieceList());
            int index = ui.m_pieceLists.Count - 1;

            if (ui.m_tabContainer.childCount == 0) { Plugin.Warn("Build menu has no tabs to copy"); return; }
            Transform template = ui.m_tabContainer.GetChild(ui.m_tabContainer.childCount - 1);
            GameObject go = Object.Instantiate(template.gameObject, ui.m_tabContainer, false);
            go.name = "Blueprints_Tab";
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (c.GetType().Name == "Localize") Object.DestroyImmediate(c);
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true))
                t.text = "Blueprints";

            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => ui.SelectPieceList(index));
            ui.m_tabButtons.Add(button);

            // TabHandler needs an entry at the same index, or SelectPieceList indexes past its list.
            TabHandler tabs = ui.m_tabHandler;
            if (tabs != null)
            {
                RectTransform sharedPage = tabs.m_tabs.Count > 0 && tabs.m_tabs.All(t => t.m_page == tabs.m_tabs[0].m_page)
                    ? tabs.m_tabs[0].m_page : null;
                while (tabs.m_tabs.Count < index)
                    tabs.m_tabs.Add(new TabHandler.Tab { m_page = sharedPage, m_onClick = new UnityEvent() });
                tabs.m_tabs.Add(new TabHandler.Tab { m_button = button, m_page = sharedPage, m_onClick = new UnityEvent() });
            }
        }

        // Make BuildUi rebuild its buttons on its next Update (it does when the available count changes).
        internal static void ForceRefresh()
        {
            if (_ui != null) _ui.m_availablePieceCount = -1;
        }
    }
}
