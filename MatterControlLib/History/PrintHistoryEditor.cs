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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class PrintHistoryEditor
	{
		private readonly PrinterConfig printer;
		private readonly ThemeConfig theme;
		private readonly PrintTask printTask;
		private readonly IEnumerable<PrintTask> printTasks;

		public PrintHistoryEditor(PrinterConfig printer, ThemeConfig theme, PrintTask printTask, IEnumerable<PrintTask> printTasks)
		{
			this.printer = printer;
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
			var addNote = popupMenu.CreateMenuItem(string.IsNullOrEmpty(printTask.Note) ? "Add Note...".Localize() : "Edit Note...".Localize());
			addNote.Enabled = printTasks.Any();
			addNote.Click += (s, e) =>
			{
				var inputBoxPage = new InputBoxPage(
					"Print History Note".Localize(),
					"",
					printTask.Note ?? "",
					"Enter Note Here".Localize(),
					string.IsNullOrEmpty(printTask.Note) ? "Add Note".Localize() : "Update".Localize(),
					(newNote) =>
					{
						printTask.Note = newNote;
						printTask.CommitAndPushToServer();
						popupMenu.Unfocus();
						notesChanged();
					})
				{
					AllowEmpty = true,
				};

				inputBoxPage.ContentRow.AddChild(CreateDefaultOptions(inputBoxPage.TextEditWidget, theme, null), 0);

				DialogWindow.Show(inputBoxPage);

				inputBoxPage.Parent.Height += 40 * GuiWidget.DeviceScale;
			};
		}

		public static GuiWidget GetQualityWidget(ThemeConfig theme, PrintTask printTask, Action clicked, double buttonFontSize)
		{
			var content = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Stretch
			};
			var siblings = new List<GuiWidget>();

			var textWidget = new TextWidget("Print Quality".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center
			};

			content.AddChild(textWidget);

			var size = (int)(buttonFontSize * GuiWidget.DeviceScale);

			var star = StaticData.Instance.LoadIcon("star.png", size, size).SetToColor(theme.TextColor);
			var openStar = StaticData.Instance.LoadIcon("open_star.png", size, size).SetToColor(theme.TextColor);
			var failure = StaticData.Instance.LoadIcon("failure.png", size, size).SetToColor(theme.TextColor);

			content.AddChild(new GuiWidget(size, 1));

			content.MouseLeaveBounds += (s, e) =>
			{
				SetStarState(theme, siblings, printTask);
			};

			for (int i = 0; i < QualityNames.Length; i++)
			{
				var buttonIndex = i;
				GuiWidget buttonContent;
				if (i == 0)
				{
					buttonContent = new ImageWidget(failure);
				}
				else
				{
					buttonContent = new GuiWidget()
					{
						HAnchor = HAnchor.Fit,
						VAnchor = VAnchor.Fit
					};
					buttonContent.AddChild(new ImageWidget(openStar)
					{
						Name = "open"
					});
					buttonContent.AddChild(new ImageWidget(star)
					{
						Name = "closed",
						Visible = false
					});
				}

				var button = new RadioButton(buttonContent)
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
					// set the correct filled stars for the hover
					for (int j = 0; j < siblings.Count; j++)
					{
						var open = siblings[j].Descendants().Where(d => d.Name == "open").FirstOrDefault();
						var closed = siblings[j].Descendants().Where(d => d.Name == "closed").FirstOrDefault();

						if (j == 0)
						{
							if (buttonIndex == 0)
							{
								siblings[j].BackgroundColor = theme.AccentMimimalOverlay;
							}
							else
							{
								siblings[j].BackgroundColor = Color.Transparent;
							}
						}
						else if (j <= buttonIndex)
						{
							siblings[j].BackgroundColor = theme.AccentMimimalOverlay;
						}
						else
						{
							siblings[j].BackgroundColor = Color.Transparent;
						}

						if (j <= buttonIndex)
						{
							if (open != null)
							{
								open.Visible = false;
								closed.Visible = true;
							}
						}
						else
						{
							if (open != null)
							{
								open.Visible = true;
								closed.Visible = false;
							}
						}
					}
				};

				siblings.Add(button);

				button.SiblingRadioButtonList = siblings;

				content.AddChild(button);

				button.Click += (s, e) =>
				{
					printTask.PrintQuality = siblings.IndexOf((GuiWidget)s);
					printTask.QualityWasSet = true;
					printTask.CommitAndPushToServer();
					clicked();
				};
			}

			SetStarState(theme, siblings, printTask);

			return content;
		}

		private static void SetStarState(ThemeConfig theme, List<GuiWidget> siblings, PrintTask printTask)
		{
			var checkedButton = -1;
			if (printTask.QualityWasSet)
			{
				checkedButton = printTask.PrintQuality;
			}

			for (int j = 0; j < siblings.Count; j++)
			{
				var open = siblings[j].Descendants().Where(d => d.Name == "open").FirstOrDefault();
				var closed = siblings[j].Descendants().Where(d => d.Name == "closed").FirstOrDefault();

				if (j == 0)
				{
					if (checkedButton == 0)
					{
						siblings[j].BackgroundColor = theme.AccentMimimalOverlay;
					}
					else
					{
						siblings[j].BackgroundColor = Color.Transparent;
					}
				}
				else if (j <= checkedButton)
				{
					siblings[j].BackgroundColor = theme.AccentMimimalOverlay;
				}
				else
				{
					siblings[j].BackgroundColor = Color.Transparent;
				}

				if (j <= checkedButton)
				{
					if (open != null)
					{
						open.Visible = false;
						closed.Visible = true;
					}
				}
				else
				{
					if (open != null)
					{
						open.Visible = true;
						closed.Visible = false;
					}
				}
			}
		}

		public static GuiWidget CreateDefaultOptions(GuiWidget textField, ThemeConfig theme, Action selectionChanged)
		{
			var selectString = "- " + "What went wrong?".Localize() + " -";
			var menuButton = new PopupMenuButton(selectString, theme);
			var menuButtonText = menuButton.Descendants<TextWidget>().First();
			menuButtonText.AutoExpandBoundsToText = true;

			void AddSelection(PopupMenu menu, string text, string helpUrl = "", bool other = false)
			{
				var menuItem = menu.CreateMenuItem(text);

				menuItem.Click += (s, e) =>
				{
					textField.Name = helpUrl;

					var markdownWidget = textField.Parents<SystemWindow>().First().Descendants<MarkdownWidget>().LastOrDefault();
					if (markdownWidget != null)
					{
						markdownWidget.Markdown = textField.Name;
						markdownWidget.Visible = !string.IsNullOrEmpty(markdownWidget.Markdown);
					}

					if (other)
					{
						textField.Text = "";
						textField.Visible = true;
						UiThread.RunOnIdle(textField.Focus);
						menuButtonText.Text = "Other".Localize() + "...";
					}
					else
					{
						textField.Text = text;
						textField.Visible = false;
						menuButtonText.Text = textField.Text;
					}

					selectionChanged?.Invoke();
				};
			}

			string TroubleShooting(string type, int issue)
			{
				return $"For help with {type} and other issues, please read MatterHackers [Troubleshooting Guide](https://www.matterhackers.com/articles/3d-printer-troubleshooting-guide#Issue{issue})";
			}

			menuButton.DynamicPopupContent = () =>
			{
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				popupMenu.CreateSubMenu("First Layer".Localize(),
					theme,
					(menu) =>
					{
						AddSelection(menu, "First Layer Bad Quality".Localize(), TroubleShooting("the first layer", 1));
						AddSelection(menu, "Initial Z Height Incorrect".Localize(), TroubleShooting("initial Z height", 1));
					});
				popupMenu.CreateSubMenu("Quality".Localize(),
					theme,
					(menu) =>
					{
						AddSelection(menu, "General Quality".Localize(), TroubleShooting("general quality", 100));
						AddSelection(menu, "Rough Overhangs".Localize());
						AddSelection(menu, "Skipped Layers".Localize());
						AddSelection(menu, "Some Parts Lifted".Localize(), TroubleShooting("lifting", 6));
						AddSelection(menu, "Stringing / Poor retractions".Localize(), TroubleShooting("stringing", 9));
						AddSelection(menu, "Warping".Localize(), TroubleShooting("warping", 6));
						AddSelection(menu, "Dislodged From Bed".Localize(), TroubleShooting("adhesion", 2));
						AddSelection(menu, "Layer Shift".Localize(), TroubleShooting("layer shifting", 8));
					});
				popupMenu.CreateSubMenu("Mechanical".Localize(),
					theme,
					(menu) =>
					{
						AddSelection(menu, "Bed Dislodged".Localize());
						AddSelection(menu, "Bowden Tube Popped Out".Localize());
						AddSelection(menu, "Extruder Slipping".Localize(), TroubleShooting("the extruder", 13));
						AddSelection(menu, "Flooded Hot End".Localize());
						AddSelection(menu, "Power Outage".Localize());
					});
				popupMenu.CreateSubMenu("Computer / MatterControl".Localize() + "    ",
					theme,
					(menu) =>
					{
						AddSelection(menu, "Computer Crashed".Localize());
						AddSelection(menu, "Computer Slow / Lagging".Localize());
						AddSelection(menu, "Couldn't Resume".Localize());
						AddSelection(menu, "Wouldn’t Slice Correctly".Localize());
					});
				popupMenu.CreateSubMenu("Filament".Localize(),
					theme,
					(menu) =>
					{
						AddSelection(menu, "Filament Jam".Localize());
						AddSelection(menu, "Filament Runout".Localize());
						AddSelection(menu, "Filament Snapped".Localize());
					});
				popupMenu.CreateSubMenu("Heating".Localize(),
					theme,
					(menu) =>
					{
						AddSelection(menu, "Thermal Runaway - Bed".Localize());
						AddSelection(menu, "Thermal Runaway - Hot End".Localize());
						AddSelection(menu, "Heating".Localize());
						AddSelection(menu, "Took Too Long To Heat".Localize());
						AddSelection(menu, "Bad Thermistor".Localize());
						AddSelection(menu, "Bad Thermistor".Localize());
					});
				AddSelection(popupMenu, "Test Print".Localize());
				AddSelection(popupMenu, "User Error".Localize());
				AddSelection(popupMenu, "Other".Localize(), "", true);

				return popupMenu;
			};

			textField.Visible = false;
			menuButton.VAnchor = VAnchor.Fit;

			return menuButton;
		}

		private static string articles = @"
