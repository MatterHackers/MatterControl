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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
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
		private PrinterConfig printer;

		public JogControls(PrinterConfig printer, XYZColors colors)
		{
			this.printer = printer;
			moveButtonFactory.Colors.Text.Normal = RGBA_Bytes.Black;

			double distanceBetweenControls = 12;
			double buttonSeparationDistance = 10;

			FlowLayoutWidget allControlsTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);

			allControlsTopToBottom.HAnchor |= Agg.UI.HAnchor.Stretch;

			{
				FlowLayoutWidget allControlsLeftToRight = new FlowLayoutWidget();

				FlowLayoutWidget xYZWithDistance = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					FlowLayoutWidget xYZControls = new FlowLayoutWidget();
					{
						GuiWidget xyGrid = CreateXYGridControl(colors, distanceBetweenControls, buttonSeparationDistance);
						xYZControls.AddChild(xyGrid);

						FlowLayoutWidget zButtons = CreateZButtons(printer, XYZColors.zColor, buttonSeparationDistance, out zPlusControl, out zMinusControl);
						zButtons.VAnchor = Agg.UI.VAnchor.Bottom;
						xYZControls.AddChild(zButtons);
						xYZWithDistance.AddChild(xYZControls);
					}

					// add in some movement radio buttons
					FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
					TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
					buttonsLabel.VAnchor = Agg.UI.VAnchor.Center;
					//setMoveDistanceControl.AddChild(buttonsLabel);

					{
						var buttonFactory = ApplicationController.Instance.Theme.MicroButton;

						FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();

						var radioList = new ObservableCollection<GuiWidget>();

						movePointZeroTwoMmButton = buttonFactory.GenerateRadioButton("0.02");
						movePointZeroTwoMmButton.VAnchor = Agg.UI.VAnchor.Center;
						movePointZeroTwoMmButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.02); };
						movePointZeroTwoMmButton.SiblingRadioButtonList = radioList;
						radioList.Add(movePointZeroTwoMmButton);
						moveRadioButtons.AddChild(movePointZeroTwoMmButton);

						RadioButton pointOneButton = buttonFactory.GenerateRadioButton("0.1");
						pointOneButton.VAnchor = Agg.UI.VAnchor.Center;
						pointOneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.1); };
						pointOneButton.SiblingRadioButtonList = radioList;
						radioList.Add(pointOneButton);
						moveRadioButtons.AddChild(pointOneButton);

						moveOneMmButton = buttonFactory.GenerateRadioButton("1");
						moveOneMmButton.VAnchor = Agg.UI.VAnchor.Center;
						moveOneMmButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(1); };
						moveOneMmButton.SiblingRadioButtonList = radioList;
						radioList.Add(moveOneMmButton);

						moveRadioButtons.AddChild(moveOneMmButton);

						tooBigForBabyStepping = new DisableableWidget()
						{
							VAnchor = VAnchor.Fit,
							HAnchor = HAnchor.Fit
						};

						var tooBigFlowLayout = new FlowLayoutWidget();
						tooBigForBabyStepping.AddChild(tooBigFlowLayout);

						tenButton = buttonFactory.GenerateRadioButton("10");
						tenButton.VAnchor = Agg.UI.VAnchor.Center;
						tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(10); };
						tenButton.SiblingRadioButtonList = radioList;
						radioList.Add(tenButton);

						tooBigFlowLayout.AddChild(tenButton);

						oneHundredButton = buttonFactory.GenerateRadioButton("100");
						oneHundredButton.VAnchor = Agg.UI.VAnchor.Center;
						oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(100); };
						oneHundredButton.SiblingRadioButtonList = radioList;
						radioList.Add(oneHundredButton);

						tooBigFlowLayout.AddChild(oneHundredButton);

						moveRadioButtons.AddChild(tooBigForBabyStepping);

						tenButton.Checked = true;
						moveRadioButtons.Margin = new BorderDouble(0, 3);

						setMoveDistanceControl.AddChild(moveRadioButtons);

						tooBigFlowLayout.AddChild(new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8)
						{
							Margin = new BorderDouble(left: 10),
							VAnchor = Agg.UI.VAnchor.Center
						});
					}

					setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.Left;
					xYZWithDistance.AddChild(setMoveDistanceControl);
				}

				allControlsLeftToRight.AddChild(xYZWithDistance);

