using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using SailwindModdingHelper;


namespace AutoPilot
{
    [BepInPlugin(AutoPilotInfo.PLUGIN_GUID, AutoPilotInfo.PLUGIN_NAME, AutoPilotInfo.PLUGIN_VERSION)]
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
            Logger.LogInfo($"PLUGIN_GUID: {AutoPilotInfo.PLUGIN_GUID} is loaded!");
            Logger.LogInfo($"PLUGIN_NAME: {AutoPilotInfo.PLUGIN_NAME} is loaded!");
            Logger.LogInfo($"PLUGIN_VERSION: {AutoPilotInfo.PLUGIN_VERSION} is loaded!");

            activateAutopilot = Config.Bind("Hotkeys", "Activate Autopilot Key", new KeyboardShortcut(KeyCode.O));
            leftAutopilot = Config.Bind("Hotkeys", "Left Autopilot Key", new KeyboardShortcut(KeyCode.K));
            rightAutopilot = Config.Bind("Hotkeys", "Right Autopilot Key", new KeyboardShortcut(KeyCode.L));

            #region Configuration Setup
            rudderHUDConfig = Config.Bind("A) Main Switches", "rudderHUD", false, "Enables or disables the rudder HUD. Requires restarting the game. For Debug Purpose");
            #endregion

            #region Patching Information
            //PATCHES INFO
            Harmony harmony = new Harmony(AutoPilotInfo.PLUGIN_GUID);
            //tiller compatibility patch
            MethodInfo original2b = AccessTools.Method(typeof(Rudder), "Start");
            MethodInfo patch2b = AccessTools.Method(typeof(AutoPilotPatches), "RudderPatch");
            harmony.Patch(original2b, new HarmonyMethod(patch2b));
            
            
            #endregion

        }


        
    }
}