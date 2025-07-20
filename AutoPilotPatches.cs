using System;
using UnityEngine;

namespace AutoPilot
{
    public class AutoPilotPatches
    {
        public static void RudderPatch(Rudder __instance)
        {   //patching the rudder to find the tiller on modded boats (could patch anything but this seems fitting)

            Transform boatModel = __instance.GetComponentInParent<BoatHorizon>()?.transform;
            if (boatModel == null) return;
            Transform tiller1;
            Transform tiller2;

            if (boatModel.name == "cutterModel")
            {
                tiller1 = __instance.transform.Find("rudder_tiller_cutter");
                tiller2 = __instance.transform.Find("rudder_tiller_cutter_center");
            }
            else if (boatModel.name == "paraw")
            {
                tiller1 = boatModel.Find("hull_regular").Find("rudder_right_reg").Find("tiller_reg");
                tiller2 = boatModel.Find("hull_extension").Find("rudder_right_ext").Find("tiller_ext");
            }
            else
            {
                GPButtonSteeringWheel[] wheels = boatModel.GetComponentsInChildren<GPButtonSteeringWheel>();
                foreach (GPButtonSteeringWheel wheel in wheels)
                {
                    wheel.gameObject.AddComponent<AutoPilotSteerage>();
                }

                return;
            }

            tiller1.gameObject.AddComponent<AutoPilotSteerage>();
            tiller2.gameObject.AddComponent<AutoPilotSteerage>();
        }
    }
}