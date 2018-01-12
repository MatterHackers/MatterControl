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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Utilities;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MovementControls : FlowLayoutWidget
	{
		private PrinterConfig printer;
		private ThemeConfig theme;
		public FlowLayoutWidget manualControlsLayout;
		private EditManualMovementSpeedsWindow editManualMovementSettingsWindow;
		internal JogControls jogControls;

		// Provides a list of DisableableWidgets controls that can be toggled on/off at runtime
		internal List<DisableableWidget> DisableableWidgets = new List<DisableableWidget>();

		private LimitCallingFrequency reportDestinationChanged = null;

		private EventHandler unregisterEvents;

		private MovementControls(PrinterConfig printer, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.theme = theme;

			jogControls = new JogControls(printer, new XYZColors())
			{
				HAnchor = HAnchor.Left | HAnchor.Stretch,
				Margin = 0
			};

			this.AddChild(CreateDisableableContainer(GetHomeButtonBar()));

			this.AddChild(jogControls);

			this.AddChild(CreateDisableableContainer(GetHWDestinationBar()));
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			var widget = new MovementControls(printer, theme);

			var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme), theme);
			editButton.Click += (s, e) => widget.EditOptions();

			return new SectionWidget(
				"Movement".Localize(),
				widget,
				theme,
				editButton);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void EditOptions()
		{
			if (editManualMovementSettingsWindow == null)
			{
				editManualMovementSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds".Localize(), printer.Settings.Helpers.GetMovementSpeedsString(), SetMovementSpeeds);
				editManualMovementSettingsWindow.Closed += (s, e) =>
				{
					editManualMovementSettingsWindow = null;
				};
			}
			else
			{
				editManualMovementSettingsWindow.BringToFront();
			}
		}

		/// <summary>
		/// Helper method to create DisableableWidget containers and populate the DisableableWidgets local property.
		/// </summary>
		/// <param name="widget">The widget to wrap.</param>
		private DisableableWidget CreateDisableableContainer(GuiWidget widget)
		{
			var container = new DisableableWidget()
			{
				HAnchor = HAnchor.Left | HAnchor.Stretch
			};
			container.AddChild(widget);

			DisableableWidgets.Add(container);

			return container;
		}

		private void SetMovementSpeeds(string speedString)
		{
			if (!string.IsNullOrEmpty(speedString))
			{
				printer.Settings.SetValue(SettingsKey.manual_movement_speeds, speedString);
				printer.Bed.GCodeRenderer?.Clear3DGCode();
			}
		}

		private FlowLayoutWidget GetHomeButtonBar()
		{
			var toolbar = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: 10)
			};

			var homeIcon = new IconButton(AggContext.StaticData.LoadIcon("fa-home_16.png", IconColor.Theme), theme)
			{
				ToolTipText = ToolTipText = "Home X, Y and Z".Localize(),
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

			// Display the current baby step offset stream values
			var offsetStreamLabel = new TextWidget("Z Offset".Localize() + ":", pointSize: 8)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(left: 10),
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center
			};
			toolbar.AddChild(offsetStreamLabel);

			var ztuningWidget = new ZTuningWidget(printer.Settings, theme);
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

			var xPosition = new TextWidget("X: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor);
			var yPosition = new TextWidget("Y: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor);
			var zPosition = new TextWidget("Z: 0.0           ", pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor);

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

			printer.Connection.DestinationChanged.RegisterEvent((object sender, EventArgs e) =>
			{
				reportDestinationChanged.CallEvent();
			}, ref unregisterEvents);

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
		public static Color eColor = new Color(180, 180, 180);
		public static Color xColor = new Color(180, 180, 180);
		public static Color yColor = new Color(255, 255, 255);
		public static Color zColor = new Color(255, 255, 255);

		public XYZColors()
		{
		}
	}

	public class ZTuningWidget : GuiWidget
	{
		private TextWidget zOffsetStreamDisplay;
		private Button clearZOffsetButton;
		private FlowLayoutWidget zOffsetStreamContainer;

		private EventHandler unregisterEvents;
		private bool allowRemoveButton;
		PrinterSettings printerSettings;

		public ZTuningWidget(PrinterSettings printerSettings, ThemeConfig theme, bool allowRemoveButton = true)
		{
			this.printerSettings = printerSettings;
			this.allowRemoveButton = allowRemoveButton;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Center;

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if ((e as StringEventArgs)?.Data == SettingsKey.baby_step_z_offset)
				{
					OffsetStreamChanged(null, null);
				}
			}, ref unregisterEvents);

			zOffsetStreamContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = new BorderDouble(3, 0),
				Padding = new BorderDouble(3),
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Center,
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				Height = 20
			};
			this.AddChild(zOffsetStreamContainer);

			double zoffset = printerSettings.GetValue<double>(SettingsKey.baby_step_z_offset);
			zOffsetStreamDisplay = new TextWidget(zoffset.ToString("0.##"), pointSize: theme.DefaultFontSize)
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0, 8, 0),
				VAnchor = VAnchor.Center
			};
			zOffsetStreamContainer.AddChild(zOffsetStreamDisplay);

			clearZOffsetButton = theme.CreateSmallResetButton();
			clearZOffsetButton.Name = "Clear ZOffset button";
			clearZOffsetButton.ToolTipText = "Clear ZOffset".Localize();
			clearZOffsetButton.Visible = allowRemoveButton && zoffset != 0;
			clearZOffsetButton.Click += (sender, e) =>
			{
				printerSettings.SetValue(SettingsKey.baby_step_z_offset, "0");
			};
			zOffsetStreamContainer.AddChild(clearZOffsetButton);
		}

		internal void OffsetStreamChanged(object sender, EventArgs e)
		{
			double zoffset = printerSettings.GetValue<double>(SettingsKey.baby_step_z_offset);
			bool hasOverriddenZOffset = (zoffset != 0);

			zOffsetStreamContainer.BackgroundColor = (allowRemoveButton && hasOverriddenZOffset) ? SliceSettingsRow.userSettingBackgroundColor : ActiveTheme.Instance.SecondaryBackgroundColor;
			clearZOffsetButton.Visible = allowRemoveButton && hasOverriddenZOffset;

			zOffsetStreamDisplay.Text = zoffset.ToString("0.##");
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(null, null);
			base.OnClosed(e);
		}
	}
}
