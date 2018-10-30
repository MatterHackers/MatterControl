/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class MHPasswordTextEditWidget : MHTextEditWidget
	{
		private TextEditWidget passwordCoverText;

		private class TextEditOverlay : TextEditWidget
		{
			public TextEditOverlay(string text, int pointSize, double pixelWidth, double pixelHeight, bool multiLine)
				: base(text, 0, 0, pointSize, pixelWidth, pixelHeight, multiLine)
			{
			}

			public override Color BackgroundColor
			{
				get => this.Parent.BackgroundColor;
				set
				{
					if (this.Parent != null)
					{
						this.Parent.BackgroundColor = value;
					}
				}
			}
		}

		public MHPasswordTextEditWidget(string text, ThemeConfig theme, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, string messageWhenEmptyAndNotSelected = "")
			: base(text, theme, pixelWidth, pixelHeight, multiLine, tabIndex, messageWhenEmptyAndNotSelected)
		{
			// remove this so that we can have other content first (the hidden letters)
			this.RemoveChild(noContentFieldDescription);

			passwordCoverText = new TextEditOverlay(text, theme.DefaultFontSize, pixelWidth, pixelHeight, multiLine)
			{
				Selectable = false,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom,
				TextColor = theme.EditFieldColors.Inactive.TextColor
			};
			passwordCoverText.MinimumSize = new Vector2(Math.Max(passwordCoverText.MinimumSize.X, pixelWidth), Math.Max(passwordCoverText.MinimumSize.Y, pixelHeight));

			var internalWidget = this.ActualTextEditWidget.InternalTextEditWidget;
			internalWidget.FocusChanged += (s, e) =>
			{
				passwordCoverText.TextColor = (internalWidget.Focused) ? theme.EditFieldColors.Focused.TextColor : theme.EditFieldColors.Inactive.TextColor;
			};

			this.AddChild(passwordCoverText);

			this.ActualTextEditWidget.TextChanged += (sender, e) =>
			{
				passwordCoverText.Text = new string('●', this.ActualTextEditWidget.Text.Length);
			};

			// put in back in after the hidden text
			noContentFieldDescription.ClearRemovedFlag();
			this.AddChild(noContentFieldDescription);
		}

		public bool Hidden
		{
			get => !passwordCoverText.Visible;
			set => passwordCoverText.Visible = !value;
		}
	}
}