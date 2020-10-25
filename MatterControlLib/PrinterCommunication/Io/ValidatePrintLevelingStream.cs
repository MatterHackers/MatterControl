/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class ValidatePrintLevelingStream : GCodeStreamProxy
	{
		private bool gcodeAlreadyLeveled;

		public ValidatePrintLevelingStream(PrinterConfig printer, PrintLevelingStream internalStream)
			: base(printer, internalStream)
		{
		}

		public override string DebugInfo => "";

		public static string BeginString => "; VALIDATE_LEVELING";

		private readonly double[] babySteppingValue = new double[4];

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend == "; Software Leveling Applied")
			{
				gcodeAlreadyLeveled = true;
			}

			if (lineToSend != null
				&& !gcodeAlreadyLeveled)
			{
				if (lineToSend == BeginString)
				{
					printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
					{
						// remember the current baby stepping values
						babySteppingValue[i] = value;
						// clear them while we measure the offsets
						printer.Settings.SetValue(key, "0");
					});

					// turn off print leveling
					printer.Connection.AllowLeveling = false;

					// clear any data that we are going to be acquiring (sampled positions, after z home offset)
					var levelingData = new PrintLevelingData()
					{
						LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
					};

					printer.Connection.QueueLine("T0");

					LevelingPlan levelingPlan;

					switch (levelingData.LevelingSystem)
					{
						case LevelingSystem.Probe3Points:
							levelingPlan = new LevelWizard3Point(printer);
							break;

						case LevelingSystem.Probe7PointRadial:
							levelingPlan = new LevelWizard7PointRadial(printer);
							break;

						case LevelingSystem.Probe13PointRadial:
							levelingPlan = new LevelWizard13PointRadial(printer);
							break;

						case LevelingSystem.Probe100PointRadial:
							levelingPlan = new LevelWizard100PointRadial(printer);
							break;

						case LevelingSystem.Probe3x3Mesh:
							levelingPlan = new LevelWizardMesh(printer, 3, 3);
							break;

						case LevelingSystem.Probe5x5Mesh:
							levelingPlan = new LevelWizardMesh(printer, 5, 5);
							break;

						case LevelingSystem.Probe10x10Mesh:
							levelingPlan = new LevelWizardMesh(printer, 10, 10);
							break;

						case LevelingSystem.ProbeCustom:
							levelingPlan = new LevelWizardCustom(printer);
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}

			return lineToSend;
		}
	}
}