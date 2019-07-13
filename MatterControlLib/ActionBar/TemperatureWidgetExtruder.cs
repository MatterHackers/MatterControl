/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class ControlContentExtruder : FlowLayoutWidget
	{
		private int moveAmount = 10;
		private PrinterConfig printer;

		internal ControlContentExtruder(PrinterConfig printer, int extruderIndex, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.printer = printer;

			GuiWidget loadUnloadButtonRow = null;
			// We do not yet support loading filament into extruders other than 0 & 1, fix it when needed.
			if (extruderIndex < 2)
			{
				// add in load and unload buttons
				loadUnloadButtonRow = new FlowLayoutWidget()
				{
					Padding = theme.ToolbarPadding,
				};

				var loadButton = theme.CreateDialogButton("Load".Localize());
				loadButton.ToolTipText = "Load filament".Localize();
				loadButton.Name = "Load Filament Button";
				loadButton.Click += (s, e) => UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show(
						new LoadFilamentWizard(printer, extruderIndex, showAlreadyLoadedButton: false));
				});

				loadUnloadButtonRow.AddChild(loadButton);

				var unloadButton = theme.CreateDialogButton("Unload".Localize());
				unloadButton.ToolTipText = "Unload filament".Localize();
				unloadButton.Click += (s, e) => UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show(new UnloadFilamentWizard(printer, extruderIndex));
				});
				loadUnloadButtonRow.AddChild(unloadButton);

				this.AddChild(new SettingsItem("Filament".Localize(), loadUnloadButtonRow, theme, enforceGutter: false));
			}

			// Add the Extrude buttons
			var extrudeRetractButtonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Padding = theme.ToolbarPadding,
			};

			int extruderButtonTopMargin = loadUnloadButtonRow == null ? 8 : 0;

			var extrudeButton = theme.CreateDialogButton("Extrude".Localize());
			extrudeButton.Name = "Extrude Button";
			extrudeButton.ToolTipText = "Extrude filament".Localize();
			extrudeButton.Click += (s, e) =>
			{
				printer.Connection.MoveExtruderRelative(moveAmount, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			extrudeRetractButtonRow.AddChild(extrudeButton);

			var retractButton = theme.CreateDialogButton("Retract".Localize());
			retractButton.ToolTipText = "Retract filament".Localize();
			retractButton.Click += (s, e) =>
			{
				printer.Connection.MoveExtruderRelative(moveAmount * -1, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			extrudeRetractButtonRow.AddChild(retractButton);

			this.AddChild(new SettingsItem(
				loadUnloadButtonRow == null ? "Filament".Localize() : "", // Don't put the name if we put in a load and unload button (it has the name)
				extrudeRetractButtonRow,
				theme,
				enforceGutter: false));

			var moveButtonsContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Fit | VAnchor.Center,
				HAnchor = HAnchor.Fit,
				Padding = theme.ToolbarPadding,
			};

			var oneButton = theme.CreateMicroRadioButton("1");
			oneButton.CheckedStateChanged += (s, e) =>
			{
				if (oneButton.Checked)
				{
					moveAmount = 1;
				}
			};
			moveButtonsContainer.AddChild(oneButton);

			var tenButton = theme.CreateMicroRadioButton("10");
			tenButton.CheckedStateChanged += (s, e) =>
			{
				if (tenButton.Checked)
				{
					moveAmount = 10;
				}
			};
			moveButtonsContainer.AddChild(tenButton);

			var oneHundredButton = theme.CreateMicroRadioButton("100");
			oneHundredButton.CheckedStateChanged += (s, e) =>
			{
				if (oneHundredButton.Checked)
				{
					moveAmount = 100;
				}
			};
			moveButtonsContainer.AddChild(oneHundredButton);

			switch (moveAmount)
			{
				case 1:
					oneButton.Checked = true;
					break;
				case 10:
					tenButton.Checked = true;
					break;
				case 100:
					oneHundredButton.Checked = true;
					break;
			}

			moveButtonsContainer.AddChild(new TextWidget("mm", textColor: theme.TextColor, pointSize: 8)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(3, 0)
			});

			this.AddChild(new SettingsItem("Distance".Localize(), moveButtonsContainer, theme, enforceGutter: false));
		}
	}

	internal class TemperatureWidgetHotend : TemperatureWidgetBase
	{
		private int hotendIndex = -1;

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();
		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();
		private RunningInterval runningInterval;
		private ThemeConfig theme;

		public TemperatureWidgetHotend(PrinterConfig printer, int hotendIndex, ThemeConfig theme, int totalHotends)
			: base(printer, "150.3°", theme)
		{
			this.theme = theme;
			this.Name = $"Hotend {hotendIndex}";
			this.hotendIndex = hotendIndex;
			this.DisplayCurrentTemperature();
			if (totalHotends == 1)
			{
				this.ToolTipText = "Hotend Temperature".Localize().FormatWith(hotendIndex);
			}
			else
			{
				this.ToolTipText = "Hotend {0} Temperature".Localize().FormatWith(hotendIndex + 1);
			}

			this.PopupContent = this.GetPopupContent(ApplicationController.Instance.MenuTheme);

			// Register listeners
			printer.Connection.HotendTemperatureRead += Connection_HotendTemperatureRead;
			printer.Connection.HotendTargetTemperatureChanged += this.Connection_HotendTargetTemperatureChanged;
		}

		protected override int ActualTemperature => (int)printer.Connection.GetActualHotendTemperature(this.hotendIndex);

		protected override int TargetTemperature => (int)printer.Connection.GetTargetHotendTemperature(this.hotendIndex);

		private string TemperatureKey
		{
			get => "temperature" + ((this.hotendIndex > 0 && this.hotendIndex < 4) ? hotendIndex.ToString() : "");
		}

		private GuiWidget GetPopupContent(ThemeConfig menuTheme)
		{
			var widget = new IgnoredPopupWidget()
			{
				Width = 350,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12, 0),
				BackgroundColor = menuTheme.BackgroundColor
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			widget.AddChild(container);

			GuiWidget hotendRow;

			container.AddChild(hotendRow = new SettingsItem(
				printer.Settings.Helpers.HotendCount() == 1 ? "Hotend".Localize() : "Hotend {0}".Localize().FormatWith(hotendIndex + 1),
				menuTheme,
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = printer.Connection.GetTargetHotendTemperature(hotendIndex) > 0,
					ToggleAction = (itemChecked) =>
					{
						if (itemChecked)
						{
							// Set to goal temp
							printer.Connection.SetTargetHotendTemperature(hotendIndex, printer.Settings.Helpers.ExtruderTargetTemperature(hotendIndex));
						}
						else
						{
							// Turn off extruder
							printer.Connection.SetTargetHotendTemperature(hotendIndex, 0);
						}
					}
				},
				enforceGutter: false));

			var toggleWidget = hotendRow.Children.FirstOrDefault(o => o is ICheckbox);
			toggleWidget.Name = "Toggle Heater";

			heatToggle = toggleWidget as ICheckbox;

			int tabIndex = 0;
			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);

			// TODO: Add guards around computed settings key to handle invalid/missing keys
			var settingsData = PrinterSettings.SettingsData[TemperatureKey];
			var temperatureRow = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, menuTheme, ref tabIndex, allUiFields);
			container.AddChild(temperatureRow);

			// Add the temperature row to the always enabled list ensuring the field can be set when disconnected
			alwaysEnabled.Add(temperatureRow);
			alwaysEnabled.Add(hotendRow);

			if (hotendIndex == 1)
			{
				// add the extrusion movement speed multiplier
				var extruderMultiplier = SliceSettingsTabView.CreateItemRow(PrinterSettings.SettingsData[SettingsKey.t1_extrusion_move_speed_multiplier],
					settingsContext,
					printer,
					menuTheme,
					ref tabIndex,
					allUiFields);
				container.AddChild(extruderMultiplier);
				alwaysEnabled.Add(extruderMultiplier);
			}

			// add in the temp graph
			var graph = new DataViewGraph()
			{
				DynamicallyScaleRange = false,
				MinValue = 0,
				ShowGoal = true,
				GoalColor = menuTheme.PrimaryAccentColor,
				GoalValue = printer.Settings.Helpers.ExtruderTargetTemperature(hotendIndex),
				MaxValue = 280, // could come from some profile value in the future
				Width = widget.Width - 20,
				Height = 35, // this works better if it is a common multiple of the Width
			};

			runningInterval = UiThread.SetInterval(() =>
			{
				graph.AddData(this.ActualTemperature);
			}, 1);

			var valueField = temperatureRow.Descendants<MHNumberEdit>().FirstOrDefault();
			valueField.Name = "Temperature Input";

			valueField.ActuallNumberEdit.EditComplete += (s, e) =>
			{
				if (printer.Connection.GetTargetHotendTemperature(hotendIndex) > 0)
				{
					printer.Settings.SetValue(TemperatureKey, valueField.Value.ToString());
				}
			};

			var settingsRow = temperatureRow.DescendantsAndSelf<SliceSettingsRow>().FirstOrDefault();

			void Printer_SettingChanged(object s, StringEventArgs stringEvent)
			{
				if (stringEvent != null)
				{
					string settingsKey = stringEvent.Data;
					if (this.allUiFields.TryGetValue(settingsKey, out UIField uifield))
					{
						string currentValue = settingsContext.GetValue(settingsKey);
						if (uifield.Value != currentValue)
						{
							uifield.SetValue(
								currentValue,
								userInitiated: false);
						}
					}

					if (stringEvent.Data == this.TemperatureKey)
					{
						graph.GoalValue = printer.Settings.Helpers.ExtruderTargetTemperature(hotendIndex);
						settingsRow.UpdateStyle();
					}
				}
			}

			printer.Settings.SettingChanged += Printer_SettingChanged;
			printer.Disposed += (s, e) => printer.Settings.SettingChanged -= Printer_SettingChanged;

			container.AddChild(graph);

			if (hotendIndex == 0)
			{
				// put in the material selector
				var presetsSelector = new PresetSelectorWidget(printer, "Material".Localize(), Color.Transparent, NamedSettingsLayers.Material, hotendIndex, menuTheme, true)
				{
					Margin = new BorderDouble(right: menuTheme.ToolbarPadding.Right),
					Padding = 0,
					BackgroundColor = Color.Transparent,
					VAnchor = VAnchor.Center | VAnchor.Fit
				};

				// Hide TextWidget
				presetsSelector.Children.First().Visible = false;

				var pulldownContainer = presetsSelector.FindDescendant("Preset Pulldown Container");
				if (pulldownContainer != null)
				{
					pulldownContainer.Padding = menuTheme.ToolbarPadding;
					pulldownContainer.HAnchor = HAnchor.MaxFitOrStretch;
					pulldownContainer.Margin = 0;
					pulldownContainer.Padding = 0;
				}

				pulldownContainer.Parent.Margin = 0;

				var dropList = pulldownContainer.Children.OfType<DropDownList>().FirstOrDefault();
				if (dropList != null)
				{
					dropList.Name = "Hotend Preset Selector";
					dropList.HAnchor = HAnchor.Stretch;
					dropList.Margin = 0;
				}

				// add in the material selector
				GuiWidget materialSettingsRow = new SettingsItem("Material".Localize(), presetsSelector, menuTheme, enforceGutter: false)
				{
					Border = new BorderDouble(0, 1),
					BorderColor = AppContext.MenuTheme.RowBorder
				};

				container.AddChild(materialSettingsRow);
				// material can be changed even when the printer is not connected
				alwaysEnabled.Add(materialSettingsRow);
				// add in a shop button
				var shopButton = theme.CreateDialogButton("Shop".Localize());
				shopButton.Margin = new BorderDouble(3, 3, 6, 3);
				shopButton.ToolTipText = "Shop Filament at MatterHackers".Localize();
				shopButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						ApplicationController.Instance.LaunchBrowser("https://www.matterhackers.com/store/c/3d-printer-filament");
					});
				};
				materialSettingsRow.AddChild(shopButton);

				presetsSelector.PerformLayout();
			}
			else // put in a temperature selector for the correct material
			{

			}

			// put in the actual extruder controls
			bool shareTemp = printer.Settings.GetValue<bool>(SettingsKey.extruders_share_temperature);
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			if (shareTemp && extruderCount > 1)
			{
				for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
				{
					container.AddChild(new HorizontalLine()
					{
						Margin = new BorderDouble(0, 5, 0, 0)
					});

					container.AddChild(new TextWidget("Extruder".Localize() + " " + (extruderIndex + 1).ToString(), pointSize: menuTheme.DefaultFontSize)
					{
						AutoExpandBoundsToText = true,
						TextColor = theme.TextColor,
						HAnchor = HAnchor.Left,
					});
					container.AddChild(new ControlContentExtruder(printer, extruderIndex, menuTheme)
					{
						Border = new BorderDouble(top: 1),
						BorderColor = AppContext.MenuTheme.MinimalShade
					});
				}
			}
			else
			{
				container.AddChild(new ControlContentExtruder(printer, hotendIndex, menuTheme));
			}

			return widget;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.HotendTemperatureRead -= Connection_HotendTemperatureRead;
			printer.Connection.HotendTargetTemperatureChanged -= this.Connection_HotendTargetTemperatureChanged;

			UiThread.ClearInterval(runningInterval);

			base.OnClosed(e);
		}

		private void Connection_HotendTemperatureRead(object s, EventArgs e)
		{
			DisplayCurrentTemperature();
		}

		private void Connection_HotendTargetTemperatureChanged(object sender, int changedHotendIndex)
		{
			if (this.hotendIndex == changedHotendIndex)
			{
				UiThread.RunOnIdle(() =>
				{
					heatToggle.Checked = printer.Connection.GetTargetHotendTemperature(changedHotendIndex) != 0;
				});
			}
		}
	}
}