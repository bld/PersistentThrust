* v1.0.4
- PersistentEngine now a PartModule, not ModuleEngines; it finds all ModuleEngines on the part and uses them
- PersistentEngine disabled if no ModuleEngines found on the part
- The ion engine patch now matches to module ModuleEnginesFX, due to the same change in KSP 1.0.5
* v1.0.3
- Made densityAverage public for external use
* v1.0.2
- Modularized the physics update in OnFixedUpdate so that intermediate
  methods calculate and apply the delta-V and resource consumption.
- Intermediate methods are virtual so they can be refined.
- The Orbit.Perturb method didn't use time step size, so it's removed.
* v1.0.1
- SolarSailPart now just doesn't perturb the orbit when the vessel
  isn't in the sun. No more multiplying the solar force by 0.
- PersistentEngine now scans the propellants list of the engine,
  generating the data needed for resource demand calculations,
  allowing multiple propellants with mass and massless to be
  used. PersistentPropellant class added to keep track of data and
  perform calculations.
- PersistentThrust is now independent of SolarSailNavigator.

Thanks to FreeThinker for these fixes:
- Orbit.Perturb() extension now directly updates current orbit from
  current position vector and new velocity vector
- PersistentEngine inherits ModuleEnginesFX
- PersistentEngine operation disabled during timewarp when suborbital
  until this is fixed.
- Get PersistentThrust from .finalThrust attribute instead of
  CalculateThrust().
* v1.0.0
- Initial release
