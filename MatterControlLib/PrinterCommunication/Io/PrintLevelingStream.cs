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

using MatterControl.Printing;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PrintLevelingStream : GCodeStreamProxy
	{
		private LevelingFunctions currentLevelingFunctions = null;
		private Vector3 currentProbeZOffset;
		private bool wroteLevelingStatus = false;
		private bool gcodeAlreadyLeveled = false;

		public PrintLevelingStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
#if DEBUG
			foreach (var subStream in this.InternalStreams())
			{
				if (subStream is BabyStepsStream)
				{
					throw new Exception("PrintLevelingStream must come before BabyStepsSteam (we need the max length points to be baby stepped).");
				}
			}
#endif

			// always reset this when we construct
			AllowLeveling = true;
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {inputUnleveled}";
			}
		}

		private GCodeFile loadedGCode;

		public bool AllowLeveling { get; set; }

		private PrinterMove inputUnleveled = PrinterMove.Unknown;

		private bool LevelingActive
		{
			get
			{
				return AllowLeveling
					&& printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled)
					&& !printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling);
			}
		}

		public static string SoftwareLevelingAppliedMessage => "; Software Leveling Applied";

		string pendingLine = null;

		public override string ReadLine()
		{
			string lineToSend;

			if (pendingLine != null)
			{
				lineToSend = pendingLine;
				pendingLine = null;
			}
			else
			{
				lineToSend = base.ReadLine();
			}

			if (lineToSend == SoftwareLevelingAppliedMessage)
			{
				gcodeAlreadyLeveled = true;
			}

			if (!gcodeAlreadyLeveled
				&& !wroteLevelingStatus
				&& LevelingActive)
			{
				// make sure we are not reading a gcode file that already has leveling applied
				if (loadedGCode?.Instruction(0)?.Line != SoftwareLevelingAppliedMessage)
				{
					wroteLevelingStatus = true;
					pendingLine = lineToSend;
					return SoftwareLevelingAppliedMessage;
				}
			}

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend != null
				&& LevelingActive
				&& !gcodeAlreadyLeveled)
			{
				if (LineIsMovement(lineToSend))
				{
					PrinterMove currentUnleveledDestination = GetPosition(lineToSend, inputUnleveled);
					var leveledLine = GetLeveledPosition(lineToSend, currentUnleveledDestination);

					// TODO: clamp to 0 - baby stepping - extruder z-offset, so we don't go below the bed (for the active extruder)

					inputUnleveled = currentUnleveledDestination;

					return leveledLine;
				}
				else if (lineToSend.StartsWith("G29"))
				{
					// remove G29 (machine prob bed) if we are running our own leveling.
					lineToSend = base.ReadLine(); // get the next line instead
				}
			}

			return lineToSend;
		}

		public override void SetPrinterPosition(PrinterMove outputPosition)
		{
			var outputWithLeveling = PrinterMove.Unknown;

			outputWithLeveling.CopyKnowSettings(outputPosition);

			if (LevelingActive
				&& outputPosition.PositionFullyKnown)
			{
				string expectedOutput = CreateMovementLine(outputPosition);
				string doubleLeveledOutput = GetLeveledPosition(expectedOutput, outputPosition);

				PrinterMove doubleLeveledDestination = GetPosition(doubleLeveledOutput, PrinterMove.Unknown);
				PrinterMove deltaToLeveledPosition = doubleLeveledDestination - outputPosition;

				this.inputUnleveled = outputPosition - deltaToLeveledPosition;

				// clean up settings that we don't want to be subtracted
				this.inputUnleveled.extrusion = outputPosition.extrusion;
				this.inputUnleveled.feedRate = outputPosition.feedRate;

				internalStream.SetPrinterPosition(this.inputUnleveled);
			}
			else
			{
				this.inputUnleveled = outputPosition;
				internalStream.SetPrinterPosition(this.inputUnleveled);
			}
		}

		private string GetLeveledPosition(string lineBeingSent, PrinterMove currentDestination)
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;

			if (levelingData != null
				&& printer.Settings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 ")))
			{
				if (currentLevelingFunctions == null
					|| currentProbeZOffset != printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset)
					|| !levelingData.SamplesAreSame(currentLevelingFunctions.SampledPositions))
				{
					currentProbeZOffset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
					currentLevelingFunctions = new LevelingFunctions(printer, levelingData);
				}

				lineBeingSent = currentLevelingFunctions.ApplyLeveling(lineBeingSent, currentDestination.position);
			}

			return lineBeingSent;
		}
	}
}