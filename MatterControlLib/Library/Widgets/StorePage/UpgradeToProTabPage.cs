/*
Copyright (c) 2020, Lars Brubaker, John Lewin
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

using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
    public class UpgradeToProTabPage : GuiWidget
	{
		private MarkdownWidget markdownWidget;

		public UpgradeToProTabPage(ThemeConfig theme)
		{
			this.Padding = new BorderDouble(3);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.MinimumSize = new Vector2(200, 200);
			this.BackgroundColor = theme.TabBodyBackground;

			this.Name = "UpgradeTab";

			markdownWidget = new MarkdownWidget(theme)
			{
				Padding = new BorderDouble(left: theme.DefaultContainerPadding / 2)
			};

			markdownWidget.Markdown = "# Upgrade to [MatterControl Pro](https://www.matterhackers.com/admin/product-preview/ag1zfm1oLXBscy1wcm9kchsLEg5Qcm9kdWN0TGlzdGluZxiAgIC_65WICww)";

			CheckForUpdate();

			this.AddChild(markdownWidget);
		}

		private void CheckForUpdate()
		{
			var uri = "https://matterhackers.github.io/MatterControl-Docs/ProContent/Upgrade_To_Pro.md";

			WebCache.RetrieveText(uri,
				(markDown) =>
				{
					UiThread.RunOnIdle(() =>
					{
						markdownWidget.Markdown = markDown;
					});
				});
		}
	}
}
