﻿/*
Copyright (c) 2019, John Lewin
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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using static MatterHackers.Agg.ShapePath;

namespace MatterHackers.MatterControl
{
	public class CalibrationTabWidget : GuiWidget
	{
		private IVertexSource tabShape;
		private Stroke tabStroke;
		private IVertexSource xHighlighter;
		private IVertexSource yHighlighter;
		private XyCalibrationWizard calibrationWizard;
		private ThemeConfig theme;

		public TextButton NextButton { get; }

		private Color tabBaseColor;
		private TextWidget xLabel;
		private TextWidget yLabel;

		private PrinterConnection.Axis _collectionMode = PrinterConnection.Axis.X;

		public CalibrationTabWidget(XyCalibrationWizard calibrationWizard, TextButton nextButton, ThemeConfig theme)
		{
			this.calibrationWizard = calibrationWizard;
			this.theme = theme;
			this.NextButton = nextButton;
			tabBaseColor = new Color(theme.SlightShade.ToColorF(), theme.SlightShade.Alpha0To1 * 1.2);

			double barWidth = 30;
			double barHeight = 300;

			double left = LocalBounds.Left + 15;
			double bottom = LocalBounds.Bottom + 15;
			double right = left + barHeight;

			var a = new Vector2(left, bottom);
			var b = new Vector2(left, bottom + barHeight);
			var c = new Vector2(left + barWidth, bottom + barHeight);
			var d = new Vector2(left + barWidth, bottom + barWidth);
			var e = new Vector2(right, bottom + barWidth);
			var f = new Vector2(right + (barWidth * .7), bottom + (barWidth / 2));
			var g = new Vector2(right, bottom);

			var m = new Vector2(b.X + (barWidth / 2), b.Y + (barWidth * .6));
			var n = new Vector2(m.X, b.Y);
			var r = new Vector2(b.X, m.Y);

			var tabShape2 = new VertexStorage();
			tabShape2.Add(a.X, a.Y, FlagsAndCommand.MoveTo); // A
			tabShape2.LineTo(b); // A - B

			tabShape2.curve3(r.X, r.Y, m.X, m.Y); // B -> C
			tabShape2.curve3(c.X, c.Y);

			tabShape2.LineTo(d); // C -> D
			tabShape2.LineTo(e); // D -> E
			tabShape2.LineTo(f); // E -> F
			tabShape2.LineTo(g); // F -> G
			tabShape2.ClosePolygon();

			int highlightStroke = 2;
			int highlightOffset = 8;
			int highlightWidth = 16;

			double x1 = d.X + highlightOffset;
			double x2 = x1 + highlightWidth;
			double y1 = d.Y + highlightOffset;
			double y2 = c.Y - highlightOffset;

			double midY = y1 + (y2 - y1) / 2;

			var highlighter = new VertexStorage();
			highlighter.MoveTo(x1, y1);
			highlighter.LineTo(x2, y1);
			highlighter.LineTo(x2, midY);
			highlighter.LineTo(x2 + highlightOffset, midY);
			highlighter.LineTo(x2, midY);
			highlighter.LineTo(x2, y2);
			highlighter.LineTo(x1, y2);

			xHighlighter = new Stroke(highlighter, highlightStroke);

			xLabel = new TextWidget("Select the most centered pad", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute
			};

			xLabel.Position = new Vector2(x2 + highlightOffset * 2, midY - xLabel.Height / 2);

			this.AddChild(xLabel);

			//////////////////////////////////
			///
			x1 = d.X + highlightOffset;
			y1 = d.Y + 50;

			x1 = d.X + highlightOffset;
			x2 = e.X - highlightOffset;
			y1 = d.Y + highlightOffset;
			y2 = y1 + highlightWidth;

			double midX = x1 + (x2 - x1) / 2;

			highlighter = new VertexStorage();
			highlighter.MoveTo(x1, y1);
			highlighter.LineTo(x1, y2);
			highlighter.LineTo(midX, y2);
			highlighter.LineTo(midX, y2 + highlightOffset);
			highlighter.LineTo(midX, y2);
			highlighter.LineTo(x2, y2);
			highlighter.LineTo(x2, y1);

			yHighlighter = new Stroke(highlighter, highlightStroke);

			yLabel = new TextWidget("Select the most centered pad", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Visible = false,
			};
			this.AddChild(yLabel);

			yLabel.Position = new Vector2(midX - yLabel.Width / 2, y2 + (highlightOffset * 2));

			yHighlighter = new Stroke(highlighter, highlightStroke);

			int padCount = 7;

			double cellSize = (barHeight - barWidth) / padCount;
			int padding = (int)(cellSize * .3);

			double padSize = cellSize - padding;

			var titles = new[] { "-3", "-2", "-1", "0", "+1", "+2", "+3" };

			for (var i = 0; i < padCount; i++)
			{
				this.AddChild(new CalibrationPad(titles[i], theme, pointSize: theme.DefaultFontSize - 1)
				{
					Position = new Vector2(left, bottom + 3 + barWidth + (cellSize * i)),
					Height = padSize,
					Width = barWidth,
					Index = i,
					IsActive = i == 3,
					Axis = PrinterConnection.Axis.X
				});

				this.AddChild(new CalibrationPad(titles[i], theme, pointSize: theme.DefaultFontSize - 1)
				{
					Position = new Vector2(left + 3 + barWidth + (cellSize * i), bottom),
					Height = barWidth,
					Width = padSize,
					Enabled = false,
					Index = i,
					IsActive = i == 3,
					Axis = PrinterConnection.Axis.Y
				});
			}

			foreach (var calibrationPad in this.Children.OfType<CalibrationPad>())
			{
				calibrationPad.Click += this.CalibrationPad_Click;
			}

			tabShape = new FlattenCurves(tabShape2);
			tabStroke = new Stroke(tabShape);
		}

		private PrinterConnection.Axis CollectionMode
		{
			get => _collectionMode;
			set
			{
				_collectionMode = value;

				switch (_collectionMode)
				{
					case PrinterConnection.Axis.Y:
						// Enable
						foreach (var pad in this.Children.OfType<CalibrationPad>().Where(p => p.Axis == PrinterConnection.Axis.Y))
						{
							pad.Enabled = true;
						}

						xLabel.Visible = false;
						yLabel.Visible = true;
						break;

					case PrinterConnection.Axis.X:
						xLabel.Visible = true;
						yLabel.Visible = false;
						break;

					default:
						xLabel.Visible = false;
						yLabel.Visible = false;
						break;
				}
			}
		}

		private void CalibrationPad_Click(object sender, MouseEventArgs e)
		{
			if (sender is CalibrationPad calibrationPad)
			{
				if (calibrationPad.Axis == PrinterConnection.Axis.X)
				{
					if (CollectionMode == PrinterConnection.Axis.X)
					{
						CollectionMode = PrinterConnection.Axis.Y;
					}

					calibrationWizard.XPick = calibrationPad.Index;

				}
				else if (calibrationPad.Axis == PrinterConnection.Axis.Y)
				{
					if (CollectionMode == PrinterConnection.Axis.Y)
					{
						CollectionMode = PrinterConnection.Axis.Z;
					}

					calibrationWizard.YPick = calibrationPad.Index;
				}

				foreach (var pad in this.Children.OfType<CalibrationPad>().Where(p => p.Axis == calibrationPad.Axis))
				{
					pad.BackgroundColor = pad == calibrationPad ? theme.PrimaryAccentColor : theme.SlightShade;
					pad.IsActive = pad == calibrationPad;
				}
			}

			// CheckIfCanAdvance
			NextButton.Enabled = calibrationWizard.YPick != -1 && calibrationWizard.XPick != -1;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(tabShape, tabBaseColor);
			//graphics2D.Render(tabStroke, theme.Shade);

			if (CollectionMode == PrinterConnection.Axis.X)
			{
				graphics2D.Render(xHighlighter, theme.PrimaryAccentColor);
			}
			else if (CollectionMode == PrinterConnection.Axis.Y)
			{
				graphics2D.Render(yHighlighter, theme.PrimaryAccentColor);
			}

			base.OnDraw(graphics2D);
		}

		private class CalibrationPad : IconButton
		{
			private static ImageBuffer activeIcon;
			private static ImageBuffer inactiveIcon;
			private bool _isActive;

			static CalibrationPad()
			{
				activeIcon = AggContext.StaticData.LoadIcon("fa-check_16.png", true);
				inactiveIcon = new ImageBuffer(16, 16);
				//inactiveIcon = activeIcon.AjustAlpha(0.2);
			}

			public CalibrationPad(string text, ThemeConfig theme, double pointSize = -1)
				: base(inactiveIcon, theme)
			{
				this.BackgroundColor = theme.PrimaryAccentColor.WithAlpha(35);
				this.HoverColor = theme.PrimaryAccentColor;
				this.BorderColor = theme.PrimaryAccentColor.WithAlpha(80);
				this.Border = 1;
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Absolute;
			}

			public int Index { get; internal set; }

			public bool IsActive
			{
				get => _isActive;
				set
				{
					_isActive = value;
					this.imageWidget.Image = _isActive ? activeIcon : inactiveIcon;
				}
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				this.imageWidget.Image = activeIcon;
				base.OnMouseEnterBounds(mouseEvent);
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				this.imageWidget.Image = this.IsActive ? activeIcon : inactiveIcon;
				base.OnMouseLeaveBounds(mouseEvent);
			}

			public PrinterConnection.Axis Axis { get; set; }
		}
	}
}