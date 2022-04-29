﻿/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using Microsoft.Extensions.Configuration;
using Mindscape.Raygun4Net;
using ServiceWire;
using SQLiteWin32;

namespace MatterHackers.MatterControl
{
	public class Program
	{
		[Flags]
		public enum EXECUTION_STATE : uint
		{
			ES_AWAYMODE_REQUIRED = 0x00000040,
			ES_CONTINUOUS = 0x80000000,
			ES_DISPLAY_REQUIRED = 0x00000002,
			ES_SYSTEM_REQUIRED = 0x00000001
			// Legacy flag, should not be used.
			// ES_USER_PRESENT = 0x00000004
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

		private static EventWaitHandle waitHandle;

		private const int RaygunMaxNotifications = 15;

		private static int raygunNotificationCount = 0;

		private static RaygunClient _raygunClient;

		//private static string mainServiceName = "shell";

		//private const string ServiceBaseUri = "net.pipe://localhost/mattercontrol";

		private const string ServicePipeName = "mattercontrol";

		[DllImport("Shcore.dll")]
		static extern int SetProcessDpiAwareness(int PROCESS_DPI_AWARENESS);

		// According to https://msdn.microsoft.com/en-us/library/windows/desktop/dn280512(v=vs.85).aspx
		private enum DpiAwareness
		{
			None = 0,
			SystemAware = 1,
			PerMonitorAware = 2
		}

		[STAThread]
		public static void Main(string[] args)
		{
#if false // this is for some early testing of SLA output
			var test = new PhotonFile();
			void Progress(string message)
			{
				Debug.WriteLine(message);
			}

			var sourceFile = @"C:\Users\LarsBrubaker\Downloads\10mm-benchy.photon";
			if (File.Exists(sourceFile))
			{
				test.ReadFile(sourceFile, Progress);
				test.SaveFile(@"C:\Users\LarsBrubaker\Downloads\10mm-bench2.photon");
			}
			else
			{
				sourceFile = @"C:\Users\larsb\Downloads\_rocktopus.ctb";
				test.ReadFile(sourceFile, Progress);
				test.SaveFile(@"C:\Users\larsb\Downloads\_rocktopus.photon");
			}
#endif

#if false // this is for processing print log exports
			var filename = "C:\\Users\\LarsBrubaker\\Downloads\\210309 B2 print_log.txt";
			var lines = File.ReadAllLines(filename);
			var newPosition = default(Vector3);
			var ePosition = 0.0;
			var instruction = 0;
			var layer = 0;
			using (var writetext = new StreamWriter("C:\\Temp\\printlog.gcode"))
			{
				foreach (var line in lines)
				{
					if (line.Contains(" G1 "))
					{
						GCodeFile.GetFirstNumberAfter("X", line, ref newPosition.X);
						GCodeFile.GetFirstNumberAfter("Y", line, ref newPosition.Y);
						GCodeFile.GetFirstNumberAfter("Z", line, ref newPosition.Z);
						GCodeFile.GetFirstNumberAfter("E", line, ref ePosition);

						writetext.WriteLine($"G1 X{newPosition.X} Y{newPosition.Y} Z{newPosition.Z} E{ePosition}");
						instruction++;

						if(instruction % 500 == 0)
						{
							writetext.WriteLine($"; LAYER:{layer++}");
						}
					}
				}
			}
#endif

			// Set the global culture for the app, current thread and all new threads
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

			// make sure we can build a system relevant serial port
			FrostedSerialPortFactory.GetPlatformSerialPort = (serialPortName) =>
			{
				return new CSharpSerialPortWrapper(serialPortName);
			};

			// Set default Agg providers
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.GlfwProvider.GlfwWindowProvider, MatterHackers.GlfwProvider";
			// for now we will ship release with the old renderer
#if !DEBUG
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.MatterControl.WinformsSingleWindowProvider, MatterControl.Winforms";
#endif

			string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			_raygunClient = new RaygunClient("hQIlyUUZRGPyXVXbI6l1dA==") // this is the PC key
			{
				ApplicationVersion = VersionInfo.Instance.ReleaseVersion
			};

			// If MatterControl isn't running and valid files were shelled, schedule a StartupAction to open the files after load
			var shellFiles = args.Where(f => File.Exists(f) && ApplicationController.ShellFileExtensions.Contains(Path.GetExtension(f).ToLower()));

#if !DEBUG
			try
			{
				using (var client = new ServiceWire.NamedPipes.NpClient<IMainService>(new ServiceWire.NamedPipes.NpEndPoint(ServicePipeName)))
				{
					if (client.IsConnected)
					{
						// and at least one argument is an acceptable shell file extension
						if (shellFiles.Any())
						{
							// notify the running instance of the event
							client.Proxy.ShellOpenFile(shellFiles.ToArray());
						}

						System.Threading.Thread.Sleep(1000);

						// Finally, close the process spawned by Explorer.exe
						return;
					}
				}
			}
			catch (Exception)
			{
			}

			var host = new ServiceWire.NamedPipes.NpHost(ServicePipeName, new ServiceWireLogger());
			host.AddService<IMainService>(new LocalService());
			host.Open();
#endif

			if (shellFiles.Any())
			{
				ApplicationController.StartupActions.Add(new ApplicationController.StartupAction()
				{
					Title = "Shell Files",
					Priority = 0,
					Action = () =>
					{
						// Open each shelled file
						foreach (string file in shellFiles)
						{
							ApplicationController.Instance.ShellOpenFile(file);
						}
					}
				});
			}

			// Load optional user configuration
			IConfiguration config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.json", optional: true)
				.AddJsonFile(Path.Combine(userProfilePath, "MatterControl.json"), optional: true)
				.Build();

			// Override defaults via configuration
			config.Bind("Agg:ProviderTypes", AggContext.Config.ProviderTypes);
			config.Bind("Agg:GraphicsMode", AggContext.Config.GraphicsMode);

			Slicer.RunInProcess = config.GetValue<bool>("MatterControl:Slicer:Debug");
			Application.EnableF5Collect = config.GetValue<bool>("MatterControl:Application:EnableF5Collect");
			Application.EnableNetworkTraffic = config.GetValue("MatterControl:Application:EnableNetworkTraffic", true);

			// Make sure we have the right working directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			Datastore.Instance.Initialize(DesktopSqlite.CreateInstance());

			if (UserSettings.Instance.get(UserSettingsKey.ApplicationUseHeigResDisplays) == "true")
			{
				SetProcessDpiAwareness((int)DpiAwareness.PerMonitorAware);
			}

			var isExperimental = OemSettings.Instance.WindowTitleExtra == "Experimental";
#if !DEBUG
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			if (string.IsNullOrEmpty(channel)
				|| channel != "release"
				|| isExperimental)
#endif
			{
				System.Windows.Forms.Application.ThreadException += (s, e) =>
				{
					if (raygunNotificationCount++ < RaygunMaxNotifications)
					{
						_raygunClient.Send(e.Exception);
					}

					if (System.Windows.Forms.Application.OpenForms.Count > 0
						&& !System.Windows.Forms.Application.OpenForms[0].InvokeRequired)
					{
						System.Windows.Forms.Application.Exit();
					}
				};

				AppDomain.CurrentDomain.UnhandledException += (s, e) =>
				{
					if (raygunNotificationCount++ < RaygunMaxNotifications)
					{
						_raygunClient.Send(e.ExceptionObject as Exception);
					}

					System.Windows.Forms.Application.Exit();
				};
			}

			// Init platformFeaturesProvider before ShowAsSystemWindow
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";

			string textSizeMode = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);
			if (!string.IsNullOrEmpty(textSizeMode))
			{
				if (double.TryParse(textSizeMode, out double textSize))
				{
					GuiWidget.DeviceScale = textSize;
				}
			}

			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();

