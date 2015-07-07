using System;
using System.Linq;
using UnityEngine;

namespace PersistentThrust {

    public class LowThrustEngine : ModuleEngines {

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
	
	// Persistent values to use during timewarp
	float IspPersistent = 0;
	float ThrustPersistent = 0;
	float ThrottlePersistent = 0;

	// Are we transitioning from timewarp to reatime?
	bool warpToReal = false;

	// Resource used for deltaV and mass calculations
	[KSPField]
	public string resourceDeltaV;
	// Density of resource
	double density;
	// Propellant
	Propellant prop;

	// Resources not used for deltaV
	Propellant[] propOther;
	
	// Update
	public override void OnUpdate() {

	    // When transitioning from timewarp to real update throttle
	    if (warpToReal) {
		vessel.ctrlState.mainThrottle = ThrottlePersistent;
		warpToReal = false;
	    }
	    
	    // Persistent thrust GUI
	    Fields["Thrust"].guiActive = isEnabled;
	    Fields["Isp"].guiActive = isEnabled;
	    Fields["Throttle"].guiActive = isEnabled;

	    // Update display values
	    Thrust = Utils.FormatThrust(thrust_d);
	    Isp = Math.Round(isp_d, 2).ToString() + " s";
	    Throttle = Math.Round(throttle_d * 100).ToString() + "%";
	}

	// Initialization
	public override void OnLoad(ConfigNode node) {

	    // Run base OnLoad method
	    base.OnLoad(node);

	    // Initialize density of propellant used in deltaV and mass calculations
	    density = PartResourceLibrary.Instance.GetDefinition(resourceDeltaV).density;
	}

	public override void OnStart(StartState state) {

	    // Save propellant used for deltaV and those that aren't
	    if (state != StartState.None && state != StartState.Editor) {
		propOther = new Propellant[propellants.Count - 1];
		var i = 0;
		foreach (var p in propellants) {
		    if (p.name == resourceDeltaV) {
			prop = p;
		    } else {
			propOther[i] = p;
			i++;
		    }
		}

		Debug.Log(prop.name + " " + prop.ratio + " " + prop.id);
		foreach (var po in propOther) {
		    Debug.Log(po.name + " " + po.ratio + " " + po.id);
		}
	    }

	    // Run base OnStart method
	    base.OnStart(state);
	}
	
	
	// Physics update
	public override void OnFixedUpdate() {

	    if (FlightGlobals.fetch != null && isEnabled) {
		// Realtime mode
		if (!this.vessel.packed) {
		    // if not transitioning from warp to real
		    // Update values to use during timewarp
		    if (!warpToReal) {
			IspPersistent = realIsp;
			ThrottlePersistent = vessel.ctrlState.mainThrottle;
			ThrustPersistent = this.CalculateThrust();
		    }
		} else { // Timewarp mode: perturb orbit using thrust
		    warpToReal = true; // Set to true for transition to realtime
		    double UT = Planetarium.GetUniversalTime(); // Universal time
		    double dT = TimeWarp.fixedDeltaTime; // Time step size
		    double m0 = this.vessel.GetTotalMass(); // Current mass
		    double mdot = ThrustPersistent / (IspPersistent * 9.81); // Mass burn rate of engine
		    double dm = mdot * dT; // Change in mass over dT
		    double demand = dm / density; // Resource demand
		    bool depleted = false; // Check if resources depleted
		    
		    // Update vessel resource
		    double demandOut = part.RequestResource(resourceDeltaV, demand);
		    // Resource depleted if demandOut = 0 & demand was > demandOut
		    if (demand > 0 && demandOut == 0) {
			depleted = true;
		    } // Revise dm if demandOut < demand
		    else if (demand > 0 && demand > demandOut) {
			dm = demandOut * density;
		    }

		    // Calculate demand of other resources
		    foreach (var p in propOther) {
			var demandOther = demandOut * p.ratio / prop.ratio;
			var demandOutOther = part.RequestResource(p.id, demandOther);
			// Depleted if any resource 
			if (demandOther > 0 && demandOutOther == 0) {
			    depleted = true;
			}
		    }
		    
		    // Calculate thrust and deltaV if demand output > 0
		    if (!depleted) {
			double m1 = m0 - dm; // Mass at end of burn
			double deltaV = IspPersistent * 9.81 * Math.Log(m0/m1); // Delta V from burn
			Vector3d thrustV = this.part.transform.up; // Thrust direction
			Vector3d deltaVV = deltaV * thrustV; // DeltaV vector
			vessel.orbit.Perturb(deltaVV, UT, dT); // Update vessel orbit
		    }
		    // Otherwise, if throttle is turned on, and demand out is 0, show warning
		    else if (ThrottlePersistent > 0) {
			Debug.Log("Propellant depleted");
			// Return to realtime mode
			TimeWarp.SetRate(0, true);
		    }
		}

		// Update display numbers
		thrust_d = ThrustPersistent;
		isp_d = IspPersistent;
		throttle_d = ThrottlePersistent;
	    }
	}
    }
}
