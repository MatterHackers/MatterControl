using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MatterHackers.InfInstaller
{
	public class InfInstallerApp
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

		public InfInstallerApp()
		{
#if DEBUG
			//Debugger.Launch();
#endif
		}

		public void InstallInfDriverFile(string pathAndDriverToInstall)
		{
			Process driverInstallerProcess = new Process();

			driverInstallerProcess.StartInfo.Arguments = string.Format("-a \"{0}\"", Path.GetFullPath(pathAndDriverToInstall));

			driverInstallerProcess.StartInfo.CreateNoWindow = true;
			driverInstallerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

			string pnpUtilFileName = "PnPUtil.exe";

			string pnPUtilPathAndFileName = Path.Combine("C:/Windows/winsxs/amd64_microsoft-windows-pnputil_31bf3856ad364e35_6.1.7600.16385_none_5958b438d6388d15", pnpUtilFileName);
			bool fileExists = File.Exists(pnPUtilPathAndFileName);

			if (!fileExists)
			{
				// Disable redirection
				IntPtr ptr = new IntPtr();
				bool isWow64FsRedirectionDisabled = Wow64DisableWow64FsRedirection(ref ptr);
				if (isWow64FsRedirectionDisabled)
				{
					pnPUtilPathAndFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), pnpUtilFileName);
				}
			}

			driverInstallerProcess.StartInfo.FileName = pnPUtilPathAndFileName;
			driverInstallerProcess.StartInfo.Verb = "runas";
			driverInstallerProcess.StartInfo.UseShellExecute = false;

			driverInstallerProcess.Start();
			driverInstallerProcess.WaitForExit();

			if (!fileExists)
			{
				// Restore redirection
				IntPtr ptr = new IntPtr();
				Wow64RevertWow64FsRedirection(ptr);
			}
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

			if (args.Length > 0 && File.Exists(args[0]))
			{
				InfInstallerApp driverInstaller = new InfInstallerApp();
				driverInstaller.InstallInfDriverFile(args[0]);
			}
		}
	}
}