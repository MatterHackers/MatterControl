/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Utilities;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MovementControls : FlowLayoutWidget
	{
		public FlowLayoutWidget manualControlsLayout;

		// Provides a list of DisableableWidgets controls that can be toggled on/off at runtime
		internal List<GuiWidget> DisableableWidgets = new List<GuiWidget>();

		internal JogControls jogControls;

		private PrinterConfig printer;

		private LimitCallingFrequency reportDestinationChanged = null;

		private ThemeConfig theme;

		private MovementControls(PrinterConfig printer, XYZColors xyzColors, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.theme = theme;

			jogControls = new JogControls(printer, xyzColors, theme)
			{
				HAnchor = HAnchor.Left | HAnchor.Stretch,
				Margin = 0
			};

			this.AddChild(AddToDisableableList(GetHomeButtonBar()));

			this.AddChild(jogControls);

			this.AddChild(AddToDisableableList(GetHWDestinationBar()));

			// Register listeners
			printer.Connection.DestinationChanged += Connection_DestinationChanged;
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			var widget = new MovementControls(printer, new XYZColors(theme), theme);

			var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, theme.InvertIcons), theme);
			editButton.Click += (s, e) => widget.EditOptions();

			return new SectionWidget(
				"Movement".Localize(),
				widget,
				theme,
				editButton);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.DestinationChanged -= Connection_DestinationChanged;

			base.OnClosed(e);
		}

		/// <summary>
		/// Helper method to populate the DisableableWidgets local property.
		/// </summary>
		/// <param name="widget">The widget to add and return.</param>
		private GuiWidget AddToDisableableList(GuiWidget widget)
		{
			this.DisableableWidgets.Add(widget);
			return widget;
		}

		private void Connection_DestinationChanged(object s, EventArgs e)
		{
			reportDestinationChanged.CallEvent();
		}

		private void EditOptions()
		{
			DialogWindow.Show(new MovementSpeedsPage(printer));
		}

		private FlowLayoutWidget GetHomeButtonBar()
		{
			var toolbar = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: 10)
			};

			var homeIcon = new IconButton(AggContext.StaticData.LoadIcon("fa-home_16.png", 16, 16, theme.InvertIcons), theme)
			{
				ToolTipText = "Home X, Y and Z".Localize(),
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			homeIcon.Click += (s, e) => printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
			toolbar.AddChild(homeIcon);

			var homeXButton = new TextButton("X", theme)
			{
				ToolTipText = "Home X".Localize(),
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			homeXButton.Click += (s, e) => printer.Connection.HomeAxis(PrinterConnection.Axis.X);
			toolbar.AddChild(homeXButton);

			var homeYButton = new TextButton("Y", theme)
			{
				ToolTipText = "Home Y".Localize(),
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			homeYButton.Click += (s, e) => printer.Connection.HomeAxis(PrinterConnection.Axis.Y);
			toolbar.AddChild(homeYButton);

			var homeZButton = new TextButton("Z", theme)
			{
				ToolTipText = "Home Z".Localize(),
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			homeZButton.Click += (s, e) => printer.Connection.HomeAxis(PrinterConnection.Axis.Z);
			toolbar.AddChild(homeZButton);

			if (printer.Settings.GetValue<bool>(SettingsKey.has_c_axis))
			{
				var homeCButton = new TextButton("C", theme)
				{
					ToolTipText = "Home C".Localize(),
					BackgroundColor = theme.MinimalShade,
					Margin = theme.ButtonSpacing
				};
				homeCButton.Click += (s, e) => printer.Connection.HomeAxis(PrinterConnection.Axis.C);
				toolbar.AddChild(homeCButton);
			}

			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			// Display the current baby step offset stream values
			var offsetStreamLabel = new TextWidget("Z Offset".Localize() + ":", pointSize: 8)
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(left: 10),
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center
			};
			toolbar.AddChild(offsetStreamLabel);

			var ztuningWidget = new ZTuningWidget(printer, theme);
			toolbar.AddChild(ztuningWidget);

			toolbar.AddChild(new HorizontalSpacer());

			// Create 'Release' button
			var disableMotors = new TextButton("Release".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
			};
			disableMotors.Click += (s, e) =>
			{
				printer.Connection.ReleaseMotors(true);
			};
			toolbar.AddChild(disableMotors);

			return toolbar;
		}

		private FlowLayoutWidget GetHWDestinationBar()
		{
			var hwDestinationBar = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 8),
				Padding = 0
			};

			var xPosition = new TextWidget("X: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.TextColor);
			var yPosition = new TextWidget("Y: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.TextColor);
			var zPosition = new TextWidget("Z: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.TextColor);

			hwDestinationBar.AddChild(xPosition);
			hwDestinationBar.AddChild(yPosition);
			hwDestinationBar.AddChild(zPosition);

			SetDestinationPositionText(xPosition, yPosition, zPosition);

			reportDestinationChanged = new LimitCallingFrequency(1, () =>
			{
				UiThread.RunOnIdle(() =>
				{
					SetDestinationPositionText(xPosition, yPosition, zPosition);
				});
			});

			return hwDestinationBar;
		}

		private void SetDestinationPositionText(TextWidget xPosition, TextWidget yPosition, TextWidget zPosition)
		{
			Vector3 destinationPosition = printer.Connection.CurrentDestination;
			xPosition.Text = "X: {0:0.00}".FormatWith(destinationPosition.X);
			yPosition.Text = "Y: {0:0.00}".FormatWith(destinationPosition.Y);
			zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.Z);
		}
	}

	public class XYZColors
	{
		public XYZColors(ThemeConfig theme)
		{
			this.EColor = theme.BorderColor40;
			this.XColor = theme.BorderColor40;
			this.YColor = theme.BorderColor40;
			this.ZColor = theme.BorderColor40;
		}

		public Color EColor { get; }

		public Color XColor { get; }

		public Color YColor { get; }

		public Color ZColor { get; }
	}

	public class ZTuningWidget : FlowLayoutWidget
	{
		private PrinterConfig printer;

		private ThemeConfig theme;

		public ZTuningWidget(PrinterConfig printer, ThemeConfig theme, bool showClearButton = true)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.printer = printer;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Center;

			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				var zOffsetStreamDisplay = new TextWidget(value.ToString("0.##"), pointSize: theme.DefaultFontSize)
				{
					AutoExpandBoundsToText = true,
					TextColor = theme.TextColor,
					Margin = new BorderDouble(5, 0, 8, 0),
					VAnchor = VAnchor.Center,
				};

				DescribeExtruder(zOffsetStreamDisplay, i);

				AddDisplay(printer.Settings, theme, showClearButton, key, i, zOffsetStreamDisplay);
			});
		}

		private void DescribeExtruder(TextWidget zOffsetStreamDisplay, int index)
		{
			if (printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				zOffsetStreamDisplay.Text += $" E{index + 1}";
			}
		}

		private void AddDisplay(PrinterSettings printerSettings,
			ThemeConfig theme,
			bool showClearButton,
			string setting,
			int toolIndex,
			TextWidget widget)
		{
			GuiWidget clearZOffsetButton = null;
			void Printer_SettingChanged(object s, StringEventArgs e)
			{
				if (e?.Data == setting)
				{
					double zOffset = printerSettings.GetValue<double>(setting);
					bool hasOverriddenZOffset = zOffset != 0;

					if (clearZOffsetButton != null)
					{
						clearZOffsetButton.Visible = hasOverriddenZOffset;
					}

					widget.Text = zOffset.ToString("0.##");
					DescribeExtruder(widget, toolIndex);
				}
			}

			var zOffsetStreamContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = new BorderDouble(3, 0),
				Padding = new BorderDouble(3),
				HAnchor = HAnchor.Fit | HAnchor.Right,
				VAnchor = VAnchor.Absolute,
				Height = 20 * GuiWidget.DeviceScale
			};
			this.AddChild(zOffsetStreamContainer);

			var zoffset = printerSettings.GetValue<double>(setting);
			zOffsetStreamContainer.AddChild(widget);

			if (showClearButton)
			{
				clearZOffsetButton = theme.CreateSmallResetButton();
				clearZOffsetButton.Name = "Clear ZOffset button";
				clearZOffsetButton.ToolTipText = "Clear ZOffset".Localize();
				clearZOffsetButton.Visible = zoffset != 0;
				clearZOffsetButton.Click += (sender, e) =>
				{
					printerSettings.SetValue(setting, "0");
				};
				zOffsetStreamContainer.AddChild(clearZOffsetButton);
			}

			printerSettings.SettingChanged += Printer_SettingChanged;

			this.Closed += (s, e) =>
			{
				printerSettings.SettingChanged -= Printer_SettingChanged;
			};
		}
	}
}