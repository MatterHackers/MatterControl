/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	using System.Collections.Generic;
	using System.Threading;
	using MatterHackers.Agg;
	using MatterHackers.DataConverters3D;
	using MatterHackers.GCodeVisualizer;
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.MeshVisualizer;
	using MatterHackers.PolygonMesh;
	using MatterHackers.VectorMath;

	public class BedConfig
	{
		public event EventHandler ActiveLayerChanged;

		public event EventHandler LoadedGCodeChanged;

		public event EventHandler SceneLoaded;

		public View3DConfig RendererOptions { get; } = new View3DConfig();

		public PrinterConfig Printer { get; set; }

		public EditContext EditContext { get; private set; }

		public Mesh PrinterShape { get; private set; }

		public SceneContextViewState ViewState { get; }

		private HistoryContainerBase historyContainer;

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

		public Task LoadLibraryContent(ILibraryItem libraryItem)
		{
			return this.LoadContent(
				new EditContext()
				{
					ContentStore = ApplicationController.Instance.Library.PlatingHistory,
					SourceItem = libraryItem
				});
		}

		public async Task LoadContent(EditContext editContext)
		{
			// Make sure we don't have a selection
			this.Scene.SelectedItem = null;

			// Store
			this.EditContext = editContext;

			var contentInfo = editContext.SourceItem as ILibraryAsset;
			if (contentInfo != null)
			{
				this.ContentType = contentInfo.ContentType;
			}

			await this.LoadIntoCurrent(editContext);
		}

		/// <summary>
		/// Load content from the given EditContext into the current one
		/// </summary>
		/// <param name="editContext"></param>
		/// <returns></returns>
		public async Task LoadIntoCurrent(EditContext editContext)
		{
			// Load
			if (editContext.SourceItem is ILibraryAssetStream contentStream
				&& contentStream.ContentType == "gcode")
			{
				using (var task = await contentStream.GetStream(null))
				{
					await LoadGCodeContent(task.Stream);
				}

				this.Scene.Children.Modify(children => children.Clear());

				editContext.FreezeGCode = true;
			}
			else
			{
				// Load last item or fall back to empty if unsuccessful
				var content = await editContext.SourceItem.CreateContent(null) ?? new Object3D();
				this.Scene.Load(content);
			}

			// Notify
			this.SceneLoaded?.Invoke(this, null);
		}

		public async Task LoadGCodeContent(Stream stream)
		{
			await ApplicationController.Instance.Tasks.Execute("Loading G-Code".Localize(), (reporter, cancellationToken) =>
			{
				var progressStatus = new ProgressStatus();
				reporter.Report(progressStatus);

				this.LoadGCode(stream, cancellationToken, (progress0To1, status) =>
				{
					progressStatus.Status = status;
					progressStatus.Progress0To1 = progress0To1;
					reporter.Report(progressStatus);
				});

				return Task.CompletedTask;
			});
		}

		internal void ClearPlate()
		{
			// Clear existing
			this.LoadedGCode = null;
			this.GCodeRenderer = null;

			// Switch back to Model view on ClearPlate
			if (this.Printer != null)
			{
				this.Printer.ViewState.ViewMode = PartViewMode.Model;
			}

			// Load
			this.LoadEmptyContent(
				new EditContext()
				{
					ContentStore = historyContainer,
					SourceItem = historyContainer.NewPlatingItem()
				});
		}

		public InsertionGroupObject3D AddToPlate(IEnumerable<ILibraryItem> itemsToAdd)
		{
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
						(Printer != null) ? Printer.Bed.BedCenter : Vector2.Zero,
						(item, itemsToAvoid) =>
						{
							PlatingHelper.MoveToOpenPositionRelativeGroup(item, itemsToAvoid);
						}));
			});

			return insertionGroup;
		}

		/// <summary>
		/// Loads content to the bed and prepares edit/persistence context for use
		/// </summary>
		/// <param name="editContext"></param>
		/// <returns></returns>
		public async Task LoadPlateFromHistory()
		{
			await this.LoadContent(new EditContext()
			{
				ContentStore = historyContainer,
				SourceItem = historyContainer.GetLastPlateOrNew()
			});
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
				});

			// Slice and print
			await ApplicationController.Instance.PrintPart(
				this.EditContext,
				this.Printer,
				null,
				CancellationToken.None);
		}

		public async Task StashAndPrint(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			// Clear plate
			this.ClearPlate();

			// Add content
			var insertionGroup = this.AddToPlate(selectedLibraryItems);
			await insertionGroup.LoadingItemsTask;

			// Persist changes
			await this.SaveChanges(null, CancellationToken.None);

			// Slice and print
			await ApplicationController.Instance.PrintPart(
				this.EditContext,
				this.Printer,
				null,
				CancellationToken.None);
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

		internal void EnsureGCodeLoaded()
		{
			if (this.LoadedGCode == null
				&& File.Exists(this.EditContext?.GCodeFilePath(this.Printer)))
			{
				UiThread.RunOnIdle(async () =>
				{
					using (var stream = File.OpenRead(this.EditContext.GCodeFilePath(this.Printer)))
					{
						await LoadGCodeContent(stream);
					}
				});
			}
		}

		public WorldView World { get; } = new WorldView(0, 0);

		public double BuildHeight  { get; internal set; }
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

		public InteractiveScene Scene { get; } = new InteractiveScene();

		public GCodeRenderInfo RenderInfo { get; set; }

		private Mesh _bedMesh;
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

		public Mesh BuildVolumeMesh => _buildVolumeMesh;

		public bool EditableScene
		{
			get => this.EditContext?.FreezeGCode != true;
		}

		public string ContentType { get; private set; }

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

		public void LoadGCode(string filePath, CancellationToken cancellationToken, Action<double, string> progressReporter)
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
			if (options.HideExtruderOffsets)
			{
				renderType |= RenderType.HideExtruderOffsets;
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
				cancellationToken, progressReporter);

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
					new Vector2[]
					{
						settings.Helpers.ExtruderOffset(0),
						settings.Helpers.ExtruderOffset(1)
					},
					this.GetRenderType,
					MeshViewerWidget.GetExtruderColor);

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
			if (this.ActiveLayerIndex > loadedGCode.LayerCount)
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
		/// <param name="progress"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task SaveChanges(IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus()
			{
				Status = "Saving Changes"
			};

			progress?.Report(progressStatus);

			if (this.Scene.Persistable)
			{
				await this.Scene.PersistAssets((progress0to1, status) =>
				{
					if (progress != null)
					{
						progressStatus.Status = status;
						progressStatus.Progress0To1 = progress0to1;
						progress.Report(progressStatus);
					}
				});

				this.EditContext?.Save(this.Scene);
			}
		}

		public List<BoolOption> GetBaseViewOptions()
		{
			return new List<BoolOption>();
		}
	}
}