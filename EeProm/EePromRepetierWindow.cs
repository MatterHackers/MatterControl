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
//#define SIMULATE_CONNECTION

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.EeProm
{
	public class EEPromPage : DialogPage
	{
		private EventHandler unregisterEvents;

		public EEPromPage(PrinterConnection printerConnection)
			: base(useOverflowBar: true)
		{
			this.HeaderText = "EEProm Settings".Localize();
			this.WindowSize = new VectorMath.Vector2(663, 575);
			headerRow.Margin = this.headerRow.Margin.Clone(bottom: 0);

			// Close window if printer is disconnected
			printerConnection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				if(!printerConnection.IsConnected)
				{
					this.WizardWindow.CloseOnIdle();
				}
			}, ref unregisterEvents);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}

	public class RepetierEEPromPage : EEPromPage
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private EePromRepetierStorage currentEePromSettings;
		private FlowLayoutWidget settingsColmun;

		private EventHandler unregisterEvents;

		public RepetierEEPromPage(PrinterConnection printerConnection)
			: base(printerConnection)
		{
			AlwaysOnTopOfMain = true;
			BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			this.WindowTitle = "Firmware EEPROM Settings".Localize();

			currentEePromSettings = new EePromRepetierStorage();

			var topToBottom = contentRow;

			var row = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};

			GuiWidget descriptionWidget = AddDescription("Description".Localize());
			descriptionWidget.Margin = new BorderDouble(left: 3);
			row.AddChild(descriptionWidget);

			CreateSpacer(row);

			row.AddChild(new TextWidget("Value".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 5, right: 60)
			});
			topToBottom.AddChild(row);

			{
				var settingsAreaScrollBox = new ScrollableWidget(true);
				settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
				settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
				topToBottom.AddChild(settingsAreaScrollBox);

				settingsColmun = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.MaxFitOrStretch
				};

				settingsAreaScrollBox.AddChild(settingsColmun);
			}

			var buttonBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};

			// put in the save button
			{
				Button buttonSave = textImageButtonFactory.Generate("Save To EEPROM".Localize());
				buttonSave.Margin = new BorderDouble(0, 3);
				buttonSave.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						currentEePromSettings.Save(printerConnection);
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
				buttonImport.Click += (s, e) =>
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
				buttonExport.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						AggContext.FileDialogs.SaveFileDialog(
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
				buttonCancel.Click += (s, e) =>
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

			currentEePromSettings.Clear();
			printerConnection.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents);
			currentEePromSettings.eventAdded += NewSettingReadFromPrinter;
			currentEePromSettings.AskPrinterForSettings(printerConnection);

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
			buttonBar.AddChild(new GuiWidget(1, 1)
			{
				HAnchor = HAnchor.Stretch
			});
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		bool waitingForUiUpdate = false;
		private void NewSettingReadFromPrinter(object sender, EventArgs e)
		{
			if (e is EePromRepetierParameter newSetting)
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
			var tempList = new List<EePromRepetierParameter>();
			lock (currentEePromSettings.eePromSettingsList)
			{
				foreach (var keyValue in currentEePromSettings.eePromSettingsList)
				{
					tempList.Add(keyValue.Value);
				}
			}

			settingsColmun.CloseAllChildren();

			foreach (EePromRepetierParameter newSetting in tempList)
			{
				if (newSetting != null)
				{
					var row = new FlowLayoutWidget
					{
						HAnchor = HAnchor.MaxFitOrStretch,
						Padding = new BorderDouble(5, 0)
					};
					row.AddChild(AddDescription(newSetting.Description));

					if ((settingsColmun.Children.Count % 2) == 1)
					{
						row.BackgroundColor = new Color(0, 0, 0, 30);
					}

					CreateSpacer(row);

					double.TryParse(newSetting.Value, out double currentValue);
					var valueEdit = new MHNumberEdit(currentValue, pixelWidth: 80 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true)
					{
						SelectAllOnFocus = true,
						TabIndex = currentTabIndex++,
						VAnchor = VAnchor.Center
					};
					valueEdit.ActuallNumberEdit.EditComplete += (s, e) =>
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
			var holder = new GuiWidget(340, 40);
			holder.AddChild(new TextWidget(description, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center
			});

			return holder;
		}
	}
}