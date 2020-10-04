/*
Copyright (c) 2020, Kevin Pope, John Lewin, Lars Brubaker
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
using System.Linq;
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class PrintHistoryEditor
	{
		private ThemeConfig theme;
		private PrintTask printTask;
		private IEnumerable<PrintTask> printTasks;

		public PrintHistoryEditor(ThemeConfig theme, PrintTask printTask, IEnumerable<PrintTask> printTasks)
		{
			this.theme = theme;
			this.printTask = printTask;
			this.printTasks = printTasks;
		}

		public static readonly string[] QualityNames = new string[]
		{
			"Failed".Localize(),
			"Terrible".Localize(),
			"Bad".Localize(),
			"Good".Localize(),
			"Great".Localize(),
		};

		public void AddNotesMenu(PopupMenu popupMenu, IEnumerable<PrintTask> printTasks, Action notesChanged)
		{
			var addNotest = popupMenu.CreateMenuItem(string.IsNullOrEmpty(printTask.Note) ? "Add Note...".Localize() : "Edit Note...".Localize());
			addNotest.Enabled = printTasks.Any();
			addNotest.Click += (s, e) =>
			{
				var inputBoxPage = new InputBoxPage(
					"Print History Note".Localize(),
					"Note".Localize(),
					printTask.Note == null ? "" : printTask.Note,
					"Enter Note Here".Localize(),
					string.IsNullOrEmpty(printTask.Note) ? "Add Note".Localize() : "Update".Localize(),
					(newNote) =>
					{
						printTask.Note = newNote;
						printTask.Commit();
						popupMenu.Unfocus();
						notesChanged();
					})
				{
					AllowEmpty = true,
				};

				inputBoxPage.ContentRow.AddChild(CreateDefaultOptions(inputBoxPage));

				DialogWindow.Show(inputBoxPage);

				inputBoxPage.Parent.Height += 40 * GuiWidget.DeviceScale;
			};
		}

		public void AddQualityMenu(PopupMenu popupMenu, Action notesChanged)
		{
			var theme = ApplicationController.Instance.MenuTheme;

			var content = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Stretch
			};

			var textWidget = new TextWidget("Print Quality".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				// Padding = MenuPadding,
				VAnchor = VAnchor.Center
			};
			content.AddChild(textWidget);

			content.AddChild(new HorizontalSpacer());

			var siblings = new List<GuiWidget>();

			for (int i = 0; i < QualityNames.Length; i++)
			{
				var button = new RadioButton(new TextWidget(i.ToString(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor))
				{
					Enabled = printTask.PrintComplete,
					Checked = printTask.QualityWasSet && printTask.PrintQuality == i,
					ToolTipText = QualityNames[i],
					Margin = 0,
					Padding = 5,
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
				};

				button.MouseEnterBounds += (s, e) =>
				{
					button.BackgroundColor = theme.AccentMimimalOverlay;
				};

				button.MouseLeaveBounds += (s, e) =>
				{
					button.BackgroundColor = button.Checked ? theme.AccentMimimalOverlay : Color.Transparent;
				};

				siblings.Add(button);

				if (button.Checked && button.Enabled)
				{
					button.BackgroundColor = theme.AccentMimimalOverlay;
				}

				button.SiblingRadioButtonList = siblings;

				content.AddChild(button);

				button.Click += (s, e) =>
				{
					printTask.PrintQuality = siblings.IndexOf((GuiWidget)s);
					printTask.QualityWasSet = true;
					printTask.Commit();
					popupMenu.Unfocus();
					notesChanged();
				};
			}

			var menuItem = new PopupMenu.MenuItem(content, theme)
			{
				HAnchor = HAnchor.Fit | HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				HoverColor = Color.Transparent,
			};
			popupMenu.AddChild(menuItem);
		}

		private GuiWidget CreateDefaultOptions(GuiWidget textField)
		{
			var issues = new string[]
			{
				"Bad Thermistor",
				"Bed Dislodged",
				"Bowden Tube Popped Out",
				"Computer Crashed",
				"Computer Slow/Lagging",
				"Couldn't Resume",
				"Dislodged From Bed",
				"Extruder Slipping",
				"Filament Jam",
				"Filament Runout",
				"Filament Snapped",
				"First Layer Bad Quality",
				"Flooded Hot End",
				"Initial Z Height Incorrect",
				"Layer Shift",
				"Power Outage",
				"Print Quality",
				"Rough Overhangs",
				"Skipped Layers",
				"Some Parts Lifted",
				"Stringing / Poor retractions",
				"Test Print",
				"Thermal Runaway - Bed",
				"Thermal Runaway - Hot End",
				"Thermal Runaway",
				"Took Too Long To Heat",
				"User Error",
				"Warping",
				"Wouldn’t Slice Correctly",
			};

			var dropdownList = new MHDropDownList("Standard Issues".Localize(), theme, maxHeight: 300 * GuiWidget.DeviceScale);

			foreach (var issue in issues)
			{
				MenuItem newItem = dropdownList.AddItem(issue);

				newItem.Selected += (sender, e) =>
				{
					textField.Text = issue;
				};
			}

			return dropdownList;
		}

		public void CollectInfoPrintCanceled()
		{
			string markdownText = @"Looks like you canceled this print. If you need help, here are some links that might be useful.
- [MatterControl Docs](https://www.matterhackers.com/mattercontrol/support)
- [Tutorials](https://www.matterhackers.com/store/l/mattercontrol/sk/MKZGTDW6#tutorials)
- [Trick, Tips & Support Articles](https://www.matterhackers.com/support#mattercontrol)
- [User Forum](https://forums.matterhackers.com/recent)";

			ShowNotification("Congratulations Print Complete".Localize(), markdownText, UserSettingsKey.ShownPrintCompleteMessage);
			//var details = new CollectPrintDetailsPage(ShowNotification("Print Canceled".Localize(),
				//markdownText,
				//UserSettingsKey.ShownPrintCanceledMessage);
		}

		public void CollectInfoPrintFinished()
		{
			// show a long running task asking about print feedback and up-selling more materials
			// Ask about the print, offer help if needed.
			// Let us know how your print came out.
			string markdownText = @"**Find more at MatterHackers**

Supplies and accessories:
- [Filament](https://www.matterhackers.com/store/c/3d-printer-filament)
- [Bed Adhesives](https://www.matterhackers.com/store/c/3d-printer-adhesive)
- [Digital Designs](https://www.matterhackers.com/store/c/digital-designs)

Support and tutorials:
- [MatterControl Docs](https://www.matterhackers.com/mattercontrol/support)
- [Tutorials](https://www.matterhackers.com/store/l/mattercontrol/sk/MKZGTDW6#tutorials)
- [Trick, Tips & Support Articles](https://www.matterhackers.com/support#mattercontrol)
- [User Forum](https://forums.matterhackers.com/recent)";

			ShowNotification("Congratulations Print Complete".Localize(), markdownText, UserSettingsKey.ShownPrintCompleteMessage);
		}

		private void ShowNotification(string title, string markdownText, string userKey)
		{
			var hideAfterPrintMessage = new CheckBox("Don't show this again".Localize())
			{
				TextColor = AppContext.Theme.TextColor,
				Margin = new BorderDouble(top: 6, left: 6),
				HAnchor = Agg.UI.HAnchor.Left,
				Checked = UserSettings.Instance.get(userKey) == "false",
			};
			hideAfterPrintMessage.Click += (s, e1) =>
			{
				if (hideAfterPrintMessage.Checked)
				{
					UserSettings.Instance.set(userKey, "false");
				}
				else
				{
					UserSettings.Instance.set(userKey, "true");
				}
			};

			if (!hideAfterPrintMessage.Checked
				&& !string.IsNullOrEmpty(markdownText))
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null,
						markdownText,
						title,
						new[] { hideAfterPrintMessage },
						StyledMessageBox.MessageType.OK,
						useMarkdown: true);
				});
			}
		}
	}

	public class CollectPrintDetailsPage : DialogPage
	{
		private MHTextEditWidget textEditWidget;

		public override string Text { get => textEditWidget.Text; set => textEditWidget.Text = value; }

		public CollectPrintDetailsPage(string windowTitle, string initialValue, string emptyText, string discriptionMarkdown, string linkoutMarkdown)
		{
			this.WindowTitle = windowTitle;
			this.HeaderText = windowTitle;
			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 200 * GuiWidget.DeviceScale);

			contentRow.AddChild(new MarkdownWidget(theme)
			{
				Markdown = discriptionMarkdown,
			});

			// Adds text box and check box to the above container
			textEditWidget = new MHTextEditWidget(initialValue, theme, pixelWidth: 300, messageWhenEmptyAndNotSelected: emptyText);
			textEditWidget.Name = "InputBoxPage TextEditWidget";
			textEditWidget.HAnchor = HAnchor.Stretch;
			textEditWidget.Margin = new BorderDouble(5);
			//textEditWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
			//{
			//	actionButton.InvokeClick();
			//};
			//contentRow.AddChild(textEditWidget);

			//actionButton = theme.CreateDialogButton(actionButtonTitle);
			//actionButton.Name = "InputBoxPage Action Button";
			//actionButton.Cursor = Cursors.Hand;
			//actionButton.Click += (s, e) =>
			//{
			//	string newName = textEditWidget.ActualTextEditWidget.Text;
			//	if (!string.IsNullOrEmpty(newName) || AllowEmpty)
			//	{
			//		action.Invoke(newName);
			//		this.DialogWindow.CloseOnIdle();
			//	}
			//};
			//this.AddPageAction(actionButton);

			contentRow.AddChild(new MarkdownWidget(theme)
			{
				Markdown = linkoutMarkdown,
			});
		}

		public bool AllowEmpty { get; set; }

		public override void OnLoad(EventArgs args)
		{
			UiThread.RunOnIdle(() =>
			{
				textEditWidget.Focus();
				textEditWidget.ActualTextEditWidget.InternalTextEditWidget.SelectAll();
			});
			base.OnLoad(args);
		}
	}
}