using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ExportPrintItemPage : WizardPage
	{
		private CheckBox showInFolderAfterSave;
		private string gcodePathAndFilenameToSave;
		private bool partIsGCode = false;
		private string documentsPath;

		private EventHandler unregisterEvents;

		private Dictionary<RadioButton, IExportPlugin> exportPluginButtons;

		private IEnumerable<ILibraryItem> libraryItems;

		public ExportPrintItemPage(IEnumerable<ILibraryItem> libraryItems)
			: base(unlocalizedTextForTitle: "File export options:")
		{
			this.libraryItems = libraryItems;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Name = "Export Item Window";

			CreateWindowContent();

			// TODO: Why? ***************************************************************************************************
			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((s, e) => CreateWindowContent(), ref unregisterEvents);
		}

		public void CreateWindowContent()
		{
			var commonMargin = new BorderDouble(4, 2);

			// GCode export
			bool showExportGCodeButton = ActiveSliceSettings.Instance.PrinterSelected || partIsGCode;
			if (showExportGCodeButton)
			{
				exportPluginButtons = new Dictionary<RadioButton, IExportPlugin>();

				foreach (IExportPlugin plugin in PluginFinder.CreateInstancesOf<IExportPlugin>())
				{
					// Create export button for each plugin
					var pluginButton = new RadioButton(plugin.ButtonText.Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor)
					{
						HAnchor = HAnchor.Left,
						Margin = commonMargin,
						Cursor = Cursors.Hand
					};
					contentRow.AddChild(pluginButton);

					var optionPanel = plugin.GetOptionsPanel();
					if (optionPanel != null)
					{
						optionPanel.HAnchor = HAnchor.Stretch;
						optionPanel.VAnchor = VAnchor.Fit;
						contentRow.AddChild(optionPanel);
					}

					exportPluginButtons.Add(pluginButton, plugin);
				}
			}

			//if (plugin.EnabledForCurrentPart(libraryContent))
			

			contentRow.AddChild(new VerticalSpacer());

			// TODO: make this work on the mac and then delete this if
			if (OsInformation.OperatingSystem == OSType.Windows
				|| OsInformation.OperatingSystem == OSType.X11)
			{
				showInFolderAfterSave = new CheckBox("Show file in folder after save".Localize(), ActiveTheme.Instance.PrimaryTextColor, 10)
				{
					HAnchor = HAnchor.Left,
					Cursor = Cursors.Hand
				};
				contentRow.AddChild(showInFolderAfterSave);
			}

			if (!showExportGCodeButton)
			{
				var noGCodeMessage = new TextWidget(
					"Note".Localize() + ": " + "To enable GCode export, select a printer profile.".Localize(), 
					textColor: ActiveTheme.Instance.PrimaryTextColor, 
					pointSize: 10);
				noGCodeMessage.HAnchor = HAnchor.Left;
				contentRow.AddChild(noGCodeMessage);
			}

			var exportButton = textImageButtonFactory.Generate("Export".Localize());
			exportButton.Click += (s, e) =>
			{
				string fileTypeFilter = "";
				string targetExtension = "";

				IExportPlugin activePlugin = null;

				// Loop over all plugin buttons, break on the first checked item found
				foreach(var button in this.exportPluginButtons.Keys)
				{
					if (button.Checked)
					{
						activePlugin = exportPluginButtons[button];
						break;
					}
				}

				// Early exit if no plugin radio button is selected
				if (activePlugin == null)
				{
					return;
				}

				fileTypeFilter = activePlugin.ExtensionFilter;
				targetExtension = activePlugin.FileExtension;

				this.Parent.CloseOnIdle();
				UiThread.RunOnIdle(() =>
				{
					string title = "MatterControl: " + "Export File".Localize();
					FileDialog.SaveFileDialog(
						new SaveFileDialogParams(fileTypeFilter)
						{
							Title = title,
							ActionButtonLabel = "Export".Localize(),
							FileName = Path.GetFileNameWithoutExtension(libraryItems.FirstOrDefault()?.Name ?? DateTime.Now.ToString("yyyyMMdd-HHmmss"))
						},
						(saveParams) =>
						{
							string savePath = saveParams.FileName;

							if (!string.IsNullOrEmpty(savePath))
							{
								Task.Run(async () =>
								{
									string extension = Path.GetExtension(savePath);
									if (extension != targetExtension)
									{
										savePath += targetExtension;
									}

									bool succeeded = false;

									if (activePlugin != null)
									{
										succeeded = await activePlugin.Generate(libraryItems, savePath);
									}

									if (succeeded)
									{
										ShowFileIfRequested(savePath);
									}
									else
									{
										UiThread.RunOnIdle(() =>
										{
											StyledMessageBox.ShowMessageBox(null, "Export failed".Localize(), title);
										});
									}
								});
							}
						});
				});
			};

			footerRow.AddChild(exportButton);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private void ShowFileIfRequested(string filename)
		{
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				if (showInFolderAfterSave.Checked)
				{
#if IS_WINDOWS_FORMS
					WindowsFormsAbstract.ShowFileInFolder(filename);
#endif
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}