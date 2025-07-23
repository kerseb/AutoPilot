using UnityEngine;
using SailwindModdingHelper;
using HarmonyLib;

namespace AutoPilot
{
    public class AutoPilotSteerage : MonoBehaviour
    {
        private Transform boat;
        private HingeJoint rudderJoint;
        private Rudder rudder;
        private GPButtonSteeringWheel steeringWheel;
        private bool autopilotActive = false;
        private float autopilotCourse;
        private bool activated = false;
        private float threshold = 15f;  // Degrees within to start smooth course correction
        private float currentInputMax;
        private float headingDifference;
        public bool allowUserInput = true; // Allow the user to control the autopilot. Intended to be toggled by another mod. 
        public void Awake()
        {
            boat = GetComponentInParent<PurchasableBoat>().transform;
            rudder = boat.GetComponentInChildren<Rudder>();
            rudderJoint = rudder.GetComponent<HingeJoint>();
            steeringWheel = boat.GetComponentInChildren<GPButtonSteeringWheel>();

            GameEvents.OnPlayerInput += (_, __) =>
            {
                if (allowUserInput)
                {
                    if (AutoPilotMain.activateAutopilot.Value.IsDown())
                    {
                        autopilotActive = !autopilotActive;
                    }
                    if (AutoPilotMain.leftAutopilot.Value.IsDown())
                    {
                        autopilotCourse = angleCorrection(autopilotCourse - 5f);
                    }
                    if (AutoPilotMain.rightAutopilot.Value.IsDown())
                    {
                        autopilotCourse = angleCorrection(autopilotCourse + 5f);
                    }
                }
            };
        }
        public void Update()
        {   
            // UI on steering wheel if activated
            if (AutoPilotMain.rudderHUDConfig.Value)
            {
                if (steeringWheel.IsLookedAt() || steeringWheel.IsStickyClicked() || steeringWheel.IsCliked())
                {

                    steeringWheel.description = "";
                    steeringWheel.description += "\n BoatHeading: " + Mathf.RoundToInt(BoatHeading());
                    steeringWheel.description += "\n Autopilot: " + Mathf.RoundToInt(autopilotCourse);
                    // for debug:
                    // steeringWheel.description += "\n Rudder Angle: " + Mathf.RoundToInt(rudder.currentAngle);
                    // steeringWheel.description += "\n currentInput: " + steeringWheel.currentInput;
                    // steeringWheel.description += "\n headingDifference: " + headingDifference;                 
                }
            }



            if (autopilotActive)
            {
                if (!activated)
                {
                    // set some values on autopilot acitvation                    
                    currentInputMax = rudderJoint.limits.max * steeringWheel.gearRatio;
                    autopilotCourse = BoatHeading();
                    activated = true;
                }
                // steering wheel needs to be locked that steering works
                if (!(bool)Traverse.Create(steeringWheel).Field("locked").GetValue())
                {
                    Traverse.Create(steeringWheel).Field("locked").SetValue(true);
                }

                // Calculate the difference in heading
                headingDifference = autopilotCourse - BoatHeading();
                // Normalize to the range (-180, 180]
                if (headingDifference > 180f)
                    headingDifference -= 360f;
                else if (headingDifference <= -180f)
                    headingDifference += 360f;


                if (headingDifference >= 0) // steer right
                {
                    if (Mathf.Abs(headingDifference) > threshold)
                    {
                        steeringWheel.currentInput = -currentInputMax;
                    }
                    else
                    {
                        steeringWheel.currentInput = -smoothSteering(headingDifference);
                    }

                }
                else // steer left
                {

                    if (Mathf.Abs(headingDifference) > threshold)
                    {
                        steeringWheel.currentInput = currentInputMax;
                    }
                    else
                    {
                        steeringWheel.currentInput = smoothSteering(headingDifference);

                    }
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

        private float angleCorrection(float angle)
        /*
        Function to ensure a newly calculated angle stays between [0,360)
        */
        {
            angle = (angle % 360f + 360f) % 360f;
            return angle; 
        }
        private float smoothSteering(float headingDifference)
        /*
        Allows for smoother steering by encorporating the headingDifference. The greater it is the more aggresive the boat should steer.
        Smooth to 0 decaying function.
        */
        {
            headingDifference = Mathf.Abs(headingDifference);
            float safeDiff = Mathf.Max(headingDifference, 0.001f); // Avoid log(0)
            return currentInputMax * Mathf.Log10(1 + safeDiff) / Mathf.Log10(1 + 180f);   


        }
        private float BoatHeading()
        /*
        calculates boat heading in an absolute frame of reference
        */
        {   
            float boatHeading = Vector3.SignedAngle(boat.forward, Vector3.forward, -Vector3.up);
            boatHeading = boatHeading < 0 ? boatHeading + 360f : boatHeading; // corrects from [-180, 180] to [0, 360]
            return boatHeading;
        }

        public void toggleAutoPilot()
        /*
        Start AutoPilot from outside the mod  
        */
        {
            autopilotActive = !autopilotActive;
        }
        public void setCourse(float course)
        /*
        set AutoPilot course from outside the mod  
        */
        {
            autopilotCourse = course;
        }
        
    }
}


            