- [MatterControl Tutorials](https://www.matterhackers.com/store/l/mattercontrol/sk/MKZGTDW6#tutorials)
- [Trick, Tips & Support Articles](https://www.matterhackers.com/topic/tips-and-tricks)
- [MatterControl Articles](https://www.matterhackers.com/topic/mattercontrol)
- [MatterControl Docs](https://www.matterhackers.com/mattercontrol/support)
- [User Forum](https://forums.matterhackers.com/recent)";

		public void CollectInfoPrintCanceled()
		{
			string markdownText = @"If you need help, here are some links that might be useful." + articles;

			new CollectPrintDetailsPage("Print Canceled".Localize(),
				printer,
				"Oops, looks like you canceled the print.",
				markdownText,
				printTask,
				false);
		}

		public void CollectInfoPrintFinished()
		{
			// show a long running task asking about print feedback and up-selling more materials
			// Ask about the print, offer help if needed.
			// Let us know how your print came out.
			string markdownText = @"**Find more at MatterHackers**

Supplies and accessories:

[![Filament](https://lh3.googleusercontent.com/2QmeGU_t2KKvAuXTSCYHq1EQTMHRurwreztY52jGdtRQStAEit7Yjsz_hW9l1akGjun7dVcaCGdHEHdGNIGkKykoMg=w100-h100)](https://www.matterhackers.com/store/c/3d-printer-filament) [![Adhesives](https://lh3.googleusercontent.com/LwlavuKk8UXhOuRVB8Q3hqj-AYDHUl8vg_cDanQ8weKM1M7iyMRLjvVD0QWvj6dmCGpSE1t2lKSeMDAmTpJVHLS1bQ=w100-h100)](https://www.matterhackers.com/store/c/3d-printer-adhesive) [![Accessories](https://lh3.googleusercontent.com/pizcbdPu1qn2_QLwyoB2mSr00ckkKkSNkRJ3YmYP-ydkwTpyKy1P_hb6SV2lrH9CbWyy4HViO3VPXV5Q7q-9iGm0wg=w100-h100)](https://www.matterhackers.com/store/c/printer-accessories)

Support and tutorials:" + articles;

			new CollectPrintDetailsPage("Print Complete".Localize(),
				printer,
				"How did this print come out?",
				markdownText,
				printTask,
				true);
		}

		public class CollectPrintDetailsPage : DialogPage
		{
			private readonly ThemedTextEditWidget textEditWidget;

			public override string Text { get => textEditWidget.Text; set => textEditWidget.Text = value; }

			public CollectPrintDetailsPage(string windowTitle,
				PrinterConfig printer,
				string topMarkDown,
				string descriptionMarkdown,
				PrintTask printTask,
				bool collectQuality)
				: base("Close".Localize())
			{
				this.WindowTitle = windowTitle;
				this.HeaderText = printer.PrinterName + ": " + windowTitle;
				this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 440 * GuiWidget.DeviceScale);

				var scrollable = new ScrollableWidget(autoScroll: true)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10),
				};

				scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
				scrollable.ScrollArea.VAnchor = VAnchor.Fit;
				contentRow.AddChild(scrollable);

				var topToBottom = scrollable.AddChild(new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch
				});

				topToBottom.AddChild(new MarkdownWidget(theme, false)
				{
					Markdown = topMarkDown,
				});

				var reasonSection = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					Visible = !collectQuality
				};

				if (collectQuality)
				{
					var qualityInput = GetQualityWidget(theme,
						printTask,
						() =>
						{
							reasonSection.Visible = printTask.PrintQuality == 0;
							this.Descendants<ScrollableWidget>().First().ScrollPositionFromTop = new Vector2(0, 0);
						},
						16);
					qualityInput.Margin = new BorderDouble(5, 0);
					qualityInput.HAnchor = HAnchor.Left;
					topToBottom.AddChild(qualityInput);
				}

				topToBottom.AddChild(reasonSection);

				// Adds text box and check box to the above container
				var emptyText = "What went wrong?".Localize();
				var initialValue = printTask.Note ?? "";
				textEditWidget = new ThemedTextEditWidget(initialValue, theme, pixelWidth: 300, messageWhenEmptyAndNotSelected: emptyText)
				{
					Name = "InputBoxPage TextEditWidget",
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(5),
				};

				textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
				{
					printTask.Note = textEditWidget.Text;
					printTask.CommitAndPushToServer();
				};

				var dropDownList = CreateDefaultOptions(textEditWidget, theme, () =>
				{
					// Delay this so we wait for the text to be updated
					UiThread.RunOnIdle(() =>
					{
						printTask.Note = textEditWidget.Text;
						printTask.CommitAndPushToServer();
					});
				});
				dropDownList.Margin = new BorderDouble(5, 0);
				dropDownList.HAnchor |= HAnchor.Left;
				reasonSection.AddChild(dropDownList);
				reasonSection.AddChild(textEditWidget);

				topToBottom.AddChild(new HorizontalLine(theme.BorderColor40)
				{
					Margin = new BorderDouble(0, 5)
				});

				topToBottom.AddChild(new MarkdownWidget(theme, false)
				{
					Markdown = descriptionMarkdown,
				});

				var collectHistoryHidden = UserSettings.Instance.get(UserSettingsKey.CollectPrintHistoryData) == "false";
				if (!collectHistoryHidden)
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(this, printTask.Id);
						// this will cause a layout that fixes a display issue
						scrollable.ScrollArea.BoundsChanged += (s, e) =>
						{
							scrollable.ScrollPositionFromTop = new Vector2(0, 0);
						};

						scrollable.ScrollPositionFromTop = new Vector2(0, 0);
					});
				}

				if (printer != null)
				{
					var printAgainButton = PrintPopupMenu.CreateStartPrintButton("Print Again", printer, theme, out _);
					printAgainButton.Click += (s, e) => this.DialogWindow?.ClosePage();
					AddPageAction(printAgainButton);
				}
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
}