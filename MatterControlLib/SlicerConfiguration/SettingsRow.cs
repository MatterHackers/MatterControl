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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SettingsRow : FlowLayoutWidget
	{
		protected GuiWidget overrideIndicator;
		protected const bool debugLayout = false;
		protected ThemeConfig theme;

		private bool mouseInBounds = false;
		private Color hoverColor;
		private bool fullRowSelect;
		private GuiWidget settingsLabel;
		protected ImageWidget imageWidget;

		public GuiWidget ActionWidget { get; set; }

		public SettingsRow(string title, string helpText, ThemeConfig theme, ImageBuffer icon = null, bool enforceGutter = false, bool fullRowSelect = false)
		{
			using (this.LayoutLock())
			{
				this.theme = theme;
				this.fullRowSelect = fullRowSelect;

				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Fit;
				this.MinimumSize = new Vector2(0, theme.ButtonHeight);
				this.Border = new BorderDouble(bottom: 1);
				this.BorderColor = theme.RowBorder;

				hoverColor = theme.MinimalShade;

				if (icon != null)
				{
					this.AddChild(
						imageWidget = new ImageWidget(icon)
						{
							Margin = new BorderDouble(right: 6, left: 6),
							VAnchor = VAnchor.Center
						});
				}
				else if (enforceGutter)
				{
					// Add an icon placeholder to get consistent label indenting on items lacking icons
					this.AddChild(new GuiWidget()
					{
						Width = 24 + 12,
						Height = 24,
						Margin = new BorderDouble(0)
					});
				}
				else
				{
					this.AddChild(overrideIndicator = new GuiWidget()
					{
						VAnchor = VAnchor.Stretch,
						HAnchor = HAnchor.Absolute,
						Width = 3,
						Margin = new BorderDouble(right: 6)
					});
				}

				this.AddChild(settingsLabel = SettingsRow.CreateSettingsLabel(title, helpText, theme.TextColor));

				this.AddChild(new HorizontalSpacer());
			}

			this.PerformLayout();

			if (fullRowSelect)
			{
				this.Cursor = Cursors.Hand;
			}
		}

		public static GuiWidget CreateSettingsLabel(string label, string helpText, Color textColor)
		{
			return new TextWidget(label, textColor: textColor, pointSize: 10)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				ToolTipText = helpText,
			};
		}

		public override string ToolTipText
		{
			get => settingsLabel?.ToolTipText;
			set
			{
				if (settingsLabel != null)
				{
					settingsLabel.ToolTipText = value;
				}
			}
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (fullRowSelect)
			{
				childToAdd.Selectable  = false;
			}

			base.AddChild(childToAdd, indexInChildrenList);
		}

		public override Color BackgroundColor
		{
			get => (mouseInBounds) ? hoverColor : base.BackgroundColor;
			set => base.BackgroundColor = value;
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			this.Invalidate();
			base.OnMouseEnter(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;

			this.Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}
	}
}
