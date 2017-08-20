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
//#define SIMULATE_CONNECTION

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.EeProm
{
	public class CloseOnDisconnectWindow : SystemWindow
	{
		private EventHandler unregisterEvents;

		public CloseOnDisconnectWindow(double width, double height)
			: base(width, height)
		{
			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				if(!PrinterConnection.Instance.PrinterIsConnected)
				{
					this.CloseOnIdle();
				}
			}, ref unregisterEvents);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}
	}

	public class EePromRepetierWindow : CloseOnDisconnectWindow
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private EePromRepetierStorage currentEePromSettings;
		private FlowLayoutWidget settingsColmun;

		private EventHandler unregisterEvents;

		public EePromRepetierWindow()
			: base(650 * GuiWidget.DeviceScale, 480 * GuiWidget.DeviceScale)
		{
			AlwaysOnTopOfMain = true;
			BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			currentEePromSettings = new EePromRepetierStorage();

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.VAnchor = Agg.UI.VAnchor.Stretch;
			topToBottom.HAnchor = Agg.UI.HAnchor.Stretch;
			topToBottom.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			topToBottom.Padding = new BorderDouble(3, 0);

			FlowLayoutWidget row = new FlowLayoutWidget();
			row.HAnchor = Agg.UI.HAnchor.Stretch;
			row.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			GuiWidget descriptionWidget = AddDescription("Description".Localize());
			descriptionWidget.Margin = new BorderDouble(left: 3);
			row.AddChild(descriptionWidget);

			CreateSpacer(row);

			GuiWidget valueText = new TextWidget("Value".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			valueText.VAnchor = Agg.UI.VAnchor.Center;
			valueText.Margin = new BorderDouble(left: 5, right: 60);
			row.AddChild(valueText);
			topToBottom.AddChild(row);

			{
				ScrollableWidget settingsAreaScrollBox = new ScrollableWidget(true);
				settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
				settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
				topToBottom.AddChild(settingsAreaScrollBox);

				settingsColmun = new FlowLayoutWidget(FlowDirection.TopToBottom);
				settingsColmun.HAnchor = HAnchor.MaxFitOrStretch;

				settingsAreaScrollBox.AddChild(settingsColmun);
			}

			FlowLayoutWidget buttonBar = new FlowLayoutWidget();
			buttonBar.HAnchor = Agg.UI.HAnchor.MaxFitOrStretch;
			buttonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			// put in the save button
			{
				Button buttonSave = textImageButtonFactory.Generate("Save To EEPROM".Localize());
				buttonSave.Margin = new BorderDouble(0, 3);
				buttonSave.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						currentEePromSettings.Save();
						currentEePromSettings.Clear();
						currentEePromSettings.eventAdded -= NewSettingReadFromPrinter;
						Close();
					});
				};

				buttonBar.AddChild(buttonSave);
			}

			CreateSpacer(buttonBar);

			// put in the import button
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
										RebuildUi();
                                    }
								});
					});
				};
				buttonBar.AddChild(buttonImport);
			}

			// put in the export button
			{
				Button buttonExport = textImageButtonFactory.Generate("Export".Localize() + "...");
				buttonExport.Margin = new BorderDouble(0, 3);
				buttonExport.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						FileDialog.SaveFileDialog(
							new SaveFileDialogParams("EEPROM Settings|*.ini")
							{
								ActionButtonLabel = "Export EEPROM Settings".Localize(),
								Title = "Export EEPROM".Localize(),
                                FileName = "eeprom_settings.ini"
							},
								(saveParams) =>
								{
									if (!string.IsNullOrEmpty(saveParams.FileName))
									{
										currentEePromSettings.Export(saveParams.FileName);
									}
								});
					});
				};
				buttonBar.AddChild(buttonExport);
			}

			// put in the cancel button
			{
				Button buttonCancel = textImageButtonFactory.Generate("Close".Localize());
				buttonCancel.Margin = new BorderDouble(10, 3, 0, 3);
				buttonCancel.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						currentEePromSettings.Clear();
						currentEePromSettings.eventAdded -= NewSettingReadFromPrinter;
						Close();
					});
				};
				buttonBar.AddChild(buttonCancel);
			}

			topToBottom.AddChild(buttonBar);

			this.AddChild(topToBottom);

			Title = "Firmware EEPROM Settings".Localize();

			ShowAsSystemWindow();

			currentEePromSettings.Clear();
			PrinterConnection.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents);
			currentEePromSettings.eventAdded += NewSettingReadFromPrinter;
			currentEePromSettings.AskPrinterForSettings();

