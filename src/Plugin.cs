using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Blueprints
{
    // Client-only. Save groups of built pieces as blueprints and place them again with the hammer.
    //
    // Hammer menu (BuildUi, Valheim 1.0.21): we add a "Blueprints" tab next to By usage / By material / Recent /
    // Favorites. It holds:
    //   - "Blueprint selection": a copy of the vanilla Repair piece (m_repairPiece = true), so it behaves like repair
    //     mode (hover highlight, left click). Player.Repair is intercepted for it: a click marks/unmarks the hovered
    //     piece, Shift+click marks everything connected to it. A key saves the marked pieces under a name.
    //   - one entry per saved blueprint: a runtime Piece whose children are copies of the saved pieces, so vanilla's
    //     placement ghost shows the whole build. Its m_resources are every piece's costs added together, so vanilla
    //     checks and consumes the full materials. Player.PlacePiece is intercepted to place each real piece instead.
    // Blueprints are stored as JSON in BepInEx/config/Blueprints. Nothing new is sent to the server: placed pieces are
    // ordinary vanilla pieces.
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jordan.valheim.blueprints";
        public const string Name = "Blueprints";
        public const string Version = "0.1.1";

        internal static Plugin Instance;
        internal static ConfigEntry<KeyboardShortcut> SaveKey;
        internal static ConfigEntry<KeyboardShortcut> ClearKey;
        internal static ConfigEntry<KeyboardShortcut> DeleteKey;
        internal static ConfigEntry<KeyCode> ConnectedModifier;
        internal static ConfigEntry<int> MaxConnected;
        internal static ConfigEntry<Color> HighlightColor;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            SaveKey = Config.Bind("Keys", "SaveOrRename", new KeyboardShortcut(KeyCode.B),
                "With 'Blueprint selection' chosen: save the marked pieces as a blueprint. With a blueprint chosen: rename it.");
            ClearKey = Config.Bind("Keys", "ClearSelection", new KeyboardShortcut(KeyCode.Backspace),
                "With 'Blueprint selection' chosen: unmark every piece.");
            DeleteKey = Config.Bind("Keys", "DeleteBlueprint", new KeyboardShortcut(KeyCode.Delete),
                "With a blueprint chosen: press twice to delete it.");
            ConnectedModifier = Config.Bind("Keys", "SelectConnectedModifier", KeyCode.LeftShift,
                "Hold this while clicking with 'Blueprint selection' to mark every piece connected to the one you click.");
            MaxConnected = Config.Bind("Selection", "MaxConnected", 500,
                new ConfigDescription("Most pieces a connected selection will add in one click.", new AcceptableValueRange<int>(10, 5000)));
            HighlightColor = Config.Bind("Selection", "HighlightColor", new Color(0.3f, 0.75f, 1f, 1f),
                "Tint for pieces marked for a blueprint.");

            BlueprintStore.Load();
            gameObject.AddComponent<BlueprintInput>();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Selection.Clear();
        }

        internal static void Log(string msg) => Instance?.Logger.LogInfo(msg);
        internal static void Warn(string msg) => Instance?.Logger.LogWarning(msg);
    }
}
