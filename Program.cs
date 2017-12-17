using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using Mindscape.Raygun4Net;

namespace MatterHackers.MatterControl
{
	static class Program
	{
		private const int RaygunMaxNotifications = 15;

		private static int raygunNotificationCount = 0;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		public static void Main()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			AggContext.Init(embeddedResourceName: "config.json");

			// Make sure we have the right working directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));


#if !DEBUG
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			if (string.IsNullOrEmpty(channel) || channel != "release" || OemSettings.Instance.WindowTitleExtra == "Experimental")
#endif
			{
				System.Windows.Forms.Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
			}

			var timer = Stopwatch.StartNew();

			// Get startup bounds from MatterControl and construct system window
			//var systemWindow = new DesktopMainWindow(400, 200)
			var (width, height) = DesktopRootSystemWindow.GetStartupBounds();

			var systemWindow = new DesktopRootSystemWindow(width, height)
			{
				BackgroundColor = Color.DarkGray
			};

			AppContext.RootSystemWindow = systemWindow;

			// Hook SystemWindow load and spin up MatterControl once we've hit first draw
			systemWindow.Load += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					Datastore.Instance.Initialize();

					var mainView = MatterControlApplication.Initialize(systemWindow, ReportStartupProgress);

					Console.WriteLine("Time to MatterControlApplication.Instance init: " + timer.Elapsed.TotalSeconds);

					systemWindow.RemoveAllChildren();

					systemWindow.AddChild(mainView);

					Console.WriteLine("Time to MatterControlApplication Layout: " + timer.Elapsed.TotalSeconds);
				});
			};

			// Block indefinitely
			systemWindow.ShowAsSystemWindow();
		}

		private static void ReportStartupProgress(string status)
		{
			Console.WriteLine(status);
		}


		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
#if !DEBUG
			if(raygunNotificationCount++ < RaygunMaxNotifications)
			{
				_raygunClient.Send(e.Exception);
			}
#endif
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
#if !DEBUG
			if(raygunNotificationCount++ < RaygunMaxNotifications)
			{
				_raygunClient.Send(e.ExceptionObject as Exception);
			}
#endif
		}

		private static RaygunClient _raygunClient = GetCorrectClient();

		private static RaygunClient GetCorrectClient()
		{
			if (AggContext.OperatingSystem == OSType.Mac)
			{
				return new RaygunClient("qmMBpKy3OSTJj83+tkO7BQ=="); // this is the Mac key
			}
			else
			{
				return new RaygunClient("hQIlyUUZRGPyXVXbI6l1dA=="); // this is the PC key
			}
		}

		// ** Standard Winforms Main ** //
		//[STAThread]
		//static void Main()
		//{
		//	Application.EnableVisualStyles();
		//	Application.SetCompatibleTextRenderingDefault(false);
		//	Application.Run(new Form1());
		//}
	}
}
