using UnityEngine;
using SailwindModdingHelper;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;
using System.Globalization;
using System.Text;


namespace AutoPilot
{
    public class AutoPilotSteerage : MonoBehaviour
    {
        private Transform boat;
        private HingeJoint rudderJoint;
        private Rudder rudder;
        private GPButtonSteeringWheel steeringWheel;
        private Rigidbody shipRigidbody;
        private bool autopilotActiveCourse = false;
        private bool autopilotActiveWind = false;
        private float autopilotCourse;
        private bool activated = false;
        private float currentInputMax;
        public bool allowUserInput = true; // Allow the user to control the autopilot. Intended to be toggled by another mod. 
        public bool canControl = false;
        // PID state
        private float integral;
        private float lastError;
        private float lastTime;
        private float lastMeasurement;   // last measured angle, for derivative-on-measurement
        private float filteredD;         // low-pass filtered rate of change of the measured angle (deg/s)
        private float lastCommand;       // last normalised command, for the rate limiter
        private bool pidPrimed;          // false until a first measurement has been taken after activation
        private float lastTarget = float.NaN;      // setpoint of the previous cycle, to notice setpoint changes
        private float lastTargetChangeTime;  // Time.time of the last setpoint change
        private bool lastSourceWind;     // true if the previous cycle regulated the apparent wind angle (not the compass heading)

        // Telemetry
        private StreamWriter telemetry;
        private float telemetryStart;
        private float telemetryLastFlush;

        // Auto tack/jibe state
        private bool autoTacking = false;

        // Initialisation / fault handling (boats without a steering wheel, e.g. tiller dinghies, must not break the update loop)
        private bool initialised = false;
        private int initAttempts = 0;
        private float nextInitAttempt = 0f;
        private float faultUntil = 0f;
        private bool faultLogged = false;

        private bool TryInit()
        /*
        Finds the steering wheel, boat, rudder joint and rigidbody that belong to this rudder.
        Returns false (and leaves the component inert) for rudders that are not steered by a wheel, e.g. the tillers of
        modded dinghies, or when something it needs is missing.
        */
        {
            if (initialised) return true;

            rudder = GetComponent<Rudder>();
            if (!rudder) return false;

            GPButtonSteeringWheel match = null;
            foreach (GPButtonSteeringWheel button in FindObjectsOfType<GPButtonSteeringWheel>())
            {
                if (!button) continue;
                Rudder wheelRudder = Traverse.Create(button).Field("rudder").GetValue() as Rudder;
                if (wheelRudder && wheelRudder == rudder)
                {
                    match = button;
                    break;
                }
            }
            if (!match) return false; // no wheel for this rudder: nothing to automate

            PurchasableBoat purchasableBoat = GetComponentInParent<PurchasableBoat>();
            if (!purchasableBoat)
            {
                AutoPilotMain.Logger.LogWarning($"AutoPilot: rudder '{name}' has a steering wheel but no PurchasableBoat parent, skipping.");
                return false;
            }

            HingeJoint joint = match.attachedRudder ? match.attachedRudder : rudder.GetComponent<HingeJoint>();
            if (!joint)
            {
                AutoPilotMain.Logger.LogWarning($"AutoPilot: rudder '{name}' on boat '{purchasableBoat.name}' has no hinge joint, skipping.");
                return false;
            }

            Rigidbody body = purchasableBoat.GetComponent<Rigidbody>();
            if (!body) body = purchasableBoat.GetComponentInParent<Rigidbody>();
            if (!body)
            {
                AutoPilotMain.Logger.LogWarning($"AutoPilot: boat '{purchasableBoat.name}' has no rigidbody, skipping.");
                return false;
            }

            steeringWheel = match;
            boat = purchasableBoat.transform;
            rudderJoint = joint;
            shipRigidbody = body;
            initialised = true;
            return true;
        }

