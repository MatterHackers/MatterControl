/*
Copyright (c) 2018, John Lewin
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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.CustomWidgets
{
	/// <summary>
	/// A container control having a header and a content panel, with optional collapse behavior and right aligned widget. Additionally support persistent expansion state via serializationKey
	/// </summary>
	public class SectionWidget : FlowLayoutWidget, IIgnoredPopupChild
	{
		protected ExpandCheckboxButton checkbox;
		protected GuiWidget rightAlignedContent;

		public event EventHandler<bool> ExpandedChanged;

		private bool setContentVAnchor;

		public SectionWidget(string sectionTitle, GuiWidget sectionContent, ThemeConfig theme, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true, string serializationKey = null, bool defaultExpansion = false, bool setContentVAnchor = true)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			theme.ApplyBorder(this, new BorderDouble(top: 1));

			this.setContentVAnchor = setContentVAnchor;

			if (!string.IsNullOrEmpty(sectionTitle))
			{
				// Add heading
				var pointSize = (headingPointSize) == -1 ? theme.DefaultFontSize : headingPointSize;

				// If the control is expandable and a serialization key is supplied, set expanded from persisted value
				if (serializationKey != null && expandingContent)
				{
					string dbValue = UserSettings.Instance.get(serializationKey);
					expanded = dbValue == "1" || (dbValue == null && defaultExpansion);
				}

				checkbox = new ExpandCheckboxButton(sectionTitle, theme, pointSize: pointSize, expandable: expandingContent)
				{
					HAnchor = HAnchor.Stretch,
					Checked = expanded,
					Padding = 0
				};
				checkbox.CheckedStateChanged += (s, e) =>
				{
					if (expandingContent)
					{
						ContentPanel.Visible = checkbox.Checked;
						this.ExpandedChanged?.Invoke(this, checkbox.Checked);
					}
					// TODO: Remove this Height = 10 and figure out why the layout engine is not sizing these correctly without this.
					ContentPanel.Height = 10;
				};

				if (serializationKey != null)
				{
					checkbox.CheckedStateChanged += (s, e) =>
					{
						UserSettings.Instance.set(serializationKey, checkbox.Checked ? "1" : "0");
					};
				}

				if (rightAlignedContent == null)
				{
					this.AddChild(checkbox);
				}
				else
				{
					rightAlignedContent.HAnchor |= HAnchor.Right;

					var headingRow = new GuiWidget()
					{
						VAnchor = VAnchor.Fit,
						HAnchor = HAnchor.Stretch
					};
					headingRow.AddChild(checkbox);
					headingRow.AddChild(rightAlignedContent);
					this.AddChild(headingRow);
				}

				this.rightAlignedContent = rightAlignedContent;
			}

			sectionContent.Visible = expanded;

			this.SetContentWidget(sectionContent);
		}

		public ICheckbox Checkbox => checkbox;

		public bool ShowExpansionIcon
		{
			get => checkbox.ShowIcon;
			set
			{
				checkbox.ShowIcon = value;
				checkbox.Padding = value ? 0 : new BorderDouble(left: this.ContentPanel.Margin.Left);
			}
		}

		public GuiWidget ContentPanel { get; private set; }

		public void SetContentWidget(GuiWidget guiWidget)
		{
			// Close old child
			this.ContentPanel?.Close();

			// Apply default rules for panel widget
			guiWidget.HAnchor = HAnchor.Stretch;

			if (setContentVAnchor)
			{
				guiWidget.VAnchor = VAnchor.Fit;
			}

			// Set
			this.AddChild(guiWidget);

			// Store
			this.ContentPanel = guiWidget;
			this.ContentPanel.BorderColor = Color.Transparent;
		}

		public int BorderRadius { get; set; } = 0;

		public bool ExpandableWhenDisabled { get; set; }

		public override string Text
		{
			get => checkbox?.Text;
			set
			{
				if (checkbox != null)
				{
					checkbox.Text = value;
				}
			}
		}

		public override bool Enabled
		{
			get => (this.ExpandableWhenDisabled) ? true: base.Enabled;
			set
			{
				if (this.ExpandableWhenDisabled)
				{
					this.ContentPanel.Enabled = value;
				}
				else
				{
					base.Enabled = value;
				}
			}
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (this.BorderRadius > 0)
			{
				var rect = new RoundedRect(this.LocalBounds, this.BorderRadius);
				graphics2D.Render(rect, this.BackgroundColor);
			}
			else
			{
				base.OnDrawBackground(graphics2D);
			}
		}

		public bool KeepMenuOpen => false;
	}
}