#if !__ANDROID__
				allControlsLeftToRight.AddChild(GetHotkeyControlContainer());
#endif
				GuiWidget barBetweenZAndE = new GuiWidget(2, 2);
				barBetweenZAndE.VAnchor = Agg.UI.VAnchor.Stretch;
				barBetweenZAndE.BackgroundColor = RGBA_Bytes.White;
				barBetweenZAndE.Margin = new BorderDouble(distanceBetweenControls, 5);
				allControlsLeftToRight.AddChild(barBetweenZAndE);

				FlowLayoutWidget eButtons = CreateEButtons(buttonSeparationDistance);
				disableableEButtons = new DisableableWidget()
				{
					Name = "disableableEButtons",
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit | VAnchor.Top,
				};
				disableableEButtons.AddChild(eButtons);

				allControlsLeftToRight.AddChild(disableableEButtons);
				allControlsTopToBottom.AddChild(allControlsLeftToRight);
			}

			this.AddChild(allControlsTopToBottom);
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			Margin = new BorderDouble(3);

			// this.HAnchor |= HAnchor.Stretch;
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
			keyFocusedContainer.HAnchor = HAnchor.Fit;
			keyFocusedContainer.VAnchor = VAnchor.Stretch;
			keyFocusedContainer.ToolTipText = "Enable cursor keys for movement".Localize();
			keyFocusedContainer.Margin = new BorderDouble(left: 10);

			keyboardImage = new ImageWidget(AggContext.StaticData.LoadIcon("hot_key_small_white.png", 19, 12, IconColor.Theme))
			{
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
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
			if (printer.Connection.CommunicationState != CommunicationStates.Printing
				&& (AggContext.OperatingSystem == OSType.Windows || AggContext.OperatingSystem == OSType.Mac))
			{
				if (e.KeyCode == Keys.Z)
				{
					if (printer.Connection.CommunicationState != CommunicationStates.Printing)
					{
						printer.Connection.HomeAxis(PrinterConnection.Axis.Z);
					}
				}
				else if (e.KeyCode == Keys.Y)
				{
					printer.Connection.HomeAxis(PrinterConnection.Axis.Y);
				}
				else if (e.KeyCode == Keys.X)
				{
					printer.Connection.HomeAxis(PrinterConnection.Axis.X);
				}
				if (e.KeyCode == Keys.Home)
				{
					printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
				}
				else if (e.KeyCode == Keys.Left)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.X, moveAmountNegative, printer.Settings.XSpeed());
				}
				else if (e.KeyCode == Keys.Right)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.X, moveAmountPositive, printer.Settings.XSpeed());
				}
				else if (e.KeyCode == Keys.Up)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.Y, moveAmountPositive, printer.Settings.YSpeed());
				}
				else if (e.KeyCode == Keys.Down)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.Y, moveAmountNegative, printer.Settings.YSpeed());
				}
				else if (e.KeyCode == Keys.E)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.E, eMoveAmountPositive, printer.Settings.EFeedRate(0));
				}
				else if (e.KeyCode == Keys.R)
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.E, eMoveAmountNegative, printer.Settings.EFeedRate(0));
				}
			}

			if ((AggContext.OperatingSystem == OSType.Windows && e.KeyCode == Keys.PageUp)
				|| (AggContext.OperatingSystem == OSType.Mac && e.KeyCode == (Keys.Back | Keys.Cancel)))
			{
				if (printer.Connection.CommunicationState == CommunicationStates.Printing)
				{
					var currentZ = printer.Settings.GetValue<double>(SettingsKey.baby_step_z_offset);
					currentZ += moveAmountPositive;
					printer.Settings.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
				}
				else
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.Z, moveAmountPositive, printer.Settings.ZSpeed());
				}
			}
			else if ((AggContext.OperatingSystem == OSType.Windows && e.KeyCode == Keys.PageDown)
				|| (AggContext.OperatingSystem == OSType.Mac && e.KeyCode == Keys.Clear))
			{
				if (printer.Connection.CommunicationState == CommunicationStates.Printing)
				{
					var currentZ = printer.Settings.GetValue<double>(SettingsKey.baby_step_z_offset);
					currentZ += moveAmountNegative;
					printer.Settings.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
				}
				else
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.Z, moveAmountNegative, printer.Settings.ZSpeed());
				}
			}
		}

		private FlowLayoutWidget CreateEButtons(double buttonSeparationDistance)
		{
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			FlowLayoutWidget eButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				FlowLayoutWidget eMinusButtonAndText = new FlowLayoutWidget();
				BorderDouble extrusionMargin = new BorderDouble(4, 0, 4, 0);

				if (extruderCount == 1)
				{
					ExtrudeButton eMinusControl = CreateExtrudeButton(printer, "E-", printer.Settings.EFeedRate(0), 0, moveButtonFactory);
					eMinusControl.Margin = extrusionMargin;
					eMinusControl.ToolTipText = "Retract filament".Localize();
					eMinusButtonAndText.AddChild(eMinusControl);
					eMinusButtons.Add(eMinusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton eMinusControl = CreateExtrudeButton(printer, $"E{i + 1}-", printer.Settings.EFeedRate(0), i, moveButtonFactory);
						eMinusControl.ToolTipText = "Retract filament".Localize();
						eMinusControl.Margin = extrusionMargin;
						eMinusButtonAndText.AddChild(eMinusControl);
						eMinusButtons.Add(eMinusControl);
					}
				}

				TextWidget eMinusControlLabel = new TextWidget("Retract".Localize(), pointSize: 11);
				eMinusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				eMinusControlLabel.VAnchor = Agg.UI.VAnchor.Center;
				eMinusButtonAndText.AddChild(eMinusControlLabel);
				eButtons.AddChild(eMinusButtonAndText);

				eMinusButtonAndText.HAnchor = HAnchor.Fit;
				eMinusButtonAndText.VAnchor = VAnchor.Fit;

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

				buttonSpacerContainer.HAnchor = HAnchor.Fit;
				buttonSpacerContainer.VAnchor = VAnchor.Fit;

				FlowLayoutWidget ePlusButtonAndText = new FlowLayoutWidget();
				if (extruderCount == 1)
				{
					ExtrudeButton ePlusControl = CreateExtrudeButton(printer, "E+", printer.Settings.EFeedRate(0), 0, moveButtonFactory);
					ePlusControl.Margin = extrusionMargin;
					ePlusControl.ToolTipText = "Extrude filament".Localize();
					ePlusButtonAndText.AddChild(ePlusControl);
					ePlusButtons.Add(ePlusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton ePlusControl = CreateExtrudeButton(printer, $"E{i + 1}+", printer.Settings.EFeedRate(0), i, moveButtonFactory);
						ePlusControl.Margin = extrusionMargin;
						ePlusControl.ToolTipText = "Extrude filament".Localize();
						ePlusButtonAndText.AddChild(ePlusControl);
						ePlusButtons.Add(ePlusControl);
					}
				}

				TextWidget ePlusControlLabel = new TextWidget("Extrude".Localize(), pointSize: 11);
				ePlusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				ePlusControlLabel.VAnchor = Agg.UI.VAnchor.Center;
				ePlusButtonAndText.AddChild(ePlusControlLabel);
				eButtons.AddChild(ePlusButtonAndText);
				ePlusButtonAndText.HAnchor = HAnchor.Fit;
				ePlusButtonAndText.VAnchor = VAnchor.Fit;
			}

			eButtons.AddChild(new GuiWidget(10, 6));

			// add in some movement radio buttons
			FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
			TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
			buttonsLabel.VAnchor = Agg.UI.VAnchor.Center;
			//setMoveDistanceControl.AddChild(buttonsLabel);

			{
				var buttonFactory = ApplicationController.Instance.Theme.MicroButton;
				
				var moveRadioButtons = new FlowLayoutWidget();
				RadioButton oneButton = buttonFactory.GenerateRadioButton("1");
				oneButton.VAnchor = Agg.UI.VAnchor.Center;
				oneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(1); };
				moveRadioButtons.AddChild(oneButton);
				RadioButton tenButton = buttonFactory.GenerateRadioButton("10");
				tenButton.VAnchor = Agg.UI.VAnchor.Center;
				tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(10); };
				moveRadioButtons.AddChild(tenButton);
				RadioButton oneHundredButton = buttonFactory.GenerateRadioButton("100");
				oneHundredButton.VAnchor = Agg.UI.VAnchor.Center;
				oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(100); };
				moveRadioButtons.AddChild(oneHundredButton);
				tenButton.Checked = true;
				moveRadioButtons.Margin = new BorderDouble(0, 3);
				setMoveDistanceControl.AddChild(moveRadioButtons);
			}

			var mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8);
			mmLabel.VAnchor = Agg.UI.VAnchor.Center;
			mmLabel.Margin = new BorderDouble(left: 10);
			setMoveDistanceControl.AddChild(mmLabel);
			setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.Left;
			eButtons.AddChild(setMoveDistanceControl);

			eButtons.HAnchor = HAnchor.Fit;
			eButtons.VAnchor = VAnchor.Fit | VAnchor.Bottom;

			return eButtons;
		}

		private static MoveButton CreateMoveButton(PrinterConfig printer, string label, PrinterConnection.Axis axis, double moveSpeed, bool levelingButtons, MoveButtonFactory buttonFactory)
		{
			var button = buttonFactory.GenerateMoveButton(printer, label, axis, moveSpeed);
			button.VAnchor = VAnchor.Absolute;
			button.HAnchor = HAnchor.Absolute;
			button.Height = (levelingButtons ? 45 : 40) * GuiWidget.DeviceScale;
			button.Width = (levelingButtons ? 90 : 40) * GuiWidget.DeviceScale;

			return button;
		}

		private static ExtrudeButton CreateExtrudeButton(PrinterConfig printer, string label, double moveSpeed, int extruderNumber, MoveButtonFactory buttonFactory = null)
		{
			var button = buttonFactory.GenerateExtrudeButton(printer, label, moveSpeed, extruderNumber);
			button.Height = 40 * GuiWidget.DeviceScale;
			button.Width = 40 * GuiWidget.DeviceScale;

			return button;
		}

		public static FlowLayoutWidget CreateZButtons(PrinterConfig printer, RGBA_Bytes color, double buttonSeparationDistance,
			out MoveButton zPlusControl, out MoveButton zMinusControl, bool levelingButtons = false)
		{
			FlowLayoutWidget zButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				MoveButtonFactory moveButtonFactory = new MoveButtonFactory();
				moveButtonFactory.Colors.Fill.Normal = color;

				zPlusControl = CreateMoveButton(printer, "Z+", PrinterConnection.Axis.Z, printer.Settings.ZSpeed(), levelingButtons, moveButtonFactory);
				zPlusControl.Name = "Move Z positive".Localize();
				zPlusControl.ToolTipText = "Move Z positive".Localize();
				zButtons.AddChild(zPlusControl);

				GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
				spacer.HAnchor = Agg.UI.HAnchor.Center;
				spacer.BackgroundColor = XYZColors.zColor;
				zButtons.AddChild(spacer);

				zMinusControl = CreateMoveButton(printer, "Z-", PrinterConnection.Axis.Z, printer.Settings.ZSpeed(), levelingButtons, moveButtonFactory);
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
					xButtons.HAnchor |= Agg.UI.HAnchor.Center;
					xButtons.VAnchor |= Agg.UI.VAnchor.Center;

					xMinusControl = CreateMoveButton(printer, "X-", PrinterConnection.Axis.X, printer.Settings.XSpeed(), false, moveButtonFactory);
					xMinusControl.ToolTipText = "Move X negative".Localize();
					xButtons.AddChild(xMinusControl);

					GuiWidget spacer = new GuiWidget(xMinusControl.Width + buttonSeparationDistance * 2, 2);
					spacer.VAnchor = Agg.UI.VAnchor.Center;
					spacer.BackgroundColor = XYZColors.xColor;
					xButtons.AddChild(spacer);

					xPlusControl = CreateMoveButton(printer, "X+", PrinterConnection.Axis.X, printer.Settings.XSpeed(), false, moveButtonFactory);
					xPlusControl.ToolTipText = "Move X positive".Localize();
					xButtons.AddChild(xPlusControl);
				}
				xyGrid.AddChild(xButtons);

				FlowLayoutWidget yButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					moveButtonFactory.Colors.Fill.Normal = XYZColors.yColor;
					yButtons.HAnchor |= Agg.UI.HAnchor.Center;
					yButtons.VAnchor |= Agg.UI.VAnchor.Center;
					yPlusControl = CreateMoveButton(printer, "Y+", PrinterConnection.Axis.Y, printer.Settings.YSpeed(), false, moveButtonFactory);
					yPlusControl.ToolTipText = "Move Y positive".Localize();
					yButtons.AddChild(yPlusControl);

					GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
					spacer.HAnchor = Agg.UI.HAnchor.Center;
					spacer.BackgroundColor = XYZColors.yColor;
					yButtons.AddChild(spacer);

					yMinusControl = CreateMoveButton(printer, "Y-", PrinterConnection.Axis.Y, printer.Settings.YSpeed(), false, moveButtonFactory);
					yMinusControl.ToolTipText = "Move Y negative".Localize();
					yButtons.AddChild(yMinusControl);
				}
				xyGrid.AddChild(yButtons);
			}
			xyGrid.HAnchor = HAnchor.Fit;
			xyGrid.VAnchor = VAnchor.Fit;
			xyGrid.VAnchor = Agg.UI.VAnchor.Bottom;
			xyGrid.Margin = new BorderDouble(0, 5, distanceBetweenControls, 5);
			return xyGrid;
		}

		public class MoveButton : Button
		{
			private PrinterConnection.Axis moveAxis;

			//Amounts in millimeters
			public double MoveAmount = 10;

			private double movementFeedRate;
			private PrinterConfig printer;

			public MoveButton(PrinterConfig printer, double x, double y, GuiWidget buttonView, PrinterConnection.Axis axis, double movementFeedRate)
				: base(x, y, buttonView)
			{
				this.printer = printer;
				this.moveAxis = axis;
				this.movementFeedRate = movementFeedRate;

				this.Click += (s, e) =>
				{
					MoveButton moveButton = (MoveButton)s;

					if (printer.Connection.CommunicationState == CommunicationStates.Printing)
					{
						if (moveAxis == PrinterConnection.Axis.Z) // only works on z
						{
							var currentZ = printer.Settings.GetValue<double>(SettingsKey.baby_step_z_offset);
							currentZ += this.MoveAmount;
							printer.Settings.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
						}
					}
					else
					{
						printer.Connection.MoveRelative(this.moveAxis, this.MoveAmount, movementFeedRate);
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

			private PrinterConfig printer;

			public ExtrudeButton(PrinterConfig printer, double x, double y, GuiWidget buttonView, double movementFeedRate, int extruderNumber)
				: base(x, y, buttonView)
			{
				this.printer = printer;
				this.ExtruderNumber = extruderNumber;
				this.movementFeedRate = movementFeedRate;
			}

			public override void OnClick(MouseEventArgs mouseEvent)
			{
				base.OnClick(mouseEvent);

				//Add more fancy movement here
				printer.Connection.MoveExtruderRelative(MoveAmount, movementFeedRate, ExtruderNumber);
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
						VAnchor = VAnchor.Center,
						HAnchor = HAnchor.Center,
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

			public MoveButton GenerateMoveButton(PrinterConfig printer, string label, PrinterConnection.Axis axis, double movementFeedRate)
			{
				//Create button based on view container widget
				return new MoveButton(printer, 0, 0, GetButtonView(label), axis, movementFeedRate)
				{
					Margin = 0,
					Padding = 0
				};
			}

			public ExtrudeButton GenerateExtrudeButton(PrinterConfig printer, string label, double movementFeedRate, int extruderNumber)
			{
				//Create button based on view container widget
				return new ExtrudeButton(printer, 0, 0, GetButtonView(label), movementFeedRate, extruderNumber)
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

				buttonViewStates.HAnchor = HAnchor.Stretch;
				buttonViewStates.VAnchor = VAnchor.Stretch;

				return buttonViewStates;
			}
		}
	}
}