        public void Awake()
        {
            TryInit();

            GameEvents.OnPlayerInput += (_, __) =>
            {
                // the handler is never unsubscribed, so ignore it for destroyed instances and for rudders we do not control
                if (this == null || !initialised) return;
                if (allowUserInput)
                {
                    if (AutoPilotMain.activateAutopilotCourse.Value.IsDown())
                    {
                        autopilotActiveCourse = !autopilotActiveCourse;
                        if (autopilotActiveWind)
                        {
                            autopilotActiveWind = false;
                            activated = false;
                        }
                    }
                    if (AutoPilotMain.activateAutopilotWind.Value.IsDown())
                    {
                        autopilotActiveWind = !autopilotActiveWind;
                        if (autopilotActiveCourse)
                        {
                            autopilotActiveCourse = false;
                            activated = false;
                        }
                    }
                    if (!autoTacking)
                    {
                        // Fine keys (Shift+K / Shift+L by default) are checked first; the normal keys are ignored while Shift is held
                        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        if (AutoPilotMain.leftAutopilotFine.Value.IsDown())
                        {
                            AdjustCourse(-AutoPilotMain.angleAdjustStepFine.Value);
                        }
                        else if (AutoPilotMain.rightAutopilotFine.Value.IsDown())
                        {
                            AdjustCourse(AutoPilotMain.angleAdjustStepFine.Value);
                        }
                        else if (!shiftHeld && AutoPilotMain.leftAutopilot.Value.IsDown())
                        {
                            AdjustCourse(-AutoPilotMain.angleAdjustStep.Value);
                        }
                        else if (!shiftHeld && AutoPilotMain.rightAutopilot.Value.IsDown())
                        {
                            AdjustCourse(AutoPilotMain.angleAdjustStep.Value);
                        }
                    }
                    if ((autopilotActiveCourse || autopilotActiveWind) && canControl && AutoPilotMain.tackJibeKey.Value.IsDown())
                    {
                        if (!autoTacking)
                        {
                            InitiateAutoTackJibe();
                        }
                    }
                }
            };
        }
        public void Update()
        {
            if (!initialised)
            {
                // wheels of modded boats may appear a little later; retry a few times, then give up
                if (initAttempts >= 5 || Time.time < nextInitAttempt) return;
                initAttempts++;
                nextInitAttempt = Time.time + 2f;
                if (!TryInit()) return;
            }
            if (Time.time < faultUntil) return;

            try
            {
                UpdateAutopilot();
            }
            catch (Exception e)
            {
                HandleFault(e);
            }
        }

        private void HandleFault(Exception e)
        {
            // Switch the autopilot off and back off for a while instead of throwing every frame
            autopilotActiveCourse = false;
            autopilotActiveWind = false;
            autoTacking = false;
            activated = false;
            canControl = false;
            CloseTelemetry();
            faultUntil = Time.time + 5f;
            if (!faultLogged)
            {
                faultLogged = true;
                AutoPilotMain.Logger.LogError(
                    $"AutoPilot switched off on '{name}' (boat '{(boat ? boat.name : "null")}'): {e.GetType().Name}: {e.Message}\n" +
                    $"  rudder={(rudder ? "ok" : "null")} wheel={(steeringWheel ? "ok" : "null")} joint={(rudderJoint ? "ok" : "null")} " +
                    $"rigidbody={(shipRigidbody ? "ok" : "null")} currentBoat={(GameState.currentBoat != null ? GameState.currentBoat.name : "null")} " +
                    $"lastBoat={(GameState.lastBoat != null ? GameState.lastBoat.name : "null")}\n{e.StackTrace}");
            }
        }

