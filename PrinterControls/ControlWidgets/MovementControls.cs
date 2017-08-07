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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Utilities;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MovementControls : ControlWidgetBase
	{
		public FlowLayoutWidget manualControlsLayout;
		private Button disableMotors;
		private EditManualMovementSpeedsWindow editManualMovementSettingsWindow;
		private Button homeAllButton;
		private Button homeXButton;
		private Button homeYButton;
		private Button homeZButton;
		internal JogControls jogControls;
		private AltGroupBox movementControlsGroupBox;

		// Provides a list of DisableableWidgets controls that can be toggled on/off at runtime
		internal List<DisableableWidget> DisableableWidgets = new List<DisableableWidget>();

		// Displays the current baby step offset stream values
		private TextWidget offsetStreamLabel;

		private LimitCallingFrequency reportDestinationChanged = null;

		private EventHandler unregisterEvents;

		public static double XSpeed => ActiveSliceSettings.Instance.Helpers.GetMovementSpeeds()["x"];

		public static double YSpeed => ActiveSliceSettings.Instance.Helpers.GetMovementSpeeds()["y"];

		public static double ZSpeed => ActiveSliceSettings.Instance.Helpers.GetMovementSpeeds()["z"];

		public static double EFeedRate(int extruderIndex)
		{
			var movementSpeeds = ActiveSliceSettings.Instance.Helpers.GetMovementSpeeds();

			string extruderIndexKey = "e" + extruderIndex.ToString();
			if (movementSpeeds.ContainsKey(extruderIndexKey))
			{
				return movementSpeeds[extruderIndexKey];
			}

			return movementSpeeds["e0"];
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		/// <summary>
		/// Helper method to create DisableableWidget containers and populate the DisableableWidgets local property.
		/// </summary>
		/// <param name="widget">The widget to wrap.</param>
		private DisableableWidget CreateDisableableContainer(GuiWidget widget)
		{
			var container = new DisableableWidget();
			container.AddChild(widget);
			DisableableWidgets.Add(container);

			return container;
		}

		public MovementControls(int headingPointSize)
		{
			var buttonFactory = ApplicationController.Instance.Theme.DisableableControlBase;

			Button editButton;
			movementControlsGroupBox = new AltGroupBox(buttonFactory.GenerateGroupBoxLabelWithEdit(new TextWidget("Movement".Localize(), pointSize: headingPointSize, textColor: ActiveTheme.Instance.SecondaryAccentColor), out editButton))
			{
				Margin = new BorderDouble(0),
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren
			};

			editButton.Click += (sender, e) =>
			{
				if (editManualMovementSettingsWindow == null)
				{
					editManualMovementSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds".Localize(), ActiveSliceSettings.Instance.Helpers.GetMovementSpeedsString(), SetMovementSpeeds);
					editManualMovementSettingsWindow.Closed += (s, e2) =>
					{
						editManualMovementSettingsWindow = null;
					};
				}
				else
				{
					editManualMovementSettingsWindow.BringToFront();
				}
			};

			jogControls = new JogControls(new XYZColors());
			jogControls.Margin = new BorderDouble(0);

			manualControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren,
				Padding = new BorderDouble(3, 0)
			};

			manualControlsLayout.AddChild(CreateDisableableContainer(GetHomeButtonBar()));
			manualControlsLayout.AddChild(CreateSeparatorLine());
			manualControlsLayout.AddChild(jogControls);

			manualControlsLayout.AddChild(CreateSeparatorLine());
			manualControlsLayout.AddChild(CreateDisableableContainer(GetHWDestinationBar()));

			var separator = CreateSeparatorLine();
			separator.Margin = new BorderDouble(0, 0, 0, 5);
			manualControlsLayout.AddChild(separator);

			movementControlsGroupBox.AddChild(manualControlsLayout);

			this.AddChild(movementControlsGroupBox);
		}

		private static void SetMovementSpeeds(string speedString)
		{
			if (!string.IsNullOrEmpty(speedString))
			{
				ActiveSliceSettings.Instance.SetValue(SettingsKey.manual_movement_speeds, speedString);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

		private FlowLayoutWidget GetHomeButtonBar()
		{
			FlowLayoutWidget homeButtonBar = new FlowLayoutWidget();
			homeButtonBar.HAnchor = HAnchor.ParentLeftRight;
			homeButtonBar.Margin = new BorderDouble(0);
			homeButtonBar.Padding = new BorderDouble(0);

			var homingButtonFactory = ApplicationController.Instance.Theme.HomingButtons;
			var commonButtonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			ImageBuffer helpIconImage = StaticData.Instance.LoadIcon("icon_home_white_24x24.png", 24, 24);
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				helpIconImage.InvertLightness();
			}
			ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);

			homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0);
			homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2) * GuiWidget.DeviceScale;

			homeAllButton = homingButtonFactory.Generate("ALL".Localize());
			
			homeAllButton.ToolTipText = "Home X, Y and Z".Localize();
			homeAllButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeAllButton.Click += homeAll_Click;

			double fixedWidth = (int)homeAllButton.Width * GuiWidget.DeviceScale;

			homeXButton = homingButtonFactory.Generate("X", centerText: true, fixedWidth: fixedWidth);
			homeXButton.ToolTipText = "Home X".Localize();
			homeXButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeXButton.Click += homeXButton_Click;

			homeYButton = homingButtonFactory.Generate("Y", centerText: true, fixedWidth: fixedWidth);
			homeYButton.ToolTipText = "Home Y".Localize();
			homeYButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeYButton.Click += homeYButton_Click;

			homeZButton = homingButtonFactory.Generate("Z", centerText: true, fixedWidth: fixedWidth);
			homeZButton.ToolTipText = "Home Z".Localize();
			homeZButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeZButton.Click += homeZButton_Click;

			// Create 'Release' button, clearing fixedWidth needed on sibling 'Home' controls
			disableMotors = commonButtonFactory.Generate("Release".Localize().ToUpper(), fixedWidth: 0);
			disableMotors.Margin = new BorderDouble(0);
			disableMotors.Click += (s, e) =>
			{
				PrinterConnection.Instance.ReleaseMotors();
			};

			homeButtonBar.AddChild(homeIconImageWidget);
			homeButtonBar.AddChild(homeAllButton);
			homeButtonBar.AddChild(homeXButton);
			homeButtonBar.AddChild(homeYButton);
			homeButtonBar.AddChild(homeZButton);

			offsetStreamLabel = new TextWidget("Z Offset".Localize() + ":", pointSize: 8)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(left: 10),
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter
			};
			homeButtonBar.AddChild(offsetStreamLabel);

			var ztuningWidget = new ZTuningWidget();
			homeButtonBar.AddChild(ztuningWidget);
			
			homeButtonBar.AddChild(new HorizontalSpacer());
			homeButtonBar.AddChild(disableMotors);

			return homeButtonBar;
		}

		private FlowLayoutWidget GetHWDestinationBar()
		{
			FlowLayoutWidget hwDestinationBar = new FlowLayoutWidget();
			hwDestinationBar.HAnchor = HAnchor.ParentLeftRight;
			hwDestinationBar.Margin = new BorderDouble(3, 0, 3, 6);
			hwDestinationBar.Padding = new BorderDouble(0);

			TextWidget xPosition = new TextWidget("X: 0.0           ", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor);
			TextWidget yPosition = new TextWidget("Y: 0.0           ", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor);
			TextWidget zPosition = new TextWidget("Z: 0.0           ", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor);

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

			PrinterConnection.Instance.DestinationChanged.RegisterEvent((object sender, EventArgs e) =>
			{
				reportDestinationChanged.CallEvent();
			}, ref unregisterEvents);

			return hwDestinationBar;
		}

		private static void SetDestinationPositionText(TextWidget xPosition, TextWidget yPosition, TextWidget zPosition)
		{
			Vector3 destinationPosition = PrinterConnection.Instance.CurrentDestination;
			xPosition.Text = "X: {0:0.00}".FormatWith(destinationPosition.x);
			yPosition.Text = "Y: {0:0.00}".FormatWith(destinationPosition.y);
			zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.z);
		}

		private void homeAll_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.XYZ);
		}

		private void homeXButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.X);
		}

		private void homeYButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.Y);
		}

		private void homeZButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.Z);
		}
	}

	public class XYZColors
	{
		public static RGBA_Bytes eColor = new RGBA_Bytes(180, 180, 180);
		public static RGBA_Bytes xColor = new RGBA_Bytes(180, 180, 180);
		public static RGBA_Bytes yColor = new RGBA_Bytes(255, 255, 255);
		public static RGBA_Bytes zColor = new RGBA_Bytes(255, 255, 255);

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

		public ZTuningWidget(bool allowRemoveButton = true)
		{
			this.allowRemoveButton = allowRemoveButton;
			this.HAnchor = HAnchor.FitToChildren;
			this.VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter;

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
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.ParentCenter,
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				Height = 20
			};
			this.AddChild(zOffsetStreamContainer);

			double zoffset = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
			zOffsetStreamDisplay = new TextWidget(zoffset.ToString("0.##"))
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0, 8, 0),
				VAnchor = VAnchor.ParentCenter
			};
			zOffsetStreamContainer.AddChild(zOffsetStreamDisplay);

			clearZOffsetButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
			clearZOffsetButton.Name = "Clear ZOffset button";
			clearZOffsetButton.ToolTipText = "Clear ZOffset".Localize();
			clearZOffsetButton.Visible = allowRemoveButton && zoffset != 0;
			clearZOffsetButton.Click += (sender, e) =>
			{
				ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, "0");
			};
			zOffsetStreamContainer.AddChild(clearZOffsetButton);
		}

		internal void OffsetStreamChanged(object sender, EventArgs e)
		{
			double zoffset = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
			bool hasOverriddenZOffset = (zoffset != 0);

			zOffsetStreamContainer.BackgroundColor = (allowRemoveButton && hasOverriddenZOffset) ? SliceSettingsWidget.userSettingBackgroundColor : ActiveTheme.Instance.SecondaryBackgroundColor;
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
