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
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
	using System.ComponentModel;
	using System.IO.Compression;
	using System.Net;
	using System.Net.Http;
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
	using MatterHackers.PolygonMesh.Processors;
	using MatterHackers.RenderOpenGl;
	using MatterHackers.SerialPortCommunication;
	using MatterHackers.VectorMath;
	using MatterHackers.VectorMath.TrackBall;
	using Newtonsoft.Json.Converters;
	using Newtonsoft.Json.Serialization;
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

	public class OpenPrintersChangedEventArgs : EventArgs
	{
		public OpenPrintersChangedEventArgs(PrinterConfig printer, OperationType operation)
		{
			this.Printer = printer;
			this.Operation = operation;
		}

		public PrinterConfig Printer { get; }
		public OperationType Operation { get; }

		public enum OperationType { Add, Remove }
	}

	public static class AppContext
	{
		/// <summary>
		/// Native platform features
		/// </summary>
		public static INativePlatformFeatures Platform { get; set; }

		public static MatterControlOptions Options { get; set; } = new MatterControlOptions();

		public static bool IsLoading { get; internal set; } = true;

		/// <summary>
		/// The root SystemWindow
		/// </summary>
		public static SystemWindow RootSystemWindow { get; internal set; }

		public static ThemeConfig Theme => themeset.Theme;

		public static ThemeConfig MenuTheme => themeset.MenuTheme;

		private static ThemeSet themeset;

		public static ThemeSet ThemeSet => themeset;

		public static Dictionary<string, IColorTheme> ThemeProviders { get; }

		private static Dictionary<string, string> themes = new Dictionary<string, string>();

		static AppContext()
		{
			ThemeProviders = new Dictionary<string, IColorTheme>();

			string themesPath = Path.Combine("Themes", "System");

			var staticData = AggContext.StaticData;

			// Load available themes from StaticData
			if (staticData.DirectoryExists(themesPath))
			{
				var themeFiles = staticData.GetDirectories(themesPath).SelectMany(d => staticData.GetFiles(d).Where(p => Path.GetExtension(p) == ".json"));
				foreach(var themeFile in themeFiles)
				{
					themes[Path.GetFileNameWithoutExtension(themeFile)] = themeFile;
				}

				foreach (var directoryTheme in AggContext.StaticData.GetDirectories(themesPath).Where(d => Path.GetFileName(d) != "Menus").Select(d => new DirectoryTheme(d)))
				{
					ThemeProviders.Add(directoryTheme.Name, directoryTheme);
				}
			}

			// Load theme
			try
			{
				if (File.Exists(ProfileManager.Instance.ProfileThemeSetPath))
				{
					themeset = JsonConvert.DeserializeObject<ThemeSet>(File.ReadAllText(ProfileManager.Instance.ProfileThemeSetPath));

					// If the serialized format is older than the current format, null and fall back to latest default below
					if (themeset.SchemeVersion != ThemeSet.LatestSchemeVersion)
					{
						themeset = null;
					}
				}
			}
			catch { }

			if (themeset == null)
			{
				var themeProvider = ThemeProviders["Modern"];
				themeset = themeProvider.GetTheme("Modern-Dark");
			}

			DefaultThumbView.ThumbColor = new Color(themeset.Theme.TextColor, 30);
		}

		public static ThemeConfig LoadTheme(string themeName)
		{
			try
			{
				if (themes.TryGetValue(themeName, out string themePath))
				{
					string json = AggContext.StaticData.ReadAllText(themePath);

					return JsonConvert.DeserializeObject<ThemeConfig>(json);
				}
			}
			catch
			{
				Console.WriteLine("Error loading theme: " + themeName);
			}

			return new ThemeConfig();
		}

		public static void SetThemeAccentColor(Color accentColor)
		{
			themeset.SetAccentColor(accentColor);
			AppContext.SetTheme(themeset);
		}

		public static void SetTheme(ThemeSet themeSet)
		{
			themeset = themeSet;

			File.WriteAllText(
				ProfileManager.Instance.ProfileThemeSetPath,
				JsonConvert.SerializeObject(
					themeset,
					Formatting.Indented,
					new JsonSerializerSettings
					{
						ContractResolver = new ThemeContractResolver()
					}));

			UiThread.RunOnIdle(() =>
			{
				UserSettings.Instance.set(UserSettingsKey.ActiveThemeName, themeset.Name);

				// Explicitly fire ReloadAll in response to user interaction
				ApplicationController.Instance.ReloadAll();
			});
		}

		public class MatterControlOptions
		{
			public bool McwsTestEnvironment { get; set; }
		}
	}

	public class ApplicationController
	{
		public event EventHandler<string> ApplicationError;

		public HelpArticle HelpArticles { get; set; }

		public ThemeConfig Theme => AppContext.Theme;

		public ThemeConfig MenuTheme => AppContext.MenuTheme;

		public RunningTasksConfig Tasks { get; set; } = new RunningTasksConfig();

		public IReadOnlyList<PrinterConfig> ActivePrinters => _activePrinters;

		// A list of printers which are open (i.e. displaying a tab) on this instance of MatterControl
		private List<PrinterConfig> _activePrinters = new List<PrinterConfig>();

		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;

		public PopupMenu GetActionMenuForSceneItem(IObject3D selectedItem, InteractiveScene scene, bool addInSubmenu)
		{
			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			var menuItem = popupMenu.CreateMenuItem("Rename".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show(
					new InputBoxPage(
						"Rename Item".Localize(),
						"Name".Localize(),
						selectedItem.Name,
						"Enter New Name Here".Localize(),
						"Rename".Localize(),
						(newName) =>
						{
							selectedItem.Name = newName;

							// TODO: Revise SelectedObjectPanel to sync name on model change
							// editorSectionWidget.Text = newName;
						}));
			};

			popupMenu.CreateSeparator();

			var selectedItemType = selectedItem.GetType();

			var menuTheme = ApplicationController.Instance.MenuTheme;

			if (addInSubmenu)
			{
				popupMenu.CreateSubMenu("Modify".Localize(), ApplicationController.Instance.MenuTheme, (modifyMenu) =>
				{
					foreach (var nodeOperation in ApplicationController.Instance.Graph.Operations)
					{
						foreach (var type in nodeOperation.MappedTypes)
						{
							if (type.IsAssignableFrom(selectedItemType)
								&& (nodeOperation.IsVisible?.Invoke(selectedItem) != false)
								&& nodeOperation.IsEnabled?.Invoke(selectedItem) != false)
							{
								var subMenuItem = modifyMenu.CreateMenuItem(nodeOperation.Title, nodeOperation.IconCollector?.Invoke(menuTheme));
								subMenuItem.Click += (s2, e2) =>
								{
									nodeOperation.Operation(selectedItem, scene).ConfigureAwait(false);
								};
							}
						}
					}
				});
			}
			else
			{
				foreach (var nodeOperation in ApplicationController.Instance.Graph.Operations)
				{
					foreach (var type in nodeOperation.MappedTypes)
					{
						if (type.IsAssignableFrom(selectedItemType)
							&& (nodeOperation.IsVisible?.Invoke(selectedItem) != false)
							&& nodeOperation.IsEnabled?.Invoke(selectedItem) != false)
						{
							menuItem = popupMenu.CreateMenuItem(nodeOperation.Title, nodeOperation.IconCollector?.Invoke(menuTheme));
							menuItem.Click += (s2, e2) =>
							{
								nodeOperation.Operation(selectedItem, scene).ConfigureAwait(false);
							};
						}
					}
				}
			}

			return popupMenu;
		}

		internal void ExportAsMatterControlConfig(PrinterConfig printer)
		{
			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings")
				{
					FileName = printer.Settings.GetValue(SettingsKey.printer_name)
				},
				(saveParams) =>
				{
					try
					{
						if (!string.IsNullOrWhiteSpace(saveParams.FileName))
						{
							File.WriteAllText(saveParams.FileName, JsonConvert.SerializeObject(printer.Settings, Formatting.Indented));
						}
					}
					catch (Exception e)
					{
						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(e.Message, "Couldn't save file".Localize());
						});
					}
				});
		}

		public void LogError(string errorMessage)
		{
			this.ApplicationError?.Invoke(this, errorMessage);
		}

		// TODO: Any references to this property almost certainly need to be reconsidered. ActiveSliceSettings static references that assume a single printer
		// selection are being redirected here. This allows us to break the dependency to the original statics and consolidates
		// us down to a single point where code is making assumptions about the presence of a printer, printer counts, etc. If we previously checked for
		// PrinterConnection.IsPrinterConnected, that could should be updated to iterate ActiverPrinters, checking each one and acting on each as it would
		// have for the single case
		[Obsolete("ActivePrinter references should be migrated to logic than supports multi-printer mode")]
		public PrinterConfig ActivePrinter => this.ActivePrinters.FirstOrDefault() ?? PrinterConfig.EmptyPrinter;

		public Action RedeemDesignCode;
		public Action EnterShareCode;

		private static ApplicationController globalInstance;

		public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();
		public RootedObjectEventHandler ActiveProfileModified = new RootedObjectEventHandler();

		public event EventHandler<OpenPrintersChangedEventArgs> OpenPrintersChanged;

		public static Action WebRequestFailed;
		public static Action WebRequestSucceeded;

		public static Action<DialogWindow> ChangeToPrintNotification = null;