			config.Bind("MatterControl", MatterHackers.MatterControl.AppContext.Options);

			// Get startup bounds from MatterControl and construct system window
			// var systemWindow = new DesktopMainWindow(400, 200)
			var (width, height) = RootSystemWindow.GetStartupBounds();

			var rootSystemWindow = Application.LoadRootWindow(width, height);

			var theme = ApplicationController.Instance.Theme;
			SingleWindowProvider.SetWindowTheme(theme.TextColor,
				theme.DefaultFontSize - 1,
				() => theme.CreateSmallResetButton(),
				theme.ToolbarPadding,
				theme.TabBarBackground,
				new Color(theme.PrimaryAccentColor, 175));

			ApplicationController.Instance.KeepAwake = KeepAwake;

			// Add a the on screen keyboard manager
			//_ = new SoftKeyboardDisplayStateManager(rootSystemWindow);

			rootSystemWindow.ShowAsSystemWindow();
		}

		private static readonly object locker = new object();

		static void KeepAwake(bool keepAwake)
		{
			if (keepAwake)
			{
				// Prevent Idle-to-Sleep
				SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED);
			}
			else
			{
				SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
			}
		}

		[Serializable]
		public class LocalService : IMainService
		{
			public void ShellOpenFile(string[] files)
			{
				// If at least one argument is an acceptable shell file extension
				var itemsToAdd = files.Where(f => File.Exists(f)
					&& ApplicationController.ShellFileExtensions.Contains(Path.GetExtension(f).ToLower()));

				if (itemsToAdd.Any())
				{
					lock (locker)
					{
						// Add each file
						foreach (string file in itemsToAdd)
						{
							ApplicationController.Instance.ShellOpenFile(file);
						}
					}
				}
			}
		}

		private class ServiceWireLogger : ServiceWire.ILog
		{
			static private void Log(ServiceWire.LogLevel level, string formattedMessage, params object[] args)
			{
				// Handled as in https://github.com/tylerjensen/ServiceWire/blob/master/src/ServiceWire/Logger.cs

				if (null == formattedMessage)
					return;

				if (level <= LogLevel.Warn)
				{
					string msg = (null != args && args.Length > 0)
						? string.Format(formattedMessage, args)
						: formattedMessage;

					System.Diagnostics.Trace.WriteLine(msg);
				}
			}

			void ILog.Debug(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Debug, formattedMessage, args);
			}

			void ILog.Error(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Error, formattedMessage, args);
			}

			void ILog.Fatal(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Fatal, formattedMessage, args);
			}

			void ILog.Info(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Info, formattedMessage, args);
			}

			void ILog.Warn(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Warn, formattedMessage, args);
			}
		}
	}

	public interface IMainService
	{
		void ShellOpenFile(string[] files);
	}
}
