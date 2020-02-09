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
using System.Text.RegularExpressions;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.EeProm
{
	public class EEPromPage : DialogPage
	{
		private static Regex nameSanitizer = new Regex("[^_a-zA-Z0-9-]", RegexOptions.Compiled);

		protected PrinterConfig printer;

		public EEPromPage(PrinterConfig printer)
			: base("Close".Localize(), useOverflowBar: true)
		{
			this.HeaderText = "EEProm Settings".Localize();
			this.WindowSize = new VectorMath.Vector2(663, 575);
			this.printer = printer;

			headerRow.Margin = this.headerRow.Margin.Clone(bottom: 0);

			printer.Connection.CommunicationStateChanged += CommunicationStateChanged;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= CommunicationStateChanged;

			base.OnClosed(e);
		}

		protected string GetSanitizedPrinterName()
		{
			// TODO: Determine best file name sanitization implementation: this, MakeValidFileName, something else?
			string printerName = printer.Settings.GetValue(SettingsKey.printer_name).Replace(" ", "_");
			return nameSanitizer.Replace(printerName, "");
		}

		private void CommunicationStateChanged(object s, EventArgs e)
		{
			if (!printer.Connection.IsConnected)
			{
				this.DialogWindow.CloseOnIdle();
			}
		}
	}

	public class RepetierEEPromPage : EEPromPage
	{
		private EePromRepetierStorage currentEePromSettings;
		private FlowLayoutWidget settingsColumn;

		public RepetierEEPromPage(PrinterConfig printer)
			: base(printer)
		{
			AlwaysOnTopOfMain = true;

			this.WindowTitle = "Firmware EEPROM Settings".Localize();

			currentEePromSettings = new EePromRepetierStorage();

			var topToBottom = contentRow;

			var row = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
			};

			GuiWidget descriptionWidget = AddDescription("Description".Localize());
			descriptionWidget.Margin = new BorderDouble(left: 3);
			row.AddChild(descriptionWidget);

			CreateSpacer(row);

			row.AddChild(new TextWidget("Value".Localize(), pointSize: theme.FontSize10, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 5, right: 60)
			});
			topToBottom.AddChild(row);

			{
				var settingsAreaScrollBox = new ScrollableWidget(true);
				settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
				settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = theme.MinimalShade;
				topToBottom.AddChild(settingsAreaScrollBox);

				settingsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.MaxFitOrStretch
				};

				settingsAreaScrollBox.AddChild(settingsColumn);
			}


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
										RebuildUi();
									}
								});
						}, .1);
					};

					menuItem = popupMenu.CreateMenuItem("Export".Localize());
					menuItem.Name = "Export Menu Item";
					menuItem.Click += (s, e) =>
					{
						UiThread.RunOnIdle(this.ExportSettings, .1);
					};
				};
			}

			// put in the save button
			var buttonSave = theme.CreateDialogButton("Save To EEPROM".Localize());
			buttonSave.Click += (s, e) =>
			{
				currentEePromSettings.Save(printer.Connection);
				currentEePromSettings.Clear();
				this.DialogWindow.Close();
			};

			this.AddPageAction(buttonSave);

			var exportButton = theme.CreateDialogButton("Export".Localize());
			exportButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(this.ExportSettings, .1);
			};
			this.AddPageAction(exportButton);

			currentEePromSettings.Clear();
			printer.Connection.LineReceived += currentEePromSettings.Add;
			currentEePromSettings.SettingAdded += NewSettingReadFromPrinter;
			currentEePromSettings.AskPrinterForSettings(printer.Connection);

#if SIMULATE_CONNECTION
            UiThread.RunOnIdle(AddSimulatedItems);
#endif
		}

		private void ExportSettings()
		{
			string defaultFileName = $"eeprom_settings_{base.GetSanitizedPrinterName()}.ini";

			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("EEPROM Settings|*.ini")
				{
					ActionButtonLabel = "Export EEPROM Settings".Localize(),
					Title = "Export EEPROM".Localize(),
					FileName = defaultFileName
				},
				(saveParams) =>
				{
					if (!string.IsNullOrEmpty(saveParams.FileName))
					{
						currentEePromSettings.Export(saveParams.FileName);
					}
				});
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

		public override void OnClosed(EventArgs e)
		{
			if (currentEePromSettings != null)
			{
				currentEePromSettings.SettingAdded -= NewSettingReadFromPrinter;
			}

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

			settingsColumn.CloseAllChildren();

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

					if ((settingsColumn.Children.Count % 2) == 1)
					{
						row.BackgroundColor = new Color(0, 0, 0, 30);
					}

					CreateSpacer(row);

					double.TryParse(newSetting.Value, out double currentValue);
					var valueEdit = new MHNumberEdit(currentValue, theme, pixelWidth: 80 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true)
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

					settingsColumn.AddChild(row);
				}
			}
			waitingForUiUpdate = false;
		}

		private GuiWidget AddDescription(string description)
		{
			var holder = new GuiWidget(340, 40);
			holder.AddChild(new TextWidget(description, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center
			});

			return holder;
		}
	}
}