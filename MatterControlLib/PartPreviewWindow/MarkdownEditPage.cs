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
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class MarkdownEditPage : DialogPage
	{
		private MHTextEditWidget editWidget;
		private MarkdownWidget markdownWidget;

		public MarkdownEditPage(UIField uiField)
		{
			this.WindowTitle = "MatterControl - " + "Markdown Edit".Localize();
			this.HeaderText = "Edit Page".Localize() + ":";

			var tabControl = new SimpleTabs(theme, new GuiWidget())
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;
			tabControl.TabBar.Padding = 0;

			contentRow.AddChild(tabControl);
			contentRow.Padding = 0;

			var editContainer = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = theme.DefaultContainerPadding,
				BackgroundColor = theme.BackgroundColor
			};

			editWidget = new MHTextEditWidget("", theme, multiLine: true, typeFace: ApplicationController.GetTypeFace(NamedTypeFace.Liberation_Mono))
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Name = this.Name
			};
			editWidget.DrawFromHintedCache();
			editWidget.ActualTextEditWidget.VAnchor = VAnchor.Stretch;

			editContainer.AddChild(editWidget);

			markdownWidget = new MarkdownWidget(theme, true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = 0,
				Padding = 0,
			};

			var previewTab = new ToolTab("Preview", "Preview".Localize(), tabControl, markdownWidget, theme, hasClose: false)
			{
				Name = "Preview Tab"
			};
			tabControl.AddTab(previewTab);

			var editTab = new ToolTab("Edit", "Edit".Localize(), tabControl, editContainer, theme, hasClose: false)
			{
				Name = "Edit Tab"
			};
			tabControl.AddTab(editTab);

			tabControl.ActiveTabChanged += (s, e) =>
			{
				if (tabControl.SelectedTabIndex == 0)
				{
					markdownWidget.Markdown = editWidget.Text;
				}
			};

			tabControl.SelectedTabIndex = 0;

			var saveButton = theme.CreateDialogButton("Save".Localize());
			saveButton.Click += (s, e) =>
			{
				uiField.SetValue(
					editWidget.Text.Replace("\n", "\\n"),
					userInitiated: true);

				this.DialogWindow.CloseOnIdle();
			};
			this.AddPageAction(saveButton);

			var link = new LinkLabel("Markdown Help", theme)
			{
				Margin = new BorderDouble(right: 20),
				VAnchor = VAnchor.Center
			};
			link.Click += (s, e) =>
			{
				ApplicationController.Instance.LaunchBrowser("https://guides.github.com/features/mastering-markdown/");
			};
			footerRow.AddChild(link, 0);
		}

		public string Markdown
		{
			get => editWidget.Text;
			set
			{
				editWidget.Text = value;
				markdownWidget.Markdown = value;
			}
		}
	}
}