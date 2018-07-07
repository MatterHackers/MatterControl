using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace MatterHackers.MatterControl.Launcher
{
	public class LauncherApp
	{
		public LauncherApp()
		{
		}

		[STAThread]
		public static void Main(string[] args)
		{
			// this sets the global culture for the app and all new threads
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

			// and make sure the app is set correctly
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

			if (args.Length == 2 && File.Exists(args[0]))
			{
				ProcessStartInfo runAppLauncherStartInfo = new ProcessStartInfo();
				runAppLauncherStartInfo.FileName = args[0];

				int timeToWait = 0;
				int.TryParse(args[1], out timeToWait);

				Stopwatch waitTime = new Stopwatch();
				waitTime.Start();
				while (waitTime.ElapsedMilliseconds < timeToWait)
				{
				}

				Process.Start(runAppLauncherStartInfo);
			}
		}
	}
}