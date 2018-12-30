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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using System;

namespace MatterControlLib.SetupWizard
{
	public class TourPopover : Popover
	{
		private ThemeConfig theme;

		public TourPopover(ProductTour productTour, ThemeConfig theme, GuiWidget targetWidget, RectangleDouble targetBounds)
			// (arrow, 0 /* padding */, notchSize, p2: arrowPosition
			: base(ArrowDirection.Left, 0, 7, 0)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.theme = theme;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};
			this.AddChild(column);

			var row = new GuiWidget()
			{
				Margin = new BorderDouble(5),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			column.AddChild(row);

			var title = new TextWidget("Title", pointSize: theme.DefaultFontSize, textColor: theme.PrimaryAccentColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(top: 4, left: 4),
				VAnchor = VAnchor.Center,
			};
			row.AddChild(title);

			var closeButton = theme.CreateSmallResetButton();
			closeButton.HAnchor = HAnchor.Right;
			closeButton.VAnchor = VAnchor.Top;
			closeButton.Margin = 0;
			closeButton.Click += (s, e) =>
			{
				this.Parent.CloseOnIdle();
			};

			row.AddChild(closeButton);

			var body = this.CreateBodyWidget(productTour);
			body.Padding = new BorderDouble(theme.DefaultContainerPadding).Clone(top: 0);
			column.AddChild(body);

			var totalWidth = this.Width + this.DeviceMarginAndBorder.Width;
			var totalHeight = this.Height + this.DeviceMarginAndBorder.Height;

			var totalBounds = new RectangleDouble(0, 0, totalWidth, totalHeight);

			var targetCenterX = targetWidget.Width / 2;
			var targetCenterY = targetWidget.Height / 2;

			if (targetBounds.Right >= totalBounds.Width)
			{
				if (targetBounds.Bottom < totalBounds.Height / 2)
				{
					if (targetBounds.Bottom - totalBounds.Height < 0)
					{
						Console.WriteLine("B1");

						// Down arrow
						this.Arrow = Popover.ArrowDirection.Bottom;

						// Arrow centered on target in x, to the right
						totalBounds = this.GetTotalBounds();
						this.ArrowOffset = (int)(totalBounds.Right - targetCenterX);

						// Popover positioned above target, aligned right
						this.Position = new Vector2(
							this.LeftForAlignTargetRight(targetBounds.Right, totalBounds),
							targetBounds.Top + 1);
					}
					else
					{
						Console.WriteLine("B2");

						// Right arrow
						this.Arrow = Popover.ArrowDirection.Right;

						//  Arrow centered on target in y, to the top
						totalBounds = this.GetTotalBounds();
						this.ArrowOffset = (int)(totalBounds.Height - targetCenterY);

						// Popover positioned left of target, aligned top
						this.Position = new Vector2(
							this.LeftForAlignTargetRight(targetBounds.Right, totalBounds),
							targetBounds.Top - totalBounds.Height);
					}
				}
				else
				{
					Console.WriteLine("A2");

					// Up arrow
					this.Arrow = Popover.ArrowDirection.Top;

					// Arrow centered on target in x, to the right
					totalBounds = this.GetTotalBounds();
					this.ArrowOffset = (int)(totalBounds.Right - targetCenterX);

					// Popover positioned below target, aligned right
					this.Position = new Vector2(
						this.LeftForAlignTargetRight(targetBounds.Right, totalBounds),
						targetBounds.Bottom - totalBounds.Height - 1);
				}
			}
			else
			{
				if (targetBounds.Bottom < totalBounds.Height)
				{
					Console.WriteLine("D1");

					// Left arrow
					this.Arrow = Popover.ArrowDirection.Left;

					// Arrow centered on target in y (or top - 20 if target larger than content)
					totalBounds = this.GetTotalBounds();
					if (targetWidget.Height > totalBounds.Height)
					{
						this.ArrowOffset = 20;
					}
					else
					{
						this.ArrowOffset = (int)targetCenterY;
					}

					// Popover positioned right of target, aligned top
					this.Position = new Vector2(
						targetBounds.Right + 1,
						targetBounds.Top - totalBounds.Height);
				}
				else
				{
					Console.WriteLine("D2");

					this.Arrow = Popover.ArrowDirection.Top;

					// Arrow centered on target in x, to the left
					totalBounds = this.GetTotalBounds();
					this.ArrowOffset = (int) targetCenterX;

					// Popover positioned below target, aligned left
					this.Position = new Vector2(
						targetBounds.Left,
						targetBounds.Bottom - totalBounds.Height - 1);
				}
			}

			this.TagColor = theme.ResolveColor(theme.BackgroundColor, theme.AccentMimimalOverlay.WithAlpha(50));

			this.RebuildShape();
		}

		private GuiWidget CreateBodyWidget(ProductTour productTour)
		{
			var body = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
			};

			body.AddChild(new WrappedTextWidget(productTour.ActiveItem.Description, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = 5,
				HAnchor = HAnchor.Stretch
			});

			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 5)
			};
			body.AddChild(buttonRow);

			var prevButton = theme.CreateDialogButton("Prev".Localize());
			prevButton.Click += (s, e) =>
			{
				this.Parent.Close();
				productTour.ShowPrevious();
			};
			buttonRow.AddChild(prevButton);

			buttonRow.AddChild(new HorizontalSpacer());

			buttonRow.AddChild(new TextWidget($"{productTour.ActiveIndex + 1} of {productTour.Count}", pointSize: theme.H1PointSize, textColor: theme.TextColor));

			buttonRow.AddChild(new HorizontalSpacer());

			var nextButton = theme.CreateDialogButton("Next".Localize());
			nextButton.Click += (s, e) =>
			{
				this.Parent.Close();

				productTour.ShowNext();
			};
			buttonRow.AddChild(nextButton);

			body.Size = new Vector2(250, body.Height);

			return body;
		}

		private double LeftForAlignTargetRight(double targetRight, RectangleDouble totalBounds)
		{
			return targetRight - totalBounds.Width;
		}

		private RectangleDouble GetTotalBounds()
		{
			var totalWidth = this.Width + this.DeviceMarginAndBorder.Width;
			var totalHeight = this.Height + this.DeviceMarginAndBorder.Height;

			var totalBounds = new RectangleDouble(0, 0, totalWidth, totalHeight);
			return totalBounds;
		}
	}
}