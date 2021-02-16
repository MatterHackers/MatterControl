/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ConductiveProbeFeedback : WizardPage
	{
		private readonly List<PrintLevelingWizard.ProbePosition> probePositions;

		private Vector3 nozzleCurrentPosition;

		public ConductiveProbeFeedback(ISetupWizard setupWizard, Vector3 nozzleStartPosition, string headerText, string details, List<PrintLevelingWizard.ProbePosition> probePositions)
			: base(setupWizard, headerText, details)
		{
			this.nozzleCurrentPosition = nozzleStartPosition;
			this.probePositions = probePositions;

			var spacer = new GuiWidget(15, 15);
			contentRow.AddChild(spacer);

			feedRates = printer.Settings.Helpers.ManualMovementSpeeds();
		}

		double moveDelta = .5;

		enum State
		{
			WaitingForEndstopStatusStart,
			WaitingForEndstopStatusOk,
		}

		State state = State.WaitingForEndstopStatusStart;
		private Vector3 feedRates;

		public bool MovedBelowMinZ { get; private set; }

		private void PrinterLineRecieved(object sender, string line)
		{
			// looking for 'conductive: TRIGGERED' in an M119 command
			if (line != null)
			{
				switch (state)
				{
					case State.WaitingForEndstopStatusStart:
						if (line.StartsWith("conductive:"))
						{
							if (line.Contains("TRIGGERED"))
							{
								if (moveDelta > .02)
								{
									nozzleCurrentPosition.Z += moveDelta * 3;
									moveDelta *= .5;
									state = State.WaitingForEndstopStatusOk;
								}
								else
								{
									probePositions[0].Position = nozzleCurrentPosition;

									// move on to the next page of the wizard
									UiThread.RunOnIdle(() => NextButton.InvokeClick());
								}
							}
							else // did not find endstop yet
							{
								nozzleCurrentPosition.Z -= moveDelta;
								state = State.WaitingForEndstopStatusOk;
							}
						}
						break;

					case State.WaitingForEndstopStatusOk:
						// found the ok of the M119 command
						// move down more
						if (printer.Connection.CurrentDestination.Z < printer.Settings.GetValue<double>(SettingsKey.conductive_probe_min_z))
						{
							// we have gone down too far
							// abort with error
							this.MovedBelowMinZ = true;
							// move on to the next page of the wizard
							UiThread.RunOnIdle(() => NextButton.InvokeClick());
						}
						else if (line.StartsWith("ok"))
						{
							state = State.WaitingForEndstopStatusStart;
							// send the next set of commands
							printer.Connection.MoveAbsolute(nozzleCurrentPosition, feedRates.X);
							printer.Connection.QueueLine("G4 P1");
							printer.Connection.QueueLine("M119");
						}
						break;
				}
			}
		}

		public override void OnLoad(EventArgs args)
		{
			// always make sure we don't have print leveling turned on
			printer.Connection.AllowLeveling = false;

			NextButton.Enabled = false;

			// do a last minute check that the printer is ready to do this action
			if (printer.Connection.IsConnected
				&& !(printer.Connection.Printing
				|| printer.Connection.Paused))
			{
				printer.Connection.LineReceived += PrinterLineRecieved;
				printer.Connection.MoveAbsolute(nozzleCurrentPosition, feedRates.X);
				printer.Connection.QueueLine("G4 P1");
				printer.Connection.QueueLine("M119");
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			printer.Connection.LineReceived -= PrinterLineRecieved;
			base.OnClosed(e);
		}
	}
}