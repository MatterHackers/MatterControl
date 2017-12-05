/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizard7PointRadial : LevelWizardRadialBase
	{
		private static readonly int numberOfRadialSamples = 6;

		public LevelWizard7PointRadial(PrinterConfig printer, LevelWizardBase.RuningState runningState)
			: base(printer, runningState, 500, 370, 21, numberOfRadialSamples)
		{
		}

		public static string ApplyLeveling(PrinterSettings printerSettings, string lineBeingSent, Vector3 currentDestination)
		{
			if (printerSettings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
				&& lineBeingSent.Length > 2
				&& lineBeingSent[2] == ' ')
			{
				PrintLevelingData levelingData = printerSettings.Helpers.GetPrintLevelingData();
				return GetLevelingFunctions(printerSettings, numberOfRadialSamples, levelingData, printerSettings.GetValue<Vector2>(SettingsKey.print_center))
					.DoApplyLeveling(lineBeingSent, currentDestination);
			}

			return lineBeingSent;
		}

		public static List<string> ProcessCommand(string lineBeingSent)
		{
			int commentIndex = lineBeingSent.IndexOf(';');
			if (commentIndex > 0) // there is content in front of the ;
			{
				lineBeingSent = lineBeingSent.Substring(0, commentIndex).Trim();
			}
			List<string> lines = new List<string>();
			lines.Add(lineBeingSent);
			if (lineBeingSent.StartsWith("G28")
				|| lineBeingSent.StartsWith("G29"))
			{
				lines.Add("M114");
			}

			return lines;
		}

		public override Vector2 GetPrintLevelPositionToSample(int index, double radius)
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
			return GetLevelingFunctions(printer.Settings, numberOfRadialSamples, levelingData, printer.Settings.GetValue<Vector2>(SettingsKey.print_center))
				.GetPrintLevelPositionToSample(index, radius);
		}
	}

	public abstract class LevelWizardRadialBase : LevelWizardBase
	{
		private static RadialLevlingFunctions currentLevelingFunctions = null;
		private LevelingStrings levelingStrings;

		public LevelWizardRadialBase(PrinterConfig printer, LevelWizardBase.RuningState runningState, int width, int height, int totalSteps, int numberOfRadialSamples)
			: base(printer, width, height, totalSteps)
		{
			levelingStrings = new LevelingStrings(printer.Settings);
			string printLevelWizardTitle = ApplicationController.Instance.ProductName;
			string printLevelWizardTitleFull = "Print Leveling Wizard".Localize();
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			List<ProbePosition> probePositions = new List<ProbePosition>(numberOfRadialSamples + 1);
			for (int i = 0; i < numberOfRadialSamples + 1; i++)
			{
				probePositions.Add(new ProbePosition());
			}

			printLevelWizard = new WizardControl();
			AddChild(printLevelWizard);

			if (runningState == LevelWizardBase.RuningState.InitialStartupCalibration)
			{
				string requiredPageInstructions = "{0}\n\n{1}".FormatWith(levelingStrings.requiredPageInstructions1, levelingStrings.requiredPageInstructions2);
				printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions));
			}

			printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.OverviewText, levelingStrings.WelcomeText(numberOfRadialSamples + 1, 5)));

			// To make sure the bed is at the correct temp, put in a filament selection page.
			bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
			if (hasHeatedBed)
			{
				string filamentSelectionPage = "{0}\n\n{1}".FormatWith(levelingStrings.materialPageInstructions1, levelingStrings.materialPageInstructions2);
				printLevelWizard.AddPage(new SelectMaterialPage(printer, levelingStrings.materialStepText, filamentSelectionPage));
			}
			printLevelWizard.AddPage(new HomePrinterPage(printer, printLevelWizard, levelingStrings.homingPageStepText, levelingStrings.homingPageInstructions));
			if (hasHeatedBed)
			{
				printLevelWizard.AddPage(new WaitForTempPage(printer, printLevelWizard, levelingStrings));
			}

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			for (int i = 0; i < numberOfRadialSamples + 1; i++)
			{
				Vector2 probePosition = GetPrintLevelPositionToSample(i, bedRadius);

				if (printer.Settings.Helpers.UseZProbe())
				{
					var stepString = string.Format("{0} {1} {2} {3}:", levelingStrings.stepTextBeg, i + 1, levelingStrings.stepTextEnd, numberOfRadialSamples + 1);
					printLevelWizard.AddPage(new AutoProbeFeedback(printer, printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", stepString, positionLabel, i + 1, autoCalibrateLabel), probePositions, i));
				}
				else
				{
					printLevelWizard.AddPage(new GetCoarseBedHeight(printer, printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, lowPrecisionLabel), probePositions, i, levelingStrings));
					printLevelWizard.AddPage(new GetFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, medPrecisionLabel), probePositions, i, levelingStrings));
					printLevelWizard.AddPage(new GetUltraFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, highPrecisionLabel), probePositions, i, levelingStrings));
				}
			}

			printLevelWizard.AddPage(new LastPagelInstructions(printer, printLevelWizard, "Done".Localize(), levelingStrings.DoneInstructions, probePositions));
		}

		public static RadialLevlingFunctions GetLevelingFunctions(PrinterSettings printerSettings, int numberOfRadialSamples, PrintLevelingData levelingData, Vector2 bedCenter)
		{
			if (currentLevelingFunctions == null
				|| currentLevelingFunctions.NumberOfRadialSamples != numberOfRadialSamples
				|| currentLevelingFunctions.BedCenter != bedCenter
				|| !levelingData.SamplesAreSame(currentLevelingFunctions.SampledPositions))
			{
				if (currentLevelingFunctions != null)
				{
					currentLevelingFunctions.Dispose();
				}

				currentLevelingFunctions = new RadialLevlingFunctions(printerSettings, numberOfRadialSamples, levelingData, bedCenter);
			}

			return currentLevelingFunctions;
		}

		public abstract Vector2 GetPrintLevelPositionToSample(int index, double radius);
	}

	public class RadialLevlingFunctions : IDisposable
	{
		PrinterSettings printerSettings;

		public RadialLevlingFunctions(PrinterSettings printerSettings, int numberOfRadialSamples, PrintLevelingData levelingData, Vector2 bedCenter)
		{
			this.printerSettings = printerSettings;
			this.SampledPositions = new List<Vector3>(levelingData.SampledPositions);
			this.BedCenter = bedCenter;
			this.NumberOfRadialSamples = numberOfRadialSamples;
		}

		public Vector2 BedCenter
		{
			get; set;
		}

		public List<Vector3> SampledPositions { get; private set; }

		public int NumberOfRadialSamples { get; set; }

		public void Dispose()
		{
		}

		public string DoApplyLeveling(string lineBeingSent, Vector3 currentDestination)
		{
			double extruderDelta = 0;
			GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
			double feedRate = 0;
			GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

			StringBuilder newLine = new StringBuilder("G1 ");

			if (lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z"))
			{
				Vector3 outPosition = GetPositionWithZOffset(currentDestination);

				newLine = newLine.Append(String.Format("X{0:0.##} Y{1:0.##} Z{2:0.###}", outPosition.X, outPosition.Y, outPosition.Z));
			}

			if (extruderDelta != 0)
			{
				newLine = newLine.Append(String.Format(" E{0:0.###}", extruderDelta));
			}

			if (feedRate != 0)
			{
				newLine = newLine.Append(String.Format(" F{0:0.##}", feedRate));
			}

			lineBeingSent = newLine.ToString();

			return lineBeingSent;
		}

		public Vector3 GetPositionWithZOffset(Vector3 currentDestination)
		{
			if (SampledPositions.Count == NumberOfRadialSamples + 1)
			{
				Vector2 destinationFromCenter = new Vector2(currentDestination) - BedCenter;

				double angleToPoint = Math.Atan2(destinationFromCenter.Y, destinationFromCenter.X);

				if (angleToPoint < 0)
				{
					angleToPoint += MathHelper.Tau;
				}

				double oneSegmentAngle = MathHelper.Tau / NumberOfRadialSamples;
				int firstIndex = (int)(angleToPoint / oneSegmentAngle);
				int lastIndex = firstIndex + 1;
				if (lastIndex == NumberOfRadialSamples)
				{
					lastIndex = 0;
				}

				Plane currentPlane = new Plane(SampledPositions[firstIndex], SampledPositions[lastIndex], SampledPositions[NumberOfRadialSamples]);

				double hitDistance = currentPlane.GetDistanceToIntersection(new Vector3(currentDestination.X, currentDestination.Y, 0), Vector3.UnitZ);

				currentDestination.Z += hitDistance;
			}

			return currentDestination;
		}

		public Vector2 GetPrintLevelPositionToSample(int index, double radius)
		{
			Vector2 bedCenter = printerSettings.GetValue<Vector2>(SettingsKey.print_center);
			if (index < NumberOfRadialSamples)
			{
				Vector2 position = new Vector2(radius, 0);
				position.Rotate(MathHelper.Tau / NumberOfRadialSamples * index);
				position += bedCenter;
				return position;
			}
			else
			{
				return bedCenter;
			}
		}
	}
}