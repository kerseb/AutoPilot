using UnityEngine;
using SailwindModdingHelper;
using HarmonyLib;
using System;
using System.Reflection;


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

        // Auto tack/jibe state
        private bool autoTacking = false;

        public void Awake()
        {
            rudder = GetComponent<Rudder>();
            if (!rudder) return;

            GPButtonSteeringWheel[] allGPButtonSteeringWheel = FindObjectsOfType<GPButtonSteeringWheel>();
            foreach (GPButtonSteeringWheel button in allGPButtonSteeringWheel)
            {
                if (rudder == ((Rudder)Traverse.Create(button).Field("rudder").GetValue()))
                {
                    steeringWheel = button;
                    boat = GetComponentInParent<PurchasableBoat>().transform;
                    rudderJoint = button.attachedRudder;
                    shipRigidbody = boat.GetComponent<Rigidbody>();
                }
            }

            // return;

            GameEvents.OnPlayerInput += (_, __) =>
            {
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
                    if (!autoTacking && AutoPilotMain.leftAutopilot.Value.IsDown())
                    {
                        autopilotCourse -= AutoPilotMain.angleAdjustStep.Value;
                        autopilotCourse = autopilotCourse < -180f ? 180f - Mathf.Repeat(-autopilotCourse, 180f) : autopilotCourse;

                    }
                    if (!autoTacking && AutoPilotMain.rightAutopilot.Value.IsDown())
                    {
                        autopilotCourse += AutoPilotMain.angleAdjustStep.Value;
                        autopilotCourse = autopilotCourse > 180f ? -180f + Mathf.Repeat(autopilotCourse, 180f) : autopilotCourse;
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
            if (!rudder || !steeringWheel) return;
            // UI on steering wheel if activated
            if (AutoPilotMain.rudderHUDConfig.Value)
            {
                if (steeringWheel.IsLookedAt() || steeringWheel.IsStickyClicked() || steeringWheel.IsCliked())
                {
                    steeringWheel.description = "";
                    // steeringWheel.description += "\n Test: " + test;
                    steeringWheel.description += "\n BoatHeading: " + Mathf.RoundToInt(BoatHeading() < 0 ? BoatHeading() + 360f : BoatHeading());
                    steeringWheel.description += "\n Autopilot: " + Mathf.RoundToInt(autopilotCourse);
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
                    
                    activated = true;
                }
                // steering wheel needs to be locked that steering works
                if (!(bool)Traverse.Create(steeringWheel).Field("locked").GetValue())
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
                }
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
        private void ApplySteering(float currentAngle, float targetAngle)
        {
            // Handle auto-tacking transition
            if (autoTacking)
            {
                
                if (Mathf.Abs(currentAngle - autopilotCourse) <= 1)// (tackProgress >= 1f)
                {
                    // Tack complete
                    autopilotCourse = autopilotActiveCourse ? BoatHeading() : autopilotCourse; 
                    autoTacking = false;
                    integral = 0f; // Reset integral on tack completion
                }
            }

            float error = Mathf.DeltaAngle(targetAngle, currentAngle);
            float deltaTime = Time.time - lastTime;
            
            if (deltaTime > 0.05f) // Update at ~20Hz
            {
                // PID - more aggressive tuning
                float p = error;
                integral += error * deltaTime;
                integral = Mathf.Clamp(integral, -50f, 50f);
                float d = (error - lastError) / deltaTime;
               
                float command =  AutoPilotMain.kp.Value * p +  AutoPilotMain.ki.Value * integral + AutoPilotMain.kd.Value * d;
                command = Mathf.Clamp(command, -1f, 1f);

                steeringWheel.currentInput = currentInputMax * command;

                lastError = error;
                lastTime = Time.time;
            }
        }
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
            float boatHeading = Vector3.SignedAngle(boat.forward, Vector3.forward, -Vector3.up);
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
                float apparentWindAngle = Vector3.SignedAngle(-shipRigidbody.velocity, apparentWind, -Vector3.up);
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


            