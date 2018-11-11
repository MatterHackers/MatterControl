/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.EeProm
{
	public class MarlinEEPromPage : EEPromPage
	{
		private EePromMarlinSettings currentEePromSettings;

		private MHNumberEdit stepsPerMmX;
		private MHNumberEdit stepsPerMmY;
		private MHNumberEdit stepsPerMmZ;
		private MHNumberEdit stepsPerMmE;

		private MHNumberEdit maxFeedrateMmPerSX;
		private MHNumberEdit maxFeedrateMmPerSY;
		private MHNumberEdit maxFeedrateMmPerSZ;
		private MHNumberEdit maxFeedrateMmPerSE;

		private MHNumberEdit maxAccelerationMmPerSSqrdX;
		private MHNumberEdit maxAccelerationMmPerSSqrdY;
		private MHNumberEdit maxAccelerationMmPerSSqrdZ;
		private MHNumberEdit maxAccelerationMmPerSSqrdE;

		private MHNumberEdit accelerationPrintingMoves;
		private MHNumberEdit accelerationRetraction;
		private MHNumberEdit accelerationTravelMoves;

		private MHNumberEdit pidP;
		private MHNumberEdit pidI;
		private MHNumberEdit pidD;

		private MHNumberEdit bedPidP;
		private MHNumberEdit bedPidI;
		private MHNumberEdit bedPidD;

		private MHNumberEdit homingOffsetX;
		private MHNumberEdit homingOffsetY;
		private MHNumberEdit homingOffsetZ;

		private MHNumberEdit minFeedrate;
		private MHNumberEdit minTravelFeedrate;
		private MHNumberEdit minSegmentTime;

		private MHNumberEdit maxXYJerk;
		private MHNumberEdit maxZJerk;
		private MHNumberEdit maxEJerk;

		private EventHandler unregisterEvents;

		private double maxWidthOfLeftStuff = 0;
		private List<GuiWidget> leftStuffToSize = new List<GuiWidget>();

		private int currentTabIndex = 0;

		public MarlinEEPromPage(PrinterConfig printer)
			: base(printer)
		{
			AlwaysOnTopOfMain = true;
			this.WindowTitle = "Marlin Firmware EEPROM Settings".Localize();

			currentEePromSettings = new EePromMarlinSettings(printer.Connection);
			currentEePromSettings.eventAdded += SetUiToPrinterSettings;

			// the center content
			var conterContent = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit | VAnchor.Top,
				HAnchor = HAnchor.Stretch
			};

			// add a scroll container
			var settingsAreaScrollBox = new ScrollableWidget(true);
			settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
			settingsAreaScrollBox.AnchorAll();
			contentRow.AddChild(settingsAreaScrollBox);

			settingsAreaScrollBox.AddChild(conterContent);

			conterContent.AddChild(Create4FieldSet("Steps per mm".Localize() + ":",
				"X:", ref stepsPerMmX,
				"Y:", ref stepsPerMmY,
				"Z:", ref stepsPerMmZ,
				"E:", ref stepsPerMmE));

			conterContent.AddChild(Create4FieldSet("Maximum feedrates [mm/s]".Localize() + ":",
				"X:", ref maxFeedrateMmPerSX,
				"Y:", ref maxFeedrateMmPerSY,
				"Z:", ref maxFeedrateMmPerSZ,
				"E:", ref maxFeedrateMmPerSE));

			conterContent.AddChild(Create4FieldSet("Maximum Acceleration [mm/s²]".Localize() + ":",
				"X:", ref maxAccelerationMmPerSSqrdX,
				"Y:", ref maxAccelerationMmPerSSqrdY,
				"Z:", ref maxAccelerationMmPerSSqrdZ,
				"E:", ref maxAccelerationMmPerSSqrdE));

			conterContent.AddChild(CreateField("Acceleration Printing".Localize() + ":", ref accelerationPrintingMoves));
			conterContent.AddChild(CreateField("Acceleration Travel".Localize() + ":", ref accelerationTravelMoves));
			conterContent.AddChild(CreateField("Retract Acceleration".Localize() + ":", ref accelerationRetraction));

			conterContent.AddChild(Create3FieldSet("PID Settings".Localize() + ":",
				"P:", ref pidP,
				"I:", ref pidI,
				"D:", ref pidD));

			conterContent.AddChild(Create3FieldSet("Bed PID Settings".Localize() + ":",
				"P:", ref bedPidP,
				"I:", ref bedPidI,
				"D:", ref bedPidD));

			conterContent.AddChild(Create3FieldSet("Homing Offset".Localize() + ":",
				"X:", ref homingOffsetX,
				"Y:", ref homingOffsetY,
				"Z:", ref homingOffsetZ));

			conterContent.AddChild(CreateField("Min feedrate [mm/s]".Localize() + ":", ref minFeedrate));
			conterContent.AddChild(CreateField("Min travel feedrate [mm/s]".Localize() + ":", ref minTravelFeedrate));
			conterContent.AddChild(CreateField("Minimum segment time [ms]".Localize() + ":", ref minSegmentTime));
			conterContent.AddChild(CreateField("Maximum X-Y jerk [mm/s]".Localize() + ":", ref maxXYJerk));
			conterContent.AddChild(CreateField("Maximum Z jerk [mm/s]".Localize() + ":", ref maxZJerk));
			conterContent.AddChild(CreateField("Maximum E jerk [mm/s]".Localize() + ":", ref maxEJerk));

			// the bottom button bar
			var buttonSave = theme.CreateDialogButton("Save to EEProm".Localize());
			buttonSave.Click += (s, e) =>UiThread.RunOnIdle(() =>
			{
				SaveSettingsToActive();
				currentEePromSettings.SaveToEeProm();
				this.DialogWindow.Close();
			});
			this.AddPageAction(buttonSave);

			var exportButton = theme.CreateDialogButton("Export".Localize());
			exportButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(this.ExportSettings, .1);
			};
			this.AddPageAction(exportButton);

			printer.Connection.LineReceived += currentEePromSettings.Add;

			// and ask the printer to send the settings
			currentEePromSettings.Update();

			if (headerRow is OverflowBar overflowBar)
			{
				overflowBar.ExtendOverflowMenu = (popupMenu) =>
				{
					var menuItem = popupMenu.CreateMenuItem("Import".Localize());
					menuItem.Name = "Import Menu Item";
					menuItem.Click += (s, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							AggContext.FileDialogs.OpenFileDialog(
								new OpenFileDialogParams("EEPROM Settings|*.ini")
								{
									ActionButtonLabel = "Import EEPROM Settings".Localize(),
									Title = "Import EEPROM".Localize(),
								},
								(openParams) =>
								{
									if (!string.IsNullOrEmpty(openParams.FileName))
									{
										currentEePromSettings.Import(openParams.FileName);
										SetUiToPrinterSettings(null, null);
									}
								});
						}, .1);
					};

					// put in the export button
					menuItem = popupMenu.CreateMenuItem("Export".Localize());
					menuItem.Name = "Export Menu Item";
					menuItem.Click += (s, e) =>
					{
						UiThread.RunOnIdle(this.ExportSettings, .1);
					};

					popupMenu.CreateSeparator();

					menuItem = popupMenu.CreateMenuItem("Reset to Factory Defaults".Localize());
					menuItem.Click += (s, e) =>
					{
						currentEePromSettings.SetPrinterToFactorySettings();
						currentEePromSettings.Update();
					};
				};
			}

			foreach (GuiWidget widget in leftStuffToSize)
			{
				widget.Width = maxWidthOfLeftStuff;
			}
		}

		private void ExportSettings()
		{
			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("EEPROM Settings|*.ini")
				{
					ActionButtonLabel = "Export EEPROM Settings".Localize(),
					Title = "Export EEPROM".Localize(),
					FileName = $"eeprom_settings_{base.GetSanitizedPrinterName()}"
				},
				(saveParams) =>
				{
					if (!string.IsNullOrEmpty(saveParams.FileName))
					{
						currentEePromSettings.Export(saveParams.FileName);
					}
				});
		}

		private GuiWidget CreateMHNumEdit(ref MHNumberEdit numberEditToCreate)
		{
			numberEditToCreate = new MHNumberEdit(0, theme, pixelWidth: 80, allowNegatives: true, allowDecimals: true)
			{
				SelectAllOnFocus = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(3, 0),
				TabIndex = GetNextTabIndex()
			};

			return numberEditToCreate;
		}

		private GuiWidget CreateField(string label, ref MHNumberEdit field1)
		{
			MHNumberEdit none = null;

			return Create4FieldSet(label,
			"", ref field1,
			null, ref none,
			null, ref none,
			null, ref none);
		}

		private GuiWidget Create3FieldSet(string label,
			string field1Label, ref MHNumberEdit field1,
			string field2Label, ref MHNumberEdit field2,
			string field3Label, ref MHNumberEdit field3)
		{
			MHNumberEdit none = null;

			return Create4FieldSet(label,
			field1Label, ref field1,
			field2Label, ref field2,
			field3Label, ref field3,
			null, ref none);
		}

		private GuiWidget CreateTextField(string label)
		{
			var textWidget = new TextWidget(label, pointSize: theme.FontSize10, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Right
			};

			var container = new GuiWidget(textWidget.Height, 24);
			container.AddChild(textWidget);

			return container;
		}

		private GuiWidget Create4FieldSet(string label,
			string field1Label, ref MHNumberEdit field1,
			string field2Label, ref MHNumberEdit field2,
			string field3Label, ref MHNumberEdit field3,
			string field4Label, ref MHNumberEdit field4)
		{
			var row = new FlowLayoutWidget
			{
				Margin = 3,
				HAnchor = HAnchor.Stretch
			};

			var labelWidget = new TextWidget(label, pointSize: theme.FontSize10, textColor: theme.TextColor);
			maxWidthOfLeftStuff = Math.Max(maxWidthOfLeftStuff, labelWidget.Width);

			var holder = new GuiWidget(labelWidget.Width, labelWidget.Height)
			{
				Margin = new BorderDouble(3, 0),
				VAnchor = VAnchor.Fit | VAnchor.Center
			};
			holder.AddChild(labelWidget);
			leftStuffToSize.Add(holder);
			row.AddChild(holder);

			row.AddChild(CreateTextField(field1Label));
			row.AddChild(CreateMHNumEdit(ref field1));

			if (field2Label != null)
			{
				row.AddChild(CreateTextField(field2Label));
				row.AddChild(CreateMHNumEdit(ref field2));
			}

			if (field3Label != null)
			{
				row.AddChild(CreateTextField(field3Label));
				row.AddChild(CreateMHNumEdit(ref field3));
			}

			if (field4Label != null)
			{
				row.AddChild(CreateTextField(field4Label));
				row.AddChild(CreateMHNumEdit(ref field4));
			}

			return row;
		}

		private int GetNextTabIndex()
		{
			return currentTabIndex++;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void SetUiToPrinterSettings(object sender, EventArgs e)
		{
			stepsPerMmX.Text = currentEePromSettings.SX;
			stepsPerMmY.Text = currentEePromSettings.SY;
			stepsPerMmZ.Text = currentEePromSettings.SZ;
			stepsPerMmE.Text = currentEePromSettings.SE;
			maxFeedrateMmPerSX.Text = currentEePromSettings.FX;
			maxFeedrateMmPerSY.Text = currentEePromSettings.FY;
			maxFeedrateMmPerSZ.Text = currentEePromSettings.FZ;
			maxFeedrateMmPerSE.Text = currentEePromSettings.FE;
			maxAccelerationMmPerSSqrdX.Text = currentEePromSettings.AX;
			maxAccelerationMmPerSSqrdY.Text = currentEePromSettings.AY;
			maxAccelerationMmPerSSqrdZ.Text = currentEePromSettings.AZ;
			maxAccelerationMmPerSSqrdE.Text = currentEePromSettings.AE;
			accelerationPrintingMoves.Text = currentEePromSettings.AccPrintingMoves;
			accelerationTravelMoves.Text = currentEePromSettings.AccTravelMoves;
			accelerationRetraction.Text = currentEePromSettings.AccRetraction;
			minFeedrate.Text = currentEePromSettings.AVS;
			minTravelFeedrate.Text = currentEePromSettings.AVT;
			minSegmentTime.Text = currentEePromSettings.AVB;
			maxXYJerk.Text = currentEePromSettings.AVX;
			maxZJerk.Text = currentEePromSettings.AVZ;
			maxEJerk.Text = currentEePromSettings.AVE;
			pidP.Enabled = pidI.Enabled = pidD.Enabled = currentEePromSettings.hasPID;
			pidP.Text = currentEePromSettings.PPID;
			pidI.Text = currentEePromSettings.IPID;
			pidD.Text = currentEePromSettings.DPID;
			bedPidP.Enabled = bedPidI.Enabled = bedPidD.Enabled = currentEePromSettings.bed_HasPID;
			bedPidP.Text = currentEePromSettings.BED_PPID;
			bedPidI.Text = currentEePromSettings.BED_IPID;
			bedPidD.Text = currentEePromSettings.BED_DPID;
			homingOffsetX.Text = currentEePromSettings.hox;
			homingOffsetY.Text = currentEePromSettings.hoy;
			homingOffsetZ.Text = currentEePromSettings.hoz;
		}

		private void SaveSettingsToActive()
		{
			currentEePromSettings.SX = stepsPerMmX.Text;
			currentEePromSettings.SY = stepsPerMmY.Text;
			currentEePromSettings.SZ = stepsPerMmZ.Text;
			currentEePromSettings.SE = stepsPerMmE.Text;
			currentEePromSettings.FX = maxFeedrateMmPerSX.Text;
			currentEePromSettings.FY = maxFeedrateMmPerSY.Text;
			currentEePromSettings.FZ = maxFeedrateMmPerSZ.Text;
			currentEePromSettings.FE = maxFeedrateMmPerSE.Text;
			currentEePromSettings.AX = maxAccelerationMmPerSSqrdX.Text;
			currentEePromSettings.AY = maxAccelerationMmPerSSqrdY.Text;
			currentEePromSettings.AZ = maxAccelerationMmPerSSqrdZ.Text;
			currentEePromSettings.AE = maxAccelerationMmPerSSqrdE.Text;
			currentEePromSettings.AccPrintingMoves = accelerationPrintingMoves.Text;
			currentEePromSettings.AccTravelMoves = accelerationTravelMoves.Text;
			currentEePromSettings.AccRetraction = accelerationRetraction.Text;
			currentEePromSettings.AVS = minFeedrate.Text;
			currentEePromSettings.AVT = minTravelFeedrate.Text;
			currentEePromSettings.AVB = minSegmentTime.Text;
			currentEePromSettings.AVX = maxXYJerk.Text;
			currentEePromSettings.AVZ = maxZJerk.Text;
			currentEePromSettings.AVE = maxEJerk.Text;
			currentEePromSettings.PPID = pidP.Text;
			currentEePromSettings.IPID = pidI.Text;
			currentEePromSettings.DPID = pidD.Text;
			currentEePromSettings.BED_PPID = bedPidP.Text;
			currentEePromSettings.BED_IPID = bedPidI.Text;
			currentEePromSettings.BED_DPID = bedPidD.Text;
			currentEePromSettings.HOX = homingOffsetX.Text;
			currentEePromSettings.HOY = homingOffsetY.Text;
			currentEePromSettings.HOZ = homingOffsetZ.Text;

			currentEePromSettings.Save();
		}
	}
}