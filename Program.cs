using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
	static class Program
	{
		
		// ** Standard Winforms Main ** //
		//[STAThread]
		//static void Main()
		//{
		//	Application.EnableVisualStyles();
		//	Application.SetCompatibleTextRenderingDefault(false);
		//	Application.Run(new Form1());
		//}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		public static void Main()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// Make sure we have the right working directory as we assume everything relative to the executable.
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			Datastore.Instance.Initialize();

#if !DEBUG
			// Conditionally spin up error reporting if not on the Stable channel
			string channel = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
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
	}
}