        private void UpdateAutopilot()
        {
            if (!rudder || !steeringWheel || !boat || !rudderJoint) return;
            // UI on steering wheel if activated
            if (AutoPilotMain.rudderHUDConfig.Value)
            {
                if (steeringWheel.IsLookedAt() || steeringWheel.IsStickyClicked() || steeringWheel.IsCliked())
                {
                    steeringWheel.description = "";
                    // steeringWheel.description += "\n Test: " + test;
                    steeringWheel.description += "\n BoatHeading: " + Display360(BoatHeading());
                    steeringWheel.description += "\n Autopilot: " + Display360(autopilotCourse);
                    steeringWheel.description += "\n Mode: " + (autopilotActiveCourse ? "Compass" : (autopilotActiveWind ? "Wind" : "Off"));
                    // for debug:
                    // steeringWheel.description += "\n Rudder Angle: " + Mathf.RoundToInt(rudder.currentAngle);
                    // steeringWheel.description += "\n GetWindAngle: " + Mathf.RoundToInt(GetApparentWindAngle());
                    // steeringWheel.description += "\n Command: " + steeringWheel.currentInput / currentInputMax;
                    // steeringWheel.description += "\n autoTacking: " + autoTacking;
                }
            }

            if (GameState.currentBoat != null)
            {
                canControl = GameState.currentBoat.IsChildOf(boat.transform);
            }
            else if (GameState.lastBoat != null)
            {
                canControl = GameState.lastBoat.IsChildOf(boat.transform);
            }
            else
            {
                canControl = false;
            }
            if ((autopilotActiveCourse || autopilotActiveWind) && canControl)
            {
                if (!activated)
                {
                    // set some values on autopilot acitvation                    
                    currentInputMax = rudderJoint.limits.max * steeringWheel.gearRatio;
                    if (autopilotActiveCourse)
                    {
                        autopilotCourse = BoatHeading();
                    }
                    else if (autopilotActiveWind)
                    {
                        autopilotCourse = GetApparentWindAngle();
                    }
                    
                    ResetPid();
                    activated = true;
                    OpenTelemetry();
                }
                // steering wheel needs to be locked that steering works
                object lockedValue = Traverse.Create(steeringWheel).Field("locked").GetValue();
                if (!(lockedValue is bool isLocked) || !isLocked)
                {
                    Traverse.Create(steeringWheel).Field("locked").SetValue(true);
                }
                if (autopilotActiveWind || autoTacking)
                {
                    ApplySteering(GetApparentWindAngle(), autopilotCourse);
                }
                else if (autopilotActiveCourse)
                {
                    ApplySteering(BoatHeading(), autopilotCourse);
                } 
            }
            else
            {
                if (activated)
                {
                    activated = false;
                    ResetPid();
                }
                CloseTelemetry();
            }
        }
        private float AngleCorrection(float angle)
        /*
        Function to ensure a newly calculated angle stays between [-180,180)
        */
        {
            angle = Mathf.Clamp(angle, -180f, 180f);
            return angle;
        }
        private void AdjustCourse(float delta)
        /*
        Moves the setpoint by delta degrees and keeps it inside (-180, 180]
        */
        {
            autopilotCourse = Mathf.DeltaAngle(0f, autopilotCourse + delta);
        }

        private static int Display360(float angle)
        /*
        Formats an angle in (-180, 180] as a 0-359 reading for the HUD (compass heading and wind mode setpoint)
        */
        {
            return Mathf.RoundToInt(Mathf.Repeat(angle, 360f)) % 360;
        }

        private void ResetPid()
        {
            integral = 0f;
            filteredD = 0f;
            lastCommand = 0f;
            pidPrimed = false;
            lastTarget = float.NaN;
            lastTargetChangeTime = Time.time;
        }

