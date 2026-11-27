# AutoPilot

**AutoPilot** is a simple ship autopilot mod that allows for automated steering. Either holds a compass course or a apparent wind course. The Hotkeys and constants (course correction and pid steering) can be changed in the config.

## 🔧 Features

* Press **`O`** to activate or deactivate the autopilot (compass version).
* Press **`P`** to activate or deactivate the autopilot (apparent wind version).
* Press **`K`** to apply a course correction to the **left**.
* Press **`L`** to apply a course correction to the **right**.
* Press **`Shift+K`** / **`Shift+L`** for a fine correction of 1 degree (normal K / L move 5 degrees; both step sizes are configurable).
* The HUD shows the heading and autopilot setpoint as 0-359.

## 📦 Requires

To use this mod, you need the following:

* [BepInEx](https://github.com/BepInEx/BepInEx/releases) 
* [SailwindModdingHelper by App24](https://thunderstore.io/c/sailwind/p/App24/SailwindModdingHelper/) 

## 🙏 Credits
Much of the implementation was inspired by the SailInfo mod.
Special thanks to aquilartz for the helpful chat!
Thanks to Specter for the implementation of the apparent wind course version and the idea using a pid for steering.


