/*
Copyright (c) 2016, Lars Brubaker, Kevin Pope
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PluginSystem;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Mindscape.Raygun4Net;

namespace MatterHackers.MatterControl
{
	public class MatterControlApplication : SystemWindow
	{
#if DEBUG

		//public static string MCWSBaseUri { get; } = "http://192.168.2.129:9206";
		public static string MCWSBaseUri { get; } = "https://mattercontrol-test.appspot.com";

#else
		public static string MCWSBaseUri { get; } = "https://mattercontrol.appspot.com";
#endif

		public static bool CameraInUseByExternalProcess { get; set; } = false;
		public bool RestartOnClose = false;
		private static MatterControlApplication instance;
		private string[] commandLineArgs = null;
		private bool DoCGCollectEveryDraw = false;
		private int drawCount = 0;
		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		private bool ShowMemoryUsed = false;

		public void ConfigureWifi()
		{
		}

		private Stopwatch totalDrawTime = new Stopwatch();

		public static bool IsLoading { get; private set; } = true;

		public static void RequestPowerShutDown()
		{
			// does nothing on windows
		}

		static MatterControlApplication()
		{
			if (AggContext.OperatingSystem == OSType.Mac && AggContext.StaticData == null)
			{
				// Set working directory - this duplicates functionality in Main but is necessary on OSX as Main fires much later (after the constructor in this case)
				// resulting in invalid paths due to path tests running before the working directory has been overridden. Setting the value before initializing StaticData
				// works around this architectural difference.
				Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
			}

			// Because fields on this class call localization methods and because those methods depend on the StaticData provider and because the field
			// initializers run before the class constructor, we need to init the platform specific provider in the static constructor (or write a custom initializer method)
			//
			// Initialize a standard file system backed StaticData provider
			if (AggContext.StaticData == null) // it may already be initialized by tests
			{
				AggContext.StaticData = new MatterHackers.Agg.FileSystemStaticData();
			}

#if DEBUG
			WinformsSystemWindow.InspectorCreator = (systemWindow) =>
			{
				if (systemWindow == Instance)
				{
					// If systemWindow is MatterControlApplication, include Scene
					var partContext = ApplicationController.Instance.DragDropData;
					return new InspectForm(systemWindow, partContext.SceneContext.Scene, partContext.View3DWidget);
				}
				else
				{
					// Otherwise, exclude Scene
					return new InspectForm(systemWindow);
				}
			};
#endif
		}

		private MatterControlApplication(double width, double height)
			: base(width, height)
		{
			ApplicationSettings.Instance.set("HardwareHasCamera", "false");

			Name = "MatterControl";

			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			this.commandLineArgs = Environment.GetCommandLineArgs();
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			for (int currentCommandIndex = 0; currentCommandIndex < commandLineArgs.Length; currentCommandIndex++)
			{
				string command = commandLineArgs[currentCommandIndex];
				string commandUpper = command.ToUpper();
				switch (commandUpper)
				{
					case "FORCE_SOFTWARE_RENDERING":
						GL.HardwareAvailable = false;
						break;

					case "CLEAR_CACHE":
						AboutWidget.DeleteCacheData(0);
						break;

					case "SHOW_MEMORY":
						ShowMemoryUsed = true;
						break;

					case "DO_GC_COLLECT_EVERY_DRAW":
						ShowMemoryUsed = true;
						DoCGCollectEveryDraw = true;
						break;

					//case "CREATE_AND_SELECT_PRINTER":
					//	if (currentCommandIndex + 1 <= commandLineArgs.Length)
					//	{
					//		currentCommandIndex++;
					//		string argument = commandLineArgs[currentCommandIndex];
					//		string[] printerData = argument.Split(',');
					//		if (printerData.Length >= 2)
					//		{
					//			Printer ActivePrinter = new Printer();

					//			ActivePrinter.Name = "Auto: {0} {1}".FormatWith(printerData[0], printerData[1]);
					//			ActivePrinter.Make = printerData[0];
					//			ActivePrinter.Model = printerData[1];

					//			if (printerData.Length == 3)
					//			{
					//				ActivePrinter.ComPort = printerData[2];
					//			}

					//			PrinterSetupStatus test = new PrinterSetupStatus(ActivePrinter);
					//			test.LoadSettingsFromConfigFile(ActivePrinter.Make, ActivePrinter.Model);
					//			ActiveSliceSettings.Instance = ActivePrinter;
					//		}
					//	}

					//	break;
				}

				if (MeshFileIo.ValidFileExtensions().Contains(Path.GetExtension(command).ToUpper()))
				{
					// If we are the only instance running then do nothing.
					// Else send these to the running instance so it can load them.
				}
			}

			//WriteTestGCodeFile();
#if !DEBUG
			if (File.Exists("RunUnitTests.txt"))
#endif
			{
#if IS_WINDOWS_FORMS
				if (!Clipboard.IsInitialized)
				{
					Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
				}
#endif

				// you can turn this on to debug some bounds issues
				//GuiWidget.DebugBoundsUnderMouse = true;
			}

			GuiWidget.DefaultEnforceIntegerBounds = true;

			if (UserSettings.Instance.IsTouchScreen)
			{
				GuiWidget.DeviceScale = 1.3;
				SystemWindow.ShareSingleOsWindow = true;
			}
			string textSizeMode = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);
			if (!string.IsNullOrEmpty(textSizeMode))
			{
				double textSize = 1.0;
				if(double.TryParse(textSizeMode, out textSize))
				{
					GuiWidget.DeviceScale = textSize;
				}
			}

			//GuiWidget.DeviceScale = 2;

			using (new PerformanceTimer("Startup", "MainView"))
			{
				this.AddChild(ApplicationController.Instance.MainView);
			}
			this.MinimumSize = minSize;
			this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off

#if false // this is to test freeing gcodefile memory
			Button test = new Button("test");
			test.Click += (sender, e) =>
			{
				//MatterHackers.GCodeVisualizer.GCodeFile gcode = new GCodeVisualizer.GCodeFile();
				//gcode.Load(@"C:\Users\lbrubaker\Downloads\drive assy.gcode");
				SystemWindow window = new SystemWindow(100, 100);
				window.ShowAsSystemWindow();
			};
			allControls.AddChild(test);
#endif
			this.AnchorAll();

			if (GL.HardwareAvailable)
			{
				UseOpenGL = true;
			}
			string version = "2.0";

			Title = "MatterHackers: MatterControl {0}".FormatWith(version);
			if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
			{
				Title = Title + " - {1}".FormatWith(version, OemSettings.Instance.WindowTitleExtra);
			}

			UiThread.RunOnIdle(CheckOnPrinter);

			string desktopPosition = ApplicationSettings.Instance.get(ApplicationSettingsKey.DesktopPosition);
			if (!string.IsNullOrEmpty(desktopPosition))
			{
				string[] sizes = desktopPosition.Split(',');

				//If the desktop position is less than -10,-10, override
				int xpos = Math.Max(int.Parse(sizes[0]), -10);
				int ypos = Math.Max(int.Parse(sizes[1]), -10);

				this.InitialDesktopPosition = new Point2D(xpos, ypos);
			}
			else
			{
				this.InitialDesktopPosition = new Point2D(-1, -1);
			}

			this.Maximized = ApplicationSettings.Instance.get(ApplicationSettingsKey.MainWindowMaximized) == "true";
		}

		public void TakePhoto(string imageFileName)
		{
			ImageBuffer noCameraImage = new ImageBuffer(640, 480);
			Graphics2D graphics = noCameraImage.NewGraphics2D();
			graphics.Clear(Color.White);
			graphics.DrawString("No Camera Detected", 320, 240, pointSize: 24, justification: Agg.Font.Justification.Center);
			graphics.DrawString(DateTime.Now.ToString(), 320, 200, pointSize: 12, justification: Agg.Font.Justification.Center);
			AggContext.ImageIO.SaveImageData(imageFileName, noCameraImage);

			PictureTaken?.Invoke(null, null);
		}

		private bool dropWasOnChild = true;

		private EventHandler unregisterEvent;

		public static MatterControlApplication Instance
		{
			get
			{
				if (instance == null)
				{
					instance = CreateInstance();
					instance.ShowAsSystemWindow();
				}

				return instance;
			}
		}

		public event EventHandler PictureTaken;

		private static Vector2 minSize { get; set; } = new Vector2(600, 600);

		public static MatterControlApplication CreateInstance(int overrideWidth = -1, int overrideHeight = -1)
		{
			int width = 0;
			int height = 0;

			if (UserSettings.Instance.IsTouchScreen)
			{
				minSize = new Vector2(800, 480);
			}

			// check if the app has a size already set
			string windowSize = ApplicationSettings.Instance.get(ApplicationSettingsKey.WindowSize);
			if (windowSize != null && windowSize != "")
			{
				// try and open our window matching the last size that we had for it.
				string[] sizes = windowSize.Split(',');

				width = Math.Max(int.Parse(sizes[0]), (int)minSize.X + 1);
				height = Math.Max(int.Parse(sizes[1]), (int)minSize.Y + 1);
			}
			else // try to set it to a big size or the min size
			{
				Point2D desktopSize = AggContext.DesktopSize;

				if (overrideWidth != -1)
				{
					width = overrideWidth;
				}
				else // try to set it to a good size
				{
					if (width < desktopSize.x)
					{
						width = 1280;
					}
				}

				if (overrideHeight != -1)
				{
					// Height should be constrained to actual
					height = Math.Min(overrideHeight, desktopSize.y);
				}
				else
				{
					if (height < desktopSize.y)
					{
						height = 720;
					}
				}
			}

			using (new PerformanceTimer("Startup", "Total"))
			{
				instance = new MatterControlApplication(width, height);
			}

			return instance;
		}

		public void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle(() =>
			{
				System.Diagnostics.Process.Start(targetUri);
			});
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			UserSettings.Instance.Fields.StartCountDurringExit = UserSettings.Instance.Fields.StartCount;

			if (ApplicationController.Instance.ActivePrinter.Connection.CommunicationState != CommunicationStates.PrintingFromSd)
			{
				ApplicationController.Instance.ActivePrinter.Connection.Disable();
			}
			//Close connection to the local datastore
			ApplicationController.Instance.ActivePrinter.Connection.HaltConnectionThread();
			ApplicationController.Instance.OnApplicationClosed();

			Datastore.Instance.Exit();

			if (RestartOnClose)
			{
				string appPathAndFile = System.Reflection.Assembly.GetExecutingAssembly().Location;
				string pathToAppFolder = Path.GetDirectoryName(appPathAndFile);

				ProcessStartInfo runAppLauncherStartInfo = new ProcessStartInfo();
				runAppLauncherStartInfo.Arguments = "\"{0}\" \"{1}\"".FormatWith(appPathAndFile, 1000);
				runAppLauncherStartInfo.FileName = Path.Combine(pathToAppFolder, "Launcher.exe");
				runAppLauncherStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				runAppLauncherStartInfo.CreateNoWindow = true;

				Process.Start(runAppLauncherStartInfo);
			}
			base.OnClosed(e);
		}

		public override void OnClosing(ClosingEventArgs eventArgs)
		{
			// save the last size of the window so we can restore it next time.
			ApplicationSettings.Instance.set(ApplicationSettingsKey.MainWindowMaximized, this.Maximized.ToString().ToLower());

			if (!this.Maximized)
			{
				ApplicationSettings.Instance.set(ApplicationSettingsKey.WindowSize, string.Format("{0},{1}", Width, Height));
				ApplicationSettings.Instance.set(ApplicationSettingsKey.DesktopPosition, string.Format("{0},{1}", DesktopPosition.x, DesktopPosition.y));
			}

			//Save a snapshot of the prints in queue
			QueueData.Instance.SaveDefaultQueue();

			// If we are waiting for a response and get another request, just cancel the close until we get a response.
			if(exitDialogOpen)
			{
				eventArgs.Cancel = true;
			}

			string caption = null;
			string message = null;

			if (!ApplicationExiting 
				&& !exitDialogOpen
				&& ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting)
			{
				if (ApplicationController.Instance.ActivePrinter.Connection.CommunicationState != CommunicationStates.PrintingFromSd)
				{
					caption = "Abort Print".Localize();
					message = "Are you sure you want to abort the current print and close MatterControl?".Localize();
				}
				else
				{
					caption = "Exit while printing".Localize();
					message = "Are you sure you want exit while a print is running from SD Card?\n\nNote: If you exit, it is recommended you wait until the print is completed before running MatterControl again.".Localize();
				}
			}
			else if (PartsSheet.IsSaving())
			{
				caption = "Confirm Exit".Localize();
				message = "You are currently saving a parts sheet, are you sure you want to exit?".Localize();
			}

			if (caption != null)
			{
				// Record that we are waiting for a response to the request to close
				exitDialogOpen = true;

				StyledMessageBox.ShowMessageBox(
					(exitConfirmed) =>
					{
						// Record that the exitDialog has closed
						exitDialogOpen = false;

						// Continue with shutdown if exit confirmed by user
						if (exitConfirmed)
						{
							this.ApplicationExiting = true;

							ApplicationController.Instance.Shutdown();

							// Always call PrinterConnection.Disable on exit unless PrintingFromSd
							PrinterConnection printerConnection = ApplicationController.Instance.ActivePrinter.Connection;
							switch (printerConnection.CommunicationState)
							{
								case CommunicationStates.PrintingFromSd:
								case CommunicationStates.Paused when printerConnection.PrePauseCommunicationState == CommunicationStates.PrintingFromSd:
									break;

								default:
									printerConnection.Disable();
									break;
							}

							this.RestartOnClose = false;
						}

						// If the user allowed the exit, don't cancel the OnClosing event
						eventArgs.Cancel = !exitConfirmed;
					},
					message,
					caption,
					StyledMessageBox.MessageType.YES_NO);

				exitDialogOpen = false;
			}
			else
			{
				this.ApplicationExiting = true;
			}
		}

		public bool ApplicationExiting { get; private set; } = false;

		private bool exitDialogOpen = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			totalDrawTime.Restart();
			GuiWidget.DrawCount = 0;
			using (new PerformanceTimer("Draw Timer", "MC Draw"))
			{
				base.OnDraw(graphics2D);
			}
			totalDrawTime.Stop();

			millisecondTimer.Update((int)totalDrawTime.ElapsedMilliseconds);

			if (ShowMemoryUsed)
			{
				long memory = GC.GetTotalMemory(false);
				this.Title = "Allocated = {0:n0} : {1:000}ms, d{2} Size = {3}x{4}, onIdle = {5:00}:{6:00}, widgetsDrawn = {7}".FormatWith(memory, millisecondTimer.GetAverage(), drawCount++, this.Width, this.Height, UiThread.CountExpired, UiThread.Count, GuiWidget.DrawCount);
				if (DoCGCollectEveryDraw)
				{
					GC.Collect();
				}
			}

			//msGraph.AddData("ms", totalDrawTime.ElapsedMilliseconds);
			//msGraph.Draw(MatterHackers.Agg.Transform.Affine.NewIdentity(), graphics2D);
		}

		public override void OnLoad(EventArgs args)
		{
			// Moved from OnParentChanged
			if (File.Exists("RunUnitTests.txt"))
			{
				//DiagnosticWidget diagnosticView = new DiagnosticWidget(this);
			}

			// now that we are all set up lets load our plugins and allow them their chance to set things up
			FindAndInstantiatePlugins();

			if (ApplicationController.Instance.PluginsLoaded != null)
			{
				ApplicationController.Instance.PluginsLoaded.CallEvents(null, null);
			}

			foreach (string arg in commandLineArgs)
			{
				string argExtension = Path.GetExtension(arg).ToUpper();
				if (argExtension.Length > 1
					&& MeshFileIo.ValidFileExtensions().Contains(argExtension))
				{
					QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileName(arg), Path.GetFullPath(arg))));
				}
			}

			ApplicationController.Instance.OnLoadActions();

			//HtmlWindowTest();

			IsLoading = false;

			base.OnLoad(args);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// run this first to make sure a child has the chance to take the drag drop event
			base.OnMouseMove(mouseEvent);

			if (!mouseEvent.AcceptDrop && mouseEvent.DragFiles != null)
			{
				// no child has accepted the drop
				foreach (string file in mouseEvent.DragFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
						|| extension == ".GCODE"
						|| extension == ".ZIP")
					{
						//mouseEvent.AcceptDrop = true;
					}
				}
				dropWasOnChild = false;
			}
			else
			{
				dropWasOnChild = true;
			}

			if (GuiWidget.DebugBoundsUnderMouse)
			{
				Invalidate();
			}
		}

		public void OpenCameraPreview()
		{
			//Camera launcher placeholder (KP)
			if (ApplicationSettings.Instance.get(ApplicationSettingsKey.HardwareHasCamera) == "true")
			{
				//Do something
			}
			else
			{
				//Do something else (like show warning message)
			}
		}

		public void PlaySound(string fileName)
		{
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				using (var mediaStream = AggContext.StaticData.OpenSteam(Path.Combine("Sounds", fileName)))
				{
					(new System.Media.SoundPlayer(mediaStream)).Play();
				}
			}
		}

		private void CheckOnPrinter()
		{
			try
			{
				ApplicationController.Instance.ActivePrinter.Connection.OnIdle();
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
#if DEBUG
				throw e;
#endif
			}
			UiThread.RunOnIdle(CheckOnPrinter);
		}

		private void FindAndInstantiatePlugins()
		{
#if false
			string pluginDirectory = Path.Combine("..", "..", "..", "MatterControlPlugins", "bin");
#if DEBUG
			pluginDirectory = Path.Combine(pluginDirectory, "Debug");
#else
			pluginDirectory = Path.Combine(pluginDirectory, "Release");
#endif
			if (!Directory.Exists(pluginDirectory))
			{
				string dataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
				pluginDirectory = Path.Combine(dataPath, "Plugins");
			}
			// TODO: this should look in a plugin folder rather than just the application directory (we probably want it in the user folder).
			PluginFinder<MatterControlPlugin> pluginFinder = new PluginFinder<MatterControlPlugin>(pluginDirectory);
#endif

			string oemName = ApplicationSettings.Instance.GetOEMName();
			foreach (MatterControlPlugin plugin in PluginFinder.CreateInstancesOf<MatterControlPlugin>())
			{
				string pluginInfo = plugin.GetPluginInfoJSon();
				Dictionary<string, string> nameValuePairs = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(pluginInfo);

				if (nameValuePairs != null && nameValuePairs.ContainsKey("OEM"))
				{
					if (nameValuePairs["OEM"] == oemName)
					{
						plugin.Initialize(this);
					}
				}
				else
				{
					plugin.Initialize(this);
				}
			}
		}

		private static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		public static void CheckKnownAssemblyConditionalCompSymbols()
		{
			MatterControlApplication.AssertDebugNotDefined();
			MatterHackers.GCodeVisualizer.GCodeFile.AssertDebugNotDefined();
			MatterHackers.Agg.Graphics2D.AssertDebugNotDefined();
			MatterHackers.Agg.UI.SystemWindow.AssertDebugNotDefined();
			MatterHackers.Agg.ImageProcessing.InvertLightness.AssertDebugNotDefined();
			MatterHackers.Localizations.TranslationMap.AssertDebugNotDefined();
			MatterHackers.MarchingSquares.MarchingSquaresByte.AssertDebugNotDefined();
			MatterHackers.MatterControl.PluginSystem.MatterControlPlugin.AssertDebugNotDefined();
			MatterHackers.MatterSlice.MatterSlice.AssertDebugNotDefined();
			MatterHackers.MeshVisualizer.MeshViewerWidget.AssertDebugNotDefined();
			MatterHackers.RenderOpenGl.GLMeshTrianglePlugin.AssertDebugNotDefined();
		}

		public bool IsNetworkConnected()
		{
			return true;
		}
	}
}
