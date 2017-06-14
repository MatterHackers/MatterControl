/*
Copyright (c) 2012, Lars Brubaker
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
using System.Collections.ObjectModel;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class JogControls : GuiWidget
	{
		public static double AxisMoveAmount = 0;
		public static int EAxisMoveAmount = 0;

		private MoveButton xPlusControl;
		private MoveButton xMinusControl;

		private MoveButton yPlusControl;
		private MoveButton yMinusControl;

		private MoveButton zPlusControl;
		private MoveButton zMinusControl;
		
		private MoveButtonFactory moveButtonFactory = new MoveButtonFactory();

		public JogControls(XYZColors colors)
		{
			moveButtonFactory.Colors.Text.Normal = RGBA_Bytes.Black;

			double distanceBetweenControls = 12;
			double buttonSeparationDistance = 10;

			FlowLayoutWidget allControlsTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);

			allControlsTopToBottom.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			{
				FlowLayoutWidget allControlsLeftToRight = new FlowLayoutWidget();

				FlowLayoutWidget xYZWithDistance = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					FlowLayoutWidget xYZControls = new FlowLayoutWidget();
					{
						GuiWidget xyGrid = CreateXYGridControl(colors, distanceBetweenControls, buttonSeparationDistance);
						xYZControls.AddChild(xyGrid);

						FlowLayoutWidget zButtons = CreateZButtons(XYZColors.zColor, buttonSeparationDistance, out zPlusControl, out zMinusControl);
						zButtons.VAnchor = Agg.UI.VAnchor.ParentBottom;
						xYZControls.AddChild(zButtons);
						xYZWithDistance.AddChild(xYZControls);
					}

					// add in some movement radio buttons
					FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
					TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
					buttonsLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
					//setMoveDistanceControl.AddChild(buttonsLabel);

					{
						TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
						buttonFactory.FixedHeight = 20 * GuiWidget.DeviceScale;
						buttonFactory.FixedWidth = 30 * GuiWidget.DeviceScale;
						buttonFactory.fontSize = 8;
						buttonFactory.Margin = new BorderDouble(0);
						buttonFactory.checkedBorderColor = ActiveTheme.Instance.PrimaryTextColor;

						FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();

						var radioList = new ObservableCollection<GuiWidget>();

						movePointZeroTwoMmButton = buttonFactory.GenerateRadioButton("0.02");
						movePointZeroTwoMmButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						movePointZeroTwoMmButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.02); };
						movePointZeroTwoMmButton.SiblingRadioButtonList = radioList;
						moveRadioButtons.AddChild(movePointZeroTwoMmButton);

						RadioButton pointOneButton = buttonFactory.GenerateRadioButton("0.1");
						pointOneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						pointOneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.1); };
						pointOneButton.SiblingRadioButtonList = radioList;
						moveRadioButtons.AddChild(pointOneButton);

						moveOneMmButton = buttonFactory.GenerateRadioButton("1");
						moveOneMmButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						moveOneMmButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(1); };
						moveOneMmButton.SiblingRadioButtonList = radioList;
						moveRadioButtons.AddChild(moveOneMmButton);

						tooBigForBabyStepping = new DisableableWidget()
						{
							VAnchor = VAnchor.FitToChildren,
							HAnchor = HAnchor.FitToChildren
						};

						var tooBigFlowLayout = new FlowLayoutWidget();
						tooBigForBabyStepping.AddChild(tooBigFlowLayout);

						tenButton = buttonFactory.GenerateRadioButton("10");
						tenButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(10); };
						tenButton.SiblingRadioButtonList = radioList;
						tooBigFlowLayout.AddChild(tenButton);

						oneHundredButton = buttonFactory.GenerateRadioButton("100");
						oneHundredButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(100); };
						oneHundredButton.SiblingRadioButtonList = radioList;
						tooBigFlowLayout.AddChild(oneHundredButton);

						moveRadioButtons.AddChild(tooBigForBabyStepping);

						tenButton.Checked = true;
						moveRadioButtons.Margin = new BorderDouble(0, 3);

						setMoveDistanceControl.AddChild(moveRadioButtons);

						TextWidget mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8);
						mmLabel.Margin = new BorderDouble(left: 10);
						mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;

						tooBigFlowLayout.AddChild(mmLabel);
					}

					setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.ParentLeft;
					xYZWithDistance.AddChild(setMoveDistanceControl);
				}

				allControlsLeftToRight.AddChild(xYZWithDistance);

#if !__ANDROID__
				allControlsLeftToRight.AddChild(GetHotkeyControlContainer());
#endif
				GuiWidget barBetweenZAndE = new GuiWidget(2, 2);
				barBetweenZAndE.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
				barBetweenZAndE.BackgroundColor = RGBA_Bytes.White;
				barBetweenZAndE.Margin = new BorderDouble(distanceBetweenControls, 5);
				allControlsLeftToRight.AddChild(barBetweenZAndE);

				FlowLayoutWidget eButtons = CreateEButtons(buttonSeparationDistance);
				disableableEButtons = new DisableableWidget()
				{
					Name = "disableableEButtons",
					HAnchor = HAnchor.FitToChildren,
					VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop,
				};
				disableableEButtons.AddChild(eButtons);

				allControlsLeftToRight.AddChild(disableableEButtons);
				allControlsTopToBottom.AddChild(allControlsLeftToRight);
			}

			this.AddChild(allControlsTopToBottom);
			this.HAnchor = HAnchor.FitToChildren;
			this.VAnchor = VAnchor.FitToChildren;

			Margin = new BorderDouble(3);

			// this.HAnchor |= HAnchor.ParentLeftRight;
		}

		internal void SetEnabledLevels(bool enableBabysteppingMode, bool enableEControls)
		{
			if (enableBabysteppingMode)
			{
				if (zPlusControl.MoveAmount >= 1)
				{
					movePointZeroTwoMmButton.Checked = true;
				}
			}
			else
			{
				if (zPlusControl.MoveAmount < 1)
				{
					moveOneMmButton.Checked = true;
				}
			}

			tenButton.Enabled = !enableBabysteppingMode;
			oneHundredButton.Enabled = !enableBabysteppingMode;

			disableableEButtons.SetEnableLevel(enableEControls ? DisableableWidget.EnableLevel.Enabled : DisableableWidget.EnableLevel.Disabled);
			tooBigForBabyStepping.SetEnableLevel(enableBabysteppingMode ? DisableableWidget.EnableLevel.Disabled : DisableableWidget.EnableLevel.Enabled);
		}

		private void SetEMoveAmount(int moveAmount)
		{
			foreach (ExtrudeButton extrudeButton in eMinusButtons)
			{
				extrudeButton.MoveAmount = -moveAmount;
				EAxisMoveAmount = moveAmount;
			}

			foreach (ExtrudeButton extrudeButton in ePlusButtons)
			{
				extrudeButton.MoveAmount = moveAmount;
				EAxisMoveAmount = moveAmount;
			}
		}

		private void SetXYZMoveAmount(double moveAmount)
		{
			xPlusControl.MoveAmount = moveAmount;
			xMinusControl.MoveAmount = -moveAmount;

			yPlusControl.MoveAmount = moveAmount;
			yMinusControl.MoveAmount = -moveAmount;

			zPlusControl.MoveAmount = moveAmount;
			zMinusControl.MoveAmount = -moveAmount;

			AxisMoveAmount = moveAmount;
		}

		private List<ExtrudeButton> eMinusButtons = new List<ExtrudeButton>();
		private List<ExtrudeButton> ePlusButtons = new List<ExtrudeButton>();
		private RadioButton oneHundredButton;
		private RadioButton tenButton;
		private DisableableWidget disableableEButtons;
		private DisableableWidget tooBigForBabyStepping;
		private RadioButton movePointZeroTwoMmButton;
		private RadioButton moveOneMmButton;
		GuiWidget keyboardFocusBorder;
		ImageWidget keyboardImage;

		private FlowLayoutWidget GetHotkeyControlContainer()
		{
			FlowLayoutWidget keyFocusedContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			keyFocusedContainer.HAnchor = HAnchor.FitToChildren;
			keyFocusedContainer.VAnchor = VAnchor.ParentBottomTop;
			keyFocusedContainer.ToolTipText = "Enable cursor keys for movement".Localize();
			keyFocusedContainer.Margin = new BorderDouble(left: 10);

			var image = StaticData.Instance.LoadIcon("hot_key_small_white.png", 19, 12);
			if(ActiveTheme.Instance.IsDarkTheme)
			{
				image = image.InvertLightness();
			}

			keyboardImage = new ImageWidget(image)
			{
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(5),
				Visible = false,
			};

			keyboardFocusBorder = new GuiWidget(1, 1)
			{
				MinimumSize = new Vector2(keyboardImage.Width + 5, keyboardImage.Height + 5),
			};

			keyboardFocusBorder.AddChild(keyboardImage);

			keyFocusedContainer.AddChild(keyboardFocusBorder);

			return keyFocusedContainer;
		}

// OnLoad overridden for keyboard and only applicable on non-Android builds
#if !__ANDROID__
		public override void OnLoad(EventArgs args)
		{
			var parents = keyboardFocusBorder.Parents<AltGroupBox>();

			parents.First().KeyDown += JogControls_KeyDown;

			parents.First().ContainsFocusChanged += (sender, e) =>
			{
				if ((sender as GuiWidget).ContainsFocus 
					&& !UserSettings.Instance.IsTouchScreen)
				{
					keyboardImage.Visible = true;
				}
				else
				{
					keyboardImage.Visible = false;
				}
			};

			base.OnLoad(args);
		}
#endif

		private void JogControls_KeyDown(object sender, KeyEventArgs e)
		{
			double moveAmountPositive = AxisMoveAmount;
			double moveAmountNegative = -AxisMoveAmount;
			int eMoveAmountPositive = EAxisMoveAmount;
			int eMoveAmountNegative = -EAxisMoveAmount;

			// if we are not printing and on mac or PC
			if (PrinterConnection.Instance.CommunicationState != PrinterConnection.CommunicationStates.Printing
				&& (OsInformation.OperatingSystem == OSType.Windows || OsInformation.OperatingSystem == OSType.Mac))
			{
				if (e.KeyCode == Keys.Z)
				{
					if (PrinterConnection.Instance.CommunicationState != PrinterConnection.CommunicationStates.Printing)
					{
						PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.Z);
					}
				}
				else if (e.KeyCode == Keys.Y)
				{
					PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.Y);
				}
				else if (e.KeyCode == Keys.X)
				{
					PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.X);
				}
				if (e.KeyCode == Keys.Home)
				{
					PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.XYZ);
				}
				else if (e.KeyCode == Keys.Left)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.X, moveAmountNegative, MovementControls.XSpeed);
				}
				else if (e.KeyCode == Keys.Right)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.X, moveAmountPositive, MovementControls.XSpeed);
				}
				else if (e.KeyCode == Keys.Up)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Y, moveAmountPositive, MovementControls.YSpeed);
				}
				else if (e.KeyCode == Keys.Down)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Y, moveAmountNegative, MovementControls.YSpeed);
				}
				else if (e.KeyCode == Keys.E)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.E, eMoveAmountPositive, MovementControls.EFeedRate(0));
				}
				else if (e.KeyCode == Keys.R)
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.E, eMoveAmountNegative, MovementControls.EFeedRate(0));
				}
			}

			if ((OsInformation.OperatingSystem == OSType.Windows && e.KeyCode == Keys.PageUp)
				|| (OsInformation.OperatingSystem == OSType.Mac && e.KeyCode == (Keys.Back | Keys.Cancel)))
			{
				if (PrinterConnection.Instance.CommunicationState == PrinterConnection.CommunicationStates.Printing)
				{
					var currentZ = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
					currentZ += moveAmountPositive;
					ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
				}
				else
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Z, moveAmountPositive, MovementControls.ZSpeed);
				}
			}
			else if ((OsInformation.OperatingSystem == OSType.Windows && e.KeyCode == Keys.PageDown)
				|| (OsInformation.OperatingSystem == OSType.Mac && e.KeyCode == Keys.Clear))
			{
				if (PrinterConnection.Instance.CommunicationState == PrinterConnection.CommunicationStates.Printing)
				{
					var currentZ = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
					currentZ += moveAmountNegative;
					ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
				}
				else
				{
					PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Z, moveAmountNegative, MovementControls.ZSpeed);
				}
			}
		}

		private FlowLayoutWidget CreateEButtons(double buttonSeparationDistance)
		{
			int extruderCount = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);

			FlowLayoutWidget eButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				FlowLayoutWidget eMinusButtonAndText = new FlowLayoutWidget();
				BorderDouble extrusionMargin = new BorderDouble(4, 0, 4, 0);

				if (extruderCount == 1)
				{
					ExtrudeButton eMinusControl = CreateExtrudeButton("E-", MovementControls.EFeedRate(0), 0, moveButtonFactory);
					eMinusControl.Margin = extrusionMargin;
					eMinusControl.ToolTipText = "Retract filament".Localize();
					eMinusButtonAndText.AddChild(eMinusControl);
					eMinusButtons.Add(eMinusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton eMinusControl = CreateExtrudeButton($"E{i + 1}-", MovementControls.EFeedRate(0), i, moveButtonFactory);
						eMinusControl.ToolTipText = "Retract filament".Localize();
						eMinusControl.Margin = extrusionMargin;
						eMinusButtonAndText.AddChild(eMinusControl);
						eMinusButtons.Add(eMinusControl);
					}
				}

				TextWidget eMinusControlLabel = new TextWidget("Retract".Localize(), pointSize: 11);
				eMinusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				eMinusControlLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
				eMinusButtonAndText.AddChild(eMinusControlLabel);
				eButtons.AddChild(eMinusButtonAndText);

				eMinusButtonAndText.HAnchor = HAnchor.FitToChildren;
				eMinusButtonAndText.VAnchor = VAnchor.FitToChildren;

				FlowLayoutWidget buttonSpacerContainer = new FlowLayoutWidget();
				for (int i = 0; i < extruderCount; i++)
				{
					GuiWidget eSpacer = new GuiWidget(2, buttonSeparationDistance);
					double buttonWidth = eMinusButtons[i].Width + 6;

					eSpacer.Margin = new BorderDouble((buttonWidth / 2), 0, ((buttonWidth) / 2), 0);
					eSpacer.BackgroundColor = XYZColors.eColor;
					buttonSpacerContainer.AddChild(eSpacer);
				}

				eButtons.AddChild(buttonSpacerContainer);

				buttonSpacerContainer.HAnchor = HAnchor.FitToChildren;
				buttonSpacerContainer.VAnchor = VAnchor.FitToChildren;

				FlowLayoutWidget ePlusButtonAndText = new FlowLayoutWidget();
				if (extruderCount == 1)
				{
					ExtrudeButton ePlusControl = CreateExtrudeButton("E+", MovementControls.EFeedRate(0), 0, moveButtonFactory);
					ePlusControl.Margin = extrusionMargin;
					ePlusControl.ToolTipText = "Extrude filament".Localize();
					ePlusButtonAndText.AddChild(ePlusControl);
					ePlusButtons.Add(ePlusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton ePlusControl = CreateExtrudeButton($"E{i + 1}+", MovementControls.EFeedRate(0), i, moveButtonFactory);
						ePlusControl.Margin = extrusionMargin;
						ePlusControl.ToolTipText = "Extrude filament".Localize();
						ePlusButtonAndText.AddChild(ePlusControl);
						ePlusButtons.Add(ePlusControl);
					}
				}

				TextWidget ePlusControlLabel = new TextWidget("Extrude".Localize(), pointSize: 11);
				ePlusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				ePlusControlLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
				ePlusButtonAndText.AddChild(ePlusControlLabel);
				eButtons.AddChild(ePlusButtonAndText);
				ePlusButtonAndText.HAnchor = HAnchor.FitToChildren;
				ePlusButtonAndText.VAnchor = VAnchor.FitToChildren;
			}

			eButtons.AddChild(new GuiWidget(10, 6));

			// add in some movement radio buttons
			FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
			TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
			buttonsLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
			//setMoveDistanceControl.AddChild(buttonsLabel);

			{
				var buttonFactory = new TextImageButtonFactory()
				{
					FixedHeight = 20 * GuiWidget.DeviceScale,
					FixedWidth = 30 * GuiWidget.DeviceScale,
					fontSize = 8,
					Margin = 0
				};
				buttonFactory.checkedBorderColor = ActiveTheme.Instance.PrimaryTextColor;

				var moveRadioButtons = new FlowLayoutWidget();
				RadioButton oneButton = buttonFactory.GenerateRadioButton("1");
				oneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
				oneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(1); };
				moveRadioButtons.AddChild(oneButton);
				RadioButton tenButton = buttonFactory.GenerateRadioButton("10");
				tenButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
				tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(10); };
				moveRadioButtons.AddChild(tenButton);
				RadioButton oneHundredButton = buttonFactory.GenerateRadioButton("100");
				oneHundredButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
				oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(100); };
				moveRadioButtons.AddChild(oneHundredButton);
				tenButton.Checked = true;
				moveRadioButtons.Margin = new BorderDouble(0, 3);
				setMoveDistanceControl.AddChild(moveRadioButtons);
			}

			var mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8);
			mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
			mmLabel.Margin = new BorderDouble(left: 10);
			setMoveDistanceControl.AddChild(mmLabel);
			setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.ParentLeft;
			eButtons.AddChild(setMoveDistanceControl);

			eButtons.HAnchor = HAnchor.FitToChildren;
			eButtons.VAnchor = VAnchor.FitToChildren | VAnchor.ParentBottom;

			return eButtons;
		}

		private static MoveButton CreateMoveButton(string label, PrinterConnection.Axis axis, double moveSpeed, bool levelingButtons, MoveButtonFactory buttonFactory)
		{
			var button = buttonFactory.GenerateMoveButton(label, axis, moveSpeed);
			button.VAnchor = VAnchor.AbsolutePosition;
			button.HAnchor = HAnchor.AbsolutePosition;
			button.Height = (levelingButtons ? 45 : 40) * GuiWidget.DeviceScale;
			button.Width = (levelingButtons ? 90 : 40) * GuiWidget.DeviceScale;

			return button;
		}

		private static ExtrudeButton CreateExtrudeButton(string label, double moveSpeed, int extruderNumber, MoveButtonFactory buttonFactory = null)
		{
			var button = buttonFactory.GenerateExtrudeButton(label, moveSpeed, extruderNumber);
			button.Height = 40 * GuiWidget.DeviceScale;
			button.Width = 40 * GuiWidget.DeviceScale;

			return button;
		}

		public static FlowLayoutWidget CreateZButtons(RGBA_Bytes color, double buttonSeparationDistance,
			out MoveButton zPlusControl, out MoveButton zMinusControl, bool levelingButtons = false)
		{
			FlowLayoutWidget zButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				MoveButtonFactory moveButtonFactory = new MoveButtonFactory();
				moveButtonFactory.Colors.Fill.Normal = color;

				zPlusControl = CreateMoveButton("Z+", PrinterConnection.Axis.Z, MovementControls.ZSpeed, levelingButtons, moveButtonFactory);
				zPlusControl.Name = "Move Z positive".Localize();
				zPlusControl.ToolTipText = "Move Z positive".Localize();
				zButtons.AddChild(zPlusControl);

				GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
				spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
				spacer.BackgroundColor = XYZColors.zColor;
				zButtons.AddChild(spacer);

				zMinusControl = CreateMoveButton("Z-", PrinterConnection.Axis.Z, MovementControls.ZSpeed, levelingButtons, moveButtonFactory);
				zMinusControl.ToolTipText = "Move Z negative".Localize();
				zButtons.AddChild(zMinusControl);
			}
			zButtons.Margin = new BorderDouble(0, 5);
			return zButtons;
		}

		private GuiWidget CreateXYGridControl(XYZColors colors, double distanceBetweenControls, double buttonSeparationDistance)
		{
			GuiWidget xyGrid = new GuiWidget();
			{
				FlowLayoutWidget xButtons = new FlowLayoutWidget();
				{
					moveButtonFactory.Colors.Fill.Normal = XYZColors.xColor;
					xButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
					xButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;

					xMinusControl = CreateMoveButton("X-", PrinterConnection.Axis.X, MovementControls.XSpeed, false, moveButtonFactory);
					xMinusControl.ToolTipText = "Move X negative".Localize();
					xButtons.AddChild(xMinusControl);

					GuiWidget spacer = new GuiWidget(xMinusControl.Width + buttonSeparationDistance * 2, 2);
					spacer.VAnchor = Agg.UI.VAnchor.ParentCenter;
					spacer.BackgroundColor = XYZColors.xColor;
					xButtons.AddChild(spacer);

					xPlusControl = CreateMoveButton("X+", PrinterConnection.Axis.X, MovementControls.XSpeed, false, moveButtonFactory);
					xPlusControl.ToolTipText = "Move X positive".Localize();
					xButtons.AddChild(xPlusControl);
				}
				xyGrid.AddChild(xButtons);

				FlowLayoutWidget yButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					moveButtonFactory.Colors.Fill.Normal = XYZColors.yColor;
					yButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
					yButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;
					yPlusControl = CreateMoveButton("Y+", PrinterConnection.Axis.Y, MovementControls.YSpeed, false, moveButtonFactory);
					yPlusControl.ToolTipText = "Move Y positive".Localize();
					yButtons.AddChild(yPlusControl);

					GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
					spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
					spacer.BackgroundColor = XYZColors.yColor;
					yButtons.AddChild(spacer);

					yMinusControl = CreateMoveButton("Y-", PrinterConnection.Axis.Y, MovementControls.YSpeed, false, moveButtonFactory);
					yMinusControl.ToolTipText = "Move Y negative".Localize();
					yButtons.AddChild(yMinusControl);
				}
				xyGrid.AddChild(yButtons);
			}
			xyGrid.HAnchor = HAnchor.FitToChildren;
			xyGrid.VAnchor = VAnchor.FitToChildren;
			xyGrid.VAnchor = Agg.UI.VAnchor.ParentBottom;
			xyGrid.Margin = new BorderDouble(0, 5, distanceBetweenControls, 5);
			return xyGrid;
		}

		public class MoveButton : Button
		{
			private PrinterConnection.Axis moveAxis;

			//Amounts in millimeters
			public double MoveAmount = 10;

			private double movementFeedRate;

			public MoveButton(double x, double y, GuiWidget buttonView, PrinterConnection.Axis axis, double movementFeedRate)
				: base(x, y, buttonView)
			{
				this.moveAxis = axis;
				this.movementFeedRate = movementFeedRate;

				this.Click += (s, e) =>
				{
					MoveButton moveButton = (MoveButton)s;

					if (PrinterConnection.Instance.CommunicationState == PrinterConnection.CommunicationStates.Printing)
					{
						if (moveAxis == PrinterConnection.Axis.Z) // only works on z
						{
							var currentZ = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
							currentZ += this.MoveAmount;
							ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
						}
					}
					else
					{
						PrinterConnection.Instance.MoveRelative(this.moveAxis, this.MoveAmount, movementFeedRate);
					}
				};
			}
		}

		public class ExtrudeButton : Button
		{
			//Amounts in millimeters
			public double MoveAmount = 10;

			private double movementFeedRate;
			public int ExtruderNumber;

			public ExtrudeButton(double x, double y, GuiWidget buttonView, double movementFeedRate, int extruderNumber)
				: base(x, y, buttonView)
			{
				this.ExtruderNumber = extruderNumber;
				this.movementFeedRate = movementFeedRate;
			}

			public override void OnClick(MouseEventArgs mouseEvent)
			{
				base.OnClick(mouseEvent);

				//Add more fancy movement here
				PrinterConnection.Instance.MoveExtruderRelative(MoveAmount, movementFeedRate, ExtruderNumber);
			}
		}

		public class MoveButtonWidget : GuiWidget
		{
			public double BorderWidth { get; set; } = 1;

			private RGBA_Bytes borderColor;
			private Stroke borderStroke = null;

			public MoveButtonWidget(string label, RGBA_Bytes textColor, double fontSize = 12)
			{
				this.Margin = 0;
				this.Padding = 0;
				this.borderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

				this.AnchorAll();

				if (label != "")
				{
					TextWidget textWidget = new TextWidget(label, pointSize: fontSize)
					{
						VAnchor = VAnchor.ParentCenter,
						HAnchor = HAnchor.ParentCenter,
						TextColor = textColor,
						Padding = new BorderDouble(3, 0)
					};
					this.AddChild(textWidget);
				}
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				borderStroke = new Stroke(
					new RoundedRect(LocalBounds, 0),
					BorderWidth);

				base.OnBoundsChanged(e);
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				if (this.BorderWidth > 0 && borderStroke != null)
				{
					graphics2D.Render(borderStroke, borderColor);
				}
			}
		}

		public class WidgetStateColors
		{
			public RGBA_Bytes Normal { get; set; }
			public RGBA_Bytes Hover { get; set; }
			public RGBA_Bytes Pressed { get; set; }
			public RGBA_Bytes Disabled { get; set; }
		}

		public class WidgetColors
		{
			public WidgetStateColors Fill { get; set; }
			public WidgetStateColors Text { get; set; }
		}

		public class MoveButtonFactory
		{
			public BorderDouble Padding;
			public BorderDouble Margin;

			public WidgetColors Colors { get; set; } = new WidgetColors()
			{
				Text = new WidgetStateColors()
				{
					Normal = RGBA_Bytes.Black,
					Hover = RGBA_Bytes.White,
					Pressed = RGBA_Bytes.White,
					Disabled = RGBA_Bytes.White
				},
				Fill = new WidgetStateColors()
				{
					Normal = RGBA_Bytes.White,
					Hover = new RGBA_Bytes(0, 0, 0, 50),
					Pressed = RGBA_Bytes.Transparent,
					Disabled = new RGBA_Bytes(255, 255, 255, 50)
				}
			};

			public double FontSize { get; set; } = 12;

			public double BorderWidth { get; set; } = 1;

			public MoveButton GenerateMoveButton(string label, PrinterConnection.Axis axis, double movementFeedRate)
			{
				//Create button based on view container widget
				return new MoveButton(0, 0, GetButtonView(label), axis, movementFeedRate)
				{
					Margin = 0,
					Padding = 0
				};
			}

			public ExtrudeButton GenerateExtrudeButton(string label, double movementFeedRate, int extruderNumber)
			{
				//Create button based on view container widget
				return new ExtrudeButton(0, 0, GetButtonView(label), movementFeedRate, extruderNumber)
				{
					Margin = 0,
					Padding = 0
				};
			}

			private ButtonViewStates GetButtonView(string label)
			{
				//Create the multi-state button view
				var buttonViewStates = new ButtonViewStates(
					new MoveButtonWidget(label, Colors.Text.Normal)
					{
						BackgroundColor = Colors.Fill.Normal,
						BorderWidth = this.BorderWidth
					},
					new MoveButtonWidget(label, Colors.Text.Hover)
					{
						BackgroundColor = Colors.Fill.Hover,
						BorderWidth = this.BorderWidth
					},
					new MoveButtonWidget(label, Colors.Text.Pressed)
					{
						BackgroundColor = Colors.Fill.Pressed,
						BorderWidth = this.BorderWidth
					},
					new MoveButtonWidget(label, Colors.Text.Disabled)
					{
						BackgroundColor = Colors.Fill.Disabled,
						BorderWidth = this.BorderWidth
					}
				);

				buttonViewStates.HAnchor = HAnchor.ParentLeftRight;
				buttonViewStates.VAnchor = VAnchor.ParentBottomTop;

				return buttonViewStates;
			}
		}
	}
}