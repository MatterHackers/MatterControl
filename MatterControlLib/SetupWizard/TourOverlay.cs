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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterControlLib.SetupWizard
{
	public class TourOverlay : GuiWidget
	{
		private GuiWidget targetWidget;
		private Popover popover;
		private GuiWidget tourWindow;
		private int nextSiteIndex;

		private string description;
		private ThemeConfig theme;

		public TourOverlay(GuiWidget tourWindow, GuiWidget targetWidget, string description, ThemeConfig theme, int nextSiteIndex)
		{
			this.tourWindow = tourWindow;
			this.nextSiteIndex = nextSiteIndex;
			this.theme = theme;
			this.targetWidget = targetWidget;
			this.description = description;

			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		public override void OnLoad(EventArgs args)
		{
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
			};

			column.AddChild(new WrappedTextWidget(description, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = 5,
				HAnchor = HAnchor.Stretch
			});

			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 5)
			};
			column.AddChild(buttonRow);

			buttonRow.AddChild(new HorizontalSpacer());

			if (nextSiteIndex > 0)
			{
				var nextButton = theme.CreateDialogButton("Next".Localize());
				nextButton.Click += (s, e) =>
				{
					this.Close();
					ShowSite(tourWindow, nextSiteIndex);
				};
				buttonRow.AddChild(nextButton);
			}

			var cancelButton = theme.CreateDialogButton("Done".Localize());
			cancelButton.Click += (s, e) => this.Close();
			buttonRow.AddChild(cancelButton);

			column.Size = new Vector2(250, column.Height);

			popover = this.GetPopover(column);
			popover.AddChild(column);
			this.AddChild(popover);

			this.Focus();

			base.OnLoad(args);
		}

		private Popover GetPopover(FlowLayoutWidget content)
		{
			int notchSize = 8;
			var padding = new BorderDouble(theme.DefaultContainerPadding);
			
			// Temporarily add the popover padding to the child content
			content.Padding = padding;

			// and last, set the size
			var targetBounds = this.GetTargetBounds();

			Vector2 contentPosition;
			int arrowPosition;
			Popover.ArrowDirection arrow;

			if (targetBounds.Right >= this.Width - content.Width - 5)
			{
				var left = targetBounds.Right - content.Width;
				if (targetBounds.Bottom < this.Height / 2)
				{
					if (targetBounds.Bottom - content.Size.Y < 0)
					{
						// position above target, arrow down aligned right center,
						contentPosition = new Vector2(left, targetBounds.Top + 1);
						arrowPosition = (int)(content.LocalBounds.Left + content.LocalBounds.Width - (targetWidget.Width / 2));
						arrow = Popover.ArrowDirection.Bottom;
					}
					else
					{
						// position left of target, arrow right aligned top center
						contentPosition = new Vector2(left - 1, targetBounds.Top - content.Size.Y);
						arrowPosition = (int)(content.LocalBounds.Top - (targetWidget.Height / 2));
						arrow = Popover.ArrowDirection.Right;
					}
				}
				else
				{
					// position under target, arrow up aligned right center
					contentPosition = new Vector2(left - content.DevicePadding.Width, targetBounds.Bottom - content.Size.Y - notchSize - 1);
					arrowPosition = (int)(content.LocalBounds.Left + content.LocalBounds.Width + content.DevicePadding.Width - (targetWidget.Width / 2));
					arrow = Popover.ArrowDirection.Top;
				}
			}
			else
			{
				if (targetBounds.Bottom < this.Height / 2)
				{
					// position right of target, arrow left aligned top center (or top 20 if target larger than content)
					contentPosition = new Vector2(targetBounds.Right + 1, targetBounds.Top - content.Size.Y);

					if (targetWidget.Height > content.Height)
					{
						arrowPosition = (int)(content.LocalBounds.Top - 20);
					}
					else
					{
						arrowPosition = (int)(content.LocalBounds.Top - (targetWidget.Height / 2));
					}

					arrow = Popover.ArrowDirection.Left;
				}
				else
				{
					// position under target, arrow up aligned left center
					contentPosition = new Vector2(targetBounds.Left, targetBounds.Bottom - content.Size.Y - notchSize - 1);
					arrowPosition = (int)(content.LocalBounds.Left + (targetWidget.Width / 2));
					arrow = Popover.ArrowDirection.Top;
				}
			}

			// Remove the temporarily padding to the child content
			content.Padding = 0;

			var popover = new Popover(arrow, padding, notchSize, p2: arrowPosition)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				TagColor = theme.ResolveColor(theme.BackgroundColor, theme.AccentMimimalOverlay.WithAlpha(50)),
			};

			popover.Position = contentPosition;

			return popover;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Escape)
			{
				this.Close();
			}

			if (keyEvent.KeyCode == Keys.Enter)
			{
				var topWindow = this.TopmostParent();
				this.Close();
				ShowSite(topWindow, nextSiteIndex);
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var dimRegion = new VertexStorage();
			dimRegion.MoveTo(LocalBounds.Left, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Top);
			dimRegion.LineTo(LocalBounds.Left, LocalBounds.Top);

			var targetBounds = this.GetTargetBounds();

			var targetRect = new VertexStorage();
			targetRect.MoveTo(targetBounds.Right, targetBounds.Bottom);
			targetRect.LineTo(targetBounds.Left, targetBounds.Bottom);
			targetRect.LineTo(targetBounds.Left, targetBounds.Top);
			targetRect.LineTo(targetBounds.Right, targetBounds.Top);

			var overlayMinusTargetRect = new CombinePaths(dimRegion, targetRect);
			graphics2D.Render(overlayMinusTargetRect, new Color(Color.Black, 180));

			base.OnDraw(graphics2D);

			graphics2D.Render(new Stroke(new RoundedRect(GetTargetBounds(), 0), 2), Color.White.WithAlpha(50));
			//graphics2D.Render(new Stroke(new RoundedRect(GetContentBounds(), 3), 4), theme.PrimaryAccentColor);
		}

		private RectangleDouble GetTargetBounds()
		{
			var childBounds = targetWidget.TransformToScreenSpace(targetWidget.LocalBounds);
			return this.TransformFromScreenSpace(childBounds);
		}

		public static void ShowSite(GuiWidget window, int siteIndex)
		{
			var tourSites = ApplicationController.Instance.ProductTour;
			
			if (siteIndex >= tourSites.Count)
			{
				siteIndex -= tourSites.Count;
			}

			GuiWidget GetSiteWidget(ref int findSiteIndex)
			{
				while (findSiteIndex < tourSites.Count)
				{
					var foundChildren = new List<GuiWidget.WidgetAndPosition>();
					window.FindNamedChildrenRecursive(tourSites[findSiteIndex].WidgetName, foundChildren);

					foreach (var widgetAndPosition in foundChildren)
					{
						if (widgetAndPosition.widget.ActuallyVisibleOnScreen())
						{
							return widgetAndPosition.widget;
						}
					}

					findSiteIndex++;
				}

				return null;
			}

			GuiWidget targetWidget = GetSiteWidget(ref siteIndex);

			if (targetWidget != null)
			{
				var tourOverlay = new TourOverlay(window, targetWidget, tourSites[siteIndex].Description, ApplicationController.Instance.Theme, siteIndex + 1);
				window.AddChild(tourOverlay);
			}
		}
	}
}
