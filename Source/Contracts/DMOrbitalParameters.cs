﻿#region license
/* DMagic Orbital Science - DMOrbitalParameters
 * Parameter To Track Orbital Parameters of a Specific Vessel
 *
 * Copyright (c) 2014, David Grandy <david.grandy@gmail.com>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used 
 * to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *  
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Contracts;
using Contracts.Parameters;

namespace DMagic
{
	class DMOrbitalParameters: ContractParameter
	{
		private CelestialBody body;
		private Vessel vessel;
		private string vName;
		private double orbitalParameter;
		private int type; //type 0 is eccentricity tracker; type 1 is inclination tracker
		private bool inOrbit;

		public DMOrbitalParameters()
		{
		}

		internal DMOrbitalParameters(CelestialBody Body, double Param, int Type)
		{
			body = Body;
			vessel = null;
			vName = "";
			inOrbit = false;
			orbitalParameter = Param;
			type = Type;
			this.disableOnStateChange = false;
		}

		//Properties to be accessed by parent contract
		internal CelestialBody Body
		{
			get { return body; }
			private set { }
		}

		internal double OrbitalParameter
		{
			get { return orbitalParameter; }
			private set { }
		}

		internal Vessel Vessel
		{
			get
			{
				if (!HighLogic.LoadedSceneIsEditor)
					return vessel;
				else
					return null;
			}
			private set { }
		}

		protected override string GetHashString()
		{
			return body.name;
		}

		protected override string GetTitle()
		{
			if (type == 0)
				return string.Format("Orbit with at least {0:N2} eccentricity", orbitalParameter);
			else if (type == 1)
				return string.Format("Orbit of at least {0:N1} degrees inclination", orbitalParameter);
			else
				return "Stupid things";
		}

		protected override void OnRegister()
		{
			GameEvents.VesselSituation.onOrbit.Add(vesselOrbit);
		}

		protected override void OnUnregister()
		{
			GameEvents.VesselSituation.onOrbit.Remove(vesselOrbit);
		}

		protected override void OnSave(ConfigNode node)
		{
			DMUtils.DebugLog("Saving Long Orbital Parameter");
			node.AddValue("Orbital_Parameter", string.Format("{0}|{1}|{2}|{3:N3}", type, body.flightGlobalsIndex, vName, orbitalParameter));
		}

		protected override void OnLoad(ConfigNode node)
		{
			DMUtils.DebugLog("Loading Orbital Parameter");
			int target;
			string[] orbitString = node.GetValue("Orbital_Parameter").Split('|');
			if (!int.TryParse(orbitString[0], out type))
			{
				DMUtils.Logging("Failed To Load Variables; Parameter Removed");
				this.Root.RemoveParameter(this);
			}
			if (int.TryParse(orbitString[1], out target))
				body = FlightGlobals.Bodies[target];
			else
			{
				DMUtils.Logging("Failed To Load Variables; Parameter Removed");
				this.Root.RemoveParameter(this);
			}
			vName = orbitString[2];
			if (!double.TryParse(orbitString[3], out orbitalParameter))
			{
				DMUtils.Logging("Failed To Load Variables; Parameter Removed");
				this.Root.RemoveParameter(this);
			}
			if (!HighLogic.LoadedSceneIsEditor)
			{
				if (!string.IsNullOrEmpty(vName))
				{
					try
					{
						vessel = FlightGlobals.Vessels.FirstOrDefault(v => v.vesselName == vName);
						DMUtils.DebugLog("Vessel {0} Loaded", vessel.vesselName);
					}
					catch
					{
						DMUtils.Logging("Failed To Load Vessel; Parameter Removed");
						this.Root.RemoveParameter(this);
					}
				}
			}
			this.disableOnStateChange = false;
		}

		private void vesselOrbit(Vessel v, CelestialBody b)
		{
			if (v == FlightGlobals.ActiveVessel)
			{
				if (!inOrbit)
				{
					//If the vessels enters orbit around the correct body and has the right parts set to inOrbit
					if (b == body)
					{
						DMUtils.DebugLog("Vessel Mainbody {0} Matches {1}, Checking For Instruments", v.mainBody.name, body.name);
						Part magPart = v.Parts.FirstOrDefault(p => p.name == "dmmagBoom" || p.name == "dmUSMagBoom");
						Part rpwsPart = v.Parts.FirstOrDefault(r => r.name == "rpwsAnt" || r.name == "USRPWS");
						if (magPart != null && rpwsPart != null)
						{
							DMUtils.DebugLog("OP Successfully Entered Orbit");
							inOrbit = true;
							vessel = v;
							vName = vessel.vesselName;
						}
					}
					else
						DMUtils.DebugLog("Vessel Mainbody {0} Does Not Match: {1}", v.mainBody.name, body.name);
				}
			}
		}

		//Track our vessel's orbit
		protected override void OnUpdate()
		{
			if (this.Root.ContractState == Contract.State.Active && !HighLogic.LoadedSceneIsEditor)
			{
				if (inOrbit)
				{
					if (type == 0)
					{
						if (vessel.orbit.eccentricity > orbitalParameter && vessel.situation == Vessel.Situations.ORBITING)
							this.SetComplete();
						else if (vessel.situation == Vessel.Situations.ESCAPING || vessel.situation == Vessel.Situations.SUB_ORBITAL)
						{
							inOrbit = false;
							this.SetIncomplete();
						}
						else
							this.SetIncomplete();
					}
					else if (type == 1)
					{
						if (vessel.orbit.inclination > orbitalParameter && vessel.orbit.inclination < (180 - orbitalParameter) && vessel.situation == Vessel.Situations.ORBITING)
							this.SetComplete();
						else if (vessel.situation == Vessel.Situations.ESCAPING || vessel.situation == Vessel.Situations.SUB_ORBITAL)
						{
							inOrbit = false;
							this.SetIncomplete();
						}
						else
							this.SetIncomplete();
					}
					if (this.State == ParameterState.Complete)
					{
						if (vessel.mainBody != body)
						{
							DMUtils.DebugLog("Vessel Orbiting Wrong Celestial Body");
							inOrbit = false;
							this.SetIncomplete();
						}
					}
				}
			}
		}

	}
}
