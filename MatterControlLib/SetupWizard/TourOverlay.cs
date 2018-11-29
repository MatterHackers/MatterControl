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
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterControlLib.SetupWizard
{
	public class TourOverlay : GuiWidget
	{
		private GuiWidget targetWidget;
		private FlowLayoutWidget content;
		private int nextSiteIndex;

		private string Description { get; }
		private ThemeConfig theme;

		public TourOverlay(GuiWidget targetWidget, string description, ThemeConfig theme, int nextSiteIndex)
		{
			this.nextSiteIndex = nextSiteIndex;
			this.theme = theme;
			this.targetWidget = targetWidget;
			this.Description = description;

			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;
		}

		public override void OnLoad(EventArgs args)
		{
			content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(5),
				BackgroundColor = theme.BackgroundColor
			};

			this.AddChild(content);

			content.AddChild(new WrappedTextWidget(Description, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(5)
			});

			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 5)
			};
			buttonRow.AddChild(new HorizontalSpacer());

			if (nextSiteIndex > 0)
			{
				var nextButton = theme.CreateDialogButton("Next");
				nextButton.Click += (s, e) =>
				{
					var topWindow = this.TopmostParent();
					this.Close();
					ShowSite(topWindow, nextSiteIndex);
				};
				buttonRow.AddChild(nextButton);
			}

			var cancelButton = theme.CreateDialogButton("Done");
			cancelButton.Click += (s, e) => this.Close();
			buttonRow.AddChild(cancelButton);

			content.AddChild(buttonRow);

			// and last, set the size
			var childBounds = GetChildBounds();
			content.Size = new Vector2(250, content.Height);

			if(childBounds.Right >= this.Width - content.Width - 5)
			{
				var left = childBounds.Right - content.Width;
				if (childBounds.Bottom < this.Height / 2)
				{
					if (childBounds.Bottom - content.Size.Y < 0)
					{
						// position above
						content.Position = new Vector2(left, childBounds.Top);
					}
					else
					{
						// position content to the left of site
						content.Position = new Vector2(left, childBounds.Top - content.Size.Y);
					}
				}
				else
				{
					// position content under site
					content.Position = new Vector2(left, childBounds.Bottom - content.Size.Y);
				}
			}
			else
			{
				if(childBounds.Bottom < this.Height / 2)
				{
					// position content to the right of site
					content.Position = new Vector2(childBounds.Right, childBounds.Top - content.Size.Y);
				}
				else 
				{
					// position content under site
					content.Position = new Vector2(childBounds.Left, childBounds.Bottom - content.Size.Y);
				}
			}

			this.Focus();

			base.OnLoad(args);
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

			var childBounds = GetChildBounds();

			var childRect = new VertexStorage();
			childRect.MoveTo(childBounds.Right, childBounds.Bottom);
			childRect.LineTo(childBounds.Left, childBounds.Bottom);
			childRect.LineTo(childBounds.Left, childBounds.Top);
			childRect.LineTo(childBounds.Right, childBounds.Top);

			var combine = new CombinePaths(dimRegion, childRect);
			//var combine = new CombinePaths(dimRegion, new ReversePath(round));

			graphics2D.Render(combine, new Color(Color.Black, 120));

			base.OnDraw(graphics2D);

			graphics2D.Render(new Stroke(new RoundedRect(GetChildBounds(), 3), 4), theme.PrimaryAccentColor);
			graphics2D.Render(new Stroke(new RoundedRect(GetContentBounds(), 3), 4), theme.PrimaryAccentColor);
		}

		private RectangleDouble GetContentBounds()
		{
			var contentBounds = content.TransformToScreenSpace(content.LocalBounds);
			contentBounds = this.TransformFromScreenSpace(contentBounds);
			return contentBounds;
		}

		private RectangleDouble GetChildBounds()
		{
			var childBounds = targetWidget.TransformToScreenSpace(targetWidget.LocalBounds);
			childBounds = this.TransformFromScreenSpace(childBounds);
			return childBounds;
		}

		public static void ShowSite(GuiWidget window, int siteIndex)
		{
			var tourSites = new List<(string site, string description)>();
			tourSites.Add(("Open File Button", "Add parts from your hard drive to the bed."));
			tourSites.Add(("LibraryView", "Drag primitives to the bed to create your own designs."));
			tourSites.Add(("Add Content Menu", "Browse your library to find parts you have previously designed."));
			tourSites.Add(("Make Support Button", "Create custom supports. Turn any object on the bed into support material."));
			tourSites.Add(("Create Printer", "Setup a printer for the first time. Dozens of profiles are available to give you optimized settings."));
			tourSites.Add(("Theme Select Button", "Change your color theme anytime you want."));
			tourSites.Add(("Authentication Sign In", "Click here to sign into you MatterHackers account."));
			tourSites.Add(("MatterControl BrandMenuButton", "Here you can find application settings, help docs, updates and more."));
			tourSites.Add(("View Options Bar", "Reset the view, change viewing modes, hide and show the bed, and adjust the grid snap."));
			tourSites.Add(("Tumble Cube Control", "Adjust the position of your view. You can also snap to specific views by clicking the cube."));
			tourSites.Add(("Print Button", "Click here to start a print. This will also help you setup a printer if needed."));
			tourSites.Add(("PrintPopupMenu", "Click here to start a print."));
			tourSites.Add(("Hotend 0", "Your printers hotend controls. Set your temperatures, materials and load & unload filament."));
			tourSites.Add(("Slice Settings Sidebar", "Have compete control of your printer with the ability to adjust individual print settings."));

			if (siteIndex >= tourSites.Count)
			{
				siteIndex -= tourSites.Count;
			}

			GuiWidget GetSiteWidget(ref int findSiteIndex)
			{
				while (findSiteIndex < tourSites.Count)
				{
					List<GuiWidget.WidgetAndPosition> foundChildren = new List<GuiWidget.WidgetAndPosition>();
					window.FindNamedChildrenRecursive(tourSites[findSiteIndex].site, foundChildren);
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
				var tourOverlay = new TourOverlay(targetWidget, tourSites[siteIndex].description, ApplicationController.Instance.MenuTheme, siteIndex + 1);
				window.AddChild(tourOverlay);
			}
		}
	}
}
