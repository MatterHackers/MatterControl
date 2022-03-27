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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ActionBar
{
    internal class TemperatureWidgetBed : TemperatureWidgetBase
	{
		private RunningInterval runningInterval;

		public TemperatureWidgetBed(PrinterConfig printer, ThemeConfig theme)
			: base(printer, "150.3°", theme)
		{
			this.Name = "Bed TemperatureWidget";
			this.DisplayCurrentTemperature();
			this.ToolTipText = "Bed Temperature".Localize();

			this.ImageWidget.Image = StaticData.Instance.LoadIcon("bed.png", 16, 16).SetToColor(theme.TextColor);

			this.PopupContent = this.GetPopupContent(ApplicationController.Instance.MenuTheme);

			// Register listeners
			printer.Connection.BedTemperatureRead += Connection_BedTemperatureRead;
			printer.Connection.BedTargetTemperatureChanged += this.Connection_BedTargetTemperatureChanged;

		}

		protected override int ActualTemperature => (int)printer.Connection.ActualBedTemperature;
		protected override int TargetTemperature => (int)printer.Connection.TargetBedTemperature;

		private GuiWidget GetPopupContent(ThemeConfig menuTheme)
		{
			var widget = new IgnoredPopupWidget()
			{
				Width = 340 * GuiWidget.DeviceScale,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12, 0),
				BackgroundColor = menuTheme.BackgroundColor
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
			};
			widget.AddChild(container);

			GuiWidget heatedBedSettingItem;

			container.AddChild(heatedBedSettingItem = new SettingsItem(
				"Heated Bed".Localize(),
				menuTheme,
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = printer.Connection.TargetBedTemperature > 0,
					ToggleAction = (itemChecked) =>
					{
						var goalTemp = itemChecked ? printer.Settings.Helpers.ActiveBedTemperature : 0;
						printer.Connection.TargetBedTemperature = goalTemp;
					}
				}));

			var toggleWidget = heatedBedSettingItem.Children.Where(o => o is ICheckbox).FirstOrDefault();
			toggleWidget.Name = "Toggle Heater";

			heatToggle = toggleWidget as ICheckbox;

			int tabIndex = 0;
			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);

			// add in the temp graph
			var graph = new DataViewGraph()
			{
				DynamicallyScaleRange = false,
				MinValue = 0,
				ShowGoal = true,
				GoalColor = menuTheme.PrimaryAccentColor,
				GoalValue = printer.Settings.Helpers.ActiveBedTemperature,
				MaxValue = 150, // could come from some profile value in the future
				Width = widget.Width - 20 * GuiWidget.DeviceScale,
				Height = 35 * GuiWidget.DeviceScale, // this works better if it is a common multiple of the Width
				Margin = new BorderDouble(0, 5, 0, 0),
			};

			var temperatureRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};

			void SettingChanged(object s, StringEventArgs stringEvent)
			{
				if (stringEvent.Data == printer.Settings.Helpers.ActiveBedTemperatureSetting
					|| stringEvent.Data == SettingsKey.bed_temperature)
				{
					graph.GoalValue = printer.Settings.Helpers.ActiveBedTemperature;
				}
				else if (stringEvent.Data == SettingsKey.bed_surface
					|| stringEvent.Data == SettingsKey.active_material_key)
				{
					AddTemperatureControlForBedSurface();
					graph.GoalValue = printer.Settings.Helpers.ActiveBedTemperature;
				}
			}

			void AddTemperatureControlForBedSurface()
            {
				temperatureRow.CloseChildren();

				var settingsData = PrinterSettings.SettingsData[printer.Settings.Helpers.ActiveBedTemperatureSetting];
				var bedTemperature = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, menuTheme, ref tabIndex);

				var settingsRow = bedTemperature.DescendantsAndSelf<SliceSettingsRow>().FirstOrDefault();

				// make sure we are not still connected when changing settings
				printer.Settings.SettingChanged -= SettingChanged;
				// connect it
				printer.Settings.SettingChanged += SettingChanged;
				// and make sure we dispose when done
				printer.Disposed += (s, e) => printer.Settings.SettingChanged -= SettingChanged;

				temperatureRow.AddChild(bedTemperature);
			}

			AddTemperatureControlForBedSurface();
			container.AddChild(temperatureRow);

			// Add the temperature row to the always enabled list ensuring the field can be set when disconnected
			alwaysEnabled.Add(temperatureRow);
			alwaysEnabled.Add(heatedBedSettingItem);

			var bedSurfaceChanger = CreateBedSurfaceSelector(printer, menuTheme, ref tabIndex);
			if (bedSurfaceChanger != null)
            {
				container.AddChild(bedSurfaceChanger);
				alwaysEnabled.Add(bedSurfaceChanger);
            }

			runningInterval = UiThread.SetInterval(() =>
			{
				graph.AddData(this.ActualTemperature);
			}, 1);

			container.AddChild(graph);

			return widget;
		}

		public static GuiWidget CreateBedSurfaceSelector(PrinterConfig printer, ThemeConfig theme, ref int tabIndex)
		{
			if (!printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed)
				|| !printer.Settings.GetValue<bool>(SettingsKey.has_swappable_bed))
			{
				return null;
			}

			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);
			var settingsData = PrinterSettings.SettingsData[SettingsKey.bed_surface];

			var surfaceSelector = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, theme, ref tabIndex);
			return surfaceSelector;
		}

		public static GuiWidget CreateAdvancedBedSurfaceSelector(PrinterConfig printer, ThemeConfig theme, ref int tabIndex)
		{
			if (!printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed)
				|| !printer.Settings.GetValue<bool>(SettingsKey.has_swappable_bed))
			{
				return null;
			}

			var bedSelectorContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			var selectBedSurfaceMessage = new TextWidget("And your printers bed surface.", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(15, 7, 0, 13),
				HAnchor = HAnchor.Left
			};
			bedSelectorContainer.AddChild(selectBedSurfaceMessage);

			var surfaceSelector = CreateBedSurfaceSelector(printer, theme, ref tabIndex);
			bedSelectorContainer.AddChild(surfaceSelector);

			void SetSelectMessageVisibility(object s, EventArgs e)
			{
				selectBedSurfaceMessage.Visible = printer.Settings.GetValue(SettingsKey.bed_surface) == "Default";
			};

			SetSelectMessageVisibility(null, null);

			printer.Settings.SettingChanged += SetSelectMessageVisibility;
			bedSelectorContainer.Closed += (s, e) => printer.Settings.SettingChanged -= SetSelectMessageVisibility;

			return bedSelectorContainer;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.BedTemperatureRead -= Connection_BedTemperatureRead;
			printer.Connection.BedTargetTemperatureChanged -= this.Connection_BedTargetTemperatureChanged;

			UiThread.ClearInterval(runningInterval);

			base.OnClosed(e);
		}

		private void Connection_BedTemperatureRead(object s, EventArgs e)
		{
			DisplayCurrentTemperature();
		}

		private void Connection_BedTargetTemperatureChanged(object sender, EventArgs e)
		{
			heatToggle.Checked = printer.Connection.TargetBedTemperature != 0;
		}
	}
}