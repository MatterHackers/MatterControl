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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PluginSystem;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.MatterControl
{
	public static class MatterControlApplication
	{
#if DEBUG

		//public static string MCWSBaseUri { get; } = "http://192.168.2.129:9206";
		public static string MCWSBaseUri { get; } = "https://mattercontrol-test.appspot.com";
#else
		public static string MCWSBaseUri { get; } = "https://mattercontrol.appspot.com";
#endif

		public static GuiWidget Initialize(SystemWindow systemWindow, Action<double, string> reporter)
		{
			if (AggContext.OperatingSystem == OSType.Mac && AggContext.StaticData == null)
			{
				// Set working directory - this duplicates functionality in Main but is necessary on OSX as Main fires much later (after the constructor in this case)
				// resulting in invalid paths due to path tests running before the working directory has been overridden. Setting the value before initializing StaticData
				// works around this architectural difference.
				Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
			}

			// Initialize a standard file system backed StaticData provider
			if (AggContext.StaticData == null) // it may already be initialized by tests
			{
				reporter?.Invoke(0.01, "StaticData");
				AggContext.StaticData = new MatterHackers.Agg.FileSystemStaticData();
			}


			ApplicationSettings.Instance.set("HardwareHasCamera", "false");

			// TODO: Appears to be unused and should be removed
			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			var commandLineArgs = Environment.GetCommandLineArgs();
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
						DesktopRootSystemWindow.ShowMemoryUsed = true;
						break;
				}
			}

			reporter?.Invoke(0.05, "ApplicationController");
			var na = ApplicationController.Instance;

			// Set the default theme colors
			reporter?.Invoke(0.1, "LoadOemOrDefaultTheme");
			ApplicationController.LoadOemOrDefaultTheme();

			// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
			reporter?.Invoke(0.2, "ProfileManager");
			bool na2 = ProfileManager.Instance.IsGuestProfile;

			reporter?.Invoke(0.3, "MainView");
			ApplicationController.Instance.MainView = new WidescreenPanel();

			// now that we are all set up lets load our plugins and allow them their chance to set things up
			reporter?.Invoke(0.8, "Plugins");
			FindAndInstantiatePlugins(systemWindow);
			if (ApplicationController.Instance.PluginsLoaded != null)
			{
				ApplicationController.Instance.PluginsLoaded.CallEvents(null, null);
			}

			// TODO: Do we still want to support command line arguments for adding to the queue?
			foreach (string arg in commandLineArgs)
			{
				string argExtension = Path.GetExtension(arg).ToUpper();
				if (argExtension.Length > 1
					&& MeshFileIo.ValidFileExtensions().Contains(argExtension))
				{
					QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileName(arg), Path.GetFullPath(arg))));
				}
			}

			reporter?.Invoke(0.9, "AfterLoad");
			AfterLoad();

			return ApplicationController.Instance.MainView;
		}

		public static void AfterLoad()
		{
			// ApplicationController.Instance.OnLoadActions {{

			// TODO: Calling UserChanged seems wrong. Load the right user before we spin up controls, rather than after
			// Pushing this after load fixes that empty printer list
			/////////////////////ApplicationController.Instance.UserChanged();

			bool showAuthWindow =  PrinterSetup.ShouldShowAuthPanel?.Invoke() ?? false;
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
				if (UserSettings.Instance.get("SoftwareLicenseAccepted") != "true")
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
			// ApplicationController.Instance.OnLoadActions }}

			//HtmlWindowTest();

			UiThread.RunOnIdle(CheckOnPrinter);

			ApplicationController.Instance.IsLoading = false;
		}

		private static void RunSetupIfRequired()
		{
			if (!ProfileManager.Instance.ActiveProfiles.Any())
			{
				// Start the setup wizard if no profiles exist
				UiThread.RunOnIdle(() => DialogWindow.Show(PrinterSetup.GetBestStartPage()));
			}
		}

		private static void CheckOnPrinter()
		{
			try
			{
				// TODO: UiThread should not be driving anything in Printer.Connection
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

		private static void FindAndInstantiatePlugins(SystemWindow systemWindow)
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
						plugin.Initialize(systemWindow);
					}
				}
				else
				{
					plugin.Initialize(systemWindow);
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
	}
}
