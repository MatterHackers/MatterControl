﻿/*
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;

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
		public bool hotKeysEnabled = false;
		TextImageButtonFactory hotKeyButtonFactory = new TextImageButtonFactory();

		private MoveButtonFactory moveButtonFactory = new MoveButtonFactory();

		public JogControls(XYZColors colors)
		{
			moveButtonFactory.normalTextColor = RGBA_Bytes.Black;

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

					this.KeyDown += (sender, e) =>
				{

					double moveAmountPositive = AxisMoveAmount;
					double moveAmountNegative = -AxisMoveAmount;
					int eMoveAmountPositive = EAxisMoveAmount;
					int eMoveAmountNegative = -EAxisMoveAmount;


					if (e.KeyCode == Keys.Home && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.XYZ);
					}
					else if (e.KeyCode == Keys.Z && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Z);
					}
					else if (e.KeyCode == Keys.Y && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Y);
					}
					else if (e.KeyCode == Keys.X && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.X);
					}
					else if (e.KeyCode == Keys.Left && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.X, moveAmountNegative, MovementControls.XSpeed);
					}
					else if (e.KeyCode == Keys.Right && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.X, moveAmountPositive, MovementControls.XSpeed);
					}
					else if (e.KeyCode == Keys.Up && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Y, moveAmountPositive, MovementControls.YSpeed);
					}
					else if (e.KeyCode == Keys.Down && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Y, moveAmountNegative, MovementControls.YSpeed);
					}
					else if (e.KeyCode == Keys.PageUp && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, moveAmountPositive, MovementControls.ZSpeed);
					}
					else if (e.KeyCode == Keys.PageDown && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, moveAmountNegative, MovementControls.ZSpeed);
					}
					else if (e.KeyCode == Keys.E && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.E, eMoveAmountPositive, MovementControls.EFeedRate(0));

					}
					else if (e.KeyCode == Keys.R && hotKeysEnabled)
					{
						PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.E, eMoveAmountNegative, MovementControls.EFeedRate(0));
					}
				};

					// add in some movement radio buttons
					FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
					TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
					buttonsLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
					//setMoveDistanceControl.AddChild(buttonsLabel);

					{
						TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
						buttonFactory.FixedHeight = 20 * TextWidget.GlobalPointSizeScaleRatio;
						buttonFactory.FixedWidth = 30 * TextWidget.GlobalPointSizeScaleRatio;
						buttonFactory.fontSize = 8;
						buttonFactory.Margin = new BorderDouble(0);
						buttonFactory.checkedBorderColor = ActiveTheme.Instance.PrimaryTextColor;

						FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();

						RadioButton pointOneButton = buttonFactory.GenerateRadioButton("0.1");
						pointOneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						pointOneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.1); };
						moveRadioButtons.AddChild(pointOneButton);

						RadioButton oneButton = buttonFactory.GenerateRadioButton("1");
						oneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						oneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(1); };
						moveRadioButtons.AddChild(oneButton);

						RadioButton tenButton = buttonFactory.GenerateRadioButton("10");
						tenButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(10); };
						moveRadioButtons.AddChild(tenButton);

						RadioButton oneHundredButton = buttonFactory.GenerateRadioButton("100");
						oneHundredButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
						oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(100); };
						moveRadioButtons.AddChild(oneHundredButton);

						tenButton.Checked = true;
						moveRadioButtons.Margin = new BorderDouble(0, 3);
						setMoveDistanceControl.AddChild(moveRadioButtons);
					}

					TextWidget mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8);
					mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
					setMoveDistanceControl.AddChild(mmLabel);
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

				//moveButtonFactory.normalFillColor = XYZColors.eColor;

				FlowLayoutWidget eButtons = CreateEButtons(buttonSeparationDistance);
				eButtons.VAnchor |= Agg.UI.VAnchor.ParentTop;
				allControlsLeftToRight.AddChild(eButtons);

			
				allControlsTopToBottom.AddChild(allControlsLeftToRight);

			}
			
			this.AddChild(allControlsTopToBottom);
			this.HAnchor = HAnchor.FitToChildren;
			this.VAnchor = VAnchor.FitToChildren;

			Margin = new BorderDouble(3);

			// this.HAnchor |= HAnchor.ParentLeftRight;

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

		private FlowLayoutWidget GetHotkeyControlContainer()
		{

			TextImageButtonFactory hotKeyButtonFactory = new TextImageButtonFactory();
			hotKeyButtonFactory.FixedHeight = 20 * TextWidget.GlobalPointSizeScaleRatio;
			hotKeyButtonFactory.FixedWidth = 30 * TextWidget.GlobalPointSizeScaleRatio;
			hotKeyButtonFactory.fontSize = 8;

			hotKeyButtonFactory.checkedBorderColor = ActiveTheme.Instance.PrimaryTextColor;

			FlowLayoutWidget hotkeyControlContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			hotkeyControlContainer.HAnchor = HAnchor.FitToChildren;
			hotkeyControlContainer.VAnchor = VAnchor.ParentBottomTop;
			hotkeyControlContainer.ToolTipText = "Enable cursor keys for movement";
			hotkeyControlContainer.Margin = new BorderDouble(left: 10);

			RadioButton hotKeyButton = hotKeyButtonFactory.GenerateRadioButton("", "hot_key_small.png");
			hotKeyButton.Margin = new BorderDouble(5);
			hotKeyButton.Click += (sender, e) =>
			{
				if (hotKeysEnabled)
				{
					hotKeysEnabled = false;
					hotKeyButton.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
					hotKeyButton.Checked = false;
				}
				else if (!hotKeysEnabled)
				{
					hotKeysEnabled = true;
					hotKeyButton.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
					hotKeyButton.Checked = true;
				}

			};

			hotkeyControlContainer.AddChild(hotKeyButton);

			return hotkeyControlContainer;

		}

		private FlowLayoutWidget CreateEButtons(double buttonSeparationDistance)
		{
			int extruderCount = ActiveSliceSettings.Instance.ExtruderCount;

			FlowLayoutWidget eButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				FlowLayoutWidget eMinusButtonAndText = new FlowLayoutWidget();
				BorderDouble extrusionMargin = new BorderDouble(4, 0, 4, 0);

				if (extruderCount == 1)
				{
					ExtrudeButton eMinusControl = moveButtonFactory.Generate("E-", MovementControls.EFeedRate(0), 0);
					eMinusControl.Margin = extrusionMargin;
					eMinusControl.ToolTipText = "Retract filament";
					eMinusButtonAndText.AddChild(eMinusControl);
					eMinusButtons.Add(eMinusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton eMinusControl = moveButtonFactory.Generate(string.Format("E{0}-", i + 1), MovementControls.EFeedRate(0), i);
						eMinusControl.ToolTipText = "Retract filament";
						eMinusControl.Margin = extrusionMargin;
						eMinusButtonAndText.AddChild(eMinusControl);
						eMinusButtons.Add(eMinusControl);
					}
				}

				TextWidget eMinusControlLabel = new TextWidget(LocalizedString.Get("Retract"), pointSize: 11);
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
					ExtrudeButton ePlusControl = moveButtonFactory.Generate("E+", MovementControls.EFeedRate(0), 0);
					ePlusControl.Margin = extrusionMargin;
					ePlusControl.ToolTipText = "Extrude filament";
					ePlusButtonAndText.AddChild(ePlusControl);
					ePlusButtons.Add(ePlusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton ePlusControl = moveButtonFactory.Generate(string.Format("E{0}+", i + 1), MovementControls.EFeedRate(0), i);
						ePlusControl.Margin = extrusionMargin;
						ePlusControl.ToolTipText = "Extrude filament";
						ePlusButtonAndText.AddChild(ePlusControl);
						ePlusButtons.Add(ePlusControl);
					}
				}

				TextWidget ePlusControlLabel = new TextWidget(LocalizedString.Get("Extrude"), pointSize: 11);
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
				TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
				buttonFactory.FixedHeight = 20 * TextWidget.GlobalPointSizeScaleRatio;
				buttonFactory.FixedWidth = 30 * TextWidget.GlobalPointSizeScaleRatio;
				buttonFactory.fontSize = 8;
				buttonFactory.Margin = new BorderDouble(0);
				buttonFactory.checkedBorderColor = ActiveTheme.Instance.PrimaryTextColor;

				FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();
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

			TextWidget mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 8);
			mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
			setMoveDistanceControl.AddChild(mmLabel);
			setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.ParentLeft;
			eButtons.AddChild(setMoveDistanceControl);

			eButtons.HAnchor = HAnchor.FitToChildren;
			eButtons.VAnchor = VAnchor.FitToChildren | VAnchor.ParentBottom;

			return eButtons;
		}

		public static FlowLayoutWidget CreateZButtons(RGBA_Bytes color, double buttonSeparationDistance,
			out MoveButton zPlusControl, out MoveButton zMinusControl)
		{
			FlowLayoutWidget zButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				MoveButtonFactory moveButtonFactory = new MoveButtonFactory();
				moveButtonFactory.normalFillColor = color;
				zPlusControl = moveButtonFactory.Generate("Z+", PrinterConnectionAndCommunication.Axis.Z, MovementControls.ZSpeed);
				zPlusControl.ToolTipText = "Move Z positive";
				zButtons.AddChild(zPlusControl);

				GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
				spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
				spacer.BackgroundColor = XYZColors.zColor;
				zButtons.AddChild(spacer);

				zMinusControl = moveButtonFactory.Generate("Z-", PrinterConnectionAndCommunication.Axis.Z, MovementControls.ZSpeed);
				zMinusControl.ToolTipText = "Move Z negative";
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
					moveButtonFactory.normalFillColor = XYZColors.xColor;
					xButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
					xButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;
					xMinusControl = moveButtonFactory.Generate("X-", PrinterConnectionAndCommunication.Axis.X, MovementControls.XSpeed);
					xMinusControl.ToolTipText = "Move X negative";
					xButtons.AddChild(xMinusControl);

					GuiWidget spacer = new GuiWidget(xMinusControl.Width + buttonSeparationDistance * 2, 2);
					spacer.VAnchor = Agg.UI.VAnchor.ParentCenter;
					spacer.BackgroundColor = XYZColors.xColor;
					xButtons.AddChild(spacer);

					xPlusControl = moveButtonFactory.Generate("X+", PrinterConnectionAndCommunication.Axis.X, MovementControls.XSpeed);
					xPlusControl.ToolTipText = "Move X positive";
					xButtons.AddChild(xPlusControl);
				}
				xyGrid.AddChild(xButtons);

				FlowLayoutWidget yButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					moveButtonFactory.normalFillColor = XYZColors.yColor;
					yButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
					yButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;
					yPlusControl = moveButtonFactory.Generate("Y+", PrinterConnectionAndCommunication.Axis.Y, MovementControls.YSpeed);
					yPlusControl.ToolTipText = "Move Y positive";
					yButtons.AddChild(yPlusControl);

					GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
					spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
					spacer.BackgroundColor = XYZColors.yColor;
					yButtons.AddChild(spacer);

					yMinusControl = moveButtonFactory.Generate("Y-", PrinterConnectionAndCommunication.Axis.Y, MovementControls.YSpeed);
					yMinusControl.ToolTipText = "Move Y negative";
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
			private PrinterConnectionAndCommunication.Axis moveAxis;

			//Amounts in millimeters
			public double MoveAmount = 10;

			private double movementFeedRate;

			public MoveButton(double x, double y, GuiWidget buttonView, PrinterConnectionAndCommunication.Axis axis, double movementFeedRate)
				: base(x, y, buttonView)
			{
				this.moveAxis = axis;
				this.movementFeedRate = movementFeedRate;

				this.Click += new EventHandler(moveAxis_Click);
			}

			private void moveAxis_Click(object sender, EventArgs mouseEvent)
			{
				MoveButton moveButton = (MoveButton)sender;

				//Add more fancy movement here
				PrinterConnectionAndCommunication.Instance.MoveRelative(this.moveAxis, this.MoveAmount, movementFeedRate);
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

				this.Click += new EventHandler(moveAxis_Click);
			}

			private void moveAxis_Click(object sender, EventArgs mouseEvent)
			{
				ExtrudeButton moveButton = (ExtrudeButton)sender;

				//Add more fancy movement here
				PrinterConnectionAndCommunication.Instance.MoveExtruderRelative(MoveAmount, movementFeedRate, ExtruderNumber);
			}
		}

		public class MoveButtonWidget : GuiWidget
		{
			protected int fontSize = 12;
			protected double borderWidth = 0;
			protected double borderRadius = 0;

			public MoveButtonWidget(string label, RGBA_Bytes fillColor, RGBA_Bytes textColor)
				: base()
			{
				this.BackgroundColor = fillColor;
				this.Margin = new BorderDouble(0);
				this.Padding = new BorderDouble(0);

				if (label != "")
				{
					TextWidget textWidget = new TextWidget(label, pointSize: fontSize);
					textWidget.VAnchor = VAnchor.ParentCenter;
					textWidget.HAnchor = HAnchor.ParentCenter;
					textWidget.TextColor = textColor;
					textWidget.Padding = new BorderDouble(3, 0);
					this.AddChild(textWidget);
				}
				this.Height = 40 * TextWidget.GlobalPointSizeScaleRatio;
				this.Width = 40 * TextWidget.GlobalPointSizeScaleRatio;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);
				RectangleDouble boarderRectangle = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(boarderRectangle, 0);
				graphics2D.Render(new Stroke(rectBorder, 1), new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200));
			}
		}

		public class MoveButtonFactory
		{
			public BorderDouble Padding;
			public BorderDouble Margin;
			public RGBA_Bytes normalFillColor = RGBA_Bytes.White;
			public RGBA_Bytes hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
			public RGBA_Bytes pressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
			public RGBA_Bytes disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);
			public RGBA_Bytes normalBorderColor = new RGBA_Bytes(255, 255, 255, 0);
			public RGBA_Bytes hoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);
			public RGBA_Bytes pressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);
			public RGBA_Bytes disabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
			public RGBA_Bytes normalTextColor = RGBA_Bytes.Black;
			public RGBA_Bytes hoverTextColor = RGBA_Bytes.White;
			public RGBA_Bytes pressedTextColor = RGBA_Bytes.White;
			public RGBA_Bytes disabledTextColor = RGBA_Bytes.White;

			public MoveButton Generate(string label, PrinterConnectionAndCommunication.Axis axis, double movementFeedRate)
			{
				//Create button based on view container widget
				ButtonViewStates buttonViewWidget = GetButtonView(label);
				MoveButton textImageButton = new MoveButton(0, 0, buttonViewWidget, axis, movementFeedRate);
				textImageButton.Margin = new BorderDouble(0);
				textImageButton.Padding = new BorderDouble(0);
				return textImageButton;
			}

			public ExtrudeButton Generate(string label, double movementFeedRate, int extruderNumber = 0)
			{
				//Create button based on view container widget
				ButtonViewStates buttonViewWidget = GetButtonView(label);
				ExtrudeButton textImageButton = new ExtrudeButton(0, 0, buttonViewWidget, movementFeedRate, extruderNumber);
				textImageButton.Margin = new BorderDouble(0);
				textImageButton.Padding = new BorderDouble(0);
				return textImageButton;
			}

			private ButtonViewStates GetButtonView(string label)
			{
				//Create the multi-state button view
				ButtonViewStates buttonViewWidget = new ButtonViewStates(
					new MoveButtonWidget(label, normalFillColor, normalTextColor),
					new MoveButtonWidget(label, hoverFillColor, hoverTextColor),
					new MoveButtonWidget(label, pressedFillColor, pressedTextColor),
					new MoveButtonWidget(label, disabledFillColor, disabledTextColor)
				);
				return buttonViewWidget;
			}
		}
	}
}