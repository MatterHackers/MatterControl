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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class HelpArticleHeader : Toolbar
	{
		public event EventHandler EditClicked;

		public HelpArticleHeader(HelpArticle helpArticle, ThemeConfig theme, bool boldFont = false, int pointSize = -1, string editToolTipText = null)
			: base(theme)
		{
			this.Padding = theme.ToolbarPadding;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			var titleText = new TextWidget(helpArticle.Name, textColor: theme.TextColor, pointSize: pointSize > 0 ? pointSize : theme.DefaultFontSize, bold: boldFont)
			{
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(left: 5)
			};
			this.AddChild(titleText);

			this.ActionArea.VAnchor = VAnchor.Stretch;
			this.ActionArea.MinimumSize = new Vector2(0, titleText.Height);

			var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, theme.InvertIcons), theme)
			{
				ToolTipText = editToolTipText ?? "Edit".Localize(),
				Name = helpArticle.Name + " Edit"
			};
			editButton.Click += (s, e) =>
			{
				this.EditClicked?.Invoke(this, null);
			};
			this.SetRightAnchorItem(editButton);

			this.ActionArea.Margin = this.ActionArea.Margin.Clone(right: editButton.Width + 5);
		}
	}
}