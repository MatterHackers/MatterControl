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
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class HardwareTreeView : TreeView
	{
		private TreeNode printersNode;
		private FlowLayoutWidget rootColumn;
		private EventHandler unregisterEvents;

		public HardwareTreeView(ThemeConfig theme)
			: base (theme)
		{
			rootColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			this.AddChild(rootColumn);

			// Printers
			printersNode = new TreeNode(theme)
			{
				Text = "Printers".Localize(),
				HAnchor = HAnchor.Stretch,
				AlwaysExpandable = true,
				Image = AggContext.StaticData.LoadIcon("printer.png", 16, 16, theme.InvertIcons)
			};
			printersNode.TreeView = this;

			var forcedHeight = 20;
			var mainRow = printersNode.Children.FirstOrDefault();
			mainRow.HAnchor = HAnchor.Stretch;
			mainRow.AddChild(new HorizontalSpacer());

			// add in the create printer button
			var createPrinter = new IconButton(AggContext.StaticData.LoadIcon("md-add-circle_18.png", 18, 18, theme.InvertIcons), theme)
			{
				Name = "Create Printer",
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing.Clone(left: theme.ButtonSpacing.Right),
				ToolTipText = "Create Printer".Localize(),
				Height = forcedHeight,
				Width = forcedHeight
			};

			createPrinter.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				if (ApplicationController.Instance.AnyPrintTaskRunning)
				{
					StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize());
				}
				else
				{
					DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowMakeModel));
				}
			});
			mainRow.AddChild(createPrinter);

			// add in the import printer button
			var importPrinter = new IconButton(AggContext.StaticData.LoadIcon("md-import_18.png", 18, 18, theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing,
				ToolTipText = "Import Printer".Localize(),
				Height = forcedHeight,
				Width = forcedHeight
			};
			importPrinter.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				AggContext.FileDialogs.OpenFileDialog(
					new OpenFileDialogParams(
						"settings files|*.ini;*.printer;*.slice"),
						(result) =>
						{
							if (!string.IsNullOrEmpty(result.FileName)
								&& File.Exists(result.FileName))
							{
								//simpleTabs.RemoveTab(simpleTabs.ActiveTab);
								if (ProfileManager.ImportFromExisting(result.FileName))
								{
									string importPrinterSuccessMessage = "You have successfully imported a new printer profile. You can find '{0}' in your list of available printers.".Localize();
									DialogWindow.Show(
										new ImportSucceeded(
											importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(result.FileName))));
								}
								else
								{
									StyledMessageBox.ShowMessageBox("Oops! Settings file '{0}' did not contain any settings we could import.".Localize().FormatWith(Path.GetFileName(result.FileName)), "Unable to Import".Localize());
								}
							}
						});
			});
			mainRow.AddChild(importPrinter);

			rootColumn.AddChild(printersNode);

			HardwareTreeView.CreatePrinterProfilesTree(printersNode, theme);
			this.Invalidate();

			// Filament
			var materialsNode = new TreeNode(theme)
			{
				Text = "Materials".Localize(),
				AlwaysExpandable = true,
				Image = AggContext.StaticData.LoadIcon("filament.png", 16, 16, theme.InvertIcons)
			};
			materialsNode.TreeView = this;

			rootColumn.AddChild(materialsNode);

			// Register listeners
			PrinterSettings.AnyPrinterSettingChanged += Printer_SettingChanged;

			// Rebuild the treeview anytime the Profiles list changes
			ProfileManager.ProfilesListChanged.RegisterEvent((s, e) =>
			{
				HardwareTreeView.CreatePrinterProfilesTree(printersNode, theme);
				this.Invalidate();
			}, ref unregisterEvents);
		}

		public static void CreatePrinterProfilesTree(TreeNode printersNode, ThemeConfig theme)
		{
			if (printersNode == null)
			{
				return;
			}

			printersNode.Nodes.Clear();

			//Add the menu items to the menu itself
			foreach (var printer in ProfileManager.Instance.ActiveProfiles.OrderBy(p => p.Name))
			{
				var printerNode = new TreeNode(theme)
				{
					Text = printer.Name,
					Name = $"{printer.Name} Node",
					Tag = printer
				};

				printerNode.Load += (s, e) =>
				{
					printerNode.Image = OemSettings.Instance.GetIcon(printer.Make);
				};

				printersNode.Nodes.Add(printerNode);
			}

			printersNode.Expanded = true;
		}

		public static void CreateOpenPrintersTree(TreeNode printersNode, ThemeConfig theme)
		{
			if (printersNode == null)
			{
				return;
			}

			printersNode.Nodes.Clear();

			//Add the menu items to the menu itself
			foreach (var printer in ApplicationController.Instance.ActivePrinters)
			{
				string printerName = printer.Settings.GetValue(SettingsKey.printer_name);

				var printerNode = new TreeNode(theme)
				{
					Text = printerName,
					Name = $"{printerName} Node",
					Tag = printer
				};

				printerNode.Load += (s, e) =>
				{
					printerNode.Image = OemSettings.Instance.GetIcon(printer.Settings.GetValue(SettingsKey.make));
				};

				printersNode.Nodes.Add(printerNode);
			}

			printersNode.Expanded = true;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			PrinterSettings.AnyPrinterSettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		private void Printer_SettingChanged(object s, EventArgs e)
		{
			string settingsName = (e as StringEventArgs)?.Data;
			if (settingsName != null && settingsName == SettingsKey.printer_name)
			{
				// Allow enough time for ProfileManager to respond and refresh its data
				UiThread.RunOnIdle(() =>
				{
					HardwareTreeView.CreatePrinterProfilesTree(printersNode, theme);
				}, .2);

				this.Invalidate();
			}
		}
	}
}
