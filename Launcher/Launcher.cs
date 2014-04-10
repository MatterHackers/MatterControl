using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

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
