/*
Copyright (c) 2017, Lars Brubaker
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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class NewTabButton : GuiWidget
	{
		private SimpleTabs parentTabControl;
		private ThemeConfig theme;

		public NewTabButton(ImageBuffer imageBuffer, SimpleTabs parentTabControl, ThemeConfig theme)
		{
			this.parentTabControl = parentTabControl;
			this.HAnchor = HAnchor.Fit;
			this.theme = theme;

			IconButton = new IconButton(imageBuffer, theme)
			{
				HAnchor = HAnchor.Left,
				Height = theme.MicroButton.Options.FixedHeight,
				Width = theme.MicroButton.Options.FixedHeight,
				Margin = new BorderDouble(left: 10),
				Name = "Create New",
			};

			this.AddChild(IconButton);
		}

		public ITab LastTab { get; set; }

		public IconButton IconButton { get; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			ChromeTab.DrawTabLowerLeft(
				graphics2D, 
				this.LocalBounds, 
				(parentTabControl.ActiveTab == this.LastTab) ? theme.ActiveTabColor : theme.InactiveTabColor);

			base.OnDraw(graphics2D);
		}
	}
}