        private void ApplySteering(float currentAngle, float targetAngle)
        {
            // The regulated quantity switches between compass heading and apparent wind angle when a tack starts or
            // finishes. Re-prime the PID then, otherwise the derivative sees a huge fake rate of change (it produced a
            // ~0.6 command kick at the end of every tack) and the old integral no longer applies.
            bool windSource = autopilotActiveWind || autoTacking;
            if (windSource != lastSourceWind)
            {
                lastSourceWind = windSource;
                pidPrimed = false;
                integral = 0f;
            }

            // Handle auto-tacking transition
            if (autoTacking)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, autopilotCourse)) <= 1f)
                {
                    // Tack complete
                    autopilotCourse = autopilotActiveCourse ? BoatHeading() : autopilotCourse;
                    autoTacking = false;
                    integral = 0f; // Reset integral on tack completion
                }
            }

            float now = Time.time;
            float dt = now - lastTime;
            if (dt <= 0.05f) return; // Update at ~20Hz

            // First sample after (re)activation, or after a long gap: just take a measurement
            if (!pidPrimed || dt > 0.5f)
            {
                lastMeasurement = currentAngle;
                filteredD = 0f;
                lastTime = now;
                pidPrimed = true;
                return;
            }

            // remember when the setpoint last changed (for the integral gate below)
            if (float.IsNaN(lastTarget) || Mathf.Abs(Mathf.DeltaAngle(lastTarget, targetAngle)) > 0.01f)
            {
                lastTarget = targetAngle;
                lastTargetChangeTime = now;
            }

            // Same sign convention as before: positive error = measured angle is above the target
            float error = Mathf.DeltaAngle(targetAngle, currentAngle);

            // Deadband (shrinks the error instead of cutting it, so there is no jump at the edge)
            float deadband = AutoPilotMain.errorDeadband.Value;
            float effError = Mathf.Abs(error) <= deadband ? 0f : error - Mathf.Sign(error) * deadband;

            // Derivative on measurement (no kick when the target changes), low-pass filtered
            float rawD = Mathf.DeltaAngle(lastMeasurement, currentAngle) / dt;
            float tau = AutoPilotMain.derivativeFilter.Value;
            float alpha = tau <= 0f ? 1f : dt / (tau + dt);
            filteredD += alpha * (rawD - filteredD);

            float gain = GainScale();
            float kp = AutoPilotMain.kp.Value * gain;
            float ki = AutoPilotMain.ki.Value * gain;
            float kd = AutoPilotMain.kd.Value * gain;

            // Conditional integration: do not wind up while the output is saturated in the direction of the error
            float unsaturated = kp * effError + ki * integral + kd * filteredD;
            bool saturated = Mathf.Abs(unsaturated) >= 1f && Mathf.Sign(unsaturated) == Mathf.Sign(effError);
            bool withinWindow = Mathf.Abs(error) <= AutoPilotMain.integralWindow.Value;
            // A large error that is not shrinking and not caused by a recent setpoint change is a steady offset
            // (weather helm, current): let the integral remove it instead of leaving the boat off course.
            bool steadyOffset = Mathf.Abs(filteredD) < AutoPilotMain.integralSteadyRate.Value && now - lastTargetChangeTime > 4f;
            if (!withinWindow && !steadyOffset)
            {
                // course change / tack in progress: do not wind up, slowly bleed the integral instead
                integral *= Mathf.Exp(-dt / 5f);
            }
            else if (!saturated)
            {
                // limit the integral term to +-0.5 of the full wheel range (was a fixed +-50 deg*s, too small for a boat with strong weather helm)
                float integralLimit = 0.5f / Mathf.Max(AutoPilotMain.ki.Value, 0.0001f);
                integral = Mathf.Clamp(integral + effError * dt, -integralLimit, integralLimit);
            }

            float pTerm = kp * effError;
            float iTerm = ki * integral;
            float dTerm = kd * filteredD;
            float command = Mathf.Clamp(pTerm + iTerm + dTerm, -1f, 1f);

            // Rate limit to avoid twitching the wheel
            float maxStep = AutoPilotMain.maxCommandRate.Value * dt;
            command = lastCommand + Mathf.Clamp(command - lastCommand, -maxStep, maxStep);

            steeringWheel.currentInput = currentInputMax * command;

            WriteTelemetry(now, currentAngle, targetAngle, error, pTerm, iTerm, dTerm, command, gain);

            lastCommand = command;
            lastMeasurement = currentAngle;
            lastTime = now;
        }

        private float GainScale()
        {
            if (!AutoPilotMain.speedGainScaling.Value || shipRigidbody == null) return 1f;
            float speed = Mathf.Max(shipRigidbody.velocity.magnitude, 0.5f);
            return Mathf.Clamp(AutoPilotMain.referenceSpeed.Value / speed, 0.5f, 2f);
        }

        #region Telemetry
        private static string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);

        private void OpenTelemetry()
        {
            if (!AutoPilotMain.telemetryEnabled.Value || telemetry != null) return;
            try
            {
                string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "AutoPilotTelemetry");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "autopilot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
                telemetry = new StreamWriter(file, false, new UTF8Encoding(false));
                telemetry.WriteLine("t,mode,tacking,target,measured,error,p,i,d,integral,command,wheel_input,rudder_angle,speed,app_wind_angle,gain_scale,kp,ki,kd,deriv_filter,deadband,max_rate");
                telemetryStart = Time.time;
                telemetryLastFlush = Time.time;
                AutoPilotMain.Logger.LogInfo("Telemetry writing to " + file);
            }
            catch (Exception e)
            {
                AutoPilotMain.Logger.LogError("Could not open telemetry file: " + e.Message);
                telemetry = null;
            }
        }

        private void WriteTelemetry(float now, float measured, float target, float error, float p, float i, float d, float command, float gain)
        {
            if (telemetry == null) return;
            try
            {
                string mode = autopilotActiveCourse ? "compass" : (autopilotActiveWind ? "wind" : "off");
                float speed = shipRigidbody != null ? shipRigidbody.velocity.magnitude : 0f;
                telemetry.WriteLine(string.Join(",", new[]
                {
                    F(now - telemetryStart), mode, autoTacking ? "1" : "0",
                    F(target), F(measured), F(error), F(p), F(i), F(d), F(integral), F(command),
                    F(steeringWheel.currentInput), F(rudder.currentAngle), F(speed), F(GetApparentWindAngle()), F(gain),
                    F(AutoPilotMain.kp.Value), F(AutoPilotMain.ki.Value), F(AutoPilotMain.kd.Value),
                    F(AutoPilotMain.derivativeFilter.Value), F(AutoPilotMain.errorDeadband.Value), F(AutoPilotMain.maxCommandRate.Value)
                }));
                if (now - telemetryLastFlush > 2f)
                {
                    telemetry.Flush();
                    telemetryLastFlush = now;
                }
            }
            catch (Exception e)
            {
                AutoPilotMain.Logger.LogError("Telemetry write failed: " + e.Message);
                CloseTelemetry();
            }
        }

        private void CloseTelemetry()
        {
            if (telemetry == null) return;
            try { telemetry.Flush(); telemetry.Dispose(); } catch { }
            telemetry = null;
        }

        private void OnDestroy()
        {
            CloseTelemetry();
        }
        #endregion

        private void InitiateAutoTackJibe()
        {
            autopilotCourse = autopilotActiveWind? autopilotCourse:-GetApparentWindAngle(); 
            autoTacking = true;
        }
        private float BoatHeading()
        /*
        calculates boat heading in an absolute frame of reference
        */
        {
            // Use the horizontal projection of the bow direction. A 3D SignedAngle includes the boat's pitch, so near
            // north its magnitude can never drop below the pitch angle and the sign flips, which makes the heading
            // jump by twice the pitch angle when crossing 0 deg (this caused a sustained limit cycle on northerly courses).
            Vector3 fwd = boat.forward;
            float boatHeading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            // boatHeading = boatHeading < 0 ? boatHeading + 360f : boatHeading; // corrects from [-180, 180] to [0, 360]
            return AngleCorrection(boatHeading);
        }

        public void ToggleAutoPilotWind()
        /*
        Start AutoPilot Wind Angle from outside the mod  
        */
        {
            autopilotActiveWind = !autopilotActiveWind;
            autopilotActiveCourse = false ? autopilotActiveCourse : autopilotActiveCourse;
        }
        public void ToggleAutoPilotCourse()
        /*
        Start AutoPilot Wind Angle from outside the mod  
        */
        {
            autopilotActiveCourse = !autopilotActiveCourse;
            autopilotActiveWind = false ? autopilotActiveWind : autopilotActiveWind;
        }
        public void SetCourse(float course)
        /*
        set AutoPilot course from outside the mod  
        */
        {
            autopilotCourse = course;
        }

        private float GetApparentWindAngle()
        {
            try
            {
                Vector3 apparentWind = Wind.currentWind - shipRigidbody.velocity;
                // flatten to the horizontal plane so pitching / heaving does not corrupt the angle
                Vector3 flatVelocity = new Vector3(shipRigidbody.velocity.x, 0f, shipRigidbody.velocity.z);
                apparentWind.y = 0f;
                float apparentWindAngle = Vector3.SignedAngle(-flatVelocity, apparentWind, -Vector3.up);
                return AngleCorrection(apparentWindAngle);
            }
            catch (Exception e)
            {
                AutoPilotMain.Logger.LogError($"GetApparentWindAngle error: {e}");
                return 0;
            }
            
            
        }

    }
}


            