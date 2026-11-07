using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Blueprints
{
    // Register our pieces in the hammer table, and make them available whether or not the player "knows" them.
    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
    internal static class PieceTable_UpdateAvailable
    {
        private static void Prefix(PieceTable __instance)
        {
            if (__instance == BlueprintPieces.HammerTable()) BlueprintPieces.EnsureRegistered(__instance);
        }

        private static void Postfix(PieceTable __instance)
        {
            if (__instance != BlueprintPieces.HammerTable() || BlueprintPieces.Tool == null) return;
            var cat = BlueprintPieces.Category;
            if ((int)cat >= __instance.m_availablePiecesByCategory.Count) return;

            List<Piece> list = __instance.m_availablePiecesByCategory[(int)cat];
            list.RemoveAll(BlueprintPieces.IsOurs);
            __instance.m_availablePieces.RemoveWhere(BlueprintPieces.IsOurs);

            void Add(Piece p)
            {
                list.Add(p);
                __instance.m_availablePieces.Add(p);
                __instance.m_enabledPieces.Add(p);
            }
            Add(BlueprintPieces.Tool);
            foreach (var rt in BlueprintPieces.Runtimes) Add(rt.Piece);
        }
    }

    // Keep our pieces out of the vanilla tabs (our tool would also take the Repair button's special slot).
    [HarmonyPatch]
    internal static class VanillaLists_Filter
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ByUsagePieceList), nameof(IPieceList.GetAvailablePiecesWithTag));
            yield return AccessTools.Method(typeof(ByMaterialPieceList), nameof(IPieceList.GetAvailablePiecesWithTag));
        }

        private static void Postfix(IList<Piece> resultOut)
        {
            for (int i = resultOut.Count - 1; i >= 0; i--)
                if (BlueprintPieces.IsOurs(resultOut[i])) resultOut.RemoveAt(i);
        }
    }

    [HarmonyPatch(typeof(BuildUi), "Awake")]
    internal static class BuildUi_Awake
    {
        private static void Postfix(BuildUi __instance) => BuildMenuTab.Attach(__instance);
    }

    // Our selection tool is a repair piece: a click with it marks pieces instead of repairing.
    [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
    internal static class Player_Repair
    {
        private static bool Prefix(Player __instance, Piece repairPiece)
        {
            if (!BlueprintPieces.IsTool(repairPiece)) return true;
            Selection.OnClick(__instance);
            return false;
        }
    }

    // Placing a blueprint: place each saved piece through vanilla PlacePiece, relative to the ghost.
    // Vanilla has already checked and will consume the summed m_resources.
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class Player_PlacePiece
    {
        private static bool Prefix(Player __instance, Piece piece, Vector3 pos, Quaternion rot, bool doAttack, bool cheated)
        {
            if (!BlueprintPieces.TryGet(piece, out var rt)) return true;

            int placed = 0;
            foreach (BlueprintEntry e in rt.Blueprint.pieces)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(e.prefab);
                Piece src = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (src == null) continue;
                __instance.PlacePiece(src, pos + rot * e.pos, rot * e.rot, doAttack && placed == 0, cheated);
                placed++;
            }
            Plugin.Log($"Placed blueprint '{rt.Blueprint.name}': {placed} pieces");
            return false;
        }
    }

    // A blueprint needs every crafting station its pieces need (vanilla Piece only has one).
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    internal static class Player_HaveRequirements
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (!__result || !BlueprintPieces.TryGet(piece, out var rt)) return;
            foreach (CraftingStation station in rt.Stations)
            {
                bool ok = mode == Player.RequirementMode.IsKnown || mode == Player.RequirementMode.CanAlmostBuild
                    ? __instance.m_knownStations.ContainsKey(station.m_name)
                    : CraftingStation.HaveBuildStationInRange(station.m_name, __instance.transform.position)
                      || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
                if (!ok) { __result = false; return; }
            }
        }
    }

    // Don't put blueprints in the Recent list: it's saved in the character file by name.
    [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.AddRecentPiece))]
    internal static class BuildUi_AddRecentPiece
    {
        private static bool Prefix(Piece piece) => !BlueprintPieces.IsOurs(piece);
    }
}
