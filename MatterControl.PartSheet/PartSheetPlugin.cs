/*
Copyright (c) 2019, Kevin Pope, John Lewin
All rights reserved.
*/

using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.Plugins
{
	public class PartSheetPlugin : IApplicationPlugin
	{
		public void Initialize()
		{
			// PDF export is limited to Windows
			if (AggContext.OperatingSystem != OSType.Windows)
			{
				return;
			}

			ApplicationController.Instance.Library.MenuExtensions.Add(
				new LibraryAction(ActionScope.ListItem)
				{
					Title = "Create Part Sheet".Localize(),
					Action = (selectedLibraryItems, listView) =>
					{
						UiThread.RunOnIdle(() =>
						{
							var printItems = selectedLibraryItems.OfType<ILibraryAssetStream>();
							if (printItems.Any())
							{
								AggContext.FileDialogs.SaveFileDialog(
									new SaveFileDialogParams("Save Parts Sheet|*.pdf")
									{
										ActionButtonLabel = "Save Parts Sheet".Localize(),
										Title = ApplicationController.Instance.ProductName + " - " + "Save".Localize()
									},
									(saveParams) =>
									{
										if (!string.IsNullOrEmpty(saveParams.FileName))
										{
											var currentPartsInQueue = new PartsSheet(printItems, saveParams.FileName);
											currentPartsInQueue.SaveSheets().ConfigureAwait(false);
										}
									});
							}
						});
					},
					IsEnabled = (selectedListItems, listView) =>
					{
						// Multiselect - disallow containers
						return listView.SelectedItems.Any()
							&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainerLink));
					}
				});
		}

		public PluginInfo MetaData { get; } = new PluginInfo()
		{
			Name = "Part Sheets",
			UUID = "580D8EF3-885C-4DD3-903A-4DB136AFD84B",
			About = "A part sheet plugin",
			Developer = "MatterHackers, Inc.",
			Url = "https://www.matterhackers.com"
		};
	}
}
