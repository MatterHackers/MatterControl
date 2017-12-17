using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
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

		private static string lastSection = "";

		private static RaygunClient _raygunClient = GetCorrectClient();
		private static Stopwatch timer;
		private static ProgressBar progressBar;
		private static TextWidget statusText;
		private static FlowLayoutWidget progressPanel;

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

			timer = Stopwatch.StartNew();

			// Get startup bounds from MatterControl and construct system window
			//var systemWindow = new DesktopMainWindow(400, 200)
			var (width, height) = DesktopRootSystemWindow.GetStartupBounds();

			var systemWindow = new DesktopRootSystemWindow(width, height)
			{
				BackgroundColor = Color.DarkGray
			};

			progressPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				MinimumSize = new VectorMath.Vector2(400, 100),
			};
			systemWindow.AddChild(progressPanel);

			progressPanel.AddChild(statusText = new TextWidget("XXXXXXXXXXXXX", textColor: new Color("#bbb"))
			{
				MinimumSize = new VectorMath.Vector2(200, 30)
			});

			progressPanel.AddChild(progressBar = new ProgressBar()
			{
				FillColor = new Color("#3D4B72"),
				BorderColor = new Color("#777"),
				Height = 11,
				Width = 300,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute
			});

			AppContext.RootSystemWindow = systemWindow;

			// Hook SystemWindow load and spin up MatterControl once we've hit first draw
			systemWindow.Load += (s, e) =>
			{
				ReportStartupProgress(0.1, "First draw->RunOnIdle");

				//UiThread.RunOnIdle(() =>
				Task.Run(() =>
				{
					ReportStartupProgress(0.5, "Datastore");
					Datastore.Instance.Initialize();

					ReportStartupProgress(0.15, "MatterControlApplication.Initialize");
					var mainView = MatterControlApplication.Initialize(systemWindow, (progress0To1, status) =>
					{
						ReportStartupProgress(0.2 + progress0To1 * 0.7, status);
					});

					ReportStartupProgress(0.9, "AddChild->MainView");
					systemWindow.RemoveAllChildren();
					systemWindow.AddChild(mainView);

					ReportStartupProgress(1, "X9x");

					systemWindow.BackgroundColor = Color.Transparent;
					systemWindow.Invalidate();

					ReportStartupProgress(1.1, "X9x");
				});
			};

			// Block indefinitely
			ReportStartupProgress(0, "ShowAsSystemWindow");
			systemWindow.ShowAsSystemWindow();
		}

		private static void ReportStartupProgress(double progress0To1, string section)
		{
			statusText.Text = section;
			progressBar.RatioComplete = progress0To1;
			progressPanel.Invalidate();

			Console.WriteLine($"Time to '{lastSection}': {timer.ElapsedMilliseconds}");
			timer.Restart();

			lastSection = section;
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
