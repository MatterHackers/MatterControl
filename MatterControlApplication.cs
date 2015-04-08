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
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace MatterHackers.MatterControl
{
	public class MatterControlApplication : SystemWindow
	{
		public bool RestartOnClose = false;
		private static readonly Vector2 minSize = new Vector2(600, 600);
		private static MatterControlApplication instance;
		private string[] commandLineArgs = null;
		private string confirmExit = "Confirm Exit".Localize();
		private bool DoCGCollectEveryDraw = false;
		private int drawCount = 0;
		private bool firstDraw = true;
		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		private Gaming.Game.DataViewGraph msGraph = new Gaming.Game.DataViewGraph(new Vector2(20, 500), 50, 50, 0, 200);
		private string savePartsSheetExitAnywayMessage = "You are currently saving a parts sheet, are you sure you want to exit?".Localize();
		private bool ShowMemoryUsed = false;
		private Stopwatch totalDrawTime = new Stopwatch();

		private string unableToExitMessage = "Oops! You cannot exit while a print is active.".Localize();

		private string unableToExitTitle = "Unable to Exit".Localize();

		static MatterControlApplication()
		{
			// Because fields on this class call localization methods and because those methods depend on the StaticData provider and because the field
			// initializers run before the class constructor, we need to init the platform specific provider in the static constructor (or write a custom initializer method)
			//
			// Initialize a standard file system backed StaticData provider
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData();
		}

		private MatterControlApplication(double width, double height, out bool showWindow)
			: base(width, height)
		{
			Name = "MatterControl";
			showWindow = false;

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
					case "TEST":
						Testing.TestingDispatch testDispatch = new Testing.TestingDispatch();
						testDispatch.RunTests();
						return;

					case "MHSERIAL_TO_ANDROID":
						{
							Dictionary<string, string> vidPid_NameDictionary = new Dictionary<string, string>();
							string[] MHSerialLines = File.ReadAllLines(Path.Combine("..", "..", "StaticData", "Drivers", "MHSerial", "MHSerial.inf"));
							foreach (string line in MHSerialLines)
							{
								if (line.Contains("=DriverInstall,"))
								{
									string name = Regex.Match(line, "%(.*).name").Groups[1].Value;
									string vid = Regex.Match(line, "VID_(.*)&PID").Groups[1].Value;
									string pid = Regex.Match(line, "PID_([0-9a-fA-F]+)").Groups[1].Value;
									string vidPid = "{0},{1}".FormatWith(vid, pid);
									if (!vidPid_NameDictionary.ContainsKey(vidPid))
									{
										vidPid_NameDictionary.Add(vidPid, name);
									}
								}
							}

							using (StreamWriter deviceFilter = new StreamWriter("deviceFilter.txt"))
							{
								using (StreamWriter serialPort = new StreamWriter("serialPort.txt"))
								{
									foreach (KeyValuePair<string, string> vidPid_Name in vidPid_NameDictionary)
									{
										string[] vidPid = vidPid_Name.Key.Split(',');
										int vid = Int32.Parse(vidPid[0], System.Globalization.NumberStyles.HexNumber);
										int pid = Int32.Parse(vidPid[1], System.Globalization.NumberStyles.HexNumber);
										serialPort.WriteLine("customTable.AddProduct(0x{0:X4}, 0x{1:X4}, cdcDriverType);  // {2}".FormatWith(vid, pid, vidPid_Name.Value));
										deviceFilter.WriteLine("<!-- {2} -->\n<usb-device vendor-id=\"{0}\" product-id=\"{1}\" />".FormatWith(vid, pid, vidPid_Name.Value));
									}
								}
							}
						}
						return;

					case "CLEAR_CACHE":
						AboutPage.DeleteCacheData();
						break;

					case "SHOW_MEMORY":
						ShowMemoryUsed = true;
						break;

					case "DO_GC_COLLECT_EVERY_DRAW":
						ShowMemoryUsed = true;
						DoCGCollectEveryDraw = true;
						break;

					case "CREATE_AND_SELECT_PRINTER":
						if (currentCommandIndex + 1 <= commandLineArgs.Length)
						{
							currentCommandIndex++;
							string argument = commandLineArgs[currentCommandIndex];
							string[] printerData = argument.Split(',');
							if (printerData.Length >= 2)
							{
								Printer ActivePrinter = new Printer();

								ActivePrinter.Name = "Auto: {0} {1}".FormatWith(printerData[0], printerData[1]);
								ActivePrinter.Make = printerData[0];
								ActivePrinter.Model = printerData[1];

								if (printerData.Length == 3)
								{
									ActivePrinter.ComPort = printerData[2];
								}

								PrinterSetupStatus test = new PrinterSetupStatus(ActivePrinter);
								test.LoadDefaultSliceSettings(ActivePrinter.Make, ActivePrinter.Model);
								ActivePrinterProfile.Instance.ActivePrinter = ActivePrinter;
							}
						}

						break;

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

				MatterHackers.PolygonMesh.UnitTests.UnitTests.Run();
				MatterHackers.RayTracer.UnitTests.Run();
				MatterHackers.Agg.Tests.UnitTests.Run();
				MatterHackers.VectorMath.Tests.UnitTests.Run();
				MatterHackers.Agg.UI.Tests.UnitTests.Run();
				MatterHackers.MatterControl.Slicing.Tests.UnitTests.Run();

				// you can turn this on to debug some bounds issues
				//GuiWidget.DebugBoundsUnderMouse = true;
			}

			GuiWidget.DefaultEnforceIntegerBounds = true;

			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
			{
				TextWidget.GlobalPointSizeScaleRatio = 1.3;
			}

			this.AddChild(ApplicationController.Instance.MainView);
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

			UseOpenGL = true;
			string version = "1.2";

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

			showWindow = true;
		}

		private event EventHandler unregisterEvent;

		public static MatterControlApplication Instance
		{
			get
			{
				if (instance == null)
				{
					// try and open our window matching the last size that we had for it.
					string windowSize = ApplicationSettings.Instance.get("WindowSize");
					int width = 1280;
					int height = 720;
					if (windowSize != null && windowSize != "")
					{
						string[] sizes = windowSize.Split(',');
						width = Math.Max(int.Parse(sizes[0]), (int)minSize.x + 1);
						height = Math.Max(int.Parse(sizes[1]), (int)minSize.y + 1);
					}

					bool showWindow;
					instance = new MatterControlApplication(width, height, out showWindow);

					if (showWindow)
					{
						instance.ShowAsSystemWindow();
					}
				}

				return instance;
			}
		}

		[STAThread]
		public static void Main()
		{
			// Make sure we have the right woring directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			Datastore.Instance.Initialize();

			MatterControlApplication app = MatterControlApplication.Instance;
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

		public void DoAutoConnectIfRequired(object state)
		{
			ActivePrinterProfile.CheckForAndDoAutoConnect();
		}

		public void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle((state) =>
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
				StyledMessageBox.ShowMessageBox(null, unableToExitMessage, unableToExitTitle);
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
			base.OnDraw(graphics2D);
			totalDrawTime.Stop();

			millisecondTimer.Update((int)totalDrawTime.ElapsedMilliseconds);

			if (ShowMemoryUsed)
			{
				long memory = GC.GetTotalMemory(false);
				this.Title = "Allocated = {0:n0} : {1:000}ms, d{2} Size = {3}x{4}, onIdle = {5:00}:{6:00}, drawCount = {7}".FormatWith(memory, millisecondTimer.GetAverage(), drawCount++, this.Width, this.Height, UiThread.CountExpired, UiThread.Count, GuiWidget.DrawCount);
				if (DoCGCollectEveryDraw)
				{
					GC.Collect();
				}
			}

			if (firstDraw && commandLineArgs.Length < 2)
			{
				UiThread.RunOnIdle(DoAutoConnectIfRequired);

				firstDraw = false;
				foreach (string arg in commandLineArgs)
				{
					string argExtension = Path.GetExtension(arg).ToUpper();
					if (argExtension.Length > 1
						&& MeshFileIo.ValidFileExtensions().Contains(argExtension))
					{
						QueueData.Instance.AddItem(new PrintItemWrapper(new DataStorage.PrintItem(Path.GetFileName(arg), Path.GetFullPath(arg))));
					}
				}

				TerminalWindow.ShowIfLeftOpen();

#if false
				foreach (CreatorInformation creatorInfo in RegisteredCreators.Instance.Creators)
				{
					if (creatorInfo.description.Contains("Image"))
					{
						creatorInfo.functionToLaunchCreator(null, null);
					}
				}
#endif
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

		private void CheckOnPrinter(object state)
		{
			try
			{
				PrinterConnectionAndCommunication.Instance.OnIdle();
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
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
				string dataPath = DataStorage.ApplicationDataStorage.Instance.ApplicationUserDataPath;
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
	}
}