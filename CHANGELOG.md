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
