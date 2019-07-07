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
using System.Collections.Generic;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
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

		private ThemeConfig theme;
		private PrinterConfig printer;

		private List<ExtrudeButton> eMinusButtons = new List<ExtrudeButton>();
		private List<ExtrudeButton> ePlusButtons = new List<ExtrudeButton>();
		private RadioTextButton movePointZeroTwoMmButton;
		private RadioTextButton moveOneMmButton;
		private RadioTextButton oneHundredButton;
		private RadioTextButton tenButton;
		private GuiWidget disableableEButtons;
		private GuiWidget keyboardFocusBorder;
		private GuiWidget keyboardImage;
		private GuiWidget xyGrid = null;

		public JogControls(PrinterConfig printer, XYZColors colors, ThemeConfig theme)
		{
			this.theme = theme;
			this.printer = printer;

			double distanceBetweenControls = 12;
			double buttonSeparationDistance = 10;

			var allControlsTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			var allControlsLeftToRight = new FlowLayoutWidget();

			using (allControlsLeftToRight.LayoutLock())
			{
				var xYZWithDistance = new FlowLayoutWidget(FlowDirection.TopToBottom);
				{
					var xYZControls = new FlowLayoutWidget();
					xYZControls.AddChild(this.CreateXYGridControl(colors, distanceBetweenControls, buttonSeparationDistance));

					FlowLayoutWidget zButtons = JogControls.CreateZButtons(printer, buttonSeparationDistance, out zPlusControl, out zMinusControl, colors, theme);
					zButtons.VAnchor = VAnchor.Bottom;
					xYZControls.AddChild(zButtons);
					xYZWithDistance.AddChild(xYZControls);

					// add in some movement radio buttons
					var setMoveDistanceControl = new FlowLayoutWidget
					{
						HAnchor = HAnchor.Left | HAnchor.Fit,
						VAnchor = VAnchor.Fit
					};

					{
						var moveRadioButtons = new FlowLayoutWidget();
						var radioList = new List<GuiWidget>();

						movePointZeroTwoMmButton = theme.CreateMicroRadioButton("0.02", radioList);
						movePointZeroTwoMmButton.CheckedStateChanged += (s, e) =>
						{
							if (movePointZeroTwoMmButton.Checked)
							{
								SetXYZMoveAmount(.02);
							}
						};
						moveRadioButtons.AddChild(movePointZeroTwoMmButton);

						var pointOneButton = theme.CreateMicroRadioButton("0.1", radioList);
						pointOneButton.CheckedStateChanged += (s, e) =>
						{
							if (pointOneButton.Checked)
							{
								SetXYZMoveAmount(.1);
							}
						};
						moveRadioButtons.AddChild(pointOneButton);

						moveOneMmButton = theme.CreateMicroRadioButton("1", radioList);
						moveOneMmButton.CheckedStateChanged += (s, e) =>
						{
							if (moveOneMmButton.Checked)
							{
								SetXYZMoveAmount(1);
							}
						};
						moveRadioButtons.AddChild(moveOneMmButton);

						tenButton = theme.CreateMicroRadioButton("10", radioList);
						tenButton.CheckedStateChanged += (s, e) =>
						{
							if (tenButton.Checked)
							{
								SetXYZMoveAmount(10);
							}
						};
						moveRadioButtons.AddChild(tenButton);

						oneHundredButton = theme.CreateMicroRadioButton("100", radioList);
						oneHundredButton.CheckedStateChanged += (s, e) =>
						{
							if (oneHundredButton.Checked)
							{
								SetXYZMoveAmount(100);
							}
						};
						moveRadioButtons.AddChild(oneHundredButton);

						tenButton.Checked = true;
						SetXYZMoveAmount(10);
						moveRadioButtons.Margin = new BorderDouble(0, 3);
						setMoveDistanceControl.AddChild(moveRadioButtons);

						moveRadioButtons.AddChild(new TextWidget("mm", textColor: theme.TextColor, pointSize: 8)
						{
							Margin = new BorderDouble(left: 10),
							VAnchor = VAnchor.Center
						});
					}

					xYZWithDistance.AddChild(setMoveDistanceControl);
				}

				allControlsLeftToRight.AddChild(xYZWithDistance);

#if !__ANDROID__
				allControlsLeftToRight.AddChild(GetHotkeyControlContainer());
#endif
				// Bar between Z And E
				allControlsLeftToRight.AddChild(new GuiWidget(1, 1)
				{
					VAnchor = VAnchor.Stretch,
					BackgroundColor = colors.ZColor,
					Margin = new BorderDouble(distanceBetweenControls, 5)
				});

				// EButtons
				disableableEButtons = CreateEButtons(buttonSeparationDistance, colors);
				disableableEButtons.Name = "disableableEButtons";
				disableableEButtons.HAnchor = HAnchor.Fit;
				disableableEButtons.VAnchor = VAnchor.Fit | VAnchor.Top;

				allControlsLeftToRight.AddChild(disableableEButtons);
				allControlsTopToBottom.AddChild(allControlsLeftToRight);
			}
			allControlsLeftToRight.PerformLayout();

			using (this.LayoutLock())
			{
				this.AddChild(allControlsTopToBottom);
				this.HAnchor = HAnchor.Fit;
				this.VAnchor = VAnchor.Fit;
				Margin = new BorderDouble(3);
			}

			this.PerformLayout();

			// Register listeners
			printer.Settings.SettingChanged += Printer_SettingChanged;
		}

		internal void SetEnabledLevels(bool enableBabysteppingMode, bool enableEControls)
		{
			if (enableBabysteppingMode)
			{
				if (zPlusControl.MoveAmount >= 1)
				{
					movePointZeroTwoMmButton.InvokeClick();
				}
			}
			else
			{
				if (zPlusControl.MoveAmount < 1)
				{
					moveOneMmButton.InvokeClick();
				}
			}

			tenButton.Enabled = !enableBabysteppingMode;
			oneHundredButton.Enabled = !enableBabysteppingMode;

			if (xyGrid != null)
			{
				xyGrid.Enabled = !enableBabysteppingMode;
			}

			disableableEButtons.Enabled = enableEControls;
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

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		private FlowLayoutWidget GetHotkeyControlContainer()
		{
			var keyFocusedContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Stretch,
				ToolTipText = "Use cursor keys for axis movements".Localize(),
				Margin = new BorderDouble(left: 10)
			};

			keyboardImage = new IconButton(AggContext.StaticData.LoadIcon("hot_key_small_white.png", 19, 12, theme.InvertIcons), theme)
			{
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(5),
				Visible = !UserSettings.Instance.IsTouchScreen,
				Enabled = false,
				Selectable = false
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
			var parent = keyboardFocusBorder.Parents<SectionWidget>().First();

			parent.KeyDown += JogControls_KeyDown;

			parent.ContainsFocusChanged += (sender, e) =>
			{
				bool hasFocus = (sender as GuiWidget).ContainsFocus;
				if (keyboardImage.Enabled != hasFocus)
				{
					keyboardImage.Enabled = hasFocus;
					//keyboardImage.BackgroundColor = (hasFocus) ? theme.SlightShade : Color.Transparent;
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
					printer.Connection.HomeAxis(PrinterAxis.Z);
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Y)
				{
					printer.Connection.HomeAxis(PrinterAxis.Y);
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.X)
				{
					printer.Connection.HomeAxis(PrinterAxis.X);
					e.Handled = true;
				}
				if (e.KeyCode == Keys.Home)
				{
					printer.Connection.HomeAxis(PrinterAxis.XYZ);
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Left)
				{
					printer.Connection.MoveRelative(PrinterAxis.X, moveAmountNegative, printer.Settings.XSpeed());
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Right)
				{
					printer.Connection.MoveRelative(PrinterAxis.X, moveAmountPositive, printer.Settings.XSpeed());
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Up)
				{
					printer.Connection.MoveRelative(PrinterAxis.Y, moveAmountPositive, printer.Settings.YSpeed());
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Down)
				{
					printer.Connection.MoveRelative(PrinterAxis.Y, moveAmountNegative, printer.Settings.YSpeed());
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.E)
				{
					printer.Connection.MoveRelative(PrinterAxis.E, eMoveAmountPositive, printer.Settings.EFeedRate(0));
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.R)
				{
					printer.Connection.MoveRelative(PrinterAxis.E, eMoveAmountNegative, printer.Settings.EFeedRate(0));
					e.Handled = true;
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
					e.Handled = true;
				}
				else
				{
					printer.Connection.MoveRelative(PrinterAxis.Z, moveAmountPositive, printer.Settings.ZSpeed());
					e.Handled = true;
				}
			}
			else if ((AggContext.OperatingSystem == OSType.Windows && e.KeyCode == Keys.PageDown)
				|| (AggContext.OperatingSystem == OSType.Mac && e.KeyCode == Keys.Clear))
			{
				if (printer.Connection.CommunicationState == CommunicationStates.Printing)
				{
					var currentZ = printer.Settings.GetValue<double>(SettingsKey.baby_step_z_offset);
					currentZ += moveAmountPositive;
					printer.Settings.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));

					e.Handled = true;
				}
				else
				{
					printer.Connection.MoveRelative(PrinterAxis.Z, moveAmountNegative, printer.Settings.ZSpeed());
					e.Handled = true;
				}
			}
		}

		private FlowLayoutWidget CreateEButtons(double buttonSeparationDistance, XYZColors colors)
		{
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			FlowLayoutWidget eButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				FlowLayoutWidget eMinusButtonAndText = new FlowLayoutWidget();
				BorderDouble extrusionMargin = new BorderDouble(4, 0, 4, 0);

				if (extruderCount == 1)
				{
					ExtrudeButton eMinusControl = theme.CreateExtrudeButton(printer, "E-", printer.Settings.EFeedRate(0), 0);
					eMinusControl.MoveAmount = -eMinusControl.MoveAmount;
					eMinusControl.Margin = extrusionMargin;
					eMinusControl.ToolTipText = "Retract filament".Localize();
					eMinusButtonAndText.AddChild(eMinusControl);
					eMinusButtons.Add(eMinusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton eMinusControl = theme.CreateExtrudeButton(printer, $"E{i + 1}-", printer.Settings.EFeedRate(0), i);
						eMinusControl.MoveAmount = -eMinusControl.MoveAmount;
						eMinusControl.ToolTipText = "Retract filament".Localize();
						eMinusControl.Margin = extrusionMargin;
						eMinusButtonAndText.AddChild(eMinusControl);
						eMinusButtons.Add(eMinusControl);
					}
				}

				TextWidget eMinusControlLabel = new TextWidget("Retract".Localize(), pointSize: 11)
				{
					TextColor = theme.TextColor,
					VAnchor = VAnchor.Center
				};
				eMinusButtonAndText.AddChild(eMinusControlLabel);
				eButtons.AddChild(eMinusButtonAndText);

				eMinusButtonAndText.HAnchor = HAnchor.Fit;
				eMinusButtonAndText.VAnchor = VAnchor.Fit;

				FlowLayoutWidget buttonSpacerContainer = new FlowLayoutWidget();
				for (int i = 0; i < extruderCount; i++)
				{
					double buttonWidth = eMinusButtons[i].Width + 6;

					var eSpacer = new GuiWidget(1, buttonSeparationDistance)
					{
						Margin = new BorderDouble((buttonWidth / 2), 0, ((buttonWidth) / 2), 0),
						BackgroundColor = colors.EColor
					};
					buttonSpacerContainer.AddChild(eSpacer);
				}

				eButtons.AddChild(buttonSpacerContainer);

				buttonSpacerContainer.HAnchor = HAnchor.Fit;
				buttonSpacerContainer.VAnchor = VAnchor.Fit;

				FlowLayoutWidget ePlusButtonAndText = new FlowLayoutWidget();
				if (extruderCount == 1)
				{
					ExtrudeButton ePlusControl = theme.CreateExtrudeButton(printer, "E+", printer.Settings.EFeedRate(0), 0);
					ePlusControl.Margin = extrusionMargin;
					ePlusControl.ToolTipText = "Extrude filament".Localize();
					ePlusButtonAndText.AddChild(ePlusControl);
					ePlusButtons.Add(ePlusControl);
				}
				else
				{
					for (int i = 0; i < extruderCount; i++)
					{
						ExtrudeButton ePlusControl = theme.CreateExtrudeButton(printer, $"E{i + 1}+", printer.Settings.EFeedRate(0), i);
						ePlusControl.Margin = extrusionMargin;
						ePlusControl.ToolTipText = "Extrude filament".Localize();
						ePlusButtonAndText.AddChild(ePlusControl);
						ePlusButtons.Add(ePlusControl);
					}
				}

				TextWidget ePlusControlLabel = new TextWidget("Extrude".Localize(), pointSize: 11);
				ePlusControlLabel.TextColor = theme.TextColor;
				ePlusControlLabel.VAnchor = VAnchor.Center;
				ePlusButtonAndText.AddChild(ePlusControlLabel);
				eButtons.AddChild(ePlusButtonAndText);
				ePlusButtonAndText.HAnchor = HAnchor.Fit;
				ePlusButtonAndText.VAnchor = VAnchor.Fit;
			}

			eButtons.AddChild(new GuiWidget(10, 6));

			// add in some movement radio buttons
			var setMoveDistanceControl = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Fit
			};

			{
				var moveRadioButtons = new FlowLayoutWidget
				{
					Margin = new BorderDouble(0, 3)
				};

				var oneButton = theme.CreateMicroRadioButton("1");
				oneButton.CheckedStateChanged += (s, e) =>
				{
					if (oneButton.Checked)
					{
						SetEMoveAmount(1);
					}
				};
				moveRadioButtons.AddChild(oneButton);

				var tenButton = theme.CreateMicroRadioButton("10");
				tenButton.CheckedStateChanged += (s, e) =>
				{
					if (tenButton.Checked)
					{
						SetEMoveAmount(10);
					}
				};
				moveRadioButtons.AddChild(tenButton);

				var oneHundredButton = theme.CreateMicroRadioButton("100");
				oneHundredButton.CheckedStateChanged += (s, e) =>
				{
					if (oneHundredButton.Checked)
					{
						SetEMoveAmount(100);
					}
				};
				moveRadioButtons.AddChild(oneHundredButton);

				tenButton.Checked = true;
				setMoveDistanceControl.AddChild(moveRadioButtons);
			}

			setMoveDistanceControl.AddChild(
				new TextWidget("mm", textColor: theme.TextColor, pointSize: 8)
				{
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(left: 10)
				});

			eButtons.AddChild(setMoveDistanceControl);

			eButtons.HAnchor = HAnchor.Fit;
			eButtons.VAnchor = VAnchor.Fit | VAnchor.Bottom;

			return eButtons;
		}

		public static FlowLayoutWidget CreateZButtons(PrinterConfig printer, double buttonSeparationDistance, out MoveButton zPlusControl, out MoveButton zMinusControl, XYZColors colors, ThemeConfig theme, bool levelingButtons = false)
		{
			var zButtons = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 5),
			};

			zPlusControl = theme.CreateMoveButton(printer, "Z+", PrinterAxis.Z, printer.Settings.ZSpeed(), levelingButtons);
			zPlusControl.Name = "Move Z positive".Localize();
			zPlusControl.ToolTipText = "Move Z positive".Localize();
			zButtons.AddChild(zPlusControl);

			// spacer
			zButtons.AddChild(new GuiWidget(1, buttonSeparationDistance)
			{
				HAnchor = HAnchor.Center,
				BackgroundColor = colors.ZColor
			});

			zMinusControl = theme.CreateMoveButton(printer, "Z-", PrinterAxis.Z, printer.Settings.ZSpeed(), levelingButtons);
			zMinusControl.ToolTipText = "Move Z negative".Localize();
			zButtons.AddChild(zMinusControl);

			return zButtons;
		}

		private void Printer_SettingChanged(object sender, StringEventArgs stringEvent)
		{
			if (stringEvent != null)
			{
				if (stringEvent.Data == SettingsKey.manual_movement_speeds)
				{
					xPlusControl.MovementFeedRate = printer.Settings.XSpeed();
					xMinusControl.MovementFeedRate = printer.Settings.XSpeed();

					yPlusControl.MovementFeedRate = printer.Settings.YSpeed();
					yMinusControl.MovementFeedRate = printer.Settings.YSpeed();

					zPlusControl.MovementFeedRate = printer.Settings.ZSpeed();
					zMinusControl.MovementFeedRate = printer.Settings.ZSpeed();
					foreach (ExtrudeButton extrudeButton in eMinusButtons)
					{
						extrudeButton.MovementFeedRate = printer.Settings.EFeedRate(0);
					}

					foreach (ExtrudeButton extrudeButton in ePlusButtons)
					{
						extrudeButton.MovementFeedRate = printer.Settings.EFeedRate(0);
					}
				}
			}
		}

		private GuiWidget CreateXYGridControl(XYZColors colors, double distanceBetweenControls, double buttonSeparationDistance)
		{
			xyGrid = new GuiWidget
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit | VAnchor.Bottom,
				Margin = new BorderDouble(0, 5, distanceBetweenControls, 5)
			};

			var xButtons = new FlowLayoutWidget();
			xButtons.HAnchor = HAnchor.Fit | HAnchor.Center;
			xButtons.VAnchor = VAnchor.Fit | VAnchor.Center;
			xyGrid.AddChild(xButtons);

			xMinusControl = theme.CreateMoveButton(printer, "X-", PrinterAxis.X, printer.Settings.XSpeed());
			xMinusControl.ToolTipText = "Move X negative".Localize();
			xButtons.AddChild(xMinusControl);

			// spacer
			xButtons.AddChild(new GuiWidget(xMinusControl.Width + buttonSeparationDistance * 2, 1)
			{
				VAnchor = VAnchor.Center,
				BackgroundColor = colors.XColor
			});

			xPlusControl = theme.CreateMoveButton(printer, "X+", PrinterAxis.X, printer.Settings.XSpeed());
			xPlusControl.ToolTipText = "Move X positive".Localize();
			xButtons.AddChild(xPlusControl);

			var yButtons = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				VAnchor = VAnchor.Fit | VAnchor.Center
			};
			xyGrid.AddChild(yButtons);

			yPlusControl = theme.CreateMoveButton(printer, "Y+", PrinterAxis.Y, printer.Settings.YSpeed());
			yPlusControl.ToolTipText = "Move Y positive".Localize();
			yButtons.AddChild(yPlusControl);

			// spacer
			yButtons.AddChild(new GuiWidget(1, buttonSeparationDistance)
			{
				HAnchor = HAnchor.Center,
				BackgroundColor = colors.YColor
			});

			yMinusControl = theme.CreateMoveButton(printer, "Y-", PrinterAxis.Y, printer.Settings.YSpeed());
			yMinusControl.ToolTipText = "Move Y negative".Localize();
			yButtons.AddChild(yMinusControl);

			return xyGrid;
		}

		public class MoveButton : TextButton
		{
			//Amounts in millimeters
			public double MoveAmount { get; set; } = 10;

			public double MovementFeedRate { get; set; }
			private PrinterConfig printer;

			private PrinterAxis moveAxis;
			public MoveButton(string text, PrinterConfig printer, PrinterAxis axis, double movementFeedRate, ThemeConfig theme)
				: base(text, theme)
			{
				this.printer = printer;
				this.moveAxis = axis;
				this.MovementFeedRate = movementFeedRate;
			}

			protected override void OnClick(MouseEventArgs mouseEvent)
			{
				base.OnClick(mouseEvent);

				if (printer.Connection.CommunicationState == CommunicationStates.Printing)
				{
					if (moveAxis == PrinterAxis.Z) // only works on z
					{
						var currentZ = printer.Settings.GetValue<double>(SettingsKey.baby_step_z_offset);
						currentZ += this.MoveAmount;
						printer.Settings.SetValue(SettingsKey.baby_step_z_offset, currentZ.ToString("0.##"));
					}
				}
				else
				{
					printer.Connection.MoveRelative(this.moveAxis, this.MoveAmount, MovementFeedRate);
				}
			}
		}

		public class ExtrudeButton : TextButton
		{
			//Amounts in millimeters
			public double MoveAmount = 10;

			public double MovementFeedRate { get; set; }
			public int ExtruderNumber;

			private PrinterConfig printer;

			public ExtrudeButton(PrinterConfig printer, string text, double movementFeedRate, int extruderNumber, ThemeConfig theme)
				: base(text, theme)
			{
				this.printer = printer;
				this.ExtruderNumber = extruderNumber;
				this.MovementFeedRate = movementFeedRate;
			}

			protected override void OnClick(MouseEventArgs mouseEvent)
			{
				base.OnClick(mouseEvent);

				//Add more fancy movement here
				printer.Connection.MoveExtruderRelative(MoveAmount, MovementFeedRate, ExtruderNumber);
			}
		}
	}
}