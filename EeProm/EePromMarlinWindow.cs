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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.EeProm
{
	public partial class EePromMarlinWindow : SystemWindow
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

		private MHNumberEdit acceleration;
		private MHNumberEdit retractAcceleration;

		private MHNumberEdit pidP;
		private MHNumberEdit pidI;
		private MHNumberEdit pidD;

		private MHNumberEdit homingOffsetX;
		private MHNumberEdit homingOffsetY;
		private MHNumberEdit homingOffsetZ;

		private MHNumberEdit minFeedrate;
		private MHNumberEdit minTravelFeedrate;
		private MHNumberEdit minSegmentTime;

		private MHNumberEdit maxXYJerk;
		private MHNumberEdit maxZJerk;

		private event EventHandler unregisterEvents;

		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private double maxWidthOfLeftStuff = 0;
		private List<GuiWidget> leftStuffToSize = new List<GuiWidget>();

		private int currentTabIndex = 0;

		public EePromMarlinWindow()
			: base(650 * GuiWidget.DeviceScale, 480 * GuiWidget.DeviceScale)
		{
			AlwaysOnTopOfMain = true;
			Title = "Marlin Firmware EEPROM Settings".Localize();

			currentEePromSettings = new EePromMarlinSettings();
			currentEePromSettings.eventAdded += SetUiToPrinterSettings;

			GuiWidget mainContainer = new GuiWidget();
			mainContainer.AnchorAll();
			mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			mainContainer.Padding = new BorderDouble(3, 0);

			// space filling color
			GuiWidget spaceFiller = new GuiWidget(0, 500);
			spaceFiller.VAnchor = VAnchor.ParentBottom;
			spaceFiller.HAnchor = HAnchor.ParentLeftRight;
			spaceFiller.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			spaceFiller.Padding = new BorderDouble(top: 3);
			mainContainer.AddChild(spaceFiller);

			double topBarHeight = 0;
			// the top button bar
			{
				FlowLayoutWidget topButtonBar = new FlowLayoutWidget();
				topButtonBar.HAnchor = HAnchor.ParentLeftRight;
				topButtonBar.VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop;
				topButtonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

				topButtonBar.Margin = new BorderDouble(0, 3);

				Button buttonSetToFactorySettings = textImageButtonFactory.Generate("Reset to Factory Defaults".Localize());
				topButtonBar.AddChild(buttonSetToFactorySettings);

				buttonSetToFactorySettings.Click += (sender, e) =>
				{
					currentEePromSettings.SetPrinterToFactorySettings();
					currentEePromSettings.Update();
				};

				mainContainer.AddChild(topButtonBar);

				topBarHeight = topButtonBar.Height;
			}

			// the center content
			FlowLayoutWidget conterContent = new FlowLayoutWidget(FlowDirection.TopToBottom);
			conterContent.VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop;
			conterContent.HAnchor = HAnchor.ParentLeftRight;
			conterContent.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			conterContent.Padding = new BorderDouble(top: 3);
			conterContent.Margin = new BorderDouble(top: topBarHeight);

			conterContent.AddChild(Create4FieldSet("Steps per mm:".Localize(),
				"X:", ref stepsPerMmX,
				"Y:", ref stepsPerMmY,
				"Z:", ref stepsPerMmZ,
				"E:", ref stepsPerMmE));

			conterContent.AddChild(Create4FieldSet("Maximum feedrates [mm/s]:".Localize(),
				"X:", ref maxFeedrateMmPerSX,
				"Y:", ref maxFeedrateMmPerSY,
				"Z:", ref maxFeedrateMmPerSZ,
				"E:", ref maxFeedrateMmPerSE));

			conterContent.AddChild(Create4FieldSet("Maximum Acceleration [mm/s²]:".Localize(),
				"X:", ref maxAccelerationMmPerSSqrdX,
				"Y:", ref maxAccelerationMmPerSSqrdY,
				"Z:", ref maxAccelerationMmPerSSqrdZ,
				"E:", ref maxAccelerationMmPerSSqrdE));

			conterContent.AddChild(CreateField("Acceleration:".Localize(), ref acceleration));
			conterContent.AddChild(CreateField("Retract Acceleration:".Localize(), ref retractAcceleration));

			conterContent.AddChild(Create3FieldSet("PID settings:".Localize(),
				"P:", ref pidP,
				"I:", ref pidI,
				"D:", ref pidD));

			conterContent.AddChild(Create3FieldSet("Homing Offset:".Localize(),
				"X:", ref homingOffsetX,
				"Y:", ref homingOffsetY,
				"Z:", ref homingOffsetZ));

			conterContent.AddChild(CreateField("Min feedrate [mm/s]:".Localize(), ref minFeedrate));
			conterContent.AddChild(CreateField("Min travel feedrate [mm/s]:".Localize(), ref minTravelFeedrate));
			conterContent.AddChild(CreateField("Minimum segment time [ms]:".Localize(), ref minSegmentTime));
			conterContent.AddChild(CreateField("Maximum X-Y jerk [mm/s]:".Localize(), ref maxXYJerk));
			conterContent.AddChild(CreateField("Maximum Z jerk [mm/s]:".Localize(), ref maxZJerk));

			GuiWidget topBottomSpacer = new GuiWidget(1, 1);
			topBottomSpacer.VAnchor = VAnchor.ParentBottomTop;
			conterContent.AddChild(topBottomSpacer);

			mainContainer.AddChild(conterContent);

			// the bottom button bar
			{
				FlowLayoutWidget bottomButtonBar = new FlowLayoutWidget();
				bottomButtonBar.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
				bottomButtonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				bottomButtonBar.Margin = new BorderDouble(0, 3);

				Button buttonSave = textImageButtonFactory.Generate("Save to EEProm".Localize());
				bottomButtonBar.AddChild(buttonSave);
				buttonSave.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						SaveSettingsToActive();
						currentEePromSettings.SaveToEeProm();
						Close();
					});
				};

				CreateSpacer(bottomButtonBar);

				// put in the import button
