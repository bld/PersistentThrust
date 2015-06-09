using System;
using System.Linq;
using UnityEngine;

namespace PersistentThrust {

    public class LowThrustEngine : ModuleEngines {

	// GUI to turn on persistent acceleration
	[KSPField(isPersistant = true)]
	public bool IsEnabled = false;

	// GUI display values
	// Thrust
	[KSPField(guiActive = true, guiName = "Thrust")]
	protected string Thrust = "";
	// Isp
	[KSPField(guiActive = true, guiName = "Isp")]
	protected string Isp = "";
	// Throttle
	[KSPField(guiActive = true, guiName = "Throttle")]
	protected string Throttle = "";

	// Numeric display values
	protected double thrust_d = 0;
	protected double isp_d = 0;
	protected double throttle_d = 0;

	// GUI to activate persistent thrust
	[KSPEvent(guiActive = true, guiName = "Activate Persistent Thrust", active = true)]
	public void ActivatePersistentThrust() {
	    IsEnabled = true;
	}

	// GUI to activate persistent thrust
	[KSPEvent(guiActive = true, guiName = "Deactivate Persistent Thrust", active = false)]
	public void DeactivatePersistentThrust() {
	    IsEnabled = false;
	}

	// Update
	public override void OnUpdate() {

	    // Base class update
	    base.OnUpdate();

	    // Persistent thrust GUI
	    Events["ActivatePersistentThrust"].active = !IsEnabled;
	    Events["DeactivatePersistentThrust"].active = IsEnabled;
	    Fields["Thrust"].guiActive = IsEnabled;
	    Fields["Isp"].guiActive = IsEnabled;
	    Fields["Throttle"].guiActive = IsEnabled;

	    // Update display values
	    Thrust = Utils.FormatThrust(thrust_d);
	    Isp = Math.Round(isp_d, 2).ToString() + " s";
	    Throttle = Math.Round(throttle_d * 100).ToString() + "%";
	}

	// Persistent values to use during timewarp
	float IspPersistent = 0;
	float ThrustPersistent = 0;
	float ThrottlePersistent = 0;

	// Low thrust acceleration
	public static Vector3d CalculateLowThrustForce (LowThrustEngine engine, float thrust, Vector3d up) {
	    if (engine.part != null) {
		return up * thrust;
	    } else {
		return Vector3d.zero;
	    }
	}

	// Calculate deltaV
	public static double CalculateDeltaV (float Isp, float m0, float thrust, double dT) {
	    // Mass flow rate
	    double mdot = thrust / (Isp * 9.81);
	    // Final mass
	    double m1 = m0 - mdot * dT;
	    // DeltaV
	    return Isp * 9.81 * Math.Log(m0 / m1);
	}

	// Physics update
	public override void OnFixedUpdate() {

	    base.OnFixedUpdate();
	    
	    if (FlightGlobals.fetch != null && IsEnabled) {

		if (!this.vessel.packed) {
		    // During realtime mode, update values to use during timewarp
		    IspPersistent = realIsp;
		    ThrottlePersistent = requestedThrottle;
		    ThrustPersistent = this.CalculateThrust();
		} else {
		    // If in timewarp, perturb orbit using thrust
		    double UT = Planetarium.GetUniversalTime(); // Universal time
		    double dT = TimeWarp.fixedDeltaTime; // Time step size
		    double m0 = this.vessel.GetTotalMass(); // Current mass
		    double mdot = ThrustPersistent / (IspPersistent * 9.81); // Mass burn rate of engine
		    double dm = mdot * dT; // Change in mass over dT
		    // TODO test if dm exceeds remaining propellant mass
		    // TODO reduce propellant mass by dm
		    double m1 = m0 - dm; // Mass at end of burn
		    double deltaV = IspPersistent * 9.81 * Math.Log(m0/m1); // Delta V from burn
		    Vector3d down = -this.part.transform.up; // Thrust direction
		    Vector3d deltaVV = deltaV * down; // DeltaV vector
		    vessel.orbit.Perturb(deltaVV, UT, dT); // Update vessel orbit
		}

		// Update display numbers
		thrust_d = ThrustPersistent;
		isp_d = IspPersistent;
		throttle_d = ThrottlePersistent;
	    }
	}
    }
}