#if DEBUG
		public const string EnvironmentName = "TestEnv_";
#else
		public const string EnvironmentName = "";
#endif

		public bool ApplicationExiting { get; internal set; } = false;

		public static Func<string, Task<Dictionary<string, string>>> GetProfileHistory;

		public void OnOpenPrintersChanged(OpenPrintersChangedEventArgs e)
		{
			this.OpenPrintersChanged?.Invoke(this, e);
		}

		public string GetFavIconUrl(string oemName)
		{
			OemSettings.Instance.OemUrls.TryGetValue(oemName, out string oemUrl);
			return "https://www.google.com/s2/favicons?domain=" + (string.IsNullOrWhiteSpace(oemUrl) ? "www.matterhackers.com" : oemUrl);
		}

		public void ClosePrinter(PrinterConfig printer, bool allowChangedEvent = true)
		{
			// Actually clear printer
			ProfileManager.Instance.ClosePrinter(printer.Settings.ID);

			_activePrinters.Remove(printer);

			if (allowChangedEvent)
			{
				this.OnOpenPrintersChanged(new OpenPrintersChangedEventArgs(printer, OpenPrintersChangedEventArgs.OperationType.Remove));
			}

			printer.Dispose();
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

		internal void MakeGrayscale(ImageBuffer sourceImage)
		{
			var buffer = sourceImage.GetBuffer();
			int destIndex = 0;
			for (int y = 0; y < sourceImage.Height; y++)
			{
				for (int x = 0; x < sourceImage.Width; x++)
				{
					int b = buffer[destIndex + 0];
					int g = buffer[destIndex + 1];
					int r = buffer[destIndex + 2];

					int c = (r * 77) + (g * 151) + (b * 28);
					byte gray = (byte)(c >> 8);

					buffer[destIndex + 0] = gray;
					buffer[destIndex + 1] = gray;
					buffer[destIndex + 2] = gray;

					destIndex += 4;
				}
			}
		}

		// Plugin Registration Points

		// Returns the user printer profile from the webservices plugin
		public static Func<PrinterInfo, string, Task<PrinterSettings>> GetPrinterProfileAsync;

		// Executes the user printer profile sync logic in the webservices plugin
		public static Func<string, IProgress<ProgressStatus>, Task> SyncPrinterProfiles;

		// Returns all public printer profiles from the webservices plugin
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;

		// Returns the public printer profile from the webservices plugin
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		// Indicates if guest, rather than an authenticated user, is active
		public static Func<bool> GuestUserActive { get; set; }

		// Returns the authentication dialog from the authentication plugin
		public static Func<AuthenticationContext, DialogPage> GetAuthPage;

		public SlicePresetsPage EditMaterialPresetsPage { get; set; }

		public SlicePresetsPage EditQualityPresetsWindow { get; set; }

		public MainViewWidget MainView;

		private EventHandler unregisterEvents;

		private Dictionary<string, List<LibraryAction>> registeredLibraryActions = new Dictionary<string, List<LibraryAction>>();

		private List<SceneSelectionOperation> registeredSceneOperations;

		public ThumbnailsConfig Thumbnails { get; }

		private void RebuildSceneOperations(ThemeConfig theme)
		{
			registeredSceneOperations = new List<SceneSelectionOperation>()
			{
				new SceneSelectionOperation()
				{
					OperationType = typeof(GroupObject3D),

					TitleResolver = () => "Group".Localize(),
					Action = (sceneContext) =>
					{
						var scene = sceneContext.Scene;
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
					IsEnabled = (scene) => scene.SelectedItem != null
						&& scene.SelectedItem is SelectionGroupObject3D
						&& scene.SelectedItem.Children.Count > 1,
					Icon = AggContext.StaticData.LoadIcon("group.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Ungroup".Localize(),
					Action = (sceneContext) => sceneContext.Scene.UngroupSelection(),
					IsEnabled = (scene) =>
					{
						var selectedItem = scene.SelectedItem;
						if(selectedItem != null)
						{
							return selectedItem is GroupObject3D
								|| selectedItem.GetType() == typeof(Object3D);
						}

						return false;
					},
					Icon = AggContext.StaticData.LoadIcon("ungroup.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Duplicate".Localize(),
					Action = (sceneContext) => sceneContext.DuplicateItem(5),
					IsEnabled = (scene) => scene.SelectedItem != null,
					Icon = AggContext.StaticData.LoadIcon("duplicate.png").SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Remove".Localize(),
					Action = (sceneContext) => sceneContext.Scene.DeleteSelection(),
					IsEnabled = (scene) => scene.SelectedItem != null,
					Icon = AggContext.StaticData.LoadIcon("remove.png").SetPreMultiply(),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(AlignObject3D),
					TitleResolver = () => "Align".Localize(),
					Action = (sceneContext) =>
					{
						var scene = sceneContext.Scene;
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
					Action = (sceneContext) =>
					{
						var scene = sceneContext.Scene;
						var selectedItem = scene.SelectedItem;
						if (selectedItem != null)
						{
							scene.MakeLowestFaceFlat(selectedItem);
						}
					},
					IsEnabled = (scene) => scene.SelectedItem != null,
					Icon = AggContext.StaticData.LoadIcon("lay_flat.png", 16, 16).SetPreMultiply(),
				},
				new SceneSelectionOperation()
				{
					TitleResolver = () => "Make Support".Localize(),
					Action = (sceneContext) =>
					{
						var scene = sceneContext.Scene;
						if (scene.SelectedItem != null
							&& !scene.SelectedItem.VisibleMeshes().All(i => i.OutputType == PrintOutputTypes.Support))
						{
							scene.UndoBuffer.AddAndDo(new MakeSupport(scene.SelectedItem));
						}
					},
					Icon = AggContext.StaticData.LoadIcon("support.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem != null,
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(CombineObject3D),
					TitleResolver = () => "Combine".Localize(),
					Action = (sceneContext) => new CombineObject3D().WrapSelectedItemAndSelect(sceneContext.Scene),
					Icon = AggContext.StaticData.LoadIcon("combine.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(SubtractObject3D),
					TitleResolver = () => "Subtract".Localize(),
					Action = (sceneContext) => new SubtractObject3D().WrapSelectedItemAndSelect(sceneContext.Scene),
					Icon = AggContext.StaticData.LoadIcon("subtract.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(IntersectionObject3D),
					TitleResolver = () => "Intersect".Localize(),
					Action = (sceneContext) => new IntersectionObject3D().WrapSelectedItemAndSelect(sceneContext.Scene),
					Icon = AggContext.StaticData.LoadIcon("intersect.png"),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(SubtractAndReplaceObject3D),
					TitleResolver = () => "Subtract & Replace".Localize(),
					Action = (sceneContext) => new SubtractAndReplaceObject3D().WrapSelectedItemAndSelect(sceneContext.Scene),
					Icon = AggContext.StaticData.LoadIcon("subtract_and_replace.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem is SelectionGroupObject3D,
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayLinearObject3D),
					TitleResolver = () => "Linear Array".Localize(),
					Action = (sceneContext) =>
					{
						var array = new ArrayLinearObject3D();
						array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_linear.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem != null && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayRadialObject3D),
					TitleResolver = () => "Radial Array".Localize(),
					Action = (sceneContext) =>
					{
						var array = new ArrayRadialObject3D();
						array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_radial.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem != null && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(ArrayAdvancedObject3D),
					TitleResolver = () => "Advanced Array".Localize(),
					Action = (sceneContext) =>
					{
						var array = new ArrayAdvancedObject3D();
						array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
						array.Invalidate(new InvalidateArgs(array, InvalidateType.Properties, null));
					},
					Icon = AggContext.StaticData.LoadIcon("array_advanced.png").SetPreMultiply(),
					IsEnabled = (scene) => scene.SelectedItem != null && !(scene.SelectedItem is SelectionGroupObject3D),
				},
				new SceneSelectionSeparator(),
				new SceneSelectionOperation()
				{
					OperationType = typeof(PinchObject3D),
					TitleResolver = () => "Pinch".Localize(),
					Action = (sceneContext) =>
					{
						var pinch = new PinchObject3D();
						pinch.WrapSelectedItemAndSelect(sceneContext.Scene);
					},
					Icon = AggContext.StaticData.LoadIcon("pinch.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.SelectedItem != null,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(CurveObject3D),
					TitleResolver = () => "Curve".Localize(),
					Action = (sceneContext) =>
					{
						var curve = new CurveObject3D();
						curve.WrapSelectedItemAndSelect(sceneContext.Scene);
					},
					Icon = AggContext.StaticData.LoadIcon("curve.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.SelectedItem != null,
				},
				new SceneSelectionOperation()
				{
					OperationType = typeof(FitToBoundsObject3D_2),
					TitleResolver = () => "Fit to Bounds".Localize(),
					Action = (sceneContext) =>
					{
						var scene = sceneContext.Scene;
						var selectedItem = scene.SelectedItem;
						scene.SelectedItem = null;
						var fit = FitToBoundsObject3D_2.Create(selectedItem.Clone());
						fit.MakeNameNonColliding();

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { selectedItem }, new List<IObject3D> { fit }));
						scene.SelectedItem = fit;
					},
					Icon = AggContext.StaticData.LoadIcon("fit.png", 16, 16, theme.InvertIcons),
					IsEnabled = (scene) => scene.SelectedItem != null && !(scene.SelectedItem is SelectionGroupObject3D),
				},
			};

			var operationIconsByType = new Dictionary<Type, Func<ImageBuffer>>();

			foreach (var operation in registeredSceneOperations)
			{
				if (operation.OperationType != null)
				{
					operationIconsByType.Add(operation.OperationType, () => operation.Icon);
				}
			}

			// TODO: Use custom selection group icon if reusing group icon seems incorrect
			//
			// Explicitly register SelectionGroup icon
			if (operationIconsByType.TryGetValue(typeof(GroupObject3D), out Func<ImageBuffer> groupIconSource))
			{
				operationIconsByType.Add(typeof(SelectionGroupObject3D), groupIconSource);
			}

			this.Thumbnails.OperationIcons = operationIconsByType;

			operationIconsByType.Add(typeof(ImageObject3D), () => AggContext.StaticData.LoadIcon("140.png", 16, 16, theme.InvertIcons));
		}

		public void OpenIntoNewTab(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			this.MainView.CreatePartTab().ContinueWith(task =>
			{
				var workspace = this.Workspaces.Last();
				workspace.SceneContext.AddToPlate(selectedLibraryItems);
			});
		}

		internal void BlinkTab(ITab tab)
		{
			var theme = this.Theme;
			if (tab is GuiWidget guiWidget)
			{
				guiWidget.Descendants<TextWidget>().FirstOrDefault().FlashBackground(theme.PrimaryAccentColor.WithContrast(theme.TextColor, 6).ToColor());
			}
		}

		public void ShowApplicationHelp()
		{
			UiThread.RunOnIdle(() =>
			{
				DialogWindow.Show(new HelpPage("AllGuides"));
			});
		}

		public void ShowAboutPage()
		{
			UiThread.RunOnIdle(() =>
			{
				DialogWindow.Show<AboutPage>();
			});
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
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "download_20x20.png")),
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "download_folder.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DownloadsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true
						}));
			}

			this.Library.LibraryCollectionContainer = new LibraryCollectionContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Library".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "library_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "library_folder.png")),
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

			this.Library.PlatingHistory = new PlatingHistoryContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "History".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "history_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "history_folder.png")),
					() => new RootHistoryContainer()));
		}

		public void ExportLibraryItems(IEnumerable<ILibraryItem> libraryItems, bool centerOnBed = true, PrinterConfig printer = null)
		{
			UiThread.RunOnIdle(() =>
			{
				if (printer != null || this.ActivePrinters.Count == 1)
				{
					// If unspecified but count is one, select the one active printer
					if (printer == null)
					{
						printer = this.ActivePrinters.First();
					}

					DialogWindow.Show(
						new ExportPrintItemPage(libraryItems, centerOnBed, printer));
				}
				else
				{
					// Resolve printer context before showing export page
					DialogWindow dialogWindow = null;

					dialogWindow = DialogWindow.Show(
						new SelectPrinterProfilePage(
							"Next".Localize(),
							(selectedPrinter) =>
							{
								var historyContainer = ApplicationController.Instance.Library.PlatingHistory;

								selectedPrinter.Bed.LoadEmptyContent(
									new EditContext()
									{
										ContentStore = historyContainer,
										SourceItem = historyContainer.NewPlatingItem()
									});

								dialogWindow.ChangeToPage(
									new ExportPrintItemPage(libraryItems, centerOnBed, selectedPrinter));
							}));
				}
			});
		}

		public static IObject3D SelectionAsSingleClone(IObject3D selection)
		{
			IEnumerable<IObject3D> items = new[] { selection };

			// If SelectionGroup, operate on Children instead
			if (selection is SelectionGroupObject3D)
			{
				items = selection.Children;

				var group = new GroupObject3D();

				group.Children.Modify(children =>
				{
					children.AddRange(items.Select(o => o.Clone()));
				});

				return group;
			}

			return selection.Clone();
		}

		public ApplicationController()
		{
			this.Thumbnails = new ThumbnailsConfig();

			ProfileManager.UserChanged += (s, e) =>
			{
				_activePrinters = new List<PrinterConfig>();
			};

			this.RebuildSceneOperations(this.Theme);

			HelpArticle helpArticle = null;

			string helpPath = Path.Combine("OEMSettings", "toc.json");
			if (AggContext.StaticData.FileExists(helpPath))
			{
				try
				{
					helpArticle = JsonConvert.DeserializeObject<HelpArticle>(AggContext.StaticData.ReadAllText(helpPath));
				}
				catch { }
			}

			this.HelpArticles = helpArticle ?? new HelpArticle();

			Object3D.AssetsPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "Assets");

			using (var meshSteam = AggContext.StaticData.OpenStream(Path.Combine("Stls", "missing.stl")))
			{
				Object3D.FileMissingMesh = StlProcessing.Load(meshSteam, CancellationToken.None);
			}

			ScrollBar.DefaultMargin = new BorderDouble(right: 1);
			ScrollBar.ScrollBarWidth = 8 * GuiWidget.DeviceScale;
			ScrollBar.GrowThumbBy = 2;

			// Initialize statics
			DefaultThumbBackground.DefaultBackgroundColor = Color.Transparent;
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;

			this.Library = new LibraryConfig();
			this.Graph = new GraphConfig(this);
			this.Library.ContentProviders.Add(new[] { "stl", "obj", "amf", "mcx" }, new MeshContentProvider());
			this.Library.ContentProviders.Add("gcode", new GCodeContentProvider());
			this.Library.ContentProviders.Add(new[] { "png", "gif", "jpg", "jpeg" }, new ImageContentProvider());

			this.Graph.RegisterOperation(
				typeof(ImageObject3D),
				typeof(ImageToPathObject3D),
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
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("noun_479927.png", theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(TranslateObject3D),
				"Translate".Localize(),
				(sceneItem, scene) =>
				{
					var selectedItem = scene.SelectedItem;
					var replaceItems = (selectedItem is SelectionGroupObject3D) ? selectedItem.Children.ToList() : new List<IObject3D> { selectedItem };
					scene.SelectedItem = null;
					var selectedClone = SelectionAsSingleClone(selectedItem);
					var tranlate = TranslateObject3D.Create(selectedClone);
					tranlate.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(replaceItems, new List<IObject3D> { tranlate }));
					scene.SelectedItem = tranlate;

					return Task.CompletedTask;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon(Path.Combine("ViewTransformControls", "translate.png"), 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(RotateObject3D_2),
				"Rotate".Localize(),
				(sceneItem, scene) =>
				{
					var selectedItem = scene.SelectedItem;
					var replaceItems = (selectedItem is SelectionGroupObject3D) ? selectedItem.Children.ToList() : new List<IObject3D> { selectedItem };
					scene.SelectedItem = null;
					var selectedClone = SelectionAsSingleClone(selectedItem);
					var rotate = new RotateObject3D_2(selectedClone);
					rotate.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(replaceItems, new List<IObject3D> { rotate }));
					scene.SelectedItem = rotate;

					return Task.CompletedTask;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon(Path.Combine("ViewTransformControls", "rotate.png"), 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(ComponentObject3D),
				"Make Component".Localize(),
				(sceneItem, scene) =>
				{
					IEnumerable<IObject3D> items = new[] { sceneItem };

					// If SelectionGroup, operate on Children instead
					if (sceneItem is SelectionGroupObject3D)
					{
						items = sceneItem.Children;
					}

					// Dump selection forcing collapse of selection group
					scene.SelectedItem = null;

					var component = new ComponentObject3D
					{
						Name = "New Component",
						Finalized = false
					};

					// Copy an selected item into the component as a clone
					component.Children.Modify(children =>
					{
						children.AddRange(items.Select(o => o.Clone()));
					});

					component.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(items, new [] { component }));
					scene.SelectedItem = component;

					return Task.CompletedTask;
				},
				isVisible: (sceneItem) =>
				{
					return sceneItem.Parent != null
						&& sceneItem.Parent.Parent == null
						&&  sceneItem.DescendantsAndSelf().All(d => !(d is ComponentObject3D));
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("scale_32x32.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(ComponentObject3D),
				"Edit Component".Localize(),
				(sceneItem, scene) =>
				{
					if (sceneItem is ComponentObject3D componentObject)
					{
						// Enable editing mode
						componentObject.Finalized = false;

						// Force editor rebuild
						scene.SelectedItem = null;
						scene.SelectedItem = componentObject;
					}

					return Task.CompletedTask;
				},
				isVisible: (sceneItem) =>
				{
					return sceneItem.Parent != null
						&& sceneItem.Parent.Parent == null
						&& sceneItem is ComponentObject3D componentObject
						&& componentObject.Finalized;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("scale_32x32.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(ScaleObject3D),
				"Scale".Localize(),
				(sceneItem, scene) =>
				{
					var selectedItem = scene.SelectedItem;
					var replaceItems = (selectedItem is SelectionGroupObject3D) ? selectedItem.Children.ToList() : new List<IObject3D> { selectedItem };
					scene.SelectedItem = null;
					var selectedClone = SelectionAsSingleClone(selectedItem);
					var scale = new ScaleObject3D(selectedClone);
					scale.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(replaceItems, new List<IObject3D> { scale }));
					scene.SelectedItem = scale;

					return Task.CompletedTask;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("scale_32x32.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(MirrorObject3D),
				"Mirror".Localize(),
				(sceneItem, scene) =>
				{
					var mirror = new MirrorObject3D();
					mirror.WrapSelectedItemAndSelect(scene);

					return Task.CompletedTask;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("mirror_32x32.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IPathObject),
				typeof(LinearExtrudeObject3D),
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
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("noun_84751.png", theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IPathObject),
				typeof(SmoothPathObject3D),
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
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("noun_simplify_340976_000000.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IPathObject),
				typeof(InflatePathObject3D),
				"Inflate Path".Localize(),
				(sceneItem, scene) =>
				{
					if (sceneItem is IPathObject imageObject)
					{
						var inflatePath = new InflatePathObject3D();
						sceneItem.WrapWith(inflatePath, scene);
						inflatePath.Invalidate(new InvalidateArgs(inflatePath, InvalidateType.Properties, null));
					}

					return Task.CompletedTask;
				},
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("noun_expand_1823853_000000.png", 16, 16, theme.InvertIcons));

			this.Graph.RegisterOperation(
				typeof(IObject3D),
				typeof(BaseObject3D),
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
				iconCollector: (theme) => AggContext.StaticData.LoadIcon("noun_55060.png", theme.InvertIcons));

			this.InitializeLibrary();

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// Initialize plugins, passing the MatterControl assembly as the only non-dll instance
			//PluginFinder.Initialize(Assembly.GetExecutingAssembly());

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

		public void Connection_ErrorReported(object sender, string line)
		{
			if (line != null)
			{
				string message = "Your printer is reporting a HARDWARE ERROR and has been paused. Check the error and cancel the print if required.".Localize()
					+ "\n"
					+ "\n"
					+ "Error Reported".Localize() + ":"
					+ $" \"{line}\".";

				if (sender is PrinterConnection printerConnection)
				{
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox(
							(clickedOk) =>
							{
								if (clickedOk && printerConnection.PrinterIsPaused)
								{
									printerConnection.Resume();
								}
							},
							message,
							"Printer Hardware Error".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Resume".Localize(),
							"OK".Localize())
					);
				}
			}
		}

		public void Connection_TemporarilyHoldingTemp(object sender, EventArgs e)
		{
			if (sender is PrinterConnection printerConnection)
			{
				if (printerConnection.AnyHeatIsOn)
				{
					var paused = false;
					Tasks.Execute("", (reporter, cancellationToken) =>
					{
						var progressStatus = new ProgressStatus();

						while (printerConnection.SecondsToHoldTemperature > 0
							&& !cancellationToken.IsCancellationRequested
							&& printerConnection.ContinuHoldingTemperature)
						{
							if (paused)
							{
								progressStatus.Status = "Holding Temperature".Localize();
							}
							else
							{
								if (printerConnection.SecondsToHoldTemperature > 60)
								{
									progressStatus.Status = string.Format(
										"{0} {1:0}m {2:0}s",
										"Automatic Heater Shutdown in".Localize(),
										(int)(printerConnection.SecondsToHoldTemperature) / 60,
										(int)(printerConnection.SecondsToHoldTemperature) % 60);
								}
								else
								{
									progressStatus.Status = string.Format(
										"{0} {1:0}s",
										"Automatic Heater Shutdown in".Localize(),
										printerConnection.SecondsToHoldTemperature);
								}
							}
							progressStatus.Progress0To1 = printerConnection.SecondsToHoldTemperature / printerConnection.TimeToHoldTemperature;
							reporter.Report(progressStatus);
							Thread.Sleep(20);
						}

						return Task.CompletedTask;
					},
					taskActions: new RunningTaskOptions()
					{
						PauseAction = () => UiThread.RunOnIdle(() =>
						{
							paused = true;
							printerConnection.TimeHaveBeenHoldingTemperature.Stop();
						}),
						PauseToolTip = "Pause automatic heater shutdown".Localize(),
						ResumeAction = () => UiThread.RunOnIdle(() =>
						{
							paused = false;
							printerConnection.TimeHaveBeenHoldingTemperature.Start();
						}),
						ResumeToolTip = "Resume automatic heater shutdown".Localize(),
						StopAction = () => UiThread.RunOnIdle(() =>
						{
							printerConnection.TurnOffBedAndExtruders(TurnOff.Now);
						}),
						StopToolTip = "Immediately turn off heaters".Localize()
					});
				}
			}
		}

		public bool RunAnyRequiredPrinterSetup(PrinterConfig printer, ThemeConfig theme)
		{
			// run probe calibration first if we need to
			if (ProbeCalibrationWizard.NeedsToBeRun(printer))
			{
				UiThread.RunOnIdle(() =>
				{
					ProbeCalibrationWizard.Start(printer, theme);
				});
				return true;
			}

			// run the leveling wizard if we need to
			if (LevelingValidation.NeedsToBeRun(printer))
			{
				UiThread.RunOnIdle(() =>
				{
					PrintLevelingWizard.Start(printer, theme);
				});
				return true;
			}

			// run load filament if we need to
			if (LoadFilamentWizard.NeedsToBeRun(printer))
			{
				UiThread.RunOnIdle(() =>
				{
					LoadFilamentWizard.Start(printer, theme, false);
				});
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

		public void Shutdown()
		{
			// Ensure all threads shutdown gracefully on close

			// Release any waiting generator threads
			this.Thumbnails.Shutdown();

			// Kill all long running tasks (this will release the slicing thread if running)
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
			string scopeDirectory = Path.Combine(ApplicationDataStorage.Instance.CacheDirectory, cacheScope);

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
						MainView = new MainViewWidget(ApplicationController.Instance.Theme);
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

			ApplicationSettings.Instance.ReleaseClientToken();
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
			// Show the End User License Agreement if it has not been shown (on windows it is shown in the installer)
			if (AggContext.OperatingSystem != OSType.Windows)
			{
				// *********************************************************************************
				// TODO: This should happen much earlier in the process and we should conditionally
				//       show License page or RootSystemWindow
				// *********************************************************************************
				//
				// Make sure this window is show modal (if available)
				// show this last so it is on top
				if (UserSettings.Instance.get(UserSettingsKey.SoftwareLicenseAccepted) != "true")
				{
					UiThread.RunOnIdle(() => DialogWindow.Show<LicenseAgreementPage>());
				}
			}

			if (AssetObject3D.AssetManager == null)
			{
				AssetObject3D.AssetManager = new AssetManager();
			}
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

			if (!string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername)
				&& AuthenticationData.Instance.ActiveSessionUsername != AuthenticationData.Instance.LastSessionUsername)
			{
				AuthenticationData.Instance.LastSessionUsername = AuthenticationData.Instance.ActiveSessionUsername;
			}

			// TODO: Unclear why we'd reload on status change - it seems like this state should be managed entirely from ProfileManager and removed from this location
			ProfileManager.ReloadActiveUser();
		}

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

		public async Task<PrinterConfig> OpenPrinter(string printerID, bool loadPlateFromHistory = true)
		{
			if (!_activePrinters.Any(p => p.Settings.ID == printerID))
			{
				ProfileManager.Instance.OpenPrinter(printerID);

				var printer = new PrinterConfig(await ProfileManager.LoadSettingsAsync(printerID));

				_activePrinters.Add(printer);

				if (loadPlateFromHistory)
				{
					await printer.Bed.LoadPlateFromHistory();
				}

				this.OnOpenPrintersChanged(new OpenPrintersChangedEventArgs(printer, OpenPrintersChangedEventArgs.OperationType.Add));

				if (printer.Settings.PrinterSelected
					&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
				{
					printer.Connection.Connect();
				}

				return printer;
			}

			return PrinterConfig.EmptyPrinter;
		}

		public async Task OpenAllPrinters()
		{
			// TODO: broadcast message to UI to close all printer tabs

			foreach (var printerID in ProfileManager.Instance.OpenPrinterIDs)
			{
				await this.OpenPrinter(printerID);
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
			WebClient client = new WebClient();
			client.DownloadDataCompleted += (sender, e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					Stream stream = new MemoryStream(e.Result);

					this.LoadImageInto(imageToLoadInto, scaleToImageX, scalingBlender, stream);
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

		private HttpClient httpClient = new HttpClient();

		public async Task LoadRemoteImage(ImageBuffer imageToLoadInto, string uriToLoad, bool scaleToImageX, IRecieveBlenderByte scalingBlender = null)
		{
			try
			{
				using (var stream = await httpClient.GetStreamAsync(uriToLoad))
				{
					this.LoadImageInto(imageToLoadInto, scaleToImageX, scalingBlender, stream);
				};
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error loading image: " + uriToLoad);
				Trace.WriteLine(ex.Message);
			}

		}

		private void LoadImageInto(ImageBuffer imageToLoadInto, bool scaleToImageX, IRecieveBlenderByte scalingBlender, Stream stream)
		{
			if (scalingBlender == null)
			{
				scalingBlender = new BlenderBGRA();
			}

			ImageBuffer unScaledImage = new ImageBuffer(10, 10);
			if (scaleToImageX)
			{
				// scale the loaded image to the size of the target image
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
		/// Register the given PrintItemAction into the named section
		/// </summary>
		/// <param name="section">The section to register in</param>
		/// <param name="printItemAction">The action to register</param>
		public void RegisterLibraryAction(string section, LibraryAction printItemAction)
		{
			List<LibraryAction> items;
			if (!registeredLibraryActions.TryGetValue(section, out items))
			{
				items = new List<LibraryAction>();
				registeredLibraryActions.Add(section, items);
			}

			items.Add(printItemAction);
		}

		/// <summary>
		/// Enumerate the given section, returning all registered actions
		/// </summary>
		/// <param name="section">The section to enumerate</param>
		/// <returns></returns>
		public IEnumerable<LibraryAction> RegisteredLibraryActions(string section)
		{
			List<LibraryAction> items;
			if (registeredLibraryActions.TryGetValue(section, out items))
			{
				return items;
			}

			return Enumerable.Empty<LibraryAction>();
		}

		public IEnumerable<SceneSelectionOperation> RegisteredSceneOperations => registeredSceneOperations;

		public static IObject3D ClipboardItem { get; internal set; }
		public Action<ILibraryItem> ShareLibraryItem { get; set; }

		public List<PartWorkspace> Workspaces { get; } = new List<PartWorkspace>();

		public AppViewState ViewState { get; } = new AppViewState();
		public Uri HelpArticleSource { get; set; }
		public Dictionary<string, HelpArticle> HelpArticlesByID { get; set; }

		public string MainTabKey { get; internal set; }

		public static List<StartupAction> StartupActions { get; } = new List<StartupAction>();

		public static List<StartupTask> StartupTasks { get; } = new List<StartupTask>();
		public static Type ServicesStatusType { get; set; }

		public bool PrinterTabSelected { get; set; } = false;

		/// <summary>
		/// Indicates if any ActivePrinter is running a print task, either in paused or printing states
		/// </summary>
		public bool AnyPrintTaskRunning => this.ActivePrinters.Any(p => p.Connection.PrinterIsPrinting || p.Connection.PrinterIsPaused);

		public event EventHandler<WidgetSourceEventArgs> AddPrintersTabRightElement;

		public void NotifyPrintersTabRightElement(GuiWidget sourceExentionArea)
		{
			AddPrintersTabRightElement?.Invoke(this, new WidgetSourceEventArgs(sourceExentionArea));
		}

		private string doNotAskAgainMessage = "Don't remind me again".Localize();

		public async Task PrintPart(EditContext editContext, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken, bool overrideAllowGCode = false)
		{
			var partFilePath = editContext.SourceFilePath;
			var gcodeFilePath = editContext.GCodeFilePath(printer);
			var printItemName = editContext.SourceItem.Name;

			// Exit if called in a non-applicable state
			if (printer.Connection.CommunicationState != CommunicationStates.Connected
				&& printer.Connection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				return;
			}

			try
			{
				// If leveling is required or is currently on
				if(this.RunAnyRequiredPrinterSetup(printer, this.Theme))
				{
					// We need to calibrate. So, don't print this part.
					return;
				}

				printer.Connection.PrintingItemName = printItemName;

				if (SettingsValidation.SettingsValid(printer))
				{
					// check that current bed temp is is within 10 degrees of leveling temp
					var enabled = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
					var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);
					if (enabled || required)
					{
						double requiredLevelingTemp = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
							printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
							: 0;
						PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
						if (!levelingData.IssuedLevelingTempWarning
							&& Math.Abs(requiredLevelingTemp - levelingData.BedTemperature) > 10)
						{
							// Show a warning that leveling may be a good idea if better adhesion needed
							UiThread.RunOnIdle(() =>
							{
								StyledMessageBox.ShowMessageBox(
									@"Leveling data created with bed temperature of: {0}°C
Current bed temperature: {1}°C

If you experience adhesion problems, please re-run leveling."
									.FormatWith(levelingData.BedTemperature, requiredLevelingTemp),
									"Leveling data warning");

								levelingData.IssuedLevelingTempWarning = true;
								printer.Settings.Helpers.SetPrintLevelingData(levelingData, true);
							});
						}
					}

					// clear the output cache prior to starting a print
					printer.Connection.TerminalLog.Clear();

					string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

					if (Path.GetExtension(partFilePath).ToUpper() == ".GCODE"
						&& hideGCodeWarning == null
						&& !overrideAllowGCode)
					{
						var hideGCodeWarningCheckBox = new CheckBox(doNotAskAgainMessage)
						{
							TextColor = this.Theme.TextColor,
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
										printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
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
						printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

						(bool slicingSucceeded, string finalPath) = await this.SliceItemLoadOutput(
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
							printer.Connection.CommunicationState = CommunicationStates.Connected;
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
			LoadTranslationMap();
		}

		public static void LoadTranslationMap()
		{
			// Select either the user supplied language name or the current thread language name
			string twoLetterIsoLanguageName = string.IsNullOrEmpty(UserSettings.Instance.Language) ?
				Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower() :
				UserSettings.Instance.Language.ToLower();

			string translationFilePath = Path.Combine("Translations", twoLetterIsoLanguageName, "Translation.txt");

			if (twoLetterIsoLanguageName == "en")
			{
				TranslationMap.ActiveTranslationMap = new TranslationMap();
			}
			else
			{
				using (var stream = AggContext.StaticData.OpenStream(translationFilePath))
				using (var streamReader = new StreamReader(stream))
				{
					TranslationMap.ActiveTranslationMap = new TranslationMap(streamReader, UserSettings.Instance.Language);
				}
			}
		}

		public void MonitorPrintTask(PrinterConfig printer)
		{
			string layerDetails = (printer.Bed.LoadedGCode?.LayerCount > 0) ? $" of {printer.Bed.LoadedGCode.LayerCount}" : "";

			this.Tasks.Execute(
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
					RichProgressWidget = () => PrinterTabPage.PrintProgressWidget(printer, this.Theme),
					PauseAction = () => UiThread.RunOnIdle(() =>
					{
						printer.Connection.RequestPause();
					}),
					IsPaused = () =>
					{
						return printer.Connection.PrinterIsPaused;
					},
					PauseToolTip = "Pause Print".Localize(),
					ResumeAction = () => UiThread.RunOnIdle(() =>
					{
						printer.Connection.Resume();
					}),
					ResumeToolTip = "Resume Print".Localize(),
					StopAction = () => UiThread.RunOnIdle(() =>
					{
						printer.CancelPrint();
					}),
					StopToolTip = "Cancel Print".Localize(),
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
							zip.CreateEntryFromFile(printer.Settings.DocumentPath, printer.Settings.GetValue(SettingsKey.printer_name) + ".printer");
							zip.CreateEntryFromFile(gcodeFilePath, "sliced.gcode");
						}
					}

					if (originalIsGCode)
					{
						await printer.Connection.StartPrint(gcodeFilePath);

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
								await printer.Connection.StartPrint(gcodeFilePath);
								MonitorPrintTask(printer);
								return;
							}
						}
					}
				}

				printer.Connection.CommunicationState = CommunicationStates.Connected;
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

			await this.Tasks.Execute("Slicing".Localize(), async (reporter, cancellationToken) =>
			{
				slicingSucceeded = await Slicer.SliceItem(
					object3D,
					gcodeFilePath,
					printer,
					reporter,
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

			await this.Tasks.Execute("Loading GCode".Localize(), (innerProgress, token) =>
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

				if (printer.Bed.LoadedGCode is GCodeMemoryFile gcodeMemoryFile)
				{
					// try to validate the gcode file and warn if it seems invalid.
					// for now the definition of invalid is that it has a print time of < 30 seconds
					var estimatedPrintSeconds = gcodeMemoryFile.EstimatedPrintSeconds();
					if (estimatedPrintSeconds < 30)
					{
						var message = "The time to print this G-Code is estimated to be {0} seconds.\n\nPlease check your part for errors if this is unexpected."
							.Localize()
							.FormatWith((int)estimatedPrintSeconds);
						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(message, "Warning, very short print".Localize());
						});
					}
				}

				// Switch to the 3D layer view if on Model view and slicing succeeded
				if (printer.ViewState.ViewMode == PartViewMode.Model)
				{
					printer.ViewState.ViewMode = PartViewMode.Layers3D;
				}

				return Task.CompletedTask;
			});

			return (slicingSucceeded, gcodeFilePath);
		}

		internal void GetViewOptionButtons(GuiWidget parent, BedConfig sceneContext, PrinterConfig printer, ThemeConfig theme)
		{
			var bedButton = new RadioIconButton(AggContext.StaticData.LoadIcon("bed.png", theme.InvertIcons), theme)
			{
				Name = "Bed Button",
				ToolTipText = "Show Print Bed".Localize(),
				Checked = sceneContext.RendererOptions.RenderBed,
				Margin = theme.ButtonSpacing,
				VAnchor = VAnchor.Absolute,
				ToggleButton = true,
				Height = theme.ButtonHeight,
				Width = theme.ButtonHeight,
				SiblingRadioButtonList = new List<GuiWidget>()
			};
			bedButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBed = bedButton.Checked;
			};
			parent.AddChild(bedButton);

			Func<bool> buildHeightValid = () => sceneContext.BuildHeight > 0;

			var printAreaButton = new RadioIconButton(AggContext.StaticData.LoadIcon("print_area.png", theme.InvertIcons), theme)
			{
				Name = "Bed Button",
				ToolTipText = (buildHeightValid()) ? "Show Print Area".Localize() : "Define printer build height to enable",
				Checked = sceneContext.RendererOptions.RenderBuildVolume,
				Margin = theme.ButtonSpacing,
				VAnchor = VAnchor.Absolute,
				ToggleButton = true,
				Enabled = buildHeightValid() && printer?.ViewState.ViewMode != PartViewMode.Layers2D,
				Height = theme.ButtonHeight,
				Width = theme.ButtonHeight,
				SiblingRadioButtonList = new List<GuiWidget>()
			};
			printAreaButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBuildVolume = printAreaButton.Checked;
			};
			parent.AddChild(printAreaButton);

			this.BindBedOptions(parent, bedButton, printAreaButton, sceneContext.RendererOptions);

			if (printer != null)
			{
				// Disable print area button in GCode2D view
				EventHandler<ViewModeChangedEventArgs> viewModeChanged = (s, e) =>
				{
					// Button is conditionally created based on BuildHeight, only set enabled if created
					printAreaButton.Enabled = buildHeightValid() && printer.ViewState.ViewMode != PartViewMode.Layers2D;
				};

				printer.ViewState.ViewModeChanged += viewModeChanged;

				parent.Closed += (s, e) =>
				{
					printer.ViewState.ViewModeChanged -= viewModeChanged;
				};
			}
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

		public class StartupTask
		{
			public string Title { get; set; }
			public int Priority { get; set; }
			public Func<IProgress<ProgressStatus>, CancellationToken, Task> Action { get; set; }
		}

		public class StartupAction
		{
			public string Title { get; set; }
			public int Priority { get; set; }
			public Action Action { get; set; }
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

		/// <summary>
		/// Set this if you would like to update the stated of the pause resume button
		/// </summary>
		public Func<bool> IsPaused { get; set; }

		public Action PauseAction { get; set; }
		public Action ResumeAction { get; set; }
		public Action StopAction { get; set; }

		public string StopToolTip { get; set; } = "Cancel".Localize();
		public string ResumeToolTip { get; set; } = "Resume".Localize();
		public string PauseToolTip { get; set; } = "Pause".Localize();

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

				UiThread.RunOnIdle(() =>
				{
					executingTasks.Remove(taskDetails);
				});
			});
		}
	}

	public enum ReportSeverity2 { Warning, Error }

	public static class Application
	{
		private static ProgressBar progressBar;
		private static TextWidget statusText;
		private static FlowLayoutWidget progressPanel;
		private static string lastSection = "";
		private static Stopwatch timer;

		public static SystemWindow LoadRootWindow(int width, int height)
		{
			timer = Stopwatch.StartNew();

			var systemWindow = new RootSystemWindow(width, height);

			var overlay = new GuiWidget()
			{
				BackgroundColor = AppContext.Theme.BackgroundColor,
			};
			overlay.AnchorAll();

			systemWindow.AddChild(overlay);

			var mutedAccentColor = AppContext.Theme.SplashAccentColor;

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

			progressPanel.AddChild(statusText = new TextWidget("", textColor: AppContext.Theme.TextColor)
			{
				MinimumSize = new Vector2(200, 30),
				HAnchor = HAnchor.Center,
				AutoExpandBoundsToText = true
			});

			progressPanel.AddChild(progressBar = new ProgressBar()
			{
				FillColor = mutedAccentColor,
				BorderColor = Color.Gray, // theme.BorderColor75,
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
						DialogWindow.Show(new HelpPage("AllGuides"));
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

						case Keys.P:
							if (keyEvent.Control)
							{
								view3D.PushToPrinterAndPrint();
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
								view3D.sceneContext.Paste();
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
								view3D.sceneContext.Paste();
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

						ApplicationController.LoadTranslationMap();

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
			var loading = "Loading...";
#if DEBUG
			loading = null;
#endif
			reporter?.Invoke(0.01, (loading != null) ? loading : "PlatformInit");
			AppContext.Platform.PlatformInit((status) =>
			{
				reporter?.Invoke(0.01, (loading != null) ? loading : status);
			});

			// TODO: Appears to be unused and should be removed
			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			reporter?.Invoke(0.05, (loading != null) ? loading : "ApplicationController");
			var applicationController = ApplicationController.Instance;

			// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
			reporter?.Invoke(0.2, (loading != null) ? loading : "ProfileManager");
			bool na2 = ProfileManager.Instance.IsGuestProfile;

			await ProfileManager.Instance.Initialize();

			reporter?.Invoke(0.25, (loading != null) ? loading : "Initialize printer");

			var printer = PrinterConfig.EmptyPrinter;

			// Restore bed
			if (printer.Settings.PrinterSelected)
			{
				printer.ViewState.ViewMode = PartViewMode.Model;

				ApplicationController.StartupTasks.Add(new ApplicationController.StartupTask()
				{
					Title = "Loading Bed".Localize(),
					Priority = 100,
					Action = (progress, cancellationToken) =>
					{
						return printer.Bed.LoadPlateFromHistory();
					}
				});
			}

			reporter?.Invoke(0.3, (loading != null) ? loading : "Plugins");
			AppContext.Platform.FindAndInstantiatePlugins(systemWindow);

			reporter?.Invoke(0.4, (loading != null) ? loading : "MainView");
			applicationController.MainView = new MainViewWidget(applicationController.Theme);

			reporter?.Invoke(0.91, (loading != null) ? loading : "OnLoadActions");
			applicationController.OnLoadActions();

			// Wired up to MainView.Load with the intent to fire startup actions and tasks in order with reporting
			async void initialWindowLoad(object s, EventArgs e)
			{
				try
				{
					// Batch startup actions
					await applicationController.Tasks.Execute(
						"Finishing Startup".Localize(),
						(progress, cancellationToken) =>
						{
							var status = new ProgressStatus();

							int itemCount = ApplicationController.StartupActions.Count;

							double i = 1;

							foreach (var action in ApplicationController.StartupActions.OrderByDescending(t => t.Priority))
							{
								status.Status = action.Title;
								progress.Report(status);

								action.Action?.Invoke();
								status.Progress0To1 = i++ / itemCount;
								progress.Report(status);
							}

							return Task.CompletedTask;
						});

					await applicationController.Tasks.Execute(
						"Restoring Printers".Localize(),
						async (progress, cancellationToken) =>
						{
							await applicationController.OpenAllPrinters();
						});

					// Batch startup tasks
					foreach (var task in ApplicationController.StartupTasks.OrderByDescending(t => t.Priority))
					{
						await applicationController.Tasks.Execute(task.Title, task.Action);
					}
				}
				catch
				{
				}

				// Unhook after execution
				applicationController.MainView.Load -= initialWindowLoad;
			}

			// Hook after first draw
			applicationController.MainView.Load += initialWindowLoad;

			return applicationController.MainView;
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

	public class PartWorkspace
	{
		public string Name { get; set; }
		public BedConfig SceneContext { get; set; }
	}
}