/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class BedConfig : ISceneContext
	{
		public event EventHandler ActiveLayerChanged;

		public event EventHandler LoadedGCodeChanged;

		public event EventHandler SceneLoaded;

		public View3DConfig RendererOptions { get; } = new View3DConfig();

		[JsonIgnore]
		public PrinterConfig Printer { get; set; }

		public EditContext EditContext { get; set; }

		[JsonIgnore]
		public Mesh PrinterShape { get; private set; }

		public SceneContextViewState ViewState { get; }

		private readonly HistoryContainerBase historyContainer;

		public BedConfig(HistoryContainerBase historyContainer, PrinterConfig printer = null)
		{
			this.historyContainer = historyContainer;
			this.Printer = printer;
			this.ViewState = new SceneContextViewState(this);
		}

		public void LoadEmptyContent(EditContext editContext)
		{
			// Make sure we don't have a selection
			this.Scene.SelectedItem = null;

			this.EditContext = editContext;
			this.ContentType = "mcx";

			this.Scene.Children.Modify(children => children.Clear());

			this.Scene.Load(new Object3D());

			// Notify
			this.SceneLoaded?.Invoke(this, null);
		}

		public Task LoadLibraryContent(ILibraryItem libraryItem, Action<double, string> progressReporter)
		{
			return this.LoadContent(
				new EditContext()
				{
					ContentStore = ApplicationController.Instance.Library.PlatingHistory,
					SourceItem = libraryItem
				},
				progressReporter);
		}

		public async Task LoadContent(EditContext editContext, Action<double, string> progressReporter)
		{
			// Make sure we don't have a selection
			this.Scene.SelectedItem = null;

			// Store
			this.EditContext = editContext;

			if (editContext.SourceItem is ILibraryAsset contentInfo)
			{
				this.ContentType = contentInfo.ContentType;
			}

			await this.LoadIntoCurrent(editContext, progressReporter);
		}

		/// <summary>
		/// Load content from the given EditContext into the current one
		/// </summary>
		/// <param name="editContext">The context to load into.</param>
		/// <returns></returns>
		public async Task LoadIntoCurrent(EditContext editContext, Action<double, string> progressReporter)
		{
			// Load
			// Load last item or fall back to empty if unsuccessful
			var content = await editContext.SourceItem.CreateContent(progressReporter) ?? new Object3D();

			this.Scene.Load(content);

			// Notify
			this.SceneLoaded?.Invoke(this, null);
		}

		public void ClearPlate()
		{
			// Clear existing
			// Switch back to Model view on ClearPlate
			if (this.Printer != null)
			{
				this.Printer.ViewState.ViewMode = PartViewMode.Model;

				this.LoadEmptyContent(
					new EditContext()
					{
						ContentStore = historyContainer,
						SourceItem = historyContainer.NewBedPlate(this)
					});
			}
			else
			{
				this.LoadEmptyContent(new EditContext());
			}
		}

		public InsertionGroupObject3D AddToPlate(IEnumerable<ILibraryItem> itemsToAdd, bool addUndoCheckPoint = true)
		{
			return this.AddToPlate(itemsToAdd, (this.Printer != null) ? this.Printer.Bed.BedCenter : Vector2.Zero, true, addUndoCheckPoint);
		}

		public InsertionGroupObject3D AddToPlate(IEnumerable<ILibraryItem> itemsToAdd, Vector2 initialPosition, bool moveToOpenPosition, bool addUndoCheckPoint = true)
		{
			if (this.Printer != null
				&& this.Printer.ViewState.ViewMode != PartViewMode.Model)
			{
				this.Printer.ViewState.ViewMode = PartViewMode.Model;
			}

			InsertionGroupObject3D insertionGroup = null;

			var context = ApplicationController.Instance.DragDropData;
			var scene = context.SceneContext.Scene;
			scene.Children.Modify(list =>
			{
				list.Add(
					insertionGroup = new InsertionGroupObject3D(
						itemsToAdd,
						context.View3DWidget,
						scene,
						initialPosition,
						(item, itemsToAvoid) =>
						{
							if (moveToOpenPosition)
							{
								if (itemsToAvoid.Count() == 0)
								{
									PlatingHelper.PlaceOnBed(item);
								}
								else
								{
									PlatingHelper.MoveToOpenPositionRelativeGroup(item, itemsToAvoid);
								}
							}
						},
						addUndoCheckPoint: addUndoCheckPoint));
			});

			return insertionGroup;
		}

		public async void AddToPlate(string[] filesToLoadIncludingZips, bool addUndoCheckPoint = true)
		{
			if (filesToLoadIncludingZips?.Any() == true)
			{
				var scene = this.Scene;

				// When a single GCode file is selected, swap the plate to the new GCode content
				if (filesToLoadIncludingZips.Count() == 1
					&& filesToLoadIncludingZips.FirstOrDefault() is string firstFilePath
					&& Path.GetExtension(firstFilePath).ToUpper() == ".GCODE")
				{
					// Special case for GCode which changes loaded scene to special mode for GCode
					await this.LoadContent(
						new EditContext()
						{
							SourceItem = new FileSystemFileItem(firstFilePath),
							ContentStore = null // No content store for GCode
						},
						null);

					return;
				}

				var filePaths = await Task.Run(() =>
				{
					var filesToLoad = new List<string>();
					foreach (string loadedFileName in filesToLoadIncludingZips)
					{
						string extension = Path.GetExtension(loadedFileName).ToUpper();
						if (extension != ""
							&& extension != ".ZIP"
							&& extension != ".GCODE"
							&& ApplicationController.Instance.Library.IsContentFileType(loadedFileName))
						{
							filesToLoad.Add(loadedFileName);
						}
						else if (extension == ".ZIP")
						{
							List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(loadedFileName);
							if (partFiles != null)
							{
								foreach (PrintItem part in partFiles)
								{
									string itemExtension = Path.GetExtension(part.FileLocation).ToUpper();
									if (itemExtension != ".GCODE")
									{
										filesToLoad.Add(part.FileLocation);
									}
								}
							}
						}
					}

					return filesToLoad;
				});

				var itemCache = new Dictionary<string, IObject3D>();
				this.AddToPlate(filePaths.Select(f => new FileSystemFileItem(f)), addUndoCheckPoint);
			}
		}

		public WorldView World { get; } = new WorldView(0, 0);

		public double BuildHeight { get; internal set; }

		public Vector3 ViewerVolume { get; internal set; }

		public Vector2 BedCenter { get; internal set; } = Vector2.Zero;

		public BedShape BedShape { get; internal set; }

		[JsonIgnore]
		public InteractiveScene Scene { get; } = new InteractiveScene();

		private Mesh _bedMesh;

		[JsonIgnore]
		public Mesh Mesh
		{
			get
			{
				if (_bedMesh == null)
				{
					Task.Run(() =>
					{
						try
						{
							string url = Printer.Settings.GetValue("PrinterShapeUrl");
							string extension = Printer.Settings.GetValue("PrinterShapeExtension");

							if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(extension))
							{
								return;
							}

							using (var stream = ApplicationController.Instance.LoadHttpAsset(url))
							{
								var mesh = Object3D.Load(stream, extension, CancellationToken.None).Mesh;

								BspNode bspTree = null;

								// if there is a cached bsp tree load it
								var meshHashCode = mesh.GetLongHashCode();
								string cachePath = ApplicationController.CacheablePath("MeshBspData", $"{meshHashCode}.bsp");
								if (File.Exists(cachePath))
								{
									JsonConvert.DeserializeObject<BspNode>(File.ReadAllText(cachePath));
								}
								else
								{
									// else calculate it
									bspTree = FaceBspTree.Create(mesh, 20, true);
									// and save it
									File.WriteAllText(cachePath, JsonConvert.SerializeObject(bspTree));
								}

								// set the mesh to use the new tree
								UiThread.RunOnIdle(() =>
								{
									mesh.FaceBspTree = bspTree;
									this.PrinterShape = mesh;

									// TODO: Need to send a notification that the mesh changed so the UI can pickup and render
								});
							}
						}
						catch { }
					});
				}

				return _bedMesh;
			}
		}

		private Mesh _buildVolumeMesh;

		[JsonIgnore]
		public Mesh BuildVolumeMesh => _buildVolumeMesh;

		public bool EditableScene => true;

		public string ContentType { get; private set; }

		/// <summary>
		/// Gets the axis aligned bounding box of the bed
		/// </summary>
		public AxisAlignedBoundingBox Aabb
		{
			get
			{
				var bedSize = Printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
				var printCenter = Printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
				var buildHeight = Printer.Settings.GetValue<double>(SettingsKey.build_height);
				if (buildHeight == 0)
				{
					buildHeight = double.PositiveInfinity;
				}

				return new AxisAlignedBoundingBox(
					printCenter.X - bedSize.X / 2, // min x
					printCenter.Y - bedSize.Y / 2, // min y
					0, // min z
					printCenter.X + bedSize.X / 2, // max x
					printCenter.Y + bedSize.Y / 2, // max y
					buildHeight); // max z
			}
		}

		public void InvalidateBedMesh()
		{
			// Invalidate bed mesh cache
			_bedMesh = null;
		}

		/// <summary>
		/// Persists modified meshes to assets and saves pending changes back to the EditContext
		/// </summary>
		/// <param name="progress">Allows for progress reporting</param>
		/// <param name="cancellationTokenSource">Allows for cancellation during processing</param>
		/// <returns>A task representing success</returns>
		public async Task SaveChanges(Action<double, string> progress, CancellationTokenSource cancellationTokenSource)
		{
			if (this.EditContext.ContentStore == null)
			{
				UiThread.RunOnIdle(() =>
				{
					// we need to ask for a destination
					DialogWindow.Show(
						new SaveAsPage(
							(container, newName) =>
							{
								this.SaveAs(container, newName);
							}));
				});

				return;
			}
			
			var status = "Saving Changes".Localize();

			progress?.Invoke(0, status);

			if (this.Scene.Persistable)
			{
				var startingMs = UiThread.CurrentTimerMs;

				// wait up to 1 second for the scene to have content
				while (!Scene.Children.Any()
					&& UiThread.CurrentTimerMs < startingMs + 1000)
				{
					Thread.Sleep(10);
				}

				// wait up to 5 seconds to finish loading before the save
				while (Scene.Children.Where(c => c is InsertionGroupObject3D).Any()
					&& UiThread.CurrentTimerMs < startingMs + 5000)
				{
					Thread.Sleep(10);
				}

				await this.Scene.PersistAssets((progress0to1, status) =>
				{
					progress?.Invoke(progress0to1, status);
				});

				await this.EditContext?.Save(this.Scene);
			}
		}

		public bool HadSaveError
        {
			get
            {
				return false;
            }
        }

		public List<BoolOption> GetBaseViewOptions()
		{
			return new List<BoolOption>();
		}
	}
}