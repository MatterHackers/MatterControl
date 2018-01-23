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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class InlineTitleEdit : Toolbar
	{
		public event EventHandler TitleChanged;

		private TextWidget titleText;
		private GuiWidget editButton;
		private GuiWidget saveButton;
		private SearchInputBox searchPanel;

		public InlineTitleEdit(string title, ThemeConfig theme, bool boldFont = false)
			: base(null)
		{
			this.Padding = theme.ToolbarPadding;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			titleText = new TextWidget(title, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: theme.DefaultFontSize, bold: boldFont)
			{
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(left: 5)
			};
			this.AddChild(titleText);

			this.ActionArea.VAnchor = VAnchor.Stretch;
			this.ActionArea.MinimumSize = new VectorMath.Vector2(0, titleText.Height);

			saveButton = new IconButton(AggContext.StaticData.LoadIcon("fa-save_16.png", 16, 16, IconColor.Theme), theme)
			{
				ToolTipText = "Save".Localize(),
				Visible = false
			};

			searchPanel = new SearchInputBox()
			{
				Visible = false,
				Margin = new BorderDouble(left: 4)
			};
			searchPanel.searchInput.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				this.Text = searchPanel.Text;
				this.SetVisibility(showEditPanel: false);
				this.TitleChanged?.Invoke(this, null);
			};

			searchPanel.ResetButton.Name = "Close Title Edit";
			searchPanel.ResetButton.ToolTipText = "Close".Localize();
			searchPanel.ResetButton.Click += (s, e) =>
			{
				this.SetVisibility(showEditPanel: false);
			};
			this.AddChild(searchPanel);

			var rightPanel = new FlowLayoutWidget();

			editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme), theme)
			{
				ToolTipText = "Edit".Localize(),
			};
			editButton.Click += (s, e) =>
			{
				searchPanel.Text = this.Text;
				this.SetVisibility(showEditPanel: true);
			};
			rightPanel.AddChild(editButton);

			saveButton.Click += (s, e) =>
			{
				this.Text = searchPanel.Text;
				this.SetVisibility(showEditPanel: false);
				this.TitleChanged?.Invoke(this, null);
			};
			rightPanel.AddChild(saveButton);

			this.SetRightAnchorItem(rightPanel);

			this.ActionArea.Margin = this.ActionArea.Margin.Clone(right: rightPanel.Width + 5);
		}

		public override string Text
		{
			get => titleText.Text;
			set
			{
				if (titleText.Text != value)
				{
					titleText.Text = value;
				}
			}
		}

		public void SetVisibility(bool showEditPanel)
		{
			editButton.Visible = !showEditPanel;
			titleText.Visible = !showEditPanel;

			saveButton.Visible = showEditPanel;
			searchPanel.Visible = showEditPanel;
		}
	}
}