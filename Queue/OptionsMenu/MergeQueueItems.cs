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
					Title = "Merge...",
					Action = (items, queueDataWidget) =>
					{
						List<QueueRowItem> allRowItems = new List<QueueRowItem>(items);
						if (allRowItems.Count > 1)
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
								PrintItemWrapper newPrintItem = new PrintItemWrapper(new PrintItem(allRowItems[0].PrintItemWrapper.Name, tempFileNameToSaveTo));
								QueueData.Instance.AddItem(newPrintItem, QueueData.Instance.GetIndex(allRowItems[0].PrintItemWrapper));

								// remove the parts that we merged
								Task.Run(() =>
								{
									for (int i=allRowItems.Count-1; i>=0; i--)
									{
										QueueRowItem rowItem = allRowItems[i];
										// remove all the items that we just merged
										QueueData.Instance.RemoveAt(QueueData.Instance.GetIndex(rowItem.PrintItemWrapper));
									}

									// select the part we added, if possible
									QueueData.Instance.SelectedIndex = QueueData.Instance.GetIndex(newPrintItem);

									queueDataWidget.LeaveEditMode();
								});
							}
						}
					}
				}
			};
		}
	}
}