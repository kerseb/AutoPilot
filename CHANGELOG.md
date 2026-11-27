## [Unreleased]

### Added
- Fine course correction on Shift+K / Shift+L (1 degree, config 'AngleStepFine'). K / L keep the 5 degree default and are ignored while Shift is held.
- Optional CSV telemetry (config 'Enable CSV Telemetry', off by default) written to BepInEx/AutoPilotTelemetry for tuning the PID.
- PID options: derivative filter time, error deadband, max command rate, integral window, optional speed based gain scaling.

### Changed
- HUD shows the heading and the autopilot setpoint as 0-359 (also in wind mode).
- Default kp raised from 0.03 to 0.04. Derivative is now taken on the measurement (no kick on setpoint changes) and low-pass filtered. Integral only accumulates close to the target and no longer winds up while the output is saturated. PID state is reset on activation and when a tack starts or ends.

### Fixed
- Heading used a 3D angle that included the boat's pitch, so near north the heading jumped by twice the pitch angle and the autopilot oscillated. It now uses the horizontal projection of the bow direction. The apparent wind angle is flattened to the horizontal plane as well.
- Tack completion check now handles the +-180 degree wrap-around.

## [1.1.0] - 2025-10-08

### Added
- Added config 'angleAdjustStep' which defines the values with which the auto pilot angle can be changed. By default it is 5, but can be increased for personal preferance.
- Wind angle hold. Can be activated with 'P'. Holds course for a give wind angle. Thanks to Specter for the implementation.

### Changed
- Course correction is now done via a pid steering control. Thanks to Specter for the implementation.

## [1.0.3] - 2025-07-24

### Fixed 
- Only ships where the player is currently on or was last on can be controlled now. Last on only possible if currently not on any ship, i.e in water or on land.

## [1.0.2] - 2025-07-23

### Fixed
- Resolved an issue where the rudder had to be clicked once in-game to initialize the mod. The mod now initializes correctly without user interaction.
