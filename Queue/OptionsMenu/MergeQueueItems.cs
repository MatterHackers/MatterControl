using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Queue.OptionsMenu
{
	public class MyMenuExtension : PrintItemMenuExtension
	{
		public override IEnumerable<PrintItemAction> GetMenuItems()
		{
			return new List<PrintItemAction>()
			{
				new PrintItemAction()
				{
					SingleItemOnly = false,
					Title = "Merge".Localize() + "...",
					Action = (items, queueDataWidget) =>
					{
						List<QueueRowItem> allRowItems = new List<QueueRowItem>(items);
						if (allRowItems.Count > 1)
						{
							RenameItemWindow renameItemWindow = new RenameItemWindow(allRowItems[0].PrintItemWrapper.Name, (returnInfo) =>
							{
								Task.Run(() =>
								{
									List<MeshGroup> combinedMeshes = new List<MeshGroup>();

									// Load up all the parts and merge them together
									foreach(QueueRowItem item in allRowItems)
									{
										List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(item.PrintItemWrapper.FileLocation);
										combinedMeshes.AddRange(loadedMeshGroups);
									}

									// save them out
									string[] metaData = { "Created By", "MatterControl", "BedPosition", "Absolute" };
									MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);
									string libraryDataPath = ApplicationDataStorage.Instance.ApplicationLibraryDataPath;
									if (!Directory.Exists(libraryDataPath))
									{
										Directory.CreateDirectory(libraryDataPath);
									}

									string tempFileNameToSaveTo = Path.Combine(libraryDataPath, Path.ChangeExtension(Path.GetRandomFileName(), "amf"));
									bool savedSuccessfully = MeshFileIo.Save(combinedMeshes, tempFileNameToSaveTo, outputInfo);

									// Swap out the files if the save operation completed successfully
									if (savedSuccessfully && File.Exists(tempFileNameToSaveTo))
									{
										// create a new print item
										// add it to the queue
										PrintItemWrapper newPrintItem = new PrintItemWrapper(new PrintItem(returnInfo.newName, tempFileNameToSaveTo));
										QueueData.Instance.AddItem(newPrintItem, 0);

										// select the part we added, if possible
										QueueData.Instance.SelectedIndex = 0;

										queueDataWidget.LeaveEditMode();
									}
								});
							}, "Set Name".Localize())
							{
								Title = "MatterHackers - Set Name".Localize(),
								ElementHeader = "Set Name".Localize(),
							};
						}
					}
				}
			};
		}
	}
}