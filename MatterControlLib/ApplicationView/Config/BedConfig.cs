﻿/*
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
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
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
			if (editContext.SourceItem is ILibraryAssetStream contentStream
				&& contentStream.ContentType == "gcode")
			{
				using (var task = await contentStream.GetStream(null))
				{
					await LoadGCodeContent(task.Stream);
				}

				// No content store for GCode
				editContext.ContentStore = null;
			}
			else
			{
				// Load last item or fall back to empty if unsuccessful
				var content = await editContext.SourceItem.CreateContent(progressReporter) ?? new Object3D();

				loadedGCode = null;
				this.GCodeRenderer = null;

				this.Scene.Load(content);
			}

			// Notify
			this.SceneLoaded?.Invoke(this, null);
		}

		public async Task LoadGCodeContent(Stream stream)
		{
			await ApplicationController.Instance.Tasks.Execute("Loading G-Code".Localize(), Printer, (reporter, cancellationTokenSource) =>
			{
				var progressStatus = new ProgressStatus();
				reporter.Report(progressStatus);

				this.LoadGCode(stream, cancellationTokenSource.Token, (progress0To1, status) =>
				{
					progressStatus.Status = status;
					progressStatus.Progress0To1 = progress0To1;
					reporter.Report(progressStatus);
				});

				this.Scene.Children.Modify(children => children.Clear());

				this.EditContext.FreezeGCode = true;

				return Task.CompletedTask;
			});
		}

		public void ClearPlate()
		{
			// Clear existing
			this.LoadedGCode = null;
			this.GCodeRenderer = null;

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
								PlatingHelper.MoveToOpenPositionRelativeGroup(item, itemsToAvoid);
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
				}).ConfigureAwait(false);

				var itemCache = new Dictionary<string, IObject3D>();
				this.AddToPlate(filePaths.Select(f => new FileSystemFileItem(f)), addUndoCheckPoint);
			}
		}

		public async Task StashAndPrintGCode(ILibraryItem libraryItem)
		{
			// Clear plate
			this.ClearPlate();

			// Add content
			await this.LoadContent(
				new EditContext()
				{
					SourceItem = libraryItem,
					// No content store for GCode
					ContentStore = null
				},
				null);

			// Slice and print
			await ApplicationController.Instance.PrintPart(
				this.EditContext,
				this.Printer,
				null,
				CancellationToken.None,
				PrinterConnection.PrintingModes.Normal);
		}

		public async Task StashAndPrint(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			// Clear plate
			this.ClearPlate();

			// Add content
			var insertionGroup = this.AddToPlate(selectedLibraryItems);
			await insertionGroup.LoadingItemsTask;

			// Persist changes
			await this.SaveChanges(null, null);

			// Slice and print
			await ApplicationController.Instance.PrintPart(
				this.EditContext,
				this.Printer,
				null,
				CancellationToken.None,
				PrinterConnection.PrintingModes.Normal);
		}

		private GCodeFile loadedGCode;

		public GCodeFile LoadedGCode
		{
			get => loadedGCode;
			private set
			{
				if (loadedGCode != value)
				{
					loadedGCode = value;
					LoadedGCodeChanged?.Invoke(null, null);
				}
			}
		}

		internal async void EnsureGCodeLoaded()
		{
			if (this.LoadedGCode == null
				&& !this.Printer.ViewState.SlicingItem
				&& File.Exists(await this.EditContext?.GCodeFilePath(this.Printer)))
			{
				UiThread.RunOnIdle(async () =>
				{
					using (var stream = File.OpenRead(await this.EditContext.GCodeFilePath(this.Printer)))
					{
						await LoadGCodeContent(stream);
					}
				});
			}
		}

		public WorldView World { get; } = new WorldView(0, 0);

		public double BuildHeight { get; internal set; }

		public Vector3 ViewerVolume { get; internal set; }

		public Vector2 BedCenter { get; internal set; } = Vector2.Zero;

		public BedShape BedShape { get; internal set; }

		// TODO: Make assignment private, wire up post slicing initialization here
		public GCodeRenderer GCodeRenderer { get; set; }

		private int _activeLayerIndex;

		public int ActiveLayerIndex
		{
			get => _activeLayerIndex;
			set
			{
				if (_activeLayerIndex != value)
				{
					_activeLayerIndex = value;

					// Clamp activeLayerIndex to valid range
					if (this.GCodeRenderer == null || _activeLayerIndex < 0)
					{
						_activeLayerIndex = 0;
					}
					else if (_activeLayerIndex >= this.LoadedGCode.LayerCount)
					{
						_activeLayerIndex = this.LoadedGCode.LayerCount - 1;
					}

					// When the active layer changes we update the selected range accordingly - constrain to applicable values
					if (this.RenderInfo != null)
					{
						// TODO: Unexpected that rendering layer 2 requires that we set the range to 0-3. Seems like model should be updated to allow 0-2 to mean render up to layer 2
						this.RenderInfo.EndLayerIndex = Math.Min(this.LoadedGCode == null ? 0 : this.LoadedGCode.LayerCount, Math.Max(_activeLayerIndex + 1, 1));
					}

					ActiveLayerChanged?.Invoke(this, null);
				}
			}
		}

		[JsonIgnore]
		public InteractiveScene Scene { get; } = new InteractiveScene();

		public GCodeRenderInfo RenderInfo { get; set; }

		private Mesh _bedMesh;

		[JsonIgnore]
		public Mesh Mesh
		{
			get
			{
				if (_bedMesh == null)
				{
					// Load bed and build volume meshes
					(_bedMesh, _buildVolumeMesh) = BedMeshGenerator.CreatePrintBedAndVolume(Printer);

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

		public bool EditableScene => this.EditContext?.FreezeGCode != true;

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

		internal void RenderGCode3D(DrawEventArgs e)
		{
			if (this.RenderInfo != null)
			{
				// If needed, update the RenderType flags to match to current user selection
				if (RendererOptions.IsDirty)
				{
					this.RenderInfo.RefreshRenderType();
					RendererOptions.IsDirty = false;
				}

				this.GCodeRenderer.Render3D(this.RenderInfo, e);
			}
		}

		internal AxisAlignedBoundingBox GetAabbOfRenderGCode3D()
		{
			if (this.RenderInfo != null)
			{
				// If needed, update the RenderType flags to match to current user selection
				if (RendererOptions.IsDirty)
				{
					this.RenderInfo.RefreshRenderType();
					RendererOptions.IsDirty = false;
				}

				return this.GCodeRenderer.GetAabbOfRender3D(this.RenderInfo);
			}

			return AxisAlignedBoundingBox.Empty();
		}

		public void LoadActiveSceneGCode(string filePath, CancellationToken cancellationToken, Action<double, string> progressReporter)
		{
			if (File.Exists(filePath))
			{
				using (var stream = File.OpenRead(filePath))
				{
					this.LoadGCode(stream, cancellationToken, progressReporter);
				}
			}
		}

		private RenderType GetRenderType()
		{
			var options = this.RendererOptions;

			RenderType renderType = RenderType.Extrusions;

			if (options.RenderMoves)
			{
				renderType |= RenderType.Moves;
			}

			if (options.RenderRetractions)
			{
				renderType |= RenderType.Retractions;
			}

			if (options.GCodeLineColorStyle == "Speeds")
			{
				renderType |= RenderType.SpeedColors;
			}
			else if (options.GCodeLineColorStyle != "Materials")
			{
				renderType |= RenderType.GrayColors;
			}

			if (options.SimulateExtrusion)
			{
				renderType |= RenderType.SimulateExtrusion;
			}

			if (options.TransparentExtrusion)
			{
				renderType |= RenderType.TransparentExtrusion;
			}

			return renderType;
		}

		public void LoadGCode(Stream stream, CancellationToken cancellationToken, Action<double, string> progressReporter)
		{
			var settings = this.Printer.Settings;
			var maxAcceleration = settings.GetValue<double>(SettingsKey.max_acceleration);
			var maxVelocity = settings.GetValue<double>(SettingsKey.max_velocity);
			var jerkVelocity = settings.GetValue<double>(SettingsKey.jerk_velocity);
			var multiplier = settings.GetValue<double>(SettingsKey.print_time_estimate_multiplier) / 100.0;

			var loadedGCode = GCodeMemoryFile.Load(stream,
				new Vector4(maxAcceleration, maxAcceleration, maxAcceleration, maxAcceleration),
				new Vector4(maxVelocity, maxVelocity, maxVelocity, maxVelocity),
				new Vector4(jerkVelocity, jerkVelocity, jerkVelocity, jerkVelocity),
				new Vector4(multiplier, multiplier, multiplier, multiplier),
				cancellationToken,
				progressReporter);

			this.GCodeRenderer = new GCodeRenderer(loadedGCode)
			{
				Gray = AppContext.Theme.IsDarkTheme ? Color.DarkGray : Color.Gray
			};

			this.RenderInfo = new GCodeRenderInfo(
					0,
					// Renderer requires endLayerIndex to be desiredLayer+1: to render layer zero we set endLayerIndex to 1
					Math.Max(1, this.ActiveLayerIndex + 1),
					Agg.Transform.Affine.NewIdentity(),
					1,
					0,
					1,
					this.GetRenderType,
					(index) => MaterialRendering.Color(this.Printer, index));

			GCodeRenderer.ExtruderWidth = this.Printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);

			try
			{
				// TODO: After loading we reprocess the entire document just to compute filament used. If it's a feature we need, seems like it should just be normal step during load and result stored in a property
				GCodeRenderer.GCodeFileToDraw?.GetFilamentUsedMm(this.Printer.Settings.GetValue<double>(SettingsKey.filament_diameter));
			}
			catch (Exception ex)
			{
				Debug.Print(ex.Message);
			}

			// Assign property causing event and UI load
			this.LoadedGCode = loadedGCode;

			// Constrain to max layers
			if (this.ActiveLayerIndex > loadedGCode?.LayerCount)
			{
				this.ActiveLayerIndex = loadedGCode.LayerCount;
			}

			ActiveLayerChanged?.Invoke(this, null);
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
		public async Task SaveChanges(IProgress<ProgressStatus> progress, CancellationTokenSource cancellationTokenSource)
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

			var progressStatus = new ProgressStatus()
			{
				Status = "Saving Changes"
			};

			progress?.Report(progressStatus);

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
					if (progress != null)
					{
						progressStatus.Status = status;
						progressStatus.Progress0To1 = progress0to1;
						progress.Report(progressStatus);
					}
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