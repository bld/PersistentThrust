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

	// Physics update
	public override void OnFixedUpdate() {
	    if (FlightGlobals.fetch != null && isEnabled) {
		// Time step size
		double dT = TimeWarp.fixedDeltaTime;
		
		// Realtime mode
		if (!this.vessel.packed) {
		    // Update persistent thrust parameters if NOT transitioning from warp to realtime
		    if (!warpToReal) {
			UpdatePersistentParameters();
		    }
		}
		
		// Timewarp mode: perturb orbit using thrust
		else if (part.vessel.situation != Vessel.Situations.SUB_ORBITAL)
		{
		    warpToReal = true; // Set to true for transition to realtime
		    double UT = Planetarium.GetUniversalTime(); // Universal time
		    double m0 = this.vessel.GetTotalMass(); // Current mass
		    float mdot = ThrustPersistent / (IspPersistent * 9.81f); // Mass flow rate of engine
		    double dm = mdot * dT; // Change in mass over dT
		    double demandMass = dm / densityAverage; // Resource demand for sum of massive propellants
		    bool depleted = false; // Check if resources depleted

		    // Update vessel resources if demand > 0
		    if (demandMass > 0) {
			foreach (var pp in pplist) {
			    // Per propellant demand
			    var demandProp = pp.Demand(demandMass);
			    if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless)) {
				var demandPropOut = part.RequestResource(pp.propellant.name, demandProp);
				// Check if depleted
				if (demandPropOut == 0) {
				    depleted = true;
				}
			    }
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
		// Otherwise suborbital - set throttle to 0 and show error message.
		// TODO fix persistent thrust orbit perturbation on suborbital trajectory.
		else if (vessel.ctrlState.mainThrottle > 0)
		{
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
