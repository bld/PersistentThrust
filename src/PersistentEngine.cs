using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust {
    
    public class PersistentEngine : ModuleEnginesFX {

	// Flag to activate force if it isn't to allow overriding stage activation
	[KSPField(isPersistant = true)]
	bool IsForceActivated;
	// Flag whether to request massless resources
	public bool RequestPropMassless = false;
	// Flag whether to request resources with mass
	public bool RequestPropMass = true;
	
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
	public float IspPersistent = 0;
	public float ThrustPersistent = 0;
	public float ThrottlePersistent = 0;

	// Are we transitioning from timewarp to reatime?
	bool warpToReal = false;

	// Propellant data
	public List<PersistentPropellant> pplist;
	// Average density of propellants
	double densityAverage;

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

	    // Activate force if engine is enabled and operational
	    if (!IsForceActivated && isEnabled && isOperational)
	    {
		IsForceActivated = true;
		part.force_activate();
	    }
	}

	// Initialization
	public override void OnLoad(ConfigNode node) {

	    // Run base OnLoad method
	    base.OnLoad(node);

	    // Initialize PersistentPropellant list
	    pplist = PersistentPropellant.MakeList(propellants);

	    // Initialize density of propellant used in deltaV and mass calculations
	    densityAverage = pplist.AverageDensity();
	}

	void UpdatePersistentParameters () {
	    // Update values to use during timewarp
	    // Update thrust calculation
	    this.CalculateThrust();
	    // Get Isp
	    IspPersistent = realIsp;
	    // Get throttle
	    ThrottlePersistent = vessel.ctrlState.mainThrottle;
	    // Get final thrust
	    ThrustPersistent = this.finalThrust;
	}

	// Calculate demands of each resource
	public double [] CalculateDemands (double demandMass) {
	    var demands = new double[pplist.Count];
	    if (demandMass > 0) {
		// Per propellant demand
		for (var i = 0; i < pplist.Count; i++) {
		    demands[i] = pplist[i].Demand(demandMass);
		}
	    }
	    return demands;
	}
	
	// Apply demanded resources & return results
	// Updated depleted boolean flag if resource request failed
	public double [] ApplyDemands (double [] demands, ref bool depleted) {
	    var demandsOut = new double [pplist.Count];
	    for (var i = 0; i < pplist.Count; i++) {
		var pp = pplist[i];
		// Request resources if:
		// - resource has mass & request mass flag true
		// - resource massless & request massless flag true
		if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless)) {
		    var demandOut = part.RequestResource(pp.propellant.name, demands[i]);
		    demandsOut[i] = demandOut;
		    // Test if resource depleted
		    // TODO test if resource partially depleted: demandOut < demands[i]
		    // For the moment, just let the full deltaV for time segment dT be applied
		    if (demandOut == 0) {
			depleted = true;
		    }
		}
		// Otherwise demand is 0
		else {
		    demandsOut[i] = 0;
		}
	    }
	    // Return demand outputs
	    return demandsOut;
	}
	
	// Calculate DeltaV vector and update resource demand from mass (demandMass)
	public Vector3d CalculateDeltaVV (double m0, double dT, float thrust, float isp, Vector3d thrustUV, out double demandMass) {
	    // Mass flow rate
	    var mdot = thrust / (isp * 9.81f);
	    // Change in mass over time interval dT
	    var dm = mdot * dT;
	    // Resource demand from propellants with mass
	    demandMass = dm / densityAverage;
	    // Mass at end of time interval dT
	    var m1 = m0 - dm;
	    // deltaV amount
	    var deltaV = isp * 9.81f * Math.Log(m0 / m1);
	    // Return deltaV vector
	    return deltaV * thrustUV;
	}

	// Apply the deltaV vector at UT and dT to 
	public static void ApplyDeltaVV (Vector3d deltaVV, double UT, Orbit orbit) {
	    orbit.Perturb(deltaVV, UT);
	}

	// Physics update
	public override void OnFixedUpdate() {
	    if (FlightGlobals.fetch != null && isEnabled) {
		// Time step size
		var dT = TimeWarp.fixedDeltaTime;
		
		// Realtime mode
		if (!this.vessel.packed) {
		    // Update persistent thrust parameters if NOT transitioning from warp to realtime
		    if (!warpToReal) {
			UpdatePersistentParameters();
		    }
		}
		
		// Timewarp mode: perturb orbit using thrust
		else if (part.vessel.situation != Vessel.Situations.SUB_ORBITAL) {
		    warpToReal = true; // Set to true for transition to realtime
		    var UT = Planetarium.GetUniversalTime(); // Universal time
		    var m0 = this.vessel.GetTotalMass(); // Current mass
		    var thrustUV = this.part.transform.up; // Thrust direction unit vector
		    // Calculate deltaV vector & resource demand from propellants with mass
		    double demandMass;
		    var deltaVV = CalculateDeltaVV(m0, dT, ThrustPersistent, IspPersistent, thrustUV, out demandMass);
		    // Calculate resource demands
		    var demands = CalculateDemands(demandMass);
		    // Apply resource demands & test for resource depletion
		    var depleted = false;
		    var demandsOut = ApplyDemands(demands, ref depleted);
		    // Apply deltaV vector at UT & dT to orbit if resources not depleted
		    if (!depleted) {
			ApplyDeltaVV(deltaVV, UT, this.vessel.orbit);
		    }
		    // Otherwise log warning and drop out of timewarp if throttle on & depleted
		    else if (ThrottlePersistent > 0) {
			Debug.Log("Propellant depleted");
			// Return to realtime
			TimeWarp.SetRate(0, true);
		    }
		}
		// Otherwise, if suborbital, set throttle to 0 and show error message
		// TODO fix persistent thrust orbit perturbation on suborbital trajectory
		else if (vessel.ctrlState.mainThrottle > 0) {
		    vessel.ctrlState.mainThrottle = 0;
		    ScreenMessages.PostScreenMessage("Cannot accelerate and timewarp durring sub orbital spaceflight!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
		}

		// Update display numbers
		thrust_d = ThrustPersistent;
		isp_d = IspPersistent;
		throttle_d = ThrottlePersistent;
	    }
	}
    }
}