#if true
				{
					Button buttonImport = textImageButtonFactory.Generate("Import".Localize() + "...");
					buttonImport.Margin = new BorderDouble(0, 3);
					buttonImport.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							FileDialog.OpenFileDialog(
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
					bottomButtonBar.AddChild(buttonImport);
				}

				// put in the export button
				{
					Button buttonExport = textImageButtonFactory.Generate("Export".Localize() + "...");
					buttonExport.Margin = new BorderDouble(0, 3);
					buttonExport.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							string defaultFileNameNoPath = "eeprom_settings.ini";
                            FileDialog.SaveFileDialog(
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
					bottomButtonBar.AddChild(buttonExport);
				}
#endif

				Button buttonAbort = textImageButtonFactory.Generate("Close".Localize());
				bottomButtonBar.AddChild(buttonAbort);
				buttonAbort.Click += buttonAbort_Click;

				mainContainer.AddChild(bottomButtonBar);
			}

			PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents);

#if __ANDROID__
			this.AddChild(new SoftKeyboardContentOffset(mainContainer));
#else
			AddChild(mainContainer);
#endif

			ShowAsSystemWindow();

			// and ask the printer to send the settings
			currentEePromSettings.Update();

			foreach (GuiWidget widget in leftStuffToSize)
			{
				widget.Width = maxWidthOfLeftStuff;
			}
		}

		private GuiWidget CreateMHNumEdit(ref MHNumberEdit numberEditToCreate)
		{
			numberEditToCreate = new MHNumberEdit(0, pixelWidth: 80, allowNegatives: true, allowDecimals: true);
			numberEditToCreate.SelectAllOnFocus = true;
			numberEditToCreate.VAnchor = Agg.UI.VAnchor.ParentCenter;
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
			textWidget.VAnchor = VAnchor.ParentCenter;
			textWidget.HAnchor = HAnchor.ParentRight;
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
			row.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

			TextWidget labelWidget = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor);
			labelWidget.VAnchor = VAnchor.ParentCenter;
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

		private static void CreateSpacer(FlowLayoutWidget buttonBar)
		{
			GuiWidget spacer = new GuiWidget(1, 1);
			spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			buttonBar.AddChild(spacer);
		}

		private void buttonAbort_Click(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(Close);
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
			acceleration.Text = currentEePromSettings.ACC;
			retractAcceleration.Text = currentEePromSettings.RACC;
			minFeedrate.Text = currentEePromSettings.AVS;
			minTravelFeedrate.Text = currentEePromSettings.AVT;
			minSegmentTime.Text = currentEePromSettings.AVB;
			maxXYJerk.Text = currentEePromSettings.AVX;
			maxZJerk.Text = currentEePromSettings.AVZ;
			pidP.Enabled = pidI.Enabled = pidD.Enabled = currentEePromSettings.hasPID;
			pidP.Text = currentEePromSettings.PPID;
			pidI.Text = currentEePromSettings.IPID;
			pidD.Text = currentEePromSettings.DPID;
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
			currentEePromSettings.ACC = acceleration.Text;
			currentEePromSettings.RACC = retractAcceleration.Text;
			currentEePromSettings.AVS = minFeedrate.Text;
			currentEePromSettings.AVT = minTravelFeedrate.Text;
			currentEePromSettings.AVB = minSegmentTime.Text;
			currentEePromSettings.AVX = maxXYJerk.Text;
			currentEePromSettings.AVZ = maxZJerk.Text;
			currentEePromSettings.PPID = pidP.Text;
			currentEePromSettings.IPID = pidI.Text;
			currentEePromSettings.DPID = pidD.Text;
			currentEePromSettings.HOX = homingOffsetX.Text;
			currentEePromSettings.HOY = homingOffsetY.Text;
			currentEePromSettings.HOZ = homingOffsetZ.Text;

			currentEePromSettings.Save();
		}
	}
}