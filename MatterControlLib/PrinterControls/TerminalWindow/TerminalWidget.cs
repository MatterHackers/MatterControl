/*
Copyright (c) 2014, Lars Brubaker
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
using System.Diagnostics;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl
{
	public class TerminalWidget : FlowLayoutWidget, ICloseableTab
	{
		private CheckBox autoUppercase;
		private MHTextEditWidget manualCommandTextEdit;
		private TextScrollWidget textScrollWidget;
		private PrinterConfig printer;

		private List<string> commandHistory = new List<string>();
		private int commandHistoryIndex = 0;

		public TerminalWidget(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.Name = "TerminalWidget";
			this.Padding = new BorderDouble(5, 0);

			// Header
			var headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Left | HAnchor.Stretch,
				Padding = new BorderDouble(0, 8)
			};
			this.AddChild(headerRow);

			headerRow.AddChild(CreateVisibilityOptions(theme));

			autoUppercase = new CheckBox("Auto Uppercase".Localize(), textSize: theme.DefaultFontSize)
			{
				Margin = new BorderDouble(left: 25),
				Checked = UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalAutoUppercase, true),
				TextColor = theme.TextColor,
				VAnchor = VAnchor.Center
			};
			autoUppercase.CheckedStateChanged += (s, e) =>
			{
				UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalAutoUppercase, autoUppercase.Checked);
			};
			headerRow.AddChild(autoUppercase);

			// Body
			var bodyRow = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(bottom: 4),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			this.AddChild(bodyRow);

			textScrollWidget = new TextScrollWidget(printer, printer.TerminalLog.PrinterLines)
			{
				BackgroundColor = theme.MinimalShade,
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = 0,
				Padding = new BorderDouble(3, 0)
			};
			bodyRow.AddChild(textScrollWidget);
			bodyRow.AddChild(new TextScrollBar(textScrollWidget, 15)
			{
				ThumbColor = theme.AccentMimimalOverlay,
				BackgroundColor = theme.SlightShade,
				Margin = 0
			});

			textScrollWidget.LineFilterFunction = lineData =>
			{
				var line = lineData.Line;
				var output = lineData.Direction == TerminalLine.MessageDirection.ToPrinter;
				var outputLine = line;

				var lineWithoutChecksum = GCodeFile.GetLineWithoutChecksum(line);

				// and set this as the output if desired
				if (output
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowChecksum, true))
				{
					outputLine = lineWithoutChecksum;
				}

				if (!output
					&& lineWithoutChecksum == "ok"
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowOks, true))
				{
					return null;
				}
				else if (output
					&& lineWithoutChecksum.StartsWith("M105")
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowTempRequests, true))
				{
					return null;
				}
				else if (output
					&& (lineWithoutChecksum.StartsWith("G0 ") || lineWithoutChecksum.StartsWith("G1 "))
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowMovementRequests, true))
				{
					return null;
				}
				else if (!output
					&& (lineWithoutChecksum.StartsWith("T") || lineWithoutChecksum.StartsWith("ok T"))
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowTempResponse, true))
				{
					return null;
				}
				else if (!output
					&& lineWithoutChecksum == "wait"
					&& !UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowWaitResponse, false))
				{
					return null;
				}

				if (UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowInputOutputMarks, true))
				{
					switch (lineData.Direction)
					{
						case TerminalLine.MessageDirection.FromPrinter:
							outputLine = "→ " + outputLine;
							break;
						case TerminalLine.MessageDirection.ToPrinter:
							outputLine = "← " + outputLine;
							break;
						case TerminalLine.MessageDirection.ToTerminal:
							outputLine = "* " + outputLine;
							break;
					}
				}

				return outputLine;
			};

			// Input Row
			var inputRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				BackgroundColor = this.BackgroundColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: 2)
			};
			this.AddChild(inputRow);

			manualCommandTextEdit = new MHTextEditWidget("", theme, typeFace: ApplicationController.GetTypeFace(NamedTypeFace.Liberation_Mono))
			{
				Margin = new BorderDouble(right: 3),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom
			};
			manualCommandTextEdit.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				SendManualCommand();
			};
			manualCommandTextEdit.ActualTextEditWidget.KeyDown += (s, keyEvent) =>
			{
				bool changeToHistory = false;
				if (keyEvent.KeyCode == Keys.Up)
				{
					commandHistoryIndex--;
					if (commandHistoryIndex < 0)
					{
						commandHistoryIndex = 0;
					}
					changeToHistory = true;
				}
				else if (keyEvent.KeyCode == Keys.Down)
				{
					commandHistoryIndex++;
					if (commandHistoryIndex > commandHistory.Count - 1)
					{
						commandHistoryIndex = commandHistory.Count - 1;
					}
					else
					{
						changeToHistory = true;
					}
				}
				else if (keyEvent.KeyCode == Keys.Escape)
				{
					manualCommandTextEdit.Text = "";
				}

				if (changeToHistory && commandHistory.Count > 0)
				{
					manualCommandTextEdit.Text = commandHistory[commandHistoryIndex];
				}
			};
			inputRow.AddChild(manualCommandTextEdit);

			// Footer
			var toolbarPadding = theme.ToolbarPadding;
			var footerRow = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(0, toolbarPadding.Bottom, toolbarPadding.Right, toolbarPadding.Top)
			};
			this.AddChild(footerRow);

			var sendButton = theme.CreateDialogButton("Send".Localize());
			sendButton.Margin = theme.ButtonSpacing;
			sendButton.Click += (s, e) =>
			{
				SendManualCommand();
			};
			footerRow.AddChild(sendButton);

			var clearButton = theme.CreateDialogButton("Clear".Localize());
			clearButton.Margin = theme.ButtonSpacing;
			clearButton.Click += (s, e) =>
			{
				printer.TerminalLog.Clear();
			};
			footerRow.AddChild(clearButton);

			var exportButton = theme.CreateDialogButton("Export".Localize());
			exportButton.Margin = theme.ButtonSpacing;
			exportButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					AggContext.FileDialogs.SaveFileDialog(
						new SaveFileDialogParams("Save as Text|*.txt")
						{
							Title = "MatterControl: Terminal Log",
							ActionButtonLabel = "Export",
							FileName = "print_log.txt"
						},
						(saveParams) =>
						{
							if (!string.IsNullOrEmpty(saveParams.FileName))
							{
								string filePathToSave = saveParams.FileName;

								if (filePathToSave != null && filePathToSave != "")
								{
									try
									{
										textScrollWidget.WriteToFile(filePathToSave);
									}
									catch (UnauthorizedAccessException ex)
									{
										Debug.Print(ex.Message);

										ApplicationController.Instance.LogError("");
										ApplicationController.Instance.LogError("WARNING: Write Failed!".Localize());
										ApplicationController.Instance.LogError("Can't access".Localize() + " " + filePathToSave);
										ApplicationController.Instance.LogError("");

										UiThread.RunOnIdle(() =>
										{
											StyledMessageBox.ShowMessageBox(ex.Message, "Couldn't save file".Localize());
										});
									}
								}
							}
						});
				});
			};
			footerRow.AddChild(exportButton);

			footerRow.AddChild(new HorizontalSpacer());

			this.AnchorAll();
		}

		private GuiWidget CreateVisibilityOptions(ThemeConfig theme)
		{
			var visibilityOptionsButton = new PopupMenuButton("Visibility Options", theme)
			{
				VAnchor = VAnchor.Center
			};

			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			visibilityOptionsButton.PopupContent = popupMenu;

			// put in options for filtering various output
			popupMenu.CreateBoolMenuItem(
				"Line Checksums".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowChecksum, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowChecksum, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			popupMenu.CreateBoolMenuItem(
				"In / Out Indicators".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowInputOutputMarks, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowInputOutputMarks, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			// request section
			popupMenu.CreateSeparator();

			popupMenu.CreateBoolMenuItem(
				"Temperature Requests".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowTempRequests, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowTempRequests, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			popupMenu.CreateBoolMenuItem(
				"Movement Requests".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowMovementRequests, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowMovementRequests, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			// response section
			popupMenu.CreateSeparator();

			popupMenu.CreateBoolMenuItem(
				"Temperature Responses".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowTempResponse, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowTempResponse, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			popupMenu.CreateBoolMenuItem(
				"'Ok' Responses".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowOks, true),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowOks, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			popupMenu.CreateBoolMenuItem(
				"'Wait' Responses".Localize(),
				() => UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowWaitResponse, false),
				(isChecked) =>
				{
					UserSettings.Instance.Fields.SetBool(UserSettingsKey.TerminalShowWaitResponse, isChecked);
					textScrollWidget.RebuildFilteredList();
				});

			return visibilityOptionsButton;
		}

		private void SendManualCommand()
		{
			string textToSend = manualCommandTextEdit.Text.Trim();
			if (autoUppercase.Checked)
			{
				textToSend = textToSend.ToUpper();
			}
			commandHistory.Add(textToSend);
			commandHistoryIndex = commandHistory.Count;
			printer.Connection.QueueLine(textToSend);
			manualCommandTextEdit.Text = "";
		}
	}
}