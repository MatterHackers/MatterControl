﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	using System.Threading;
	using MatterHackers.Agg;
	using MatterHackers.DataConverters3D;
	using MatterHackers.GCodeVisualizer;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PrinterCommunication;
	using MatterHackers.MeshVisualizer;
	using MatterHackers.PolygonMesh;
	using MatterHackers.VectorMath;
	using MatterHackers.MatterControl.PartPreviewWindow;
    using System.Collections.Generic;
    using MatterHackers.MatterControl.PrintLibrary;

    public class BedConfig
	{
		public event EventHandler ActiveLayerChanged;

		public event EventHandler LoadedGCodeChanged;

		public event EventHandler SceneLoaded;

		public View3DConfig RendererOptions { get; } = new View3DConfig();

		public PrinterConfig Printer { get; set; }

		public EditContext EditContext { get; private set; }

		public Mesh PrinterShape { get; private set; }

		public BedConfig(PrinterConfig printer = null)
		{
			this.Printer = printer;
		}

		public async Task LoadContent(EditContext editContext)
		{
			// Load

			if (editContext.SourceItem is ILibraryContentStream contentStream
				&& contentStream.ContentType == "gcode")
			{
				using (var task = await contentStream.GetContentStream(null))
				{
					this.LoadGCode(task.Stream, CancellationToken.None, null);
				}

				this.Scene.Children.Modify(children => children.Clear());
				this.EditableScene = false;
			}
			else
			{
				editContext.Content = await editContext.SourceItem.CreateContent(null);
				this.Scene.Load(editContext.Content);

				if (File.Exists(editContext?.GCodeFilePath))
				{
					this.LoadGCode(editContext.GCodeFilePath, CancellationToken.None, null);
				}

				this.EditableScene = true;
			}

			// Store
			this.EditContext = editContext;

			// Notify
			this.SceneLoaded?.Invoke(this, null);
		}

		internal static ILibraryItem NewPlatingItem()
		{
			string now = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			string mcxPath = Path.Combine(ApplicationDataStorage.Instance.PlatingDirectory, now + ".mcx");

			File.WriteAllText(mcxPath, new Object3D().ToJson());

			return new FileSystemFileItem(mcxPath);
		}

		internal async Task ClearPlate()
		{
			// Clear existing
			this.LoadedGCode = null;
			this.GCodeRenderer = null;

			// Load
			await this.LoadContent(new EditContext()
			{
				ContentStore = ApplicationController.Instance.Library.PlatingHistory,
				SourceItem = BedConfig.NewPlatingItem()
			});
		}

		public InsertionGroup AddToPlate(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			InsertionGroup insertionGroup = null;

			var context = ApplicationController.Instance.DragDropData;
			var scene = context.SceneContext.Scene;
			scene.Children.Modify(list =>
			{
				list.Add(
					insertionGroup = new InsertionGroup(
						selectedLibraryItems,
						context.View3DWidget,
						scene,
						context.SceneContext.BedCenter,
						dragOperationActive: () => false));
			});

			return insertionGroup;
		}

		public async Task StashAndPrint(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			// Clear plate
			await this.ClearPlate();

			// Add content
			var insertionGroup = this.AddToPlate(selectedLibraryItems);
			await insertionGroup.LoadingItemsTask;

			// Persist changes
			this.Save();

			// Slice and print
			var context = this.EditContext;
			await ApplicationController.Instance.PrintPart(
				context.PartFilePath,
				context.GCodeFilePath,
				context.SourceItem.Name,
				this.Printer,
				null);
		}

		internal static ILibraryItem LoadLastPlateOrNew()
		{
			// Find the last used bed plate mcx
			var directoryInfo = new DirectoryInfo(ApplicationDataStorage.Instance.PlatingDirectory);
			var firstFile = directoryInfo.GetFileSystemInfos("*.mcx").OrderByDescending(fl => fl.LastWriteTime).FirstOrDefault();

			// Set as the current item - should be restored as the Active scene in the MeshViewer
			if (firstFile != null)
			{
				return new FileSystemFileItem(firstFile.FullName);
			}

			// Otherwise generate a new plating item
			return NewPlatingItem();
		}

		private GCodeFile loadedGCode;
		public GCodeFile LoadedGCode
		{
			get => loadedGCode;
			set
			{
				if (loadedGCode != value)
				{
					loadedGCode = value;
					LoadedGCodeChanged?.Invoke(null, null);
				}
			}
		}

		public WorldView World { get; } = new WorldView(0, 0);

		public double BuildHeight  { get; internal set; }
		public Vector3 ViewerVolume { get; internal set; }
		public Vector2 BedCenter { get; internal set; }
		public BedShape BedShape { get; internal set; }

		// TODO: Make assignment private, wire up post slicing initialization here
		public GCodeRenderer GCodeRenderer { get; set; }

		public int ActiveLayerIndex
		{
			get
			{
				return activeLayerIndex;
			}

			set
			{
				if (activeLayerIndex != value)
				{
					activeLayerIndex = value;

					// Clamp activeLayerIndex to valid range
					if (this.GCodeRenderer == null || activeLayerIndex < 0)
					{
						activeLayerIndex = 0;
					}
					else if (activeLayerIndex >= this.LoadedGCode.LayerCount)
					{
						activeLayerIndex = this.LoadedGCode.LayerCount - 1;
					}

					// When the active layer changes we update the selected range accordingly - constrain to applicable values
					if (this.RenderInfo != null)
					{
						// TODO: Unexpected that rendering layer 2 requires that we set the range to 0-3. Seems like model should be updated to allow 0-2 to mean render up to layer 2
						this.RenderInfo.EndLayerIndex = Math.Min(this.LoadedGCode == null ? 0 : this.LoadedGCode.LayerCount, Math.Max(activeLayerIndex + 1, 1));
					}

					ActiveLayerChanged?.Invoke(this, null);
				}
			}
		}

		private int activeLayerIndex;

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
					var bedGenerator = new BedMeshGenerator();
					(_bedMesh, _buildVolumeMesh) = bedGenerator.CreatePrintBedAndVolume(Printer);

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
								var mesh = MeshFileIo.Load(stream, extension, CancellationToken.None).Mesh;

								BspNode bspTree = null;

								// if there is a chached bsp tree load it
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

		public Mesh BuildVolumeMesh
		{
			get
			{
				return _buildVolumeMesh;
			}
		}

		public bool EditableScene { get; private set; }

		internal void Render3DLayerFeatures(DrawEventArgs e)
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
			using (var stream = File.OpenRead(filePath))
			{
				this.LoadGCode(stream, cancellationToken, progressReporter);
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
			if (options.RenderSpeeds)
			{
				renderType |= RenderType.SpeedColors;
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
			this.LoadedGCode = GCodeMemoryFile.Load(stream, cancellationToken, progressReporter);
			this.GCodeRenderer = new GCodeRenderer(loadedGCode);
			this.RenderInfo = new GCodeRenderInfo(
					0,
					1,
					Agg.Transform.Affine.NewIdentity(),
					1,
					0,
					1,
					new Vector2[]
					{
						this.Printer.Settings.Helpers.ExtruderOffset(0),
						this.Printer.Settings.Helpers.ExtruderOffset(1)
					},
					this.GetRenderType,
					MeshViewerWidget.GetExtruderColor);

			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				GCodeRenderer.ExtruderWidth = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.nozzle_diameter);
			}
			else
			{
				GCodeRenderer.ExtruderWidth = .4;
			}

			try
			{
				// TODO: After loading we reprocess the entire document just to compute filament used. If it's a feature we need, seems like it should just be normal step during load and result stored in a property
				GCodeRenderer.GCodeFileToDraw?.GetFilamentUsedMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter));
			}
			catch (Exception ex)
			{
				Debug.Print(ex.Message);
			}
		}

		public void InvalidateBedMesh()
		{
			// Invalidate bed mesh cache
			_bedMesh = null;
		}

		internal void Save()
		{
			if (this.Scene.Persistable)
			{
				this.Scene.PersistAssets(ApplicationDataStorage.Instance.ApplicationLibraryDataPath);
				this.EditContext.Save();
			}
		}
	}

	public class EditContext
	{
		private ILibraryItem _sourceItem;

		public IContentStore ContentStore { get; set; }

		public ILibraryItem SourceItem
		{
			get => _sourceItem;
			set
			{
				if (_sourceItem != value)
				{
					_sourceItem = value;

					if (_sourceItem is FileSystemFileItem fileItem)
					{
						printItem = new PrintItemWrapper(new PrintItem(fileItem.FileName, fileItem.Path));
					}
				}
			}
		}

		public IObject3D Content { get; set; }

		public string GCodeFilePath => printItem?.GetGCodePathAndFileName();

		public string PartFilePath => printItem?.FileLocation;

		/// <summary>
		/// Short term stop gap that should only be used until GCode path helpers, hash code and print recovery components can be extracted
		/// </summary>
		[Obsolete]
		internal PrintItemWrapper printItem { get; set; }

		internal void Save()
		{
			if (this.ContentStore != null)
			{
				var thumbnailPath = ApplicationController.Instance.ThumbnailCachePath(this.SourceItem);
				if (File.Exists(thumbnailPath))
				{
					File.Delete(thumbnailPath);
				}

				// Call save on the provider
				this.ContentStore.Save(this.SourceItem, this.Content);
			}
		}
	}

	public class PrinterViewState
	{
		public event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

		public bool SliceSettingsTabPinned
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsTabPinned) == "true";
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsTabPinned, value ? "true" : "false");
			}
		}

		public int SliceSettingsTabIndex
		{
			get
			{
				int.TryParse(UserSettings.Instance.get(UserSettingsKey.SliceSettingsTabIndex), out int tabIndex);
				return tabIndex;
			}
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsTabIndex, value.ToString());
			}
		}

		public bool DockWindowFloating { get; internal set; }

		public double SliceSettingsWidth
		{
			get
			{
				double.TryParse(UserSettings.Instance.get(UserSettingsKey.SliceSettingsWidth), out double controlWidth);
				return controlWidth;
			}
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsWidth, value.ToString());
			}
		}

		private PartViewMode viewMode = PartViewMode.Model;
		public PartViewMode ViewMode
		{
			get => viewMode;
			set
			{
				if (viewMode != value)
				{
					viewMode = value;

					ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs()
					{
						ViewMode = this.ViewMode
					});
				}
			}
		}
	}

	public class PrinterConfig
	{
		public BedConfig Bed { get; }
		public PrinterViewState ViewState { get; } = new PrinterViewState();

		private PrinterSettings _settings;
		public PrinterSettings Settings
		{
			get => _settings;
			private set
			{
				if (_settings != value)
				{
					_settings = value;
					this.ReloadSettings();
					this.Bed.InvalidateBedMesh();
				}
			}
		}

		public PrinterConnection Connection { get; private set; }

		private EventHandler unregisterEvents;

		public PrinterConfig(EditContext editContext, PrinterSettings settings)
		{
			this.Bed = new BedConfig(this);

			if (editContext != null)
			{
				this.Bed.LoadContent(editContext).ConfigureAwait(false);
			}

			this.Connection = new PrinterConnection(printer: this);

			this.Settings = settings;
			this.Settings.printer = this;

			ActiveSliceSettings.SettingChanged.RegisterEvent(Printer_SettingChanged, ref unregisterEvents);
		}

		internal void SwapToSettings(PrinterSettings printerSettings)
		{
			_settings = printerSettings;
			ApplicationController.Instance.ReloadAll();
		}

		private void ReloadSettings()
		{
			this.Bed.BuildHeight = this.Settings.GetValue<double>(SettingsKey.build_height);
			this.Bed.ViewerVolume = new Vector3(this.Settings.GetValue<Vector2>(SettingsKey.bed_size), this.Bed.BuildHeight);
			this.Bed.BedCenter = this.Settings.GetValue<Vector2>(SettingsKey.print_center);
			this.Bed.BedShape = this.Settings.GetValue<BedShape>(SettingsKey.bed_shape);
		}

		private void Printer_SettingChanged(object sender, EventArgs e)
		{
			if (e is StringEventArgs stringEvent)
			{
				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					this.ReloadSettings();
					this.Bed.InvalidateBedMesh();
				}
			}
		}
	}

	public class View3DConfig
	{
		public bool IsDirty { get; internal set; }

		public bool RenderBed
		{
			get
			{
				string value = UserSettings.Instance.get("GcodeViewerRenderGrid");
				if (value == null)
				{
					RenderBed = true;
					return true;
				}
				return (value == "True");
			}
			set
			{
				UserSettings.Instance.set("GcodeViewerRenderGrid", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool RenderMoves
		{
			get { return (UserSettings.Instance.get("GcodeViewerRenderMoves") == "True"); }
			set
			{
				UserSettings.Instance.set("GcodeViewerRenderMoves", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool RenderRetractions
		{
			get { return (UserSettings.Instance.get("GcodeViewerRenderRetractions") == "True"); }
			set
			{
				UserSettings.Instance.set("GcodeViewerRenderRetractions", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool RenderSpeeds
		{
			get { return (UserSettings.Instance.get("GcodeViewerRenderSpeeds") == "True"); }
			set
			{
				UserSettings.Instance.set("GcodeViewerRenderSpeeds", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool SimulateExtrusion
		{
			get { return (UserSettings.Instance.get("GcodeViewerSimulateExtrusion") == "True"); }
			set
			{
				UserSettings.Instance.set("GcodeViewerSimulateExtrusion", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool TransparentExtrusion
		{
			get { return (UserSettings.Instance.get("GcodeViewerTransparentExtrusion") == "True"); }
			set
			{
				UserSettings.Instance.set("GcodeViewerTransparentExtrusion", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool HideExtruderOffsets
		{
			get
			{
				string value = UserSettings.Instance.get("GcodeViewerHideExtruderOffsets");
				if (value == null)
				{
					return true;
				}
				return (value == "True");
			}
			set
			{
				UserSettings.Instance.set("GcodeViewerHideExtruderOffsets", value.ToString());
				this.IsDirty = true;
			}
		}

		public bool SyncToPrint
		{
			get => UserSettings.Instance.get("LayerViewSyncToPrint") == "True";
			set
			{
				UserSettings.Instance.set("LayerViewSyncToPrint", value.ToString());
				this.IsDirty = true;
			}
		}
	}
}