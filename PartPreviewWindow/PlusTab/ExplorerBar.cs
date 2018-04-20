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
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorerBar : FlowLayoutWidget
	{
		protected Toolbar toolbar;
		protected FlowLayoutWidget headingBar;

		public ExplorerBar(string text, ThemeConfig theme, GuiWidget rightAnchorItem = null)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Margin = new BorderDouble(30, 0, 30, 15);

			headingBar = new FlowLayoutWidget()
			{

			};
			this.AddChild(headingBar);

			headingBar.AddChild(theme.CreateHeading(text));

			toolbar = new Toolbar(rightAnchorItem)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = 8,
				BackgroundColor = theme.MinimalShade
			};
			this.AddChild(toolbar);
		}
	}

	public class PrinterBar : ExplorerBar
	{
		private PrinterInfo printerInfo;

		public PrinterBar(PartPreviewContent partPreviewContent, PrinterInfo printerInfo, ThemeConfig theme)
			: base(printerInfo?.Name ?? "", theme)
		{
			headingBar.CloseAllChildren();
			headingBar.AddChild(new PrinterSelector(theme)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Absolute,
				Border = 0,
				MinimumSize = Vector2.Zero,
				Width = 200,
				BackgroundColor = theme.MinimalShade
			});

			// add in the create printer button
			var createPrinter = new IconButton(AggContext.StaticData.LoadIcon("icon_circle_plus.png", 16, 16, theme.InvertIcons), theme)
			{
				Name = "Create Printer",
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing,
				ToolTipText = "Create Printer".Localize()
			};

			createPrinter.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					//simpleTabs.RemoveTab(simpleTabs.ActiveTab);

					if (ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting
					|| ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPaused)
					{
						StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize());
					}
					else
					{
						DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowMakeModel));
					}
				});
			};
			headingBar.AddChild(createPrinter);

			// add in the import printer button
			var importPrinter = new IconButton(AggContext.StaticData.LoadIcon("icon_import_white_full.png", 16, 16, theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing,
				ToolTipText = "Import Printer".Localize()
			};
			importPrinter.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
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
											new ImportSucceeded(importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(result.FileName))));
									}
									else
									{
										StyledMessageBox.ShowMessageBox("Oops! Settings file '{0}' did not contain any settings we could import.".Localize().FormatWith(Path.GetFileName(result.FileName)), "Unable to Import".Localize());
									}
								}
							});
				});
			};
			headingBar.AddChild(importPrinter);

			this.printerInfo = printerInfo;

			// Select the 25 most recent files and project onto FileSystemItems
			var recentFiles = new DirectoryInfo(ApplicationDataStorage.Instance.PlatingDirectory).GetFiles("*.mcx").OrderByDescending(f => f.LastWriteTime);

			var listView = new ListView(ApplicationController.Instance.Library, theme);

			var emptyPlateButton = new ImageWidget(AggContext.StaticData.LoadIcon("empty-workspace.png", 70, 70))
			{
				Margin = new BorderDouble(right: 5),
				Selectable = true,
				BackgroundColor = theme.MinimalShade,
				Name = "Open Empty Plate Button"
			};
			emptyPlateButton.Click += async (s, e) =>
			{
				var printer = await ProfileManager.Instance.LoadPrinter();

				await printer.Bed.ClearPlate();

				string printerName = printer.Settings.GetValue(SettingsKey.printer_name);

				partPreviewContent.CreatePrinterTab(printer, theme, printerName);
			};
			toolbar.AddChild(emptyPlateButton);

			foreach (var item in recentFiles.Take(10).Select(f => new SceneReplacementFileItem(f.FullName)).ToList<ILibraryItem>())
			{
				var iconButton = new IconViewItem(new ListViewItem(item, listView), 70, 70, theme)
				{
					Margin = new BorderDouble(right: 5),
					Selectable = true
				};

				iconButton.Children.First().Click += (s, e) =>
				{
					if (this.PositionWithinLocalBounds(e.X, e.Y))
					{
						UiThread.RunOnIdle(async () =>
						{
							await ProfileManager.Instance.LoadPrinterOpenItem(item);

							string tabTitle = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);

							var printer = ApplicationController.Instance.ActivePrinter;

							partPreviewContent.CreatePrinterTab(printer, theme, tabTitle);
						});
					}
				};

				toolbar.AddChild(iconButton);
			}
		}

		public bool Equals(PrinterBar other)
		{
			throw new NotImplementedException();
		}
	}

	public class PartsBar : ExplorerBar
	{
		public PartsBar(PartPreviewContent partPreviewContent, ThemeConfig theme)
			: base("Parts".Localize(), theme)
		{
			// ** Mock recent parts **
			var recentParts = new DirectoryInfo(ApplicationDataStorage.Instance.PlatingDirectory).GetFiles("*.mcx").OrderBy(f => f.LastWriteTime);

			var listView = new ListView(ApplicationController.Instance.Library, theme);

			var emptyPlateButton = new ImageWidget(AggContext.StaticData.LoadIcon("empty-workspace.png", 70, 70))
			{
				Margin = new BorderDouble(right: 5),
				Selectable = true,
				BackgroundColor = theme.MinimalShade,
				Name = "Create Part Button"
			};
			emptyPlateButton.Click += async (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					BedConfig bed;
					//simpleTabs.RemoveTab(simpleTabs.ActiveTab);
					partPreviewContent.CreatePartTab(
						"New Part",
						bed = new BedConfig(),
						theme);

					bed.LoadContent(
						new EditContext()
						{
							ContentStore = ApplicationController.Instance.Library.PlatingHistory,
							SourceItem = BedConfig.NewPlatingItem()
						}).ConfigureAwait(false);
				});
			};
			toolbar.AddChild(emptyPlateButton);


			foreach (var item in recentParts.Take(10).Select(f => new SceneReplacementFileItem(f.FullName)).ToList<ILibraryItem>())
			{
				toolbar.AddChild(new IconViewItem(new ListViewItem(item, listView), 70, 70, theme)
				{
					Margin = new BorderDouble(right: 5)
				});
			}

		}
	}
}