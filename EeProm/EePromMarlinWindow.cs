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

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;

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

		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private double maxWidthOfLeftStuff = 0;
		private List<GuiWidget> leftStuffToSize = new List<GuiWidget>();

		private int currentTabIndex = 0;

		public MarlinEEPromPage(PrinterConnection printerConnection)
			: base(printerConnection)
		{
			AlwaysOnTopOfMain = true;
			this.WindowTitle = "Marlin Firmware EEPROM Settings".Localize();

			currentEePromSettings = new EePromMarlinSettings(printerConnection);
			currentEePromSettings.eventAdded += SetUiToPrinterSettings;

			var mainContainer = contentRow;

			// the center content
			var conterContent = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit | VAnchor.Top,
				HAnchor = HAnchor.Stretch
			};

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

			mainContainer.AddChild(conterContent);

			// the bottom button bar
			{
				var buttonSave = theme.CreateDialogButton("Save to EEProm".Localize());
				buttonSave.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						SaveSettingsToActive();
						currentEePromSettings.SaveToEeProm();
						Close();
					});
				};
				this.AddPageAction(buttonSave);
			}

			printerConnection.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents);

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
						});
					};

					// put in the export button
					menuItem = popupMenu.CreateMenuItem("Export".Localize());
					menuItem.Name = "Export Menu Item";
					menuItem.Click += (s, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							string defaultFileNameNoPath = "eeprom_settings.ini";
							AggContext.FileDialogs.SaveFileDialog(
								new SaveFileDialogParams("EEPROM Settings|*.ini")
								{
									ActionButtonLabel = "Export EEPROM Settings".Localize(),
									Title = "Export EEPROM".Localize(),
									FileName = defaultFileNameNoPath
								},
									(saveParams) =>
									{
										if (!string.IsNullOrEmpty(saveParams.FileName)
										&& saveParams.FileName != defaultFileNameNoPath)
										{
											currentEePromSettings.Export(saveParams.FileName);
										}
									});
						});
					};

					popupMenu.CreateHorizontalLine();

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

		private GuiWidget CreateMHNumEdit(ref MHNumberEdit numberEditToCreate)
		{
			numberEditToCreate = new MHNumberEdit(0, pixelWidth: 80, allowNegatives: true, allowDecimals: true);
			numberEditToCreate.SelectAllOnFocus = true;
			numberEditToCreate.VAnchor = Agg.UI.VAnchor.Center;
			numberEditToCreate.Margin = new BorderDouble(3, 0);
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
			GuiWidget textWidget = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor);
			textWidget.VAnchor = VAnchor.Center;
			textWidget.HAnchor = HAnchor.Right;
			GuiWidget container = new GuiWidget(textWidget.Height, 24);
			container.AddChild(textWidget);
			return container;
		}

		private GuiWidget Create4FieldSet(string label,
			string field1Label, ref MHNumberEdit field1,
			string field2Label, ref MHNumberEdit field2,
			string field3Label, ref MHNumberEdit field3,
			string field4Label, ref MHNumberEdit field4)
		{
			FlowLayoutWidget row = new FlowLayoutWidget();
			row.Margin = new BorderDouble(3);
			row.HAnchor = Agg.UI.HAnchor.Stretch;

			TextWidget labelWidget = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor);
			labelWidget.VAnchor = VAnchor.Center;
			maxWidthOfLeftStuff = Math.Max(maxWidthOfLeftStuff, labelWidget.Width);
			GuiWidget holder = new GuiWidget(labelWidget.Width, labelWidget.Height);
			holder.Margin = new BorderDouble(3, 0);
			holder.AddChild(labelWidget);
			leftStuffToSize.Add(holder);
			row.AddChild(holder);

			{
				row.AddChild(CreateTextField(field1Label));
				GuiWidget nextTabIndex = CreateMHNumEdit(ref field1);
				nextTabIndex.TabIndex = GetNextTabIndex();
				row.AddChild(nextTabIndex);
			}

			if (field2Label != null)
			{
				row.AddChild(CreateTextField(field2Label));
				GuiWidget nextTabIndex = CreateMHNumEdit(ref field2);
				nextTabIndex.TabIndex = GetNextTabIndex();
				row.AddChild(nextTabIndex);
			}

			if (field3Label != null)
			{
				row.AddChild(CreateTextField(field3Label));
				GuiWidget nextTabIndex = CreateMHNumEdit(ref field3);
				nextTabIndex.TabIndex = GetNextTabIndex();
				row.AddChild(nextTabIndex);
			}

			if (field4Label != null)
			{
				row.AddChild(CreateTextField(field4Label));
				GuiWidget nextTabIndex = CreateMHNumEdit(ref field4);
				nextTabIndex.TabIndex = GetNextTabIndex();
				row.AddChild(nextTabIndex);
			}

			return row;
		}

		private int GetNextTabIndex()
		{
			return currentTabIndex++;
		}

		public override void OnClosed(ClosedEventArgs e)
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