/*
Copyright (c) 2014, Lars Brubaker, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PluginSystem;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Mindscape.Raygun4Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GCodeVisualizer;
using Gaming.Game;
using MatterHackers.GuiAutomation;

namespace MatterHackers.MatterControl
{
    public class MatterControlApplication : SystemWindow
	{
		public static bool CameraPreviewActive = false;
		public static Action AfterFirstDraw = null;
		public bool RestartOnClose = false;
		private static readonly Vector2 minSize = new Vector2(600, 600);
		private static MatterControlApplication instance;
		private string[] commandLineArgs = null;
		private string confirmExit = "Confirm Exit".Localize();
		private bool DoCGCollectEveryDraw = false;
		private int drawCount = 0;
		private bool firstDraw = true;
		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		private DataViewGraph msGraph;
		private string savePartsSheetExitAnywayMessage = "You are currently saving a parts sheet, are you sure you want to exit?".Localize();
		private bool ShowMemoryUsed = false;

		public void ConfigureWifi()
		{
		}

		private Stopwatch totalDrawTime = new Stopwatch();

#if true//!DEBUG
		static RaygunClient _raygunClient = GetCorrectClient();
#endif

		static RaygunClient GetCorrectClient()
		{
			if (OsInformation.OperatingSystem == OSType.Mac)
			{
				return new RaygunClient("qmMBpKy3OSTJj83+tkO7BQ=="); // this is the Mac key
			}
			else
			{
				return new RaygunClient("hQIlyUUZRGPyXVXbI6l1dA=="); // this is the PC key
			}
		}

		public static bool IsLoading { get; private set; } = true;

		static MatterControlApplication()
		{
			if (OsInformation.OperatingSystem == OSType.Mac)
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
			if (StaticData.Instance == null) // it may already be initialized by tests
			{
				StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData();
			}
		}

		private MatterControlApplication(double width, double height)
			: base(width, height)
		{
			Name = "MatterControl";

			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			this.commandLineArgs = Environment.GetCommandLineArgs();
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			bool forceSofwareRendering = false;

			for (int currentCommandIndex = 0; currentCommandIndex < commandLineArgs.Length; currentCommandIndex++)
			{
				string command = commandLineArgs[currentCommandIndex];
				string commandUpper = command.ToUpper();
				switch (commandUpper)
				{
					case "FORCE_SOFTWARE_RENDERING":
						forceSofwareRendering = true;
						GL.ForceSoftwareRendering();
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

					case "CONNECT_TO_PRINTER":
						if (currentCommandIndex + 1 <= commandLineArgs.Length)
						{
							PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
						}
						break;

					case "START_PRINT":
						if (currentCommandIndex + 1 <= commandLineArgs.Length)
						{
							bool hasBeenRun = false;
							currentCommandIndex++;
							string fullPath = commandLineArgs[currentCommandIndex];
							QueueData.Instance.RemoveAll();
							if (!string.IsNullOrEmpty(fullPath))
							{
								string fileName = Path.GetFileNameWithoutExtension(fullPath);
								QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(fileName, fullPath)));
								PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((sender, e) =>
								{
									if (!hasBeenRun && PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.Connected)
									{
										hasBeenRun = true;
										PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
									}
								}, ref unregisterEvent);
							}
						}
						break;

					case "SLICE_AND_EXPORT_GCODE":
						if (currentCommandIndex + 1 <= commandLineArgs.Length)
						{
							currentCommandIndex++;
							string fullPath = commandLineArgs[currentCommandIndex];
							QueueData.Instance.RemoveAll();
							if (!string.IsNullOrEmpty(fullPath))
							{
								string fileName = Path.GetFileNameWithoutExtension(fullPath);
								PrintItemWrapper printItemWrapper = new PrintItemWrapper(new PrintItem(fileName, fullPath));
								QueueData.Instance.AddItem(printItemWrapper);

								SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
								ExportPrintItemWindow exportForTest = new ExportPrintItemWindow(printItemWrapper);
								exportForTest.ExportGcodeCommandLineUtility(fileName);
							}
						}
						break;
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

			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
			{
				GuiWidget.DeviceScale = 1.3;
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

			if (!forceSofwareRendering)
			{
				UseOpenGL = true;
			}
			string version = "1.5";

			Title = "MatterControl {0}".FormatWith(version);
			if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
			{
				Title = Title + " - {1}".FormatWith(version, OemSettings.Instance.WindowTitleExtra);
			}

			UiThread.RunOnIdle(CheckOnPrinter);

			string desktopPosition = ApplicationSettings.Instance.get("DesktopPosition");
			if (desktopPosition != null && desktopPosition != "")
			{
				string[] sizes = desktopPosition.Split(',');

				//If the desktop position is less than -10,-10, override
				int xpos = Math.Max(int.Parse(sizes[0]), -10);
				int ypos = Math.Max(int.Parse(sizes[1]), -10);

				DesktopPosition = new Point2D(xpos, ypos);
			}

			IsLoading = false;
		}

        bool dropWasOnChild = true;
        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            base.OnDragEnter(fileDropEventArgs);

            if (!fileDropEventArgs.AcceptDrop)
            {
                // no child has accepted the drop
                foreach (string file in fileDropEventArgs.DroppedFiles)
                {
                    string extension = Path.GetExtension(file).ToUpper();
                    if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
                        || extension == ".GCODE"
                        || extension == ".ZIP")
                    {
                        fileDropEventArgs.AcceptDrop = true;
                    }
                }
                dropWasOnChild = false;
            }
            else
            {
                dropWasOnChild = true;
            }
        }

        public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
        {
            base.OnDragOver(fileDropEventArgs);

            if (!fileDropEventArgs.AcceptDrop)
            {
                // no child has accepted the drop
                foreach (string file in fileDropEventArgs.DroppedFiles)
                {
                    string extension = Path.GetExtension(file).ToUpper();
                    if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
                        || extension == ".GCODE"
                        || extension == ".ZIP")
                    {
                        fileDropEventArgs.AcceptDrop = true;
                    }
                }
                dropWasOnChild = false;
            }
            else
            {
                dropWasOnChild = true;
            }
        }

        public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
        {
            base.OnDragDrop(fileDropEventArgs);

            if (!dropWasOnChild)
            {
                QueueDataWidget.DoAddFiles(fileDropEventArgs.DroppedFiles);
            }
        }

        public enum ReportSeverity2 { Warning, Error }

		public void ReportException(Exception e, string key = "", string value = "", ReportSeverity2 warningLevel = ReportSeverity2.Warning)
		{
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get("UpdateFeedType");
			if (string.IsNullOrEmpty(channel) || channel != "release" || OemSettings.Instance.WindowTitleExtra == "Experimental")
			{
#if !DEBUG
				_raygunClient.Send(e);
#endif
			}
		}

		private event EventHandler unregisterEvent;

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

		public static MatterControlApplication CreateInstance()
		{
			// try and open our window matching the last size that we had for it.
			string windowSize = ApplicationSettings.Instance.get("WindowSize");
			int width = 601;
			int height = 601;
			if (windowSize != null && windowSize != "")
			{
				string[] sizes = windowSize.Split(',');
				width = Math.Max(int.Parse(sizes[0]), (int)minSize.x + 1);
				height = Math.Max(int.Parse(sizes[1]), (int)minSize.y + 1);
			}

            using (new PerformanceTimer("Startup", "Total"))
            {
                instance = new MatterControlApplication(width, height);
            }

			return instance;
		}

		[STAThread]
		public static void Main()
		{
            PerformanceTimer.GetParentWindowFunction = () => { return MatterControlApplication.instance; };

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// Make sure we have the right working directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			Datastore.Instance.Initialize();

#if !DEBUG
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get("UpdateFeedType");
			if (string.IsNullOrEmpty(channel) || channel != "release" || OemSettings.Instance.WindowTitleExtra == "Experimental")
#endif
			{
				System.Windows.Forms.Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
			}

			MatterControlApplication app = MatterControlApplication.Instance;
		}

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
#if !DEBUG
			_raygunClient.Send(e.Exception);
#endif
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
#if !DEBUG
			_raygunClient.Send(e.ExceptionObject as Exception);
#endif
		}

		public static void WriteTestGCodeFile()
		{
			using (StreamWriter file = new StreamWriter("PerformanceTest.gcode"))
			{
				//int loops = 150000;
				int loops = 150;
				int steps = 200;
				double radius = 50;
				Vector2 center = new Vector2(150, 100);

				file.WriteLine("G28 ; home all axes");
				file.WriteLine("G90 ; use absolute coordinates");
				file.WriteLine("G21 ; set units to millimeters");
				file.WriteLine("G92 E0");
				file.WriteLine("G1 F7800");
				file.WriteLine("G1 Z" + (5).ToString());
				WriteMove(file, center);

				for (int loop = 0; loop < loops; loop++)
				{
					for (int step = 0; step < steps; step++)
					{
						Vector2 nextPosition = new Vector2(radius, 0);
						nextPosition.Rotate(MathHelper.Tau / steps * step);
						WriteMove(file, center + nextPosition);
					}
				}

				file.WriteLine("M84     ; disable motors");
			}
		}

		public void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle(() =>
			{
				System.Diagnostics.Process.Start(targetUri);
			});
		}

		public override void OnClosed(EventArgs e)
		{
			UserSettings.Instance.Fields.StartCountDurringExit = UserSettings.Instance.Fields.StartCount;

			TerminalWindow.CloseIfOpen();
			PrinterConnectionAndCommunication.Instance.Disable();
			//Close connection to the local datastore
			Datastore.Instance.Exit();
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			SlicingQueue.Instance.ShutDownSlicingThread();
			ApplicationController.Instance.OnApplicationClosed();

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

		public override void OnClosing(out bool CancelClose)
		{
			// save the last size of the window so we can restore it next time.
			ApplicationSettings.Instance.set("WindowSize", string.Format("{0},{1}", Width, Height));
			ApplicationSettings.Instance.set("DesktopPosition", string.Format("{0},{1}", DesktopPosition.x, DesktopPosition.y));

			//Save a snapshot of the prints in queue
			QueueData.Instance.SaveDefaultQueue();

			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				// Needed as we can't assign to CancelClose inside of the lambda below
				bool continueWithShutdown = false;

				StyledMessageBox.ShowMessageBox(
					(shutdownConfirmed) => continueWithShutdown = shutdownConfirmed,
					"Are you sure you want to abort the current print and close MatterControl?".Localize(),
					"Abort Print".Localize(),
					StyledMessageBox.MessageType.YES_NO);

				if (continueWithShutdown)
				{
					PrinterConnectionAndCommunication.Instance.Disable();
					this.Close();
				}

				// It's safe to cancel an active print because PrinterConnectionAndCommunication.Disable will be called 
				// when MatterControlApplication.OnClosed is invoked
				CancelClose = true;
			}
			else if (PartsSheet.IsSaving())
			{
				StyledMessageBox.ShowMessageBox(onConfirmExit, savePartsSheetExitAnywayMessage, confirmExit, StyledMessageBox.MessageType.YES_NO);
				CancelClose = true;
			}
			else
			{
				base.OnClosing(out CancelClose);
			}
		}

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

			if (firstDraw)
			{
				firstDraw = false;
				foreach (string arg in commandLineArgs)
				{
					string argExtension = Path.GetExtension(arg).ToUpper();
					if (argExtension.Length > 1
						&& MeshFileIo.ValidFileExtensions().Contains(argExtension))
					{
						QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileName(arg), Path.GetFullPath(arg))));
					}
				}

				TerminalWindow.ShowIfLeftOpen();

#if false
			{
				SystemWindow releaseNotes = new SystemWindow(640, 480);
				string releaseNotesFile = Path.Combine("C:/Users/LarsBrubaker/Downloads", "test1.html");
				string releaseNotesContent = StaticData.Instance.ReadAllText(releaseNotesFile);
				HtmlWidget content = new HtmlWidget(releaseNotesContent, RGBA_Bytes.Black);
				content.AddChild(new GuiWidget(HAnchor.AbsolutePosition, VAnchor.ParentBottomTop));
				content.VAnchor |= VAnchor.ParentTop;
				content.BackgroundColor = RGBA_Bytes.White;
				releaseNotes.AddChild(content);
				releaseNotes.BackgroundColor = RGBA_Bytes.Cyan;
				UiThread.RunOnIdle((state) =>
				{
					releaseNotes.ShowAsSystemWindow();
				}, 1);
			}
#endif

				AfterFirstDraw?.Invoke();
			}

			//msGraph.AddData("ms", totalDrawTime.ElapsedMilliseconds);
			//msGraph.Draw(MatterHackers.Agg.Transform.Affine.NewIdentity(), graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (GuiWidget.DebugBoundsUnderMouse)
			{
				Invalidate();
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnParentChanged(EventArgs e)
		{
			if (File.Exists("RunUnitTests.txt"))
			{
				//DiagnosticWidget diagnosticView = new DiagnosticWidget(this);
			}

			base.OnParentChanged(e);

			// now that we are all set up lets load our plugins and allow them their chance to set things up
			FindAndInstantiatePlugins();

			if(ApplicationController.Instance.PluginsLoaded != null)
			{
				ApplicationController.Instance.PluginsLoaded.CallEvents(null, null);
			}
		}

		public void OpenCameraPreview()
		{
			//Camera launcher placeholder (KP)
			if (ApplicationSettings.Instance.get("HardwareHasCamera") == "true")
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
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				using (var mediaStream = StaticData.Instance.OpenSteam(Path.Combine("Sounds", fileName)))
				{
					(new System.Media.SoundPlayer(mediaStream)).Play();
				}
			}
		}

		private static void WriteMove(StreamWriter file, Vector2 center)
		{
			file.WriteLine("G1 X" + center.x.ToString() + " Y" + center.y.ToString());
		}

		private void CheckOnPrinter()
		{
			try
			{
				PrinterConnectionAndCommunication.Instance.OnIdle();
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
#else
			PluginFinder<MatterControlPlugin> pluginFinder = new PluginFinder<MatterControlPlugin>();
#endif

			string oemName = ApplicationSettings.Instance.GetOEMName();
			foreach (MatterControlPlugin plugin in pluginFinder.Plugins)
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

		private void onConfirmExit(bool messageBoxResponse)
		{
			bool CancelClose;
			if (messageBoxResponse)
			{
				base.OnClosing(out CancelClose);
			}
		}

		private static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		bool showNamesUnderMouse = false;
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.F2)
			{
				Task.Run((Action)AutomationTest);
			}
			else if (keyEvent.KeyCode == Keys.F1)
			{
				showNamesUnderMouse = !showNamesUnderMouse;
			}

			base.OnKeyDown(keyEvent);
		}

		private void AutomationTest()
		{
			AutomationRunner test = new AutomationRunner();
			test.ClickByName("Library Tab", 5);
			test.ClickByName("Queue Tab", 5);
			test.ClickByName("Queue Item SkeletonArm_Med", 5);
			test.ClickByName("3D View Edit", 5);
			test.Wait(.2);
			test.DragByName("SkeletonArm_Med_IObject3D", 5);
			test.DropByName("SkeletonArm_Med_IObject3D", 5, offset: new Point2D(0, -40));
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