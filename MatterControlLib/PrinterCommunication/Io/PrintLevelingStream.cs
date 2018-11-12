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

using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PrintLevelingStream : GCodeStreamProxy
	{
		private PrinterMove lastDestination = new PrinterMove();
		private bool activePrinting;
		private LevelingFunctions currentLevelingFunctions = null;
		private double currentProbeOffset;
		private bool wroteLevelingStatus = false;
		private bool gcodeAlreadyLeveled = false;

		public PrintLevelingStream(PrinterConfig printer, GCodeStream internalStream, bool activePrinting)
			: base(printer, internalStream)
		{
			// always reset this when we construct
			AllowLeveling = true;
			this.activePrinting = activePrinting;
		}

		public static bool AllowLeveling { get; set; }

		public PrinterMove LastDestination => lastDestination;

		bool LevelingActive
		{
			get
			{
				return AllowLeveling
					&& printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled)
					&& !printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling);
			}
		}

		public override string ReadLine()
		{
			if(!wroteLevelingStatus && LevelingActive)
			{
				wroteLevelingStatus = true;
				return "; Software Leveling Applied";
			}

			string lineFromChild = base.ReadLine();

			if(lineFromChild == "; Software Leveling Applied")
			{
				gcodeAlreadyLeveled = true;
			}

			if (lineFromChild != null
				&& LevelingActive
				&& !gcodeAlreadyLeveled)
			{
				if (LineIsMovement(lineFromChild))
				{
					PrinterMove currentDestination = GetPosition(lineFromChild, lastDestination);
					var leveledLine = GetLeveledPosition(lineFromChild, currentDestination);
					lastDestination = currentDestination;

					return leveledLine;
				}
				else if (lineFromChild.StartsWith("G29"))
				{
					// remove G29 (machine prob bed) if we are running our own leveling.
					lineFromChild = base.ReadLine(); // get the next line instead
				}
			}

			return lineFromChild;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			if (LevelingActive)
			{
				string lineBeingSent = CreateMovementLine(position);
				string leveledPosition = GetLeveledPosition(lineBeingSent, position);

				PrinterMove leveledDestination = GetPosition(leveledPosition, PrinterMove.Nowhere);
				PrinterMove deltaToLeveledPosition = leveledDestination - position;

				PrinterMove withoutLevelingOffset = position - deltaToLeveledPosition;

				lastDestination = withoutLevelingOffset;
				lastDestination.extrusion = position.extrusion;
				lastDestination.feedRate = position.feedRate;

				internalStream.SetPrinterPosition(lastDestination);
			}
			else
			{
				internalStream.SetPrinterPosition(position);
			}
		}

		private string GetLeveledPosition(string lineBeingSent, PrinterMove currentDestination)
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();

			if (levelingData != null
				&& printer.Settings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 ")))
			{
				if (currentLevelingFunctions == null
					|| currentProbeOffset != printer.Settings.GetValue<double>(SettingsKey.z_probe_z_offset)
					|| !levelingData.SamplesAreSame(currentLevelingFunctions.SampledPositions))
				{
					currentProbeOffset = printer.Settings.GetValue<double>(SettingsKey.z_probe_z_offset);
					currentLevelingFunctions = new LevelingFunctions(printer.Settings, levelingData);
				}

				lineBeingSent = currentLevelingFunctions.ApplyLeveling(lineBeingSent, currentDestination.position);
			}

			return lineBeingSent;
		}
	}
}