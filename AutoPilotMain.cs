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
        internal static ConfigEntry<KeyboardShortcut> leftAutopilotFine;
        internal static ConfigEntry<KeyboardShortcut> rightAutopilotFine;
        internal static ConfigEntry<KeyboardShortcut> tackJibeKey;
		internal static ConfigEntry<float> angleAdjustStep; 
        internal static ConfigEntry<float> angleAdjustStepFine;
        internal static ConfigEntry<float> kp; 
        internal static ConfigEntry<float> ki; 
        internal static ConfigEntry<float> kd;
        internal static ConfigEntry<float> derivativeFilter;
        internal static ConfigEntry<float> errorDeadband;
        internal static ConfigEntry<float> integralWindow;
        internal static ConfigEntry<float> integralSteadyRate;
        internal static ConfigEntry<float> maxCommandRate;
        internal static ConfigEntry<bool> speedGainScaling;
        internal static ConfigEntry<float> referenceSpeed;
        internal static ConfigEntry<bool> telemetryEnabled;

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
            leftAutopilotFine = Config.Bind("Hotkeys", "Left Autopilot Key (fine)", new KeyboardShortcut(KeyCode.K, KeyCode.LeftShift), "Fine course correction to the left. While Shift is held, the normal Left Autopilot Key is ignored.");
            rightAutopilotFine = Config.Bind("Hotkeys", "Right Autopilot Key (fine)", new KeyboardShortcut(KeyCode.L, KeyCode.LeftShift), "Fine course correction to the right. While Shift is held, the normal Right Autopilot Key is ignored.");
            tackJibeKey = Config.Bind("Hotkeys", "Auto tack/jibe to opposite side", new KeyboardShortcut(KeyCode.Semicolon));

			angleAdjustStep = Config.Bind("Controls", "AngleStep", 5f, "Degrees to adjust per press of the Left/Right Autopilot Key");
            angleAdjustStepFine = Config.Bind("Controls", "AngleStepFine", 1f, "Degrees to adjust per press of the fine Left/Right Autopilot Key (Shift+K / Shift+L by default)");
            kp = Config.Bind("Controls", "kp", 0.040f, "PID Costant: Proportional gain");
            ki = Config.Bind("Controls", "ki",  0.005f, "PID Costant: Integral gain");
            kd = Config.Bind("Controls", "kd", 0.015f, "PID Costant: Derivative gain");

            derivativeFilter = Config.Bind("PID", "Derivative Filter Time (s)", 0.5f, new ConfigDescription("Low-pass time constant for the derivative term. Larger values smooth out wave-induced heading noise but make the damping slower. 0 disables filtering.", new AcceptableValueRange<float>(0f, 5f)));
            errorDeadband = Config.Bind("PID", "Error Deadband (deg)", 0.5f, new ConfigDescription("Heading errors smaller than this are ignored, so the wheel stops hunting around the target.", new AcceptableValueRange<float>(0f, 5f)));
            integralWindow = Config.Bind("PID", "Integral Window (deg)", 4f, new ConfigDescription("The integral term only accumulates when the heading error is within this many degrees (prevents overshoot after large course changes). Outside the window the integral slowly bleeds off.", new AcceptableValueRange<float>(1f, 45f)));
            integralSteadyRate = Config.Bind("PID", "Integral Steady Rate (deg/s)", 1.5f, new ConfigDescription("Outside the integral window the integral still accumulates once the heading is steady (turning slower than this) and the setpoint has not changed for a few seconds. This removes a constant offset, e.g. from weather helm.", new AcceptableValueRange<float>(0.1f, 10f)));
            maxCommandRate = Config.Bind("PID", "Max Command Rate (1/s)", 1.5f, new ConfigDescription("Maximum change of the normalised steering command per second (1.0 = full wheel travel). Limits twitching.", new AcceptableValueRange<float>(0.1f, 10f)));
            speedGainScaling = Config.Bind("PID", "Scale Gains With Speed", false, "Experimental. Scale all gains by referenceSpeed / boat speed (clamped to 0.5x-2x) to compensate for rudder authority growing with speed.");
            referenceSpeed = Config.Bind("PID", "Reference Speed (m/s)", 2f, new ConfigDescription("Boat speed at which the configured gains apply unscaled (only used when speed scaling is enabled).", new AcceptableValueRange<float>(0.5f, 10f)));
            telemetryEnabled = Config.Bind("Telemetry", "Enable CSV Telemetry", false, "Writes a CSV with heading, error, PID terms, command, rudder angle, speed and wind (~20 Hz) to BepInEx/AutoPilotTelemetry while the autopilot is active. Used for tuning the gains.");

            rudderHUDConfig = Config.Bind("HUD", "rudderHUD", false, "Enables or disables the rudder HUD. Requires restarting the game. For Debug Purpose");

            

            //PATCHES INFO
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            


        }


        
    }
}