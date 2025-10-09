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
        internal static ConfigEntry<KeyboardShortcut> activateAutopilotCourse;
        internal static ConfigEntry<KeyboardShortcut> activateAutopilotWind;
        internal static ConfigEntry<KeyboardShortcut> leftAutopilot;
        internal static ConfigEntry<KeyboardShortcut> rightAutopilot;
        internal static ConfigEntry<KeyboardShortcut> tackJibeKey;
		internal static ConfigEntry<float> angleAdjustStep; 
        internal static ConfigEntry<float> kp; 
        internal static ConfigEntry<float> ki; 
        internal static ConfigEntry<float> kd; 

        //config file info
        //MAIN SWITCHES
        public static ConfigEntry<bool> rudderHUDConfig;        //Disables the rudder HUD entirely.
        #endregion

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            activateAutopilotCourse = Config.Bind("Hotkeys", "Activate Autopilot Key (compass)", new KeyboardShortcut(KeyCode.O));
            activateAutopilotWind = Config.Bind("Hotkeys", "Activate Autopilot Key (wind)", new KeyboardShortcut(KeyCode.P));
            leftAutopilot = Config.Bind("Hotkeys", "Left Autopilot Key", new KeyboardShortcut(KeyCode.K));
            rightAutopilot = Config.Bind("Hotkeys", "Right Autopilot Key", new KeyboardShortcut(KeyCode.L));
            tackJibeKey = Config.Bind("Hotkeys", "Auto tack/jibe to opposite side", new KeyboardShortcut(KeyCode.Semicolon));

			angleAdjustStep = Config.Bind("Controls", "AngleStep", 5f, "Degrees to adjust per key press");
            kp = Config.Bind("Controls", "kp", 0.030f, "PID Costant: Proportional gain");
            ki = Config.Bind("Controls", "ki",  0.005f, "PID Costant: Integral gain");
            kd = Config.Bind("Controls", "kd", 0.015f, "PID Costant: Derivative gain");

            rudderHUDConfig = Config.Bind("HUD", "rudderHUD", false, "Enables or disables the rudder HUD. Requires restarting the game. For Debug Purpose");

            

            //PATCHES INFO
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            


        }


        
    }
}