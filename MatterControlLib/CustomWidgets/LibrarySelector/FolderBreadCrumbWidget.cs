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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class FolderBreadCrumbWidget : FlowLayoutWidget
	{
		private ThemeConfig theme;
		private ILibraryContext libraryContext;

		public FolderBreadCrumbWidget(ILibraryContext libraryContext, ThemeConfig theme)
		{
			this.libraryContext = libraryContext;
			this.Name = "FolderBreadCrumbWidget";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.MinimumSize = new VectorMath.Vector2(0, 1); // Force some minimum bounds to ensure draw and thus onload (and our local init) are called on startup
			this.theme = theme;
		}

		public void SetContainer(ILibraryContainer currentContainer)
		{
			this.CloseAllChildren();

			var upbutton = new IconButton(AggContext.StaticData.LoadIcon(Path.Combine("Library", "upfolder_20.png"), theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Enabled = currentContainer.Parent != null,
				Name = "Library Up Button",
				ToolTipText = "Up one folder".Localize(),
				Margin = theme.ButtonSpacing,
				MinimumSize = new Vector2(theme.ButtonHeight, theme.ButtonHeight)
			};
			upbutton.Click += (s, e) =>
			{
				if (libraryContext.ActiveContainer.Parent != null)
				{
					UiThread.RunOnIdle(() => libraryContext.ActiveContainer = libraryContext.ActiveContainer.Parent);
				}
			};
			this.AddChild(upbutton);

			bool firstItem = true;

			if (this.Width < 250)
			{
				var containerButton = new LinkLabel((libraryContext.ActiveContainer.Name == null ? "?" : libraryContext.ActiveContainer.Name), theme)
				{
					Name = "Bread Crumb Button " + libraryContext.ActiveContainer.Name,
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					TextColor = theme.TextColor
				};
				this.AddChild(containerButton);
			}
			else
			{
				var extraSpacing = (theme.ButtonSpacing).Clone(left: theme.ButtonSpacing.Right * .4);

				foreach (var container in currentContainer.AncestorsAndSelf().Reverse())
				{
					if (!firstItem)
					{
						// Add path separator
						this.AddChild(new TextWidget("/", pointSize: theme.DefaultFontSize + 1, textColor: theme.TextColor)
						{
							VAnchor = VAnchor.Center,
							Margin = extraSpacing.Clone(top: 2)
						});
					}

					// Create a button for each container
					var containerButton = new LinkLabel(container.Name, theme)
					{
						Name = "Bread Crumb Button " + container.Name,
						VAnchor = VAnchor.Center,
						Margin = theme.ButtonSpacing.Clone(top: 1),
						TextColor = theme.TextColor
					};
					containerButton.Click += (s, e) =>
					{
						UiThread.RunOnIdle(() => libraryContext.ActiveContainer = container);
					};
					this.AddChild(containerButton);

					firstItem = false;
				}

				// while all the buttons don't fit in the control
				if (this.Parent != null
					&& this.Width > 0
					&& this.Children.Count > 4
					&& this.GetChildrenBoundsIncludingMargins().Width > (this.Width - 20))
				{
					// lets take out the > and put in a ...
					this.RemoveChild(1);

					var separator = new TextWidget("...", textColor: theme.TextColor)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(right:  5)
					};
					this.AddChild(separator, 1);

					while (this.GetChildrenBoundsIncludingMargins().Width > this.Width - 20
						&& this.Children.Count > 4)
					{
						this.RemoveChild(3);
						this.RemoveChild(2);
					}
				}
			}
		}

		public override void OnLoad(EventArgs args)
		{
			this.SetContainer(libraryContext.ActiveContainer);
			base.OnLoad(args);
		}
	}
}