#if SIMULATE_CONNECTION
            UiThread.RunOnIdle(AddSimulatedItems);
#endif
		}

#if SIMULATE_CONNECTION
        int count;
        void AddSimulatedItems(object state)
        {
            NewSettingReadFromPrinter(this, new EePromRepetierParameter("this is a test line " + count.ToString()));

            count++;
            if (count < 30)
            {
                UiThread.RunOnIdle(AddSimulatedItems);
            }
        }
#endif

		private static void CreateSpacer(FlowLayoutWidget buttonBar)
		{
			GuiWidget spacer = new GuiWidget(1, 1);
			spacer.HAnchor = Agg.UI.HAnchor.Stretch;
			buttonBar.AddChild(spacer);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		bool waitingForUiUpdate = false;
		private void NewSettingReadFromPrinter(object sender, EventArgs e)
		{
			EePromRepetierParameter newSetting = e as EePromRepetierParameter;
			if (newSetting != null)
			{
				if (!waitingForUiUpdate)
				{
					waitingForUiUpdate = true;
					UiThread.RunOnIdle(RebuildUi, 1);
				}
			}
		}

		private int currentTabIndex = 0;

		private void RebuildUi()
		{
			List<EePromRepetierParameter> tempList = new List<EePromRepetierParameter>();
			lock (currentEePromSettings.eePromSettingsList)
			{
				foreach (KeyValuePair<int, EePromRepetierParameter> keyValue in currentEePromSettings.eePromSettingsList)
				{
					tempList.Add(keyValue.Value);
				}
			}

			settingsColmun.CloseAllChildren();

			foreach (EePromRepetierParameter newSetting in tempList)
			{
				if (newSetting != null)
				{
					FlowLayoutWidget row = new FlowLayoutWidget();
					row.HAnchor = Agg.UI.HAnchor.MaxFitOrStretch;
					row.AddChild(AddDescription(newSetting.Description));
					row.Padding = new BorderDouble(5, 0);
					if ((settingsColmun.Children.Count % 2) == 1)
					{
						row.BackgroundColor = new RGBA_Bytes(0, 0, 0, 30);
					}

					CreateSpacer(row);

					double currentValue;
					double.TryParse(newSetting.Value, out currentValue);
					MHNumberEdit valueEdit = new MHNumberEdit(currentValue, pixelWidth: 80 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true);
					valueEdit.SelectAllOnFocus = true;
					valueEdit.TabIndex = currentTabIndex++;
					valueEdit.VAnchor = Agg.UI.VAnchor.Center;
					valueEdit.ActuallNumberEdit.EditComplete += (sender, e) =>
					{
						newSetting.Value = valueEdit.ActuallNumberEdit.Value.ToString();
					};
					row.AddChild(valueEdit);

					settingsColmun.AddChild(row);
				}
			}
			waitingForUiUpdate = false;
		}

		private GuiWidget AddDescription(string description)
		{
			GuiWidget holder = new GuiWidget(340, 40);
			TextWidget textWidget = new TextWidget(description, textColor: ActiveTheme.Instance.PrimaryTextColor);
			textWidget.VAnchor = Agg.UI.VAnchor.Center;
			holder.AddChild(textWidget);

			return holder;
		}
	}
}