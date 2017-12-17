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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PluginSystem;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl.OpenGl;
using Mindscape.Raygun4Net;

namespace MatterHackers.MatterControl
{
	public class MatterControlApplication : GuiWidget
	{
#if DEBUG

		//public static string MCWSBaseUri { get; } = "http://192.168.2.129:9206";
		public static string MCWSBaseUri { get; } = "https://mattercontrol-test.appspot.com";

#else
		public static string MCWSBaseUri { get; } = "https://mattercontrol.appspot.com";
#endif

		public static bool CameraInUseByExternalProcess { get; set; } = false;
		public bool RestartOnClose = false;
		
		private string[] commandLineArgs = null;

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
		}

		public MatterControlApplication(double width, double height)
			: base(width, height)
		{
			this.Name = "MatterControlApplication Widget";

			ApplicationSettings.Instance.set("HardwareHasCamera", "false");

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
						DesktopRootSystemWindow.ShowMemoryUsed = true;
						break;
				}

				if (MeshFileIo.ValidFileExtensions().Contains(Path.GetExtension(command).ToUpper()))
				{
					// If we are the only instance running then do nothing.
					// Else send these to the running instance so it can load them.
				}
			}

			using (new PerformanceTimer("Startup", "MainView"))
			{
				this.AddChild(ApplicationController.Instance.MainView);
			}

			this.AnchorAll();

			UiThread.RunOnIdle(CheckOnPrinter);
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
	}
}
