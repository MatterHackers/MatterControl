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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.DesignTools.Operations;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace MatterHackers.MatterControl
{
	using System.ComponentModel;
	using System.IO.Compression;
	using System.Net;
	using System.Reflection;
	using System.Text;
	using System.Threading;
	using Agg.Font;
	using Agg.Image;
	using CustomWidgets;
	using global::MatterControl.Printing;
	using MatterHackers.Agg.Platform;
	using MatterHackers.Agg.VertexSource;
	using MatterHackers.DataConverters3D;
	using MatterHackers.DataConverters3D.UndoCommands;
	using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
	using MatterHackers.MatterControl.DesignTools;
	using MatterHackers.MatterControl.DesignTools.Operations;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.MatterControl.PartPreviewWindow.View3D;
	using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
	using MatterHackers.MatterControl.SetupWizard;
	using MatterHackers.PolygonMesh;
	using MatterHackers.RenderOpenGl;
	using MatterHackers.SerialPortCommunication;
	using MatterHackers.VectorMath;
	using MatterHackers.VectorMath.TrackBall;
	using Newtonsoft.Json.Converters;
	using SettingsManagement;

	[JsonConverter(typeof(StringEnumConverter))]
	public enum NamedTypeFace
	{
		Alfa_Slab,
		Audiowide,
		Bangers,
		Courgette,
		Damion,
		Fredoka,
		Great_Vibes,
		Liberation_Mono,
		Liberation_Sans,
		Liberation_Sans_Bold,
		Lobster,
		Pacifico,
		Poppins,
		Questrial,
		Righteous,
		Russo,
		Titan,
		Titillium,
	};

	public class AppContext
	{
		/// <summary>
		/// Native platform features
		/// </summary>
		public static INativePlatformFeatures Platform { get; set; }

		public static bool IsLoading { get; internal set; } = true;

		/// <summary>
		/// The root SystemWindow
		/// </summary>
		public static SystemWindow RootSystemWindow { get; internal set; }
	}

	public class ApplicationController
	{
		public HelpContainer FeatureGuides { get; set; }

		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;

		public ThemeConfig Theme { get; set; }

		public ThemeConfig MenuTheme { get; set; }

		public RunningTasksConfig Tasks { get; set; } = new RunningTasksConfig();

		// A list of printers which are open (i.e. displaying a tab) on this instance of MatterControl
		public IEnumerable<PrinterConfig> ActivePrinters { get; } = new List<PrinterConfig>();

		private static PrinterConfig emptyPrinter = new PrinterConfig(PrinterSettings.Empty);

		private static string cacheDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache");

		// TODO: Any references to this property almost certainly need to be reconsidered. ActiveSliceSettings static references that assume a single printer
		// selection are being redirected here. This allows us to break the dependency to the original statics and consolidates
		// us down to a single point where code is making assumptions about the presence of a printer, printer counts, etc. If we previously checked for
		// PrinterConnection.IsPrinterConnected, that could should be updated to iterate ActiverPrinters, checking each one and acting on each as it would
		// have for the single case
		public PrinterConfig ActivePrinter { get; private set; } = emptyPrinter;

		public Action RedeemDesignCode;
		public Action EnterShareCode;

		private static ApplicationController globalInstance;
		public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();

		public static Action SignInAction;
		public static Action SignOutAction;

		public static Action WebRequestFailed;
		public static Action WebRequestSucceeded;

#if DEBUG
		public const string EnvironmentName = "TestEnv_";
#else
		public const string EnvironmentName = "";
#endif

		public bool ApplicationExiting { get; internal set; } = false;

		public static Func<string, Task<Dictionary<string, string>>> GetProfileHistory;

		public async Task SetActivePrinter(PrinterConfig printer, bool allowChangedEvent = true)
		{
			var initialPrinter = this.ActivePrinter;
			if (initialPrinter?.Settings.ID != printer.Settings.ID)
			{
				// TODO: Consider if autosave is appropriate
				if (initialPrinter != emptyPrinter)
				{
					await initialPrinter.Bed.SaveChanges(null, CancellationToken.None);
				}

				// If we have an active printer, run Disable
				if (initialPrinter.Settings != PrinterSettings.Empty)
				{
					initialPrinter?.Connection?.Disable();
				}

				// ActivePrinters is IEnumerable to force us to use SetActivePrinter until it's ingrained in our pattern
				// Cast to list since it is one and we need to clear and add
				if (this.ActivePrinters is List<PrinterConfig> activePrinterList)
				{
					activePrinterList.Clear();
					activePrinterList.Add(printer);

					this.ActivePrinter = printer;
				}

				// TODO: Decide if non-printer contexts should prompt for a printer, if we should have a default printer, or get "ActiveTab printer" working
				// HACK: short term solution to resolve printer reference for non-printer related contexts
				DragDropData.Printer = printer;

				if (!AppContext.IsLoading)
				{
					// Fire printer changed event
				}

				BedSettings.SetMakeAndModel(
					printer.Settings.GetValue(SettingsKey.make),
					printer.Settings.GetValue(SettingsKey.model));

				if (allowChangedEvent)
				{
					ActiveSliceSettings.OnActivePrinterChanged(null);
				}

				if (!AppContext.IsLoading
					&& printer.Settings.PrinterSelected
					&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
				{
					UiThread.RunOnIdle(() =>
					{
						printer.Settings.printer.Connection.Connect();
					}, 2);
				}

				if (this.Library != null)
				{
					this.Library.NotifyContainerChanged();
				}
			}
		}

		public string GetFavIconUrl(string oemName)
		{
			OemSettings.Instance.OemUrls.TryGetValue(oemName, out string oemUrl);
			return "https://www.google.com/s2/favicons?domain=" + (string.IsNullOrWhiteSpace(oemUrl) ? "www.matterhackers.com" : oemUrl);
		}

		internal async Task ClearActivePrinter()
		{
			await this.SetActivePrinter(emptyPrinter);
		}

		public void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle(() =>
			{
				if (!string.IsNullOrEmpty(OemSettings.Instance.AffiliateCode)
					&& targetUri.Contains("matterhackers.com"))
				{
					if (targetUri.Contains("?"))
					{
						targetUri += $"&aff={OemSettings.Instance.AffiliateCode}";
					}
					else
					{
						targetUri += $"?aff={OemSettings.Instance.AffiliateCode}";
					}

				}
				Process.Start(targetUri);
			});
		}

		public void RefreshActiveInstance(PrinterSettings updatedPrinterSettings)
		{
			ActivePrinter.SwapToSettings(updatedPrinterSettings);

			/*
			// TODO: Should we rebroadcast settings changed events for each settings?
			bool themeChanged = ActivePrinter.Settings.GetValue(SettingsKey.active_theme_name) != updatedProfile.GetValue(SettingsKey.active_theme_name);
			ActiveSliceSettings.SettingChanged.CallEvents(null, new StringEventArgs(SettingsKey.printer_name));

			// TODO: Decide if non-printer contexts should prompt for a printer, if we should have a default printer, or get "ActiveTab printer" working
			// HACK: short term solution to resolve printer reference for non-printer related contexts
			DragDropData.Printer = printer;
			if (themeChanged)
			{
				UiThread.RunOnIdle(ActiveSliceSettings.SwitchToPrinterTheme);
			}
			else
			{
				UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
			}*/
		}

		public static Func<PrinterInfo, string, Task<PrinterSettings>> GetPrinterProfileAsync;
		public static Func<string, IProgress<ProgressStatus>, Task> SyncPrinterProfiles;
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		public SlicePresetsPage EditMaterialPresetsPage { get; set; }

		public SlicePresetsPage EditQualityPresetsWindow { get; set; }

		public GuiWidget MainView;

		private EventHandler unregisterEvents;

		private Dictionary<string, List<PrintItemAction>> registeredLibraryActions = new Dictionary<string, List<PrintItemAction>>();

		private List<SceneSelectionOperation> registeredSceneOperations;

		public ThumbnailsConfig Thumbnails { get; } = new ThumbnailsConfig();

		private void RebuildSceneOperations(ThemeConfig theme)
		{
			registeredSceneOperations = new List<SceneSelectionOperation>()
			{
				new SceneSelectionOperation()
				{
					OperationType = typeof(GroupObject3D),

					TitleResolver = () => "Group".Localize(),
					Action = (scene) =>
					{
						var selectedItem = scene.SelectedItem;
						scene.SelectedItem = null;

						var newGroup = new GroupObject3D();
						// When grouping items, move them to be centered on their bounding box
						newGroup.Children.Modify((gChildren) =>
						{
							selectedItem.Clone().Children.Modify((sChildren) =>
							{
								var center = selectedItem.GetAxisAlignedBoundingBox().Center;

								foreach (var child in sChildren)
								{
									child.Translate(-center.X, -center.Y, 0);
									gChildren.Add(child);
								}

								newGroup.Translate(center.X, center.Y, 0);
							});
						});

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(selectedItem.Children.ToList(), new List<IObject3D> { newGroup }));

						newGroup.MakeNameNonColliding();

						scene.SelectedItem = newGroup;
					},
					IsEnabled = (scene) => scene.HasSelection
						&& scene.SelectedItem is SelectionGroupObject3D
						&& scene.SelectedItem.Children.Count > 1,
					Icon = AggContext.StaticData.LoadIcon("group.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Ungroup".Localize(),
					Action = (scene) => scene.UngroupSelection(),
					IsEnabled = (scene) => scene.HasSelection,
					Icon = AggContext.StaticData.LoadIcon("ungroup.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Duplicate".Localize(),
					Action = (scene) => scene.DuplicateItem(),
					IsEnabled = (scene) => scene.HasSelection,
					Icon = AggContext.StaticData.LoadIcon("duplicate.png").SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Remove".Localize(),
					Action = (scene) => scene.DeleteSelection(),
					IsEnabled = (scene) => scene.HasSelection,
					Icon = AggContext.StaticData.LoadIcon("remove.png").SetPreMultiply(),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(AlignObject3D),
					TitleResolver = () => "Align".Localize(),
					Action = (scene) =>
					{
						var selectedItem = scene.SelectedItem;
						var align = new AlignObject3D();
						align.AddSelectionAsChildren(scene, selectedItem);
						align.Invalidate(new InvalidateArgs(align, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("align_left.png", 16, 16, theme.InvertIcons).SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Lay Flat".Localize(),
					Action = (scene) =>
					{
						if (scene.HasSelection)
						{
							scene.MakeLowestFaceFlat(scene.SelectedItem);
						}
					},
					IsEnabled = (scene) => scene.HasSelection,
					Icon = AggContext.StaticData.LoadIcon("lay_flat.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Make Support".Localize(),
					Action = (scene) =>
					{
						if (scene.SelectedItem != null
							&& !scene.SelectedItem.VisibleMeshes().All(i => i.OutputType == PrintOutputTypes.Support))
						{
							scene.UndoBuffer.AddAndDo(new MakeSupport(scene.SelectedItem));
						}
					},
					Icon = AggContext.StaticData.LoadIcon("support.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.HasSelection,
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(CombineObject3D),
					TitleResolver = () => "Combine".Localize(),
					Action = (scene) => new CombineObject3D().WrapSelectedItemAndSelect(scene),
					Icon = AggContext.StaticData.LoadIcon("combine.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(SubtractObject3D),
					TitleResolver = () => "Subtract".Localize(),
					Action = (scene) => new SubtractObject3D().WrapSelectedItemAndSelect(scene),
					Icon = AggContext.StaticData.LoadIcon("subtract.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(IntersectionObject3D),
					TitleResolver = () => "Intersect".Localize(),
					Action = (scene) => new IntersectionObject3D().WrapSelectedItemAndSelect(scene),
					Icon = AggContext.StaticData.LoadIcon("intersect.png"),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(SubtractAndReplaceObject3D),
					TitleResolver = () => "Subtract & Replace".Localize(),
					Action = (scene) => new SubtractAndReplaceObject3D().WrapSelectedItemAndSelect(scene),
					Icon = AggContext.StaticData.LoadIcon("subtract_and_replace.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayLinearObject3D),
					TitleResolver = () => "Linear Array".Localize(),
					Action = (scene) =>
					{
						var array = new ArrayLinearObject3D();
						array.AddSelectionAsChildren(scene, scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_linear.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.HasSelection && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayRadialObject3D),
					TitleResolver = () => "Radial Array".Localize(),
					Action = (scene) =>
					{
						var array = new ArrayRadialObject3D();
						array.AddSelectionAsChildren(scene, scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_radial.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.HasSelection && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayAdvancedObject3D),
					TitleResolver = () => "Advanced Array".Localize(),
					Action = (scene) =>
					{
						var array = new ArrayAdvancedObject3D();
						array.AddSelectionAsChildren(scene, scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_advanced.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.HasSelection && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(PinchObject3D),
					TitleResolver = () => "Pinch".Localize(),
					Action = (scene) =>
					{
						var pinch = new PinchObject3D();
						pinch.WrapSelectedItemAndSelect(scene);
					},
					Icon = AggContext.StaticData.LoadIcon("pinch.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.HasSelection,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(CurveObject3D),
					TitleResolver = () => "Curve".Localize(),
					Action = (scene) =>
					{
						var curve = new CurveObject3D();
						curve.WrapSelectedItemAndSelect(scene);
					},
					Icon = AggContext.StaticData.LoadIcon("curve.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.HasSelection,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(FitToBoundsObject3D_2),
					TitleResolver = () => "Fit to Bounds".Localize(),
					Action = (scene) =>
					{
						var selectedItem = scene.SelectedItem;
						scene.SelectedItem = null;
						var fit = FitToBoundsObject3D_2.Create(selectedItem.Clone());
						fit.MakeNameNonColliding();

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { selectedItem }, new List<IObject3D> { fit }));
						scene.SelectedItem = fit;
					},
					Icon = AggContext.StaticData.LoadIcon("fit.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.HasSelection && !(scene.SelectedItem is SelectionGroupObject3D),
				},
			};

			var operationIconsByType = new Dictionary<Type, ImageBuffer>();

			foreach (var operation in registeredSceneOperations)
			{
				if (operation.OperationType != null)
				{
					operationIconsByType.Add(operation.OperationType, operation.Icon);
				}
			}

			// TODO: Use custom selection group icon if reusing group icon seems incorrect
			//
			// Explicitly register SelectionGroup icon
			if (operationIconsByType.TryGetValue(typeof(GroupObject3D), out ImageBuffer groupIcon))
			{
				operationIconsByType.Add(typeof(SelectionGroupObject3D), groupIcon);
			}

			this.Thumbnails.OperationIcons = operationIconsByType;
		}

		public ImageSequence GetProcessingSequence(Color color)
		{
			int size = (int)Math.Round(80 * GuiWidget.DeviceScale);
			double radius = size / 8.0;
			var workingAnimation = new ImageSequence();
			var frameCount = 30.0;
			var strokeWidth = 4 * GuiWidget.DeviceScale;
			for (int i = 0; i < frameCount; i++)
			{
				var frame = new ImageBuffer(size, size);
				var graphics = frame.NewGraphics2D();
				graphics.Render(new Stroke(new Arc(frame.Width / 2, frame.Height / 2,
					size / 4 - strokeWidth / 2, size / 4 - strokeWidth / 2,
					MathHelper.Tau / frameCount * i,
					MathHelper.Tau / 4 + MathHelper.Tau / frameCount * i), strokeWidth), color);
				workingAnimation.AddImage(frame);
			}

			return workingAnimation;
		}

		static int applicationInstanceCount = 0;
		public static int ApplicationInstanceCount
		{
			get
			{
				if (applicationInstanceCount == 0)
				{
					Assembly mcAssembly = Assembly.GetEntryAssembly();
					if (mcAssembly != null)
					{
						string applicationName = Path.GetFileNameWithoutExtension(mcAssembly.Location).ToUpper();
						Process[] p1 = Process.GetProcesses();
						foreach (System.Diagnostics.Process pro in p1)
						{
							try
							{
								if (pro?.ProcessName != null
								   && pro.ProcessName.ToUpper().Contains(applicationName))
								{
									applicationInstanceCount++;
								}
							}
							catch
							{
							}
						}
					}
				}

				return applicationInstanceCount;
			}
		}

		public LibraryConfig Library { get; }

		public GraphConfig Graph { get; }

		private void InitializeLibrary()
		{
			if (Directory.Exists(ApplicationDataStorage.Instance.DownloadsDirectory))
			{
				this.Library.RegisterContainer(
					new DynamicContainerLink(
						() => "Downloads".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "download_folder.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DownloadsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true
						}));
			}

			this.Library.LibraryCollectionContainer = new LibraryCollectionContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Library".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "library_folder.png")),
					() => this.Library.LibraryCollectionContainer));

			if (File.Exists(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
			{
				// Add each path defined in the CustomLibraryFolders file as a new FileSystemContainerItem
				foreach (string directory in File.ReadLines(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
				{
					//if (Directory.Exists(directory))
					{
						this.Library.RegisterContainer(
							new FileSystemContainer.DirectoryContainerLink(directory)
							{
								UseIncrementedNameDuringTypeChange = true
							});
					}
				}
			}

			this.Library.RegisterContainer(
				new DynamicContainerLink(
						() => "SD Card".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "sd_folder.png")),
						() => new SDCardContainer(),
						() =>
						{
							var printer = this.ActivePrinter;
							return printer.Settings.GetValue<bool>(SettingsKey.has_sd_card_reader);
						})
				{
					IsReadOnly = true
				});

			this.Library.PlatingHistory = new PlatingHistoryContainer();

			this.Library.PartHistory = new PartHistoryContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "History".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "history_folder.png")),
					() => new RootHistoryContainer()));
		}

		public ApplicationController()
		{
			// Initialize the AppContext theme object which will sync its content with Agg ActiveTheme changes
			this.Theme = new ThemeConfig();
			this.MenuTheme = new ThemeConfig();

			HelpContainer helpGuides = null;

			string helpGuidesPath = Path.Combine("OEMSettings", "HelpGuides.json");
			if (AggContext.StaticData.FileExists(helpGuidesPath))
			{
				try
				{
					helpGuides = JsonConvert.DeserializeObject<HelpContainer>(AggContext.StaticData.ReadAllText(helpGuidesPath));
				}
				catch { }
			}

			this.FeatureGuides = helpGuides ?? new HelpContainer();

			ActiveTheme.ThemeChanged.RegisterEvent((s, e) =>
			{
				ChangeToTheme(ActiveTheme.Instance);
			}, ref unregisterEvents);

			this.ChangeToTheme(ActiveTheme.Instance);

			Object3D.AssetsPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "Assets");

			ScrollBar.DefaultMargin = new BorderDouble(right: 1);
			ScrollBar.ScrollBarWidth = 8 * GuiWidget.DeviceScale;
			ScrollBar.GrowThumbBy = 2;

			// Initialize statics
			DefaultThumbBackground.DefaultBackgroundColor = Color.Transparent;
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;

			this.Library = new LibraryConfig();
			this.Graph = new GraphConfig();
			this.Library.ContentProviders.Add(new[] { "stl", "obj", "amf", "mcx" }, new MeshContentProvider());
			this.Library.ContentProviders.Add("gcode", new GCodeContentProvider());
			this.Library.ContentProviders.Add(new[] { "png", "gif", "jpg", "jpeg" }, new ImageContentProvider());

			this.Graph.RegisterOperation(
				typeof(ImageObject3D),
				"Image to Path".Localize(),
				(sceneItem, scene) =>
				{
					if (sceneItem is IObject3D imageObject)
					{
						var path = new ImageToPathObject3D();
						sceneItem.WrapWith(path, scene);
						path.Invalidate(new InvalidateArgs(path, InvalidateType.Properties, null));
					}

					return Task.CompletedTask;
				},
				iconCollector: () => AggContext.StaticData.LoadIcon("noun_479927.png", ApplicationController.Instance.MenuTheme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				"Rotate".Localize(),
				(sceneItem, scene) =>
				{
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;
					var rotate = RotateObject3D_2.Create(selectedItem.Clone());
					rotate.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { selectedItem }, new List<IObject3D> { rotate }));
					scene.SelectedItem = rotate;

					return Task.CompletedTask;
				},
				iconCollector: () => AggContext.StaticData.LoadIcon("icon_rotate_32x32.png", ApplicationController.Instance.MenuTheme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IPathObject),
				"Linear Extrude".Localize(),
				(sceneItem, scene) =>
				{
					if (sceneItem is IPathObject imageObject)
					{
						var extrude = new LinearExtrudeObject3D();
						sceneItem.WrapWith(extrude, scene);
						extrude.Invalidate(new InvalidateArgs(extrude, InvalidateType.Properties, null));
					}

					return Task.CompletedTask;
				},
				iconCollector: () => AggContext.StaticData.LoadIcon("noun_84751.png", ApplicationController.Instance.MenuTheme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IPathObject),
				"Smooth Path".Localize(),
				(sceneItem, scene) =>
				{
					if (sceneItem is IPathObject imageObject)
					{
						var smoothPath = new SmoothPathObject3D();
						sceneItem.WrapWith(smoothPath, scene);
						smoothPath.Invalidate(new InvalidateArgs(smoothPath, InvalidateType.Properties, null));
					}

					return Task.CompletedTask;
				},
				iconCollector: () => AggContext.StaticData.LoadIcon("noun_479927.png", ApplicationController.Instance.MenuTheme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				"Add Base".Localize(),
				(item, scene) =>
				{
					bool wasSelected = scene.SelectedItem == item;

					var newChild = item.Clone();
					var baseMesh = new BaseObject3D()
					{
						Matrix = newChild.Matrix
					};
					newChild.Matrix = Matrix4X4.Identity;
					baseMesh.Children.Add(newChild);
					baseMesh.Invalidate(new InvalidateArgs(baseMesh, InvalidateType.Properties, null));

					scene.UndoBuffer.AddAndDo(
						new ReplaceCommand(
							new List<IObject3D> { item },
							new List<IObject3D> { baseMesh }));

					if(wasSelected)
					{
						scene.SelectedItem = baseMesh;
					}

					return Task.CompletedTask;
				},
				isVisible: (sceneItem) => sceneItem.Children.Any((i) => i is IPathObject),
				iconCollector: () => AggContext.StaticData.LoadIcon("noun_55060.png", ApplicationController.Instance.MenuTheme.InvertIcons));


			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringArg
					&& SettingsOrganizer.SettingsData.TryGetValue(stringArg.Data, out SliceSettingData settingsData)
					&& settingsData.ReloadUiWhenChanged)
				{
					UiThread.RunOnIdle(ReloadAll);
				}
			}, ref unregisterEvents);

			bool waitingForBedHeat = false;
			bool waitingForExtruderHeat = false;
			double heatDistance = 0;
			double heatStart = 0;

			// show temperature heating for m109 and m190
			PrinterConnection.AnyCommunicationStateChanged.RegisterEvent((s, e) =>
			{
				var printerConnection = this.ActivePrinter.Connection;

				if (printerConnection.PrinterIsPrinting || printerConnection.PrinterIsPaused)
				{
					switch (printerConnection.DetailedPrintingState)
					{
						case DetailedPrintingState.HeatingBed:
							Tasks.Execute(
								"Heating Bed".Localize(),
								(reporter, cancellationToken) =>
								{
									waitingForBedHeat = true;
									waitingForExtruderHeat = false;

									var progressStatus = new ProgressStatus();
									heatStart = printerConnection.ActualBedTemperature;
									heatDistance = Math.Abs(printerConnection.TargetBedTemperature - heatStart);

									while (heatDistance > 0 && waitingForBedHeat)
									{
										var remainingDistance = Math.Abs(printerConnection.TargetBedTemperature - printerConnection.ActualBedTemperature);
										progressStatus.Status = $"Heating Bed ({printerConnection.ActualBedTemperature:0}/{printerConnection.TargetBedTemperature:0})";
										progressStatus.Progress0To1 = (heatDistance - remainingDistance) / heatDistance;
										reporter.Report(progressStatus);
										Thread.Sleep(10);
									}

									return Task.CompletedTask;
								},
								new RunningTaskOptions()
								{
									ReadOnlyReporting = true
								});
							break;

						case DetailedPrintingState.HeatingExtruder:
							Tasks.Execute(
								"Heating Extruder".Localize(),
								(reporter, cancellationToken) =>
								{
									waitingForBedHeat = false;
									waitingForExtruderHeat = true;

									var progressStatus = new ProgressStatus();

									heatStart = printerConnection.GetActualHotendTemperature(0);
									heatDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - heatStart);

									while (heatDistance > 0 && waitingForExtruderHeat)
									{
										var currentDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - printerConnection.GetActualHotendTemperature(0));
										progressStatus.Progress0To1 = (heatDistance - currentDistance) / heatDistance;
										progressStatus.Status = $"Heating Extruder ({printerConnection.GetActualHotendTemperature(0):0}/{printerConnection.GetTargetHotendTemperature(0):0})";
										reporter.Report(progressStatus);
										Thread.Sleep(1000);
									}

									return Task.CompletedTask;
								},
								new RunningTaskOptions()
								{
									ReadOnlyReporting = true
								});
							break;

						case DetailedPrintingState.HomingAxis:
						case DetailedPrintingState.Printing:
						default:
							// clear any existing waiting states
							waitingForBedHeat = false;
							waitingForExtruderHeat = false;
							break;
					}
				}
				else
				{
					// turn of any running temp feedback tasks
					waitingForBedHeat = false;
					waitingForExtruderHeat = false;
				}
			}, ref unregisterEvent);

			// show countdown for turning off heat if required
			PrinterConnection.HeatTurningOffSoon.RegisterEvent((s, e) =>
			{
				var printerConnection = this.ActivePrinter.Connection;

				if (printerConnection.AnyHeatIsOn)
				{
					Tasks.Execute("Disable Heaters".Localize(), (reporter, cancellationToken) =>
					{
						var progressStatus = new ProgressStatus();

						while (printerConnection.SecondsUntilTurnOffHeaters > 0
							&& !cancellationToken.IsCancellationRequested
							&& printerConnection.ContinuWaitingToTurnOffHeaters)
						{
							reporter.Report(progressStatus);
							progressStatus.Status = "Turn Off Heat in".Localize() + " " + printerConnection.SecondsUntilTurnOffHeaters.ToString("0");
							Thread.Sleep(100);
						}

						if (!cancellationToken.IsCancellationRequested
							&& printerConnection.ContinuWaitingToTurnOffHeaters)
						{
							printerConnection.TurnOffBedAndExtruders(TurnOff.Now);
						}

						if (cancellationToken.IsCancellationRequested)
						{
							printerConnection.ContinuWaitingToTurnOffHeaters = false;
						}

						return Task.CompletedTask;
					});
				}
			}, ref unregisterEvents);

			PrinterConnection.ErrorReported.RegisterEvent((s, e) =>
			{
				var foundStringEventArgs = e as FoundStringEventArgs;
				if (foundStringEventArgs != null)
				{
					string message = "Your printer is reporting a hardware Error. This may prevent your printer from functioning properly.".Localize()
						+ "\n"
						+ "\n"
						+ "Error Reported".Localize() + ":"
						+ $" \"{foundStringEventArgs.LineToCheck}\".";
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox(message, "Printer Hardware Error".Localize())
					);
				}
			}, ref unregisterEvent);

			this.InitializeLibrary();

			PrinterConnection.AnyConnectionSucceeded.RegisterEvent((s, e) =>
			{
				// run the print leveling wizard if we need to for this printer
				var printer = ApplicationController.Instance.ActivePrinters.Where(p => p.Connection == s).FirstOrDefault();
				if (printer != null)
				{
					UiThread.RunOnIdle(() =>
					{
						ApplicationController.Instance.RunAnyRequiredPrinterSetup(printer, this.Theme);
					});
				}
			}, ref unregisterEvents);

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			foreach (IObject3DEditor editor in PluginFinder.CreateInstancesOf<IObject3DEditor>())
			{
				foreach (Type type in editor.SupportedTypes())
				{
					if (!objectEditorsByType.TryGetValue(type, out mappedEditors))
					{
						mappedEditors = new HashSet<IObject3DEditor>();
						objectEditorsByType.Add(type, mappedEditors);
					}

					mappedEditors.Add(editor);
				}
			}
		}

		private void ChangeToTheme(IThemeColors themeColors)
		{
			this.Theme.RebuildTheme(themeColors);

			var json = JsonConvert.SerializeObject(ActiveTheme.Instance);

			var clonedColors = JsonConvert.DeserializeObject<ThemeColors>(json);
			clonedColors.IsDarkTheme = false;
			clonedColors.Name = "MenuColors";
			clonedColors.PrimaryTextColor = new Color("#222");
			clonedColors.SecondaryTextColor = new Color("#666");
			clonedColors.PrimaryBackgroundColor = new Color("#fff");
			clonedColors.SecondaryBackgroundColor = new Color("#ddd");
			clonedColors.TertiaryBackgroundColor = new Color("#ccc");

			this.MenuTheme.RebuildTheme(clonedColors);

			this.RebuildSceneOperations(this.Theme);

#if DEBUG && !__ANDROID__
			if (AggContext.StaticData is FileSystemStaticData staticData)
			{
				staticData.PurgeCache();
			}
#endif
		}

		public bool RunAnyRequiredPrinterSetup(PrinterConfig printer, ThemeConfig theme)
		{
			if (PrintLevelingData.NeedsToBeRun(printer))
			{
				// run probe calibration first if we need to
				if (ProbeCalibrationWizard.NeedsToBeRun(printer))
				{
					UiThread.RunOnIdle(() =>
					{
						LevelingWizard.ShowProbeCalibrationWizard(printer, theme);
					});
				}
				else // run the leveling wizard
				{
					UiThread.RunOnIdle(() =>
					{
						LevelingWizard.ShowPrintLevelWizard(printer, theme);
					});
				}
				return true;
			}

			return false;
		}

		public HashSet<IObject3DEditor> GetEditorsForType(Type selectedItemType)
		{
			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItemType, out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var kvp in objectEditorsByType)
				{
					var editorType = kvp.Key;

					if (editorType.IsAssignableFrom(selectedItemType)
						&& selectedItemType != typeof(Object3D))
					{
						mappedEditors = kvp.Value;
						break;
					}
				}
			}

			return mappedEditors;
		}

		internal void Shutdown()
		{
			// Ensure all threads shutdown gracefully on close

			// Release any waiting generator threads
			this.Thumbnails.Shutdown();

			// Kill all long running tasks (this will release the silcing thread if running)
			foreach (var task in Tasks.RunningTasks)
			{
				task.CancelTask();
			}
		}

		static Dictionary<NamedTypeFace, TypeFace> TypeFaceCache { get; set; } = new Dictionary<NamedTypeFace, TypeFace>()
		{
			[NamedTypeFace.Liberation_Sans] = LiberationSansFont.Instance,
			[NamedTypeFace.Liberation_Sans_Bold] = LiberationSansBoldFont.Instance,
			[NamedTypeFace.Liberation_Mono] = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")))
		};

		public static TypeFace GetTypeFace(NamedTypeFace Name)
		{
			if(!TypeFaceCache.ContainsKey(Name))
			{
				TypeFace typeFace = new TypeFace();
				var file = Path.Combine("Fonts", $"{Name}.ttf");
				var exists = AggContext.StaticData.FileExists(file);
				var stream = exists ? AggContext.StaticData.OpenStream(file) : null;
				if (stream != null
					&& typeFace.LoadTTF(stream))
				{
					TypeFaceCache.Add(Name, typeFace);
				}
				else
				{
					// try the svg
					file = Path.Combine("Fonts", $"{Name}.svg");
					exists = AggContext.StaticData.FileExists(file);
					typeFace = exists ? TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(file)) : null;
					if (typeFace != null)
					{
						TypeFaceCache.Add(Name, typeFace);
					}
					else
					{
						// assign it to the default
						TypeFaceCache.Add(Name, TypeFaceCache[NamedTypeFace.Liberation_Sans]);
					}
				}
			}

			return TypeFaceCache[Name];
		}


		private static TypeFace titilliumTypeFace = null;
		public static TypeFace TitilliumTypeFace
		{
			get
			{
				if (titilliumTypeFace == null)
				{
					titilliumTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "TitilliumWeb-Black.svg")));
				}

				return titilliumTypeFace;
			}
		}


		public static string LoadCachedFile(string cacheKey, string cacheScope)
		{
			string cachePath = CacheablePath(cacheScope, cacheKey);

			if (File.Exists(cachePath))
			{
				// Load from cache and deserialize
				return File.ReadAllText(cachePath);
			}

			return null;
		}

		public static Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, string staticDataFallbackPath = null) where T : class
		{
			if (LoadCachedFile(cacheKey, cacheScope) is string cachedFile)
			{
				// Load from cache and deserialize
					return Task.FromResult(
						JsonConvert.DeserializeObject<T>(cachedFile));
			}

			try
			{
				if (staticDataFallbackPath != null
					&& AggContext.StaticData.FileExists(staticDataFallbackPath))
				{
					return Task.FromResult(
						JsonConvert.DeserializeObject<T>(AggContext.StaticData.ReadAllText(staticDataFallbackPath)));
				}
			}
			catch
			{
			}

			return Task.FromResult(default(T));
		}

		/// <summary>
		/// Requests fresh content from online services, falling back to cached content if offline
		/// </summary>
		/// <param name="collector">The custom collector function to load the content</param>
		/// <returns></returns>
		public async static Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, Func<Task<T>> collector, string staticDataFallbackPath = null) where T : class
		{
			string cachePath = CacheablePath(cacheScope, cacheKey);

			try
			{
				// Try to update the document
				T item = await collector();
				if (item != null)
				{
					// update cache on success
					File.WriteAllText(cachePath, JsonConvert.SerializeObject(item, Formatting.Indented));
					return item;
				}
			}
			catch
			{
				// Fall back to preexisting cache if failed
			}

			return await LoadCacheableAsync<T>(cacheKey, cacheScope, staticDataFallbackPath);
		}

		public static string CacheablePath(string cacheScope, string cacheKey)
		{
			string scopeDirectory = Path.Combine(cacheDirectory, cacheScope);

			// Ensure directory exists
			Directory.CreateDirectory(scopeDirectory);

			return Path.Combine(scopeDirectory, cacheKey);
		}

		// Indicates if given file can be opened on the design surface
		public bool IsLoadableFile(string filePath)
		{
			string extension = Path.GetExtension(filePath).ToLower();
			string extensionWithoutPeriod = extension.Trim('.');

			return !string.IsNullOrEmpty(extension)
				&& (ApplicationSettings.OpenDesignFileParams.Contains(extension)
					|| this.Library.ContentProviders.Keys.Contains(extensionWithoutPeriod));
		}

		public bool IsReloading { get; private set; } = false;

		public void ReloadAll()
		{
			UiThread.RunOnIdle(() =>
			{
				var reloadingOverlay = new GuiWidget
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					BackgroundColor = this.Theme.DarkShade
				};

				reloadingOverlay.AddChild(new TextWidget("Reloading".Localize() + "...", textColor: Color.White, pointSize: this.Theme.DefaultFontSize * 1.5)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center
				});

				AppContext.RootSystemWindow.AddChild(reloadingOverlay);

				this.IsReloading = true;

				UiThread.RunOnIdle(() =>
				{
					GuiWidget.LayoutCount = 0;
					using (new QuickTimer($"ReloadAll_{reloadCount++}:"))
					{
						MainView = new WidescreenPanel();
						this.DoneReloadingAll?.CallEvents(null, null);

						using (new QuickTimer("Time to AddMainview: "))
						{
							AppContext.RootSystemWindow.CloseAllChildren();
							AppContext.RootSystemWindow.AddChild(MainView);
						}
					}
					Debug.WriteLine($"LayoutCount: {GuiWidget.LayoutCount:0.0}");

					this.IsReloading = false;
				});
			});
		}

		static int reloadCount = 0;

		public async void OnApplicationClosed()
		{
			this.Thumbnails.Shutdown();

			// Save changes before close
			if (this.ActivePrinter != null
				&& this.ActivePrinter != emptyPrinter)
			{
				await this.ActivePrinter.Bed.SaveChanges(null, CancellationToken.None);
			}

			ApplicationSettings.Instance.ReleaseClientToken();
		}

		internal static void LoadTheme()
		{
			string activeThemeName = UserSettings.Instance.get(UserSettingsKey.ActiveThemeName);
			if (!string.IsNullOrEmpty(activeThemeName))
			{
				ActiveTheme.Instance = ActiveTheme.GetThemeColors(activeThemeName);
			}
			else if (!string.IsNullOrEmpty(OemSettings.Instance.ThemeColor))
			{
				ActiveTheme.Instance = ActiveTheme.GetThemeColors(OemSettings.Instance.ThemeColor);
			}
			else
			{
				ActiveTheme.Instance = ActiveTheme.GetThemeColors("Blue - Light");
			}
		}

		public static ApplicationController Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationController();
				}

				return globalInstance;
			}
		}

		public DragDropData DragDropData { get; set; } = new DragDropData();

		public string PrintingItemName { get; set; }

		public string ShortProductName => "MatterControl";
		public string ProductName => "MatterHackers: MatterControl";

		public void SwitchToPurchasedLibrary()
		{
			var purchasedContainer = Library.RootLibaryContainer.ChildContainers.Where(c => c.ID == "LibraryProviderPurchasedKey").FirstOrDefault();
			if (purchasedContainer != null)
			{
				// TODO: Navigate to purchased container
				throw new NotImplementedException("SwitchToPurchasedLibrary");
			}
		}

		public void OnLoadActions()
		{
			// TODO: Calling UserChanged seems wrong. Load the right user before we spin up controls, rather than after
			// Pushing this after load fixes that empty printer list
			/////////////////////ApplicationController.Instance.UserChanged();

			bool showAuthWindow = PrinterSetup.ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthWindow)
			{
				if (ApplicationSettings.Instance.get(ApplicationSettingsKey.SuppressAuthPanel) != "True")
				{
					//Launch window to prompt user to sign in
					UiThread.RunOnIdle(() => DialogWindow.Show(PrinterSetup.GetBestStartPage()));
				}
			}
			else
			{
				//If user in logged in sync before checking to prompt to create printer
				if (ApplicationController.SyncPrinterProfiles == null)
				{
					RunSetupIfRequired();
				}
				else
				{
					ApplicationController.SyncPrinterProfiles.Invoke("ApplicationController.OnLoadActions()", null).ContinueWith((task) =>
					{
						RunSetupIfRequired();
					});
				}
			}

			// TODO: This should be moved into the splash screen and shown instead of MainView
			if (AggContext.OperatingSystem == OSType.Android)
			{
				// show this last so it is on top
				if (UserSettings.Instance.get(UserSettingsKey.SoftwareLicenseAccepted) != "true")
				{
					UiThread.RunOnIdle(() => DialogWindow.Show<LicenseAgreementPage>());
				}
			}

			if (ApplicationController.Instance.ActivePrinter is PrinterConfig printer
				&& printer.Settings.PrinterSelected
				&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
			{
				UiThread.RunOnIdle(() =>
				{
					//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
					printer.Connection.Connect();
				}, 2);
			}

			if (AssetObject3D.AssetManager == null)
			{
				AssetObject3D.AssetManager = new AssetManager();
			}

			//HtmlWindowTest();
		}

		private static void RunSetupIfRequired()
		{
			if (!ProfileManager.Instance.ActiveProfiles.Any())
			{
				// Start the setup wizard if no profiles exist
				UiThread.RunOnIdle(() => DialogWindow.Show(PrinterSetup.GetBestStartPage()));
			}
		}

		public void SwitchToSharedLibrary()
		{
			// Switch to the shared library
			var libraryContainer = Library.RootLibaryContainer.ChildContainers.Where(c => c.ID == "LibraryProviderSharedKey").FirstOrDefault();
			if (libraryContainer != null)
			{
				// TODO: Navigate to purchased container
				throw new NotImplementedException("SwitchToSharedLibrary");
			}
		}

		public void ChangeCloudSyncStatus(bool userAuthenticated, string reason = "")
		{
			UserSettings.Instance.set(UserSettingsKey.CredentialsInvalid, userAuthenticated ? "false" : "true");
			UserSettings.Instance.set(UserSettingsKey.CredentialsInvalidReason, userAuthenticated ? "" : reason);

			CloudSyncStatusChanged.CallEvents(this, new CloudSyncEventArgs() { IsAuthenticated = userAuthenticated });

			// Only fire UserChanged if it actually happened - prevents runaway positive feedback loop
			if (!string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername)
				&& AuthenticationData.Instance.ActiveSessionUsername != AuthenticationData.Instance.LastSessionUsername)
			{
				// only set it if it is an actual user name
				AuthenticationData.Instance.LastSessionUsername = AuthenticationData.Instance.ActiveSessionUsername;
			}

			UiThread.RunOnIdle(this.UserChanged);
		}

		// Called after every startup and at the completion of every authentication change
		public void UserChanged()
		{
			ProfileManager.ReloadActiveUser();

			// Ensure SQLite printers are imported
			ProfileManager.Instance.EnsurePrintersImported();

			var guest = ProfileManager.Load("guest");

			// If profiles.json was created, run the import wizard to pull in any SQLite printers
			if (guest?.Profiles?.Any() == true
				&& !ProfileManager.Instance.IsGuestProfile
				&& !ProfileManager.Instance.PrintersImported)
			{
				// Show the import printers wizard
				DialogWindow.Show<CopyGuestProfilesToUser>();
			}
		}

		private EventHandler unregisterEvent;

		public Stream LoadHttpAsset(string url)
		{
			string fingerPrint = ToSHA1(url);
			string cachePath = ApplicationController.CacheablePath("HttpAssets", fingerPrint);

			if (File.Exists(cachePath))
			{
				return File.Open(cachePath, FileMode.Open);
			}
			else
			{
				var client = new WebClient();
				var bytes = client.DownloadData(url);

				File.WriteAllBytes(cachePath, bytes);

				return new MemoryStream(bytes);
			}
		}

		/// <summary>
		/// Compute hash for string encoded as UTF8
		/// </summary>
		/// <param name="s">String to be hashed</param>
		public static string ToSHA1(string s)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s);

			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(bytes);
				string SHA1 = BitConverter.ToString(hash).Replace("-", string.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
		}

		/// <summary>
		/// Download an image from the web into the specified ImageBuffer
		/// </summary>
		/// <param name="uri"></param>
		public void DownloadToImageAsync(ImageBuffer imageToLoadInto, string uriToLoad, bool scaleToImageX, IRecieveBlenderByte scalingBlender = null)
		{
			if (scalingBlender == null)
			{
				scalingBlender = new BlenderBGRA();
			}

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					// scale the loaded image to the size of the target image
					byte[] raw = e.Result;
					Stream stream = new MemoryStream(raw);
					ImageBuffer unScaledImage = new ImageBuffer(10, 10);
					if (scaleToImageX)
					{
						AggContext.StaticData.LoadImageData(stream, unScaledImage);
						// If the source image (the one we downloaded) is more than twice as big as our dest image.
						while (unScaledImage.Width > imageToLoadInto.Width * 2)
						{
							// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
							// So we scale as many times as we need to get the Image to be the right size.
							// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
							ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, scalingBlender);
							halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
							unScaledImage = halfImage;
						}

						double finalScale = imageToLoadInto.Width / (double)unScaledImage.Width;
						imageToLoadInto.Allocate(imageToLoadInto.Width, (int)(unScaledImage.Height * finalScale), imageToLoadInto.Width * (imageToLoadInto.BitDepth / 8), imageToLoadInto.BitDepth);
						imageToLoadInto.NewGraphics2D().Render(unScaledImage, 0, 0, 0, finalScale, finalScale);
					}
					else
					{
						AggContext.StaticData.LoadImageData(stream, imageToLoadInto);
					}

					imageToLoadInto.MarkImageChanged();
				}
				catch
				{
				}
			};

			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{
			}
		}

		/// <summary>
		/// Download an image from the web into the specified ImageSequence
		/// </summary>
		/// <param name="uri"></param>
		public void DownloadToImageSequenceAsync(ImageSequence imageSequenceToLoadInto, string uriToLoad)
		{
			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					Task.Run(() =>
					{
						// scale the loaded image to the size of the target image
						byte[] raw = e.Result;
						Stream stream = new MemoryStream(raw);

						var asyncImageSequence = new ImageSequence();

						AggContext.StaticData.LoadImageSequenceData(stream, asyncImageSequence);

						UiThread.RunOnIdle(() =>
						{
							imageSequenceToLoadInto.Copy(asyncImageSequence);
							imageSequenceToLoadInto.Invalidate();
						});
					});
				}
				catch
				{
				}
			};

			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{
			}
		}

		/// <summary>
		/// Cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <returns>A boolean value indicating if the print was canceled</returns>
		public bool ConditionalCancelPrint()
		{
			bool canceled = false;

			if (this.ActivePrinter.Connection.SecondsPrinted > 120)
			{
				StyledMessageBox.ShowMessageBox(
					(bool response) =>
					{
						if (response)
						{
							UiThread.RunOnIdle(() => this.ActivePrinter.Connection.Stop());
							canceled = true;
						}

						canceled = false;
					},
					"Cancel the current print?".Localize(),
					"Cancel Print?".Localize(),
					StyledMessageBox.MessageType.YES_NO,
					"Cancel Print".Localize(),
					"Continue Printing".Localize());
			}
			else
			{
				this.ActivePrinter.Connection.Stop();
				canceled = false;
			}

			return canceled;
		}

		/// <summary>
		/// Register the given PrintItemAction into the named section
		/// </summary>
		/// <param name="section">The section to register in</param>
		/// <param name="printItemAction">The action to register</param>
		public void RegisterLibraryAction(string section, PrintItemAction printItemAction)
		{
			List<PrintItemAction> items;
			if (!registeredLibraryActions.TryGetValue(section, out items))
			{
				items = new List<PrintItemAction>();
				registeredLibraryActions.Add(section, items);
			}

			items.Add(printItemAction);
		}

		/// <summary>
		/// Enumerate the given section, returning all registered actions
		/// </summary>
		/// <param name="section">The section to enumerate</param>
		/// <returns></returns>
		public IEnumerable<PrintItemAction> RegisteredLibraryActions(string section)
		{
			List<PrintItemAction> items;
			if (registeredLibraryActions.TryGetValue(section, out items))
			{
				return items;
			}

			return Enumerable.Empty<PrintItemAction>();
		}

		public IEnumerable<SceneSelectionOperation> RegisteredSceneOperations => registeredSceneOperations;

		public static IObject3D ClipboardItem { get; internal set; }
		public Action<ILibraryItem> ShareLibraryItem { get; set; }

		public List<BedConfig> Workspaces { get; } = new List<BedConfig>();

		public AppViewState ViewState { get; } = new AppViewState();

		public event EventHandler<WidgetSourceEventArgs> AddPrintersTabRightElement;

		public void NotifyPrintersTabRightElement(GuiWidget sourceExentionArea)
		{
			AddPrintersTabRightElement?.Invoke(this, new WidgetSourceEventArgs(sourceExentionArea));
		}

		private string doNotAskAgainMessage = "Don't remind me again".Localize();

		public async Task PrintPart(EditContext editContext, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken, bool overrideAllowGCode = false)
		{
			var object3D = editContext.Content;
			var partFilePath = editContext.SourceFilePath;
			var gcodeFilePath = editContext.GCodeFilePath;
			var printItemName = editContext.SourceItem.Name;

			// Exit if called in a non-applicable state
			if (this.ActivePrinter.Connection.CommunicationState != CommunicationStates.Connected
				&& this.ActivePrinter.Connection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				return;
			}

			try
			{
				// If leveling is required or is currently on
				if(ApplicationController.Instance.RunAnyRequiredPrinterSetup(printer, this.Theme))
				{
					// We need to calibrate. So, don't print this part.
					return;
				}

				this.PrintingItemName = printItemName;

				if (ActiveSliceSettings.Instance.IsValid())
				{
					// clear the output cache prior to starting a print
					this.ActivePrinter.Connection.TerminalLog.Clear();

					string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

					if (Path.GetExtension(partFilePath).ToUpper() == ".GCODE"
						&& hideGCodeWarning == null
						&& !overrideAllowGCode)
					{
						var hideGCodeWarningCheckBox = new CheckBox(doNotAskAgainMessage)
						{
							TextColor = ActiveTheme.Instance.PrimaryTextColor,
							Margin = new BorderDouble(top: 6, left: 6),
							HAnchor = Agg.UI.HAnchor.Left
						};
						hideGCodeWarningCheckBox.Click += (sender, e) =>
						{
							if (hideGCodeWarningCheckBox.Checked)
							{
								ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, "true");
							}
							else
							{
								ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, null);
							}
						};

						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(
								(messageBoxResponse) =>
								{
									if (messageBoxResponse)
									{
										this.ActivePrinter.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
										this.ArchiveAndStartPrint(partFilePath, gcodeFilePath, printer);
									}
								},
								"The file you are attempting to print is a GCode file.\n\nIt is recommended that you only print Gcode files known to match your printer's configuration.\n\nAre you sure you want to print this GCode file?".Localize(),
								"Warning - GCode file".Localize(),
								new GuiWidget[]
								{
										new VerticalSpacer(),
										hideGCodeWarningCheckBox
								},
								StyledMessageBox.MessageType.YES_NO);
						});
					}
					else
					{
						this.ActivePrinter.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

						(bool slicingSucceeded, string finalPath) = await ApplicationController.Instance.SliceItemLoadOutput(
							printer,
							printer.Bed.Scene,
							gcodeFilePath);

						// Only start print if slicing completed
						if (slicingSucceeded)
						{
							this.ArchiveAndStartPrint(partFilePath, finalPath, printer);
						}
						else
						{
							// TODO: Need to reset printing state? This seems like I shouldn't own this indicator
							this.ActivePrinter.Connection.CommunicationState = CommunicationStates.Connected;
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public void ResetTranslationMap()
		{
			TranslationMap.ActiveTranslationMap = new TranslationMap("Translations", UserSettings.Instance.Language);
		}

		public void MonitorPrintTask(PrinterConfig printer)
		{
			string layerDetails = (printer.Bed.LoadedGCode?.LayerCount > 0) ? $" of {printer.Bed.LoadedGCode.LayerCount}" : "";

			ApplicationController.Instance.Tasks.Execute(
				"Printing".Localize(),
				(reporterB, cancellationTokenB) =>
				{
					var progressStatus = new ProgressStatus();
					reporterB.Report(progressStatus);

					return Task.Run(() =>
					{
						string printing = "Printing".Localize();
						int totalLayers = printer.Connection.TotalLayersInPrint;

						while (!printer.Connection.PrinterIsPrinting
							&& !cancellationTokenB.IsCancellationRequested)
						{
							// Wait for printing
							Thread.Sleep(200);
						}

						while ((printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused)
							&& !cancellationTokenB.IsCancellationRequested)
						{
							progressStatus.Status = $"{printing} ({printer.Connection.CurrentlyPrintingLayer + 1}{layerDetails}) - {printer.Connection.PercentComplete:0}%";

							progressStatus.Progress0To1 = printer.Connection.PercentComplete / 100;
							reporterB.Report(progressStatus);
							Thread.Sleep(200);
						}
					});
				},
				taskActions: new RunningTaskOptions()
				{
					ExpansionSerializationKey = $"{nameof(MonitorPrintTask)}_expanded",
					RichProgressWidget = () => PrinterTabPage.PrintProgressWidget(printer, ApplicationController.Instance.Theme),
					PauseAction = () => UiThread.RunOnIdle(() =>
					{
						printer.Connection.RequestPause();
					}),
					ResumeAction = () => UiThread.RunOnIdle(() =>
					{
						printer.Connection.Resume();
					}),
					StopAction = () => UiThread.RunOnIdle(() =>
					{
						ApplicationController.Instance.ConditionalCancelPrint();
					})
				});
		}

		/// <summary>
		/// Archives MCX and validates GCode results before starting a print operation
		/// </summary>
		/// <param name="sourcePath">The source file which originally caused the slice->print operation</param>
		/// <param name="gcodeFilePath">The resulting GCode to print</param>
		private async void ArchiveAndStartPrint(string sourcePath, string gcodeFilePath, PrinterConfig printer)
		{
			if (File.Exists(sourcePath)
				&& File.Exists(gcodeFilePath))
			{
				bool originalIsGCode = Path.GetExtension(sourcePath).ToUpper() == ".GCODE";
				if (File.Exists(gcodeFilePath))
				{
					// Create archive point for printing attempt
					if (Path.GetExtension(sourcePath).ToUpper() == ".MCX")
					{
						// TODO: We should zip mcx and settings when starting a print
						string platingDirectory = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "PrintHistory");
						Directory.CreateDirectory(platingDirectory);

						string now = "Workspace " + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
						string archivePath = Path.Combine(platingDirectory, now + ".zip");

						using (var file = File.OpenWrite(archivePath))
						using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
						{
							zip.CreateEntryFromFile(sourcePath, "PrinterPlate.mcx");
							zip.CreateEntryFromFile(ActiveSliceSettings.Instance.DocumentPath, ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name) + ".printer");
							zip.CreateEntryFromFile(gcodeFilePath, "sliced.gcode");
						}
					}

					if (originalIsGCode)
					{
						await this.ActivePrinter.Connection.StartPrint(gcodeFilePath);

						MonitorPrintTask(printer);

						return;
					}
					else
					{
						// read the last few k of the file and see if it says "filament used". We use this marker to tell if the file finished writing
						int bufferSize = 32000;
						using (Stream fileStream = new FileStream(gcodeFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
						{
							byte[] buffer = new byte[bufferSize];
							fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
							int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
							fileStream.Close();

							string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
							if (fileEnd.Contains("filament used"))
							{
								await this.ActivePrinter.Connection.StartPrint(gcodeFilePath);
								MonitorPrintTask(printer);
								return;
							}
						}
					}
				}

				this.ActivePrinter.Connection.CommunicationState = CommunicationStates.Connected;
			}
		}

		/// <summary>
		/// Slice the given IObject3D to the target GCode file using the referenced printer settings
		/// </summary>
		/// <param name="printer">The printer/settings to use</param>
		/// <param name="object3D">The IObject3D to slice</param>
		/// <param name="gcodeFilePath">The path to write the file to</param>
		/// <returns>A boolean indicating if the slicing operation completed without aborting</returns>
		public async Task<(bool, string)> SliceItemLoadOutput(PrinterConfig printer, IObject3D object3D, string gcodeFilePath)
		{
			// Slice
			bool slicingSucceeded = false;

			await ApplicationController.Instance.Tasks.Execute("Slicing".Localize(), async (reporter, cancellationToken) =>
			{
				slicingSucceeded = await Slicer.SliceItem(
					object3D,
					gcodeFilePath,
					printer,
					new SliceProgressReporter(reporter, printer),
					cancellationToken);
			});

			// Skip loading GCode output if slicing failed
			if (!slicingSucceeded)
			{
				return (false, gcodeFilePath);
			}

			var postProcessors = printer.Bed.Scene.Children.OfType<IGCodePostProcessor>();
			if (postProcessors.Any())
			{
				using (var resultStream = File.OpenRead(gcodeFilePath))
				{
					Stream contextStream = resultStream;

					// Execute each post processor
					foreach (var processor in postProcessors)
					{
						// Invoke the processor and store the resulting output to the context stream reference
						contextStream = processor.ProcessOutput(contextStream);

						// Reset to the beginning
						contextStream.Position = 0;
					}

					// Modify final file name
					gcodeFilePath = Path.ChangeExtension(gcodeFilePath, GCodeFile.PostProcessedExtension);

					// Copy the final stream to the revised gcodeFilePath
					using (var finalStream = File.OpenWrite(gcodeFilePath))
					{
						contextStream.CopyTo(finalStream);
					}
				}
			}

			await ApplicationController.Instance.Tasks.Execute("Loading GCode".Localize(), (innerProgress, token) =>
			{
				var status = new ProgressStatus();

				innerProgress.Report(status);

				printer.Bed.LoadGCode(gcodeFilePath, token, (progress0to1, statusText) =>
				{
					UiThread.RunOnIdle(() =>
					{
						status.Progress0To1 = progress0to1;
						status.Status = statusText;

						innerProgress.Report(status);
					});
				});

				return Task.CompletedTask;
			});

			return (slicingSucceeded, gcodeFilePath);
		}

		internal GuiWidget GetViewOptionButtons(BedConfig sceneContext, PrinterConfig printer, ThemeConfig theme)
		{
			var container = new FlowLayoutWidget();

			var bedButton = new RadioIconButton(AggContext.StaticData.LoadIcon("bed.png", theme.InvertIcons), theme)
			{
				Name = "Bed Button",
				ToolTipText = "Show Print Bed".Localize(),
				Checked = sceneContext.RendererOptions.RenderBed,
				Margin = theme.ButtonSpacing,
				ToggleButton = true,
				Height = 24,
				Width = 24
			};
			bedButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBed = bedButton.Checked;
			};
			container.AddChild(bedButton);

			Func<bool> buildHeightValid = () => sceneContext.BuildHeight > 0;

			var printAreaButton = new RadioIconButton(AggContext.StaticData.LoadIcon("print_area.png", theme.InvertIcons), theme)
			{
				Name = "Bed Button",
				ToolTipText = (buildHeightValid()) ? "Show Print Area".Localize() : "Define printer build height to enable",
				Checked = sceneContext.RendererOptions.RenderBuildVolume,
				Margin = theme.ButtonSpacing,
				ToggleButton = true,
				Enabled = buildHeightValid() && printer?.ViewState.ViewMode != PartViewMode.Layers2D,
				Height = 24,
				Width = 24
			};
			printAreaButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBuildVolume = printAreaButton.Checked;
			};
			container.AddChild(printAreaButton);

			this.BindBedOptions(container, bedButton, printAreaButton, sceneContext.RendererOptions);

			if (printer != null)
			{
				// Disable print area button in GCode2D view
				EventHandler<ViewModeChangedEventArgs> viewModeChanged = (s, e) =>
				{
					// Button is conditionally created based on BuildHeight, only set enabled if created
					printAreaButton.Enabled = buildHeightValid() && printer.ViewState.ViewMode != PartViewMode.Layers2D;
				};

				printer.ViewState.ViewModeChanged += viewModeChanged;

				container.Closed += (s, e) =>
				{
					printer.ViewState.ViewModeChanged -= viewModeChanged;
				};
			}

			return container;
		}

		public void BindBedOptions(GuiWidget container, ICheckbox bedButton, ICheckbox printAreaButton, View3DConfig renderOptions)
		{
			PropertyChangedEventHandler syncProperties = (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(renderOptions.RenderBed):
						bedButton.Checked = renderOptions.RenderBed;
						break;

					case nameof(renderOptions.RenderBuildVolume) when printAreaButton != null:
						printAreaButton.Checked = renderOptions.RenderBuildVolume;
						break;
				}
			};

			renderOptions.PropertyChanged += syncProperties;

			container.Closed += (s, e) =>
			{
				renderOptions.PropertyChanged -= syncProperties;
			};
		}

		public class CloudSyncEventArgs : EventArgs
		{
			public bool IsAuthenticated { get; set; }
		}
	}

	public class WidgetSourceEventArgs : EventArgs
	{
		public GuiWidget Source { get; }

		public WidgetSourceEventArgs(GuiWidget source)
		{
			this.Source = source;
		}
	}

	public class DragDropData
	{
		public View3DWidget View3DWidget { get; set; }
		public PrinterConfig Printer { get; internal set; }
		public BedConfig SceneContext { get; set; }
	}

	public class RunningTaskDetails : IProgress<ProgressStatus>
	{
		public event EventHandler<ProgressStatus> ProgressChanged;

		public Func<GuiWidget> DetailsItemAction { get; set; }

		private CancellationTokenSource tokenSource;

		private bool? _isExpanded = null;

		public RunningTaskDetails(CancellationTokenSource tokenSource)
		{
			this.tokenSource = tokenSource;
		}

		public string Title { get; set; }

		public RunningTaskOptions Options { get; internal set; }

		public bool IsExpanded
		{
			get
			{
				if (_isExpanded == null)
				{
					if (this.Options is RunningTaskOptions options
						&& !string.IsNullOrWhiteSpace(options.ExpansionSerializationKey))
					{
						string dbValue = UserSettings.Instance.get(options.ExpansionSerializationKey);
						_isExpanded = dbValue != "0";
					}
					else
					{
						_isExpanded = false;
					}
				}

				return _isExpanded ?? false;
			}
			set
			{
				_isExpanded = value;

				if (this.Options?.ExpansionSerializationKey is string expansionKey
					&& !string.IsNullOrWhiteSpace(expansionKey))
				{
					UserSettings.Instance.set(expansionKey, (_isExpanded ?? false) ? "1" : "0");
				}
			}
		}

		public void Report(ProgressStatus progressStatus)
		{
			this.ProgressChanged?.Invoke(this, progressStatus);
		}

		public void CancelTask()
		{
			this.tokenSource.Cancel();
		}
	}

	public class RunningTaskOptions
	{
		/// <summary>
		/// The Rich progress widget to be shown when expanded
		/// </summary>
		public Func<GuiWidget> RichProgressWidget { get; set; }

		/// <summary>
		/// The database key used to round trip expansion state
		/// </summary>
		public string ExpansionSerializationKey { get; set; }

		public Action PauseAction { get; set; }
		public Action ResumeAction { get; set; }
		public Action StopAction { get; set; }

		/// <summary>
		/// Indicates if the task should suppress pause/resume/stop operations
		/// </summary>
		public bool ReadOnlyReporting { get; set; } = false;
	}

	public class RunningTasksConfig
	{
		public event EventHandler TasksChanged;

		private ObservableCollection<RunningTaskDetails> executingTasks = new ObservableCollection<RunningTaskDetails>();

		public IEnumerable<RunningTaskDetails> RunningTasks => executingTasks.ToList();

		public RunningTasksConfig()
		{
			executingTasks.CollectionChanged += (s, e) =>
			{
				UiThread.RunOnIdle(() => this.TasksChanged?.Invoke(this, null));
			};
		}

		public Task Execute(string taskTitle, Func<IProgress<ProgressStatus>, CancellationToken, Task> func, RunningTaskOptions taskActions = null)
		{
			var tokenSource = new CancellationTokenSource();

			var taskDetails = new RunningTaskDetails(tokenSource)
			{
				Options = taskActions,
				Title = taskTitle
			};

			executingTasks.Add(taskDetails);

			return Task.Run(async () =>
			{
				try
				{
					await func?.Invoke(taskDetails, tokenSource.Token);
				}
				catch
				{
				}

				executingTasks.Remove(taskDetails);
			});
		}
	}

	public enum ReportSeverity2 { Warning, Error }

	public interface INativePlatformFeatures
	{
		event EventHandler PictureTaken;
		void TakePhoto(string imageFileName);
		void OpenCameraPreview();
		void PlaySound(string fileName);
		void ConfigureWifi();
		bool CameraInUseByExternalProcess { get; set; }
		bool IsNetworkConnected();
		void FindAndInstantiatePlugins(SystemWindow systemWindow);
		void ProcessCommandline();
		void PlatformInit(Action<string> reporter);
	}

	public static class Application
	{
		private static ProgressBar progressBar;
		private static TextWidget statusText;
		private static FlowLayoutWidget progressPanel;
		private static string lastSection = "";
		private static Stopwatch timer;

		public static string PlatformFeaturesProvider { get; set; } = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl";

		public static SystemWindow LoadRootWindow(int width, int height)
		{
			timer = Stopwatch.StartNew();

			var systemWindow = new RootSystemWindow(width, height);

			// Load theme
			ApplicationController.LoadTheme();

			var overlay = new GuiWidget()
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};
			overlay.AnchorAll();

			systemWindow.AddChild(overlay);

			var mutedAccentColor = new Color(ActiveTheme.Instance.PrimaryAccentColor, 185).OverlayOn(Color.White).ToColor();

			var spinner = new LogoSpinner(overlay, rotateX: -0.05)
			{
				MeshColor = mutedAccentColor
			};

			progressPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Position = new Vector2(0, height * .25),
				HAnchor = HAnchor.Center | HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(400, 100),
				Margin = new BorderDouble(0, 0, 0, 200)
			};
			overlay.AddChild(progressPanel);

			progressPanel.AddChild(statusText = new TextWidget("", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				MinimumSize = new Vector2(200, 30),
				HAnchor = HAnchor.Center,
				AutoExpandBoundsToText = true
			});

			progressPanel.AddChild(progressBar = new ProgressBar()
			{
				FillColor = mutedAccentColor,
				BorderColor = Color.Gray, // theme.GetBorderColor(75),
				Height = 11,
				Width = 230,
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute
			});

			AppContext.RootSystemWindow = systemWindow;

			// hook up a keyboard watcher to rout keys when not handled by children

			systemWindow.KeyPressed += (s, keyEvent) =>
			{
				var view3D = systemWindow.Descendants<View3DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var printerTabPage = systemWindow.Descendants<PrinterTabPage>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var offsetDist = 50;

				if (!keyEvent.Handled
					&& view3D != null)
				{
					switch (keyEvent.KeyChar)
					{
						case 'w':
						case 'W':
							view3D.ResetView();
							keyEvent.Handled = true;
							break;

						case ' ':
							view3D.Scene.ClearSelection();
							keyEvent.Handled = true;
							break;
					}
				}
			};

			systemWindow.KeyDown += (s, keyEvent) =>
			{
				var view3D = systemWindow.Descendants<View3DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var printerTabPage = systemWindow.Descendants<PrinterTabPage>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var offsetDist = 50;
				var arrowKeyOpperation = keyEvent.Shift ? TrackBallTransformType.Translation : TrackBallTransformType.Rotation;

				var gcode2D = systemWindow.Descendants<GCode2DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();

				if (keyEvent.KeyCode == Keys.F1)
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(new DesignSpaceGuide("AllGuides"));
					});
				}

				if (!keyEvent.Handled
					&& gcode2D != null)
				{
					switch (keyEvent.KeyCode)
					{
						case Keys.Oemplus:
						case Keys.Add:
							if (keyEvent.Control)
							{
								// Zoom out
								gcode2D.Zoom(1.2);
								keyEvent.Handled = true;
							}
							break;

						case Keys.OemMinus:
						case Keys.Subtract:
							if (keyEvent.Control)
							{
								// Zoom in
								gcode2D.Zoom(.8);
								keyEvent.Handled = true;
							}
							break;
					}
				}

				if (!keyEvent.Handled
					&& view3D != null)
				{
					switch (keyEvent.KeyCode)
					{
						case Keys.C:
							if (keyEvent.Control)
							{
								view3D.Scene.Copy();
								keyEvent.Handled = true;
							}
							break;

						case Keys.X:
							if (keyEvent.Control)
							{
								view3D.Scene.Cut();
								keyEvent.Handled = true;
							}
							break;

						case Keys.Y:
							if (keyEvent.Control)
							{
								view3D.Scene.UndoBuffer.Redo();
								keyEvent.Handled = true;
							}
							break;

						case Keys.A:
							if (keyEvent.Control)
							{
								view3D.SelectAll();
								keyEvent.Handled = true;
							}
							break;

						case Keys.S:
							if (keyEvent.Control)
							{
								view3D.Save();
								keyEvent.Handled = true;
							}
							break;

						case Keys.V:
							if (keyEvent.Control)
							{
								view3D.Scene.Paste();
								keyEvent.Handled = true;
							}
							break;

						case Keys.Oemplus:
						case Keys.Add:
							if (keyEvent.Control)
							{
								// Zoom out
								Offset3DView(view3D, new Vector2(0, offsetDist), TrackBallTransformType.Scale);
								keyEvent.Handled = true;
							}
							break;

						case Keys.OemMinus:
						case Keys.Subtract:
							if (keyEvent.Control)
							{
								// Zoom in
								Offset3DView(view3D, new Vector2(0, -offsetDist), TrackBallTransformType.Scale);
								keyEvent.Handled = true;
							}
							break;

						case Keys.Z:
							if (keyEvent.Control)
							{
								if (keyEvent.Shift)
								{
									view3D.Scene.UndoBuffer.Redo();
								}
								else
								{
									// undo last operation
									view3D.Scene.UndoBuffer.Undo();
								}
								keyEvent.Handled = true;
							}
							break;

						case Keys.Insert:
							if(keyEvent.Shift)
							{
								view3D.Scene.Paste();
								keyEvent.Handled = true;
							}
							break;

						case Keys.Delete:
						case Keys.Back:
							view3D.Scene.DeleteSelection();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Escape:
							if (view3D.CurrentSelectInfo.DownOnPart)
							{
								view3D.CurrentSelectInfo.DownOnPart = false;

								view3D.Scene.SelectedItem.Matrix = view3D.TransformOnMouseDown;

								keyEvent.Handled = true;
								keyEvent.SuppressKeyPress = true;
							}
							foreach(var interactionVolume in view3D.InteractionLayer.InteractionVolumes)
							{
								interactionVolume.CancelOpperation();
							}
							break;

						case Keys.Left:
							// move or rotate view left
							Offset3DView(view3D, new Vector2(-offsetDist, 0), arrowKeyOpperation);
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Right:
							Offset3DView(view3D, new Vector2(offsetDist, 0), arrowKeyOpperation);
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Up:
							if (view3D.Printer != null
								&& printerTabPage != null
								&& view3D.Printer.ViewState.ViewMode != PartViewMode.Model)
							{
								printerTabPage.LayerScrollbar.Value += 1;
							}
							else
							{
								Offset3DView(view3D, new Vector2(0, offsetDist), arrowKeyOpperation);
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Down:
							if (view3D.Printer != null
								&& printerTabPage != null
								&& view3D.Printer.ViewState.ViewMode != PartViewMode.Model)
							{
								printerTabPage.LayerScrollbar.Value -= 1;
							}
							else
							{
								Offset3DView(view3D, new Vector2(0, -offsetDist), arrowKeyOpperation);
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;
					}
				}
			};

			// Hook SystemWindow load and spin up MatterControl once we've hit first draw
			systemWindow.Load += (s, e) =>
			{
				ReportStartupProgress(0.02, "First draw->RunOnIdle");

				//UiThread.RunOnIdle(() =>
				Task.Run(async () =>
				{
					try
					{
						ReportStartupProgress(0.15, "MatterControlApplication.Initialize");
						var mainView = await Initialize(systemWindow, (progress0To1, status) =>
						{
							ReportStartupProgress(0.2 + progress0To1 * 0.7, status);
						});

						TranslationMap.ActiveTranslationMap = new TranslationMap("Translations", UserSettings.Instance.Language);

						ReportStartupProgress(0.9, "AddChild->MainView");
						systemWindow.AddChild(mainView, 0);

						ReportStartupProgress(1, "");
						systemWindow.BackgroundColor = Color.Transparent;
						overlay.Close();
					}
					catch (Exception ex)
					{
						UiThread.RunOnIdle(() =>
						{
							statusText.Visible = false;

							var errorTextColor = Color.White;

							progressPanel.Margin = 0;
							progressPanel.VAnchor = VAnchor.Center | VAnchor.Fit;
							progressPanel.BackgroundColor = Color.DarkGray;
							progressPanel.Padding = 20;
							progressPanel.Border = 1;
							progressPanel.BorderColor = Color.Red;

							var theme = new ThemeConfig();

							progressPanel.AddChild(
								new TextWidget("Startup Failure".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: errorTextColor));

							progressPanel.AddChild(
								new TextWidget(ex.Message, pointSize: theme.FontSize9, textColor: errorTextColor));

							var closeButton = new TextButton("Close", theme)
							{
								BackgroundColor = theme.SlightShade,
								HAnchor = HAnchor.Right,
								VAnchor = VAnchor.Absolute
							};
							closeButton.Click += (s1, e1) =>
							{
								systemWindow.Close();
							};

							spinner.SpinLogo = false;
							progressBar.Visible = false;

							progressPanel.AddChild(closeButton);
						});
					}

					AppContext.IsLoading = false;
				});
			};

			ReportStartupProgress(0, "ShowAsSystemWindow");

			return systemWindow;
		}

		private static void Offset3DView(View3DWidget view3D, Vector2 offset, TrackBallTransformType opperation)
		{
			var center = view3D.TrackballTumbleWidget.LocalBounds.Center;

			view3D.TrackballTumbleWidget.TrackBallController.OnMouseDown(center, Matrix4X4.Identity, opperation);
			view3D.TrackballTumbleWidget.TrackBallController.OnMouseMove(center + offset);
			view3D.TrackballTumbleWidget.TrackBallController.OnMouseUp();
			view3D.TrackballTumbleWidget.Invalidate();
		}

		public static async Task<GuiWidget> Initialize(SystemWindow systemWindow, Action<double, string> reporter)
		{
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(PlatformFeaturesProvider);

			reporter?.Invoke(0.01, "PlatformInit");
			AppContext.Platform.PlatformInit((status) =>
			{
				reporter?.Invoke(0.01, status);
			});

			// TODO: Appears to be unused and should be removed
			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			reporter?.Invoke(0.05, "ApplicationController");
			var na = ApplicationController.Instance;

			// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
			reporter?.Invoke(0.2, "ProfileManager");
			bool na2 = ProfileManager.Instance.IsGuestProfile;

			await ProfileManager.Instance.Initialize();

			reporter?.Invoke(0.3, "MainView");
			ApplicationController.Instance.MainView = new WidescreenPanel();

			// now that we are all set up lets load our plugins and allow them their chance to set things up
			reporter?.Invoke(0.8, "Plugins");
			AppContext.Platform.FindAndInstantiatePlugins(systemWindow);

			reporter?.Invoke(0.9, "Process Commandline");
			AppContext.Platform.ProcessCommandline();

			reporter?.Invoke(0.91, "OnLoadActions");
			ApplicationController.Instance.OnLoadActions();

			UiThread.SetInterval(() =>
			{
				ApplicationController.Instance.ActivePrinter.Connection.OnIdle();
			}, .1);

			return ApplicationController.Instance.MainView;
		}

		private static void ReportStartupProgress(double progress0To1, string section)
		{
			UiThread.RunOnIdle(() =>
			{
				statusText.Text = section;
				progressBar.RatioComplete = progress0To1;
				progressPanel.Invalidate();

				Console.WriteLine($"Time to '{lastSection}': {timer.ElapsedMilliseconds}");
				timer.Restart();

				lastSection = section;
			});
		}
	}
}