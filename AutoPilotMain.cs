using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using SailwindModdingHelper;


namespace AutoPilot
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(SailwindModdingHelperMain.GUID, "2.1.1")]
    public class AutoPilotMain : BaseUnityPlugin
    {
        #region Variable Definition
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<KeyboardShortcut> activateAutopilot;
        internal static ConfigEntry<KeyboardShortcut> leftAutopilot;
        internal static ConfigEntry<KeyboardShortcut> rightAutopilot;

        //config file info
        //MAIN SWITCHES
        public static ConfigEntry<bool> rudderHUDConfig;        //Disables the rudder HUD entirely.
        #endregion

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"PLUGIN_GUID: {MyPluginInfo.PLUGIN_GUID} is loaded!");
            Logger.LogInfo($"PLUGIN_NAME: {MyPluginInfo.PLUGIN_NAME} is loaded!");
            Logger.LogInfo($"PLUGIN_VERSION: {MyPluginInfo.PLUGIN_VERSION} is loaded!");
            activateAutopilot = Config.Bind("Hotkeys", "Activate Autopilot Key", new KeyboardShortcut(KeyCode.O));
            leftAutopilot = Config.Bind("Hotkeys", "Left Autopilot Key", new KeyboardShortcut(KeyCode.K));
            rightAutopilot = Config.Bind("Hotkeys", "Right Autopilot Key", new KeyboardShortcut(KeyCode.L));

            rudderHUDConfig = Config.Bind("HUD", "rudderHUD", false, "Enables or disables the rudder HUD. Requires restarting the game. For Debug Purpose");

            //PATCHES INFO
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            


        }


        
    }
}