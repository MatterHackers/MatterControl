/*
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
	using MatterHackers.MatterControl.PrinterCommunication;
	using MatterHackers.MeshVisualizer;
	using MatterHackers.PolygonMesh;
	using MatterHackers.VectorMath;

	public class BedConfig
	{
		public event EventHandler ActiveLayerChanged;

		public event EventHandler LoadedGCodeChanged;

		public View3DConfig RendererOptions { get; } = new View3DConfig();

		public PrintItemWrapper printItem = null;

		public PrinterConfig Printer { get; set; }

		public Mesh PrinterShape { get; private set; }

		public BedConfig(PrinterConfig printer = null, bool loadLastBedplate = false)
		{
			this.Printer = printer;

			if (loadLastBedplate)
			{
				// Find the last used bed plate mcx
				var directoryInfo = new DirectoryInfo(ApplicationDataStorage.Instance.PlatingDirectory);
				var firstFile = directoryInfo.GetFileSystemInfos("*.mcx").OrderByDescending(fl => fl.CreationTime).FirstOrDefault();

				// Set as the current item - should be restored as the Active scene in the MeshViewer
				if (firstFile != null)
				{
					try
					{
						var loadedItem = new PrintItemWrapper(new PrintItem(firstFile.Name, firstFile.FullName));
						if (loadedItem != null)
						{
							this.printItem = loadedItem;
						}

						this.Scene.Load(firstFile.FullName);
					}
					catch { }
				}
			}

			// Clear if not assigned above
			if (this.printItem == null)
			{
				this.ClearPlate();
			}
		}

		internal void ClearPlate()
		{
			string now = DateTime.Now.ToString("yyyyMMdd-HHmmss");

			string mcxPath = Path.Combine(ApplicationDataStorage.Instance.PlatingDirectory, now + ".mcx");

			this.printItem = new PrintItemWrapper(new PrintItem(now, mcxPath));

			File.WriteAllText(mcxPath, new Object3D().ToJson());

			this.Scene.Load(mcxPath);

			// TODO: Define and fire event and eliminate ActiveView3DWidget - model objects need to be dependency free. For the time being prevent application spin up in ClearPlate due to the call below - if MC isn't loaded, don't notify
			if (!MatterControlApplication.IsLoading)
			{
				ApplicationController.Instance.ActiveView3DWidget?.PartHasBeenChanged();
			}
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
						this.RenderInfo.EndLayerIndex = Math.Min(this.LoadedGCode == null ? 0 : this.LoadedGCode.LayerCount - 1, Math.Max(activeLayerIndex, 1));
					}

					ActiveLayerChanged?.Invoke(this, null);
				}
			}
		}

		private int activeLayerIndex;

		public InteractiveScene Scene { get; } = new InteractiveScene();

		public GCodeRenderInfo RenderInfo { get; set; }

		public string GCodePath
		{
			get
			{
				bool isGCode = Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE";
				return isGCode ? printItem.FileLocation : printItem.GetGCodePathAndFileName();
			}
		}

		BedMeshGenerator bedGenerator;

		private Mesh _bedMesh;
		public Mesh Mesh
		{
			get
			{
				if (_bedMesh == null)
				{
					bedGenerator = new BedMeshGenerator();

					//Construct the thing
					_bedMesh = bedGenerator.CreatePrintBed(Printer);

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
				if (_buildVolumeMesh == null)
				{
					//Construct the thing
					//_buildVolumeMesh = CreatePrintBed(printer);
				}

				return _buildVolumeMesh;
			}
		}

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
			this.LoadedGCode = GCodeMemoryFile.Load(filePath, cancellationToken, progressReporter);
			this.GCodeRenderer = new GCodeRenderer(loadedGCode);

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
	}

	public class PrinterViewState
	{
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

		public PrinterConfig(bool loadLastBedplate, PrinterSettings settings)
		{
			this.Bed = new BedConfig(this, loadLastBedplate);

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

		public bool RenderGrid
		{
			get
			{
				string value = UserSettings.Instance.get("GcodeViewerRenderGrid");
				if (value == null)
				{
					RenderGrid = true;
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