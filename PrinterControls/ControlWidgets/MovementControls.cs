/*
Copyright (c) 2014, Kevin Pope
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MovementControls : ControlWidgetBase
	{
		public bool hotKeysEnabled = false;
		public FlowLayoutWidget manualControlsLayout;
		private Button disableMotors;
		private EditManualMovementSpeedsWindow editManualMovementSettingsWindow;
		private Button homeAllButton;
		private Button homeXButton;
		private Button homeYButton;
		private Button homeZButton;
		private TextImageButtonFactory hotKeyButtonFactory = new TextImageButtonFactory();
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

		public override void OnClosed(EventArgs e)
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

		public MovementControls()
		{
			Button editButton;
			movementControlsGroupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(new TextWidget("Movement".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor), out editButton));
			editButton.Click += (sender, e) =>
			{
				if (editManualMovementSettingsWindow == null)
				{
					editManualMovementSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds".Localize(), ActiveSliceSettings.Instance.Helpers.GetMovementSpeedsString(), SetMovementSpeeds);
					editManualMovementSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editManualMovementSettingsWindow = null; };
				}
				else
				{
					editManualMovementSettingsWindow.BringToFront();
				}
			};

			movementControlsGroupBox.Margin = new BorderDouble(0);
			movementControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			movementControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			movementControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

			jogControls = new JogControls(new XYZColors());
			jogControls.Margin = new BorderDouble(0);
			{
				manualControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
				manualControlsLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				manualControlsLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
				manualControlsLayout.Padding = new BorderDouble(3, 5, 3, 0);
				{
					FlowLayoutWidget leftToRightContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);

					manualControlsLayout.AddChild(CreateDisableableContainer(GetHomeButtonBar()));
					manualControlsLayout.AddChild(CreateDisableableContainer(CreateSeparatorLine()));
					manualControlsLayout.AddChild(jogControls);
					////manualControlsLayout.AddChild(leftToRightContainer);
					manualControlsLayout.AddChild(CreateDisableableContainer(CreateSeparatorLine()));
					manualControlsLayout.AddChild(CreateDisableableContainer(GetHWDestinationBar()));
					manualControlsLayout.AddChild(CreateDisableableContainer(CreateSeparatorLine()));
				}

				movementControlsGroupBox.AddChild(manualControlsLayout);
			}

			this.AddChild(movementControlsGroupBox);
		}

		private static void SetMovementSpeeds(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null && stringEvent.Data != null)
			{
				ActiveSliceSettings.Instance.Helpers.SetManualMovementSpeeds(stringEvent.Data);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

		private void disableMotors_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.ReleaseMotors();
		}

		private FlowLayoutWidget GetHomeButtonBar()
		{
			FlowLayoutWidget homeButtonBar = new FlowLayoutWidget();
			homeButtonBar.HAnchor = HAnchor.ParentLeftRight;
			homeButtonBar.Margin = new BorderDouble(3, 0, 3, 6);
			homeButtonBar.Padding = new BorderDouble(0);

			textImageButtonFactory.borderWidth = 1;
			textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			ImageBuffer helpIconImage = StaticData.Instance.LoadIcon("icon_home_white_24x24.png", 24, 24);
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				helpIconImage.InvertLightness();
			}
			ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);

			homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0);
			homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2) * GuiWidget.DeviceScale;
			RGBA_Bytes oldColor = this.textImageButtonFactory.normalFillColor;
			textImageButtonFactory.normalFillColor = new RGBA_Bytes(180, 180, 180);
			homeAllButton = textImageButtonFactory.Generate("ALL".Localize());
			this.textImageButtonFactory.normalFillColor = oldColor;
			homeAllButton.ToolTipText = "Home X, Y and Z".Localize();
			homeAllButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeAllButton.Click += homeAll_Click;

			textImageButtonFactory.FixedWidth = (int)homeAllButton.Width * GuiWidget.DeviceScale;
			homeXButton = textImageButtonFactory.Generate("X", centerText: true);
			homeXButton.ToolTipText = "Home X".Localize();
			homeXButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeXButton.Click += homeXButton_Click;

			homeYButton = textImageButtonFactory.Generate("Y", centerText: true);
			homeYButton.ToolTipText = "Home Y".Localize();
			homeYButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeYButton.Click += homeYButton_Click;

			homeZButton = textImageButtonFactory.Generate("Z", centerText: true);
			homeZButton.ToolTipText = "Home Z".Localize();
			homeZButton.Margin = new BorderDouble(0, 0, 6, 0);
			homeZButton.Click += homeZButton_Click;

			textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			textImageButtonFactory.FixedWidth = 0;

			disableMotors = textImageButtonFactory.Generate("Release".Localize().ToUpper());
			disableMotors.Margin = new BorderDouble(0);
			disableMotors.Click += disableMotors_Click;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			GuiWidget spacerReleaseShow = new GuiWidget(10 * GuiWidget.DeviceScale, 0);

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
			homeButtonBar.AddChild(spacerReleaseShow);

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

			PrinterConnectionAndCommunication.Instance.DestinationChanged.RegisterEvent((object sender, EventArgs e) =>
			{
				reportDestinationChanged.CallEvent();
			}, ref unregisterEvents);

			return hwDestinationBar;
		}

		private static void SetDestinationPositionText(TextWidget xPosition, TextWidget yPosition, TextWidget zPosition)
		{
			Vector3 destinationPosition = PrinterConnectionAndCommunication.Instance.CurrentDestination;
			xPosition.Text = "X: {0:0.00}".FormatWith(destinationPosition.x);
			yPosition.Text = "Y: {0:0.00}".FormatWith(destinationPosition.y);
			zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.z);
		}

		private void homeAll_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.XYZ);
		}

		private void homeXButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.X);
		}

		private void homeYButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Y);
		}

		private void homeZButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Z);
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

		public ZTuningWidget()
		{
			this.HAnchor = HAnchor.FitToChildren;
			this.VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter;

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				StringEventArgs stringEvent = e as StringEventArgs;
				if (stringEvent != null)
				{
					if (stringEvent.Data == SettingsKey.baby_step_z_offset)
					{
						OffsetStreamChanged(null, null);
					}
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

			zOffsetStreamDisplay = new TextWidget("0")
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0, 8, 0),
				VAnchor = VAnchor.ParentCenter
			};
			zOffsetStreamContainer.AddChild(zOffsetStreamDisplay);

			clearZOffsetButton = new Button(
				new ButtonViewStates(
					new ImageWidget(SliceSettingsWidget.restoreNormal),
					new ImageWidget(SliceSettingsWidget.restoreHover),
					new ImageWidget(SliceSettingsWidget.restorePressed),
					new ImageWidget(SliceSettingsWidget.restoreNormal)))
			{
				Name = "Clear ZOffset button",
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(0, 0, 5, 0),
				ToolTipText = "Clear ZOffset".Localize(),
				Visible = false
			};
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

			zOffsetStreamContainer.BackgroundColor = (hasOverriddenZOffset) ? SliceSettingsWidget.userSettingBackgroundColor : ActiveTheme.Instance.SecondaryBackgroundColor;
			clearZOffsetButton.Visible = hasOverriddenZOffset;

			zOffsetStreamDisplay.Text = zoffset.ToString("0.##");
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(null, null);
			base.OnClosed(e);
		}
	}

}
