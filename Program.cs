/*
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
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using Microsoft.Extensions.Configuration;
using Mindscape.Raygun4Net;
using SQLiteWin32;

namespace MatterHackers.MatterControl
{
	public class Program
	{
		private static EventWaitHandle waitHandle;

		private const int RaygunMaxNotifications = 15;

		private static int raygunNotificationCount = 0;

		private static RaygunClient _raygunClient;

		private static string mainServiceName = "shell";

		private const string ServiceBaseUri = "net.pipe://localhost/mattercontrol";

		[STAThread]
		public static void Main(string[] args)
		{
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
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.MatterControl.WinformsSingleWindowProvider, MatterControl.Winforms";

			string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			_raygunClient = new RaygunClient("hQIlyUUZRGPyXVXbI6l1dA==") // this is the PC key
			{
				ApplicationVersion = VersionInfo.Instance.ReleaseVersion
			};

			if (AggContext.OperatingSystem == OSType.Windows)
			{
				waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, "MatterControl#Startup", out bool created);

				if (!created)
				{
					// If an instance is already running, create a service proxy and execute ShellOpenFile
					var proxy = new ServiceProxy();

					// and at least one argument is an acceptable shell file extension
					var itemsToAdd = args.Where(f => File.Exists(f) && shellFileExtensions.Contains(Path.GetExtension(f).ToLower()));
					if (itemsToAdd.Any())
					{
						// notify the running instance of the event
						proxy.ShellOpenFile(itemsToAdd.ToArray());
					}

					System.Threading.Thread.Sleep(1000);

					// Finally, close the process spawned by Explorer.exe
					return;
				}

				var serviceHost = new ServiceHost(typeof(LocalService), new[] { new Uri(ServiceBaseUri) });
				serviceHost.AddServiceEndpoint(typeof(IMainService), new NetNamedPipeBinding(), mainServiceName);
				serviceHost.Open();

				Console.Write(
					"Service started: {0};",
					string.Join(", ", serviceHost.Description.Endpoints.Select(s => s.ListenUri.AbsoluteUri).ToArray()));
			}

			// If MatterControl isn't running and valid files were shelled, schedule a StartupAction to open the files after load
			var shellFiles = args.Where(f => File.Exists(f) && shellFileExtensions.Contains(Path.GetExtension(f).ToLower()));
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
			Application.EnableNetworkTraffic = config.GetValue<bool>("MatterControl:Application:EnableNetworkTraffic", true);

			// Make sure we have the right working directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			Datastore.Instance.Initialize(DesktopSqlite.CreateInstance());
#if !DEBUG
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			if (string.IsNullOrEmpty(channel) || channel != "release" || OemSettings.Instance.WindowTitleExtra == "Experimental")
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

			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();

			config.Bind("MatterControl", MatterHackers.MatterControl.AppContext.Options);

			// Get startup bounds from MatterControl and construct system window
			// var systemWindow = new DesktopMainWindow(400, 200)
			var (width, height) = RootSystemWindow.GetStartupBounds();

			var systemWindow = Application.LoadRootWindow(width, height);
			systemWindow.ShowAsSystemWindow();
		}

		private static string[] shellFileExtensions = new string[] { ".stl", ".amf" };

		private static readonly object locker = new object();

		public class LocalService : IMainService
		{
			public void ShellOpenFile(string[] files)
			{
				// If at least one argument is an acceptable shell file extension
				var itemsToAdd = files.Where(f => File.Exists(f)
					&& shellFileExtensions.Contains(Path.GetExtension(f).ToLower()));

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

		public class ServiceProxy : ClientBase<IMainService>
		{
			public ServiceProxy()
				: base(
					new ServiceEndpoint(
						ContractDescription.GetContract(typeof(IMainService)),
						new NetNamedPipeBinding(),
						new EndpointAddress($"{ServiceBaseUri}/{mainServiceName}")))
			{
			}

			public void ShellOpenFile(string[] files)
			{
				Channel.ShellOpenFile(files);
			}
		}
	}

	[ServiceContract]
	public interface IMainService
	{
		[OperationContract]
		void ShellOpenFile(string[] files);
	}
}
