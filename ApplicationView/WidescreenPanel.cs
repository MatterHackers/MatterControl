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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class WidescreenPanel : FlowLayoutWidget
	{
		public WidescreenPanel()
		{
		}

		public override void Initialize()
		{
			base.Initialize();

			this.AnchorAll();
			this.Name = "WidescreenPanel";
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			var theme = ApplicationController.Instance.Theme;

			var library3DViewSplitter = new Splitter()
			{
				SplitterDistance = UserSettings.Instance.LibraryViewWidth,
				SplitterWidth = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			library3DViewSplitter.AnchorAll();

			library3DViewSplitter.DistanceChanged += (s, e) =>
			{
				UserSettings.Instance.LibraryViewWidth = library3DViewSplitter.SplitterDistance;
			};

			this.AddChild(library3DViewSplitter);

			var leftNav = new FlowLayoutWidget(FlowDirection.TopToBottom);
			leftNav.AnchorAll();

			var toolbar = new Toolbar()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(16, 16),
				BackgroundColor = theme.ActiveTabBarBackground,
				BorderColor = theme.ActiveTabColor,
				Border = 0 //new BorderDouble(bottom: 2),
			};
			toolbar.ActionArea.AddChild(new BrandMenuButton(theme)
			{
				MinimumSize = new Vector2(0, 34),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Border = new BorderDouble(right: 1),
				BorderColor = theme.MinimalShade
			});
			leftNav.AddChild(toolbar);

			var partPreviewContent = new PartPreviewContent()
			{
				VAnchor = VAnchor.Bottom | VAnchor.Top,
				HAnchor = HAnchor.Left | HAnchor.Right
			};

			leftNav.AddChild(new PrintLibraryWidget(partPreviewContent, theme)
			{
				BackgroundColor = theme.ActiveTabColor
			});

			// put in the left column
			library3DViewSplitter.Panel1.AddChild(leftNav);

			// put in the right column
			library3DViewSplitter.Panel2.AddChild(partPreviewContent);
		}
	}

	public class BrandMenuButton : PopupButton
	{
		public BrandMenuButton(ThemeConfig theme)
		{
			this.Name = "MatterControl BrandMenuButton";
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = 0;
			this.PopupContent = new ApplicationSettingsWidget(theme.MenuButtonFactory, theme)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Width = 500,
				BackgroundColor = Color.White
			};

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(row);

			row.AddChild(new IconButton(AggContext.StaticData.LoadIcon("mh-app-logo.png", IconColor.Theme), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 4),
				Selectable = false
			});

			row.AddChild(new TextWidget(ApplicationController.Instance.ShortProductName, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center
			});
		}
	}

	public class UpdateNotificationMark : GuiWidget
	{
		public UpdateNotificationMark()
			: base(12, 12)
		{
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Circle(Width / 2, Height / 2, Width / 2, Color.White);
			graphics2D.Circle(Width / 2, Height / 2, Width / 2 - 1, Color.Red);
			graphics2D.FillRectangle(Width / 2 - 1, Height / 2 - 3, Width / 2 + 1, Height / 2 + 3, Color.White);
			base.OnDraw(graphics2D);
		}
	}
}
