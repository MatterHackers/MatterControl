using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

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

            driverInstallerProcess.StartInfo.Arguments = "/a {0}".FormatWith(Path.GetFullPath(pathAndDriverToInstall));

            driverInstallerProcess.StartInfo.CreateNoWindow = true;
            driverInstallerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            string pnpUtilFileName = "PnPUtil.exe";
            
            string pnPUtilPathAndFileName = Path.Combine("C:/WIndows/winsxs/amd64_microsoft-windows-pnputil_31bf3856ad364e35_6.1.7600.16385_none_5958b438d6388d15", pnpUtilFileName);

            // Disable redirection  
            IntPtr ptr = new IntPtr();
            bool isWow64FsRedirectionDisabled = Wow64DisableWow64FsRedirection(ref ptr);
            if (isWow64FsRedirectionDisabled)
            {
                pnPUtilPathAndFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), pnpUtilFileName);
            }

            driverInstallerProcess.StartInfo.FileName = pnPUtilPathAndFileName;
            driverInstallerProcess.StartInfo.Verb = "runas";
            driverInstallerProcess.StartInfo.UseShellExecute = true;

            driverInstallerProcess.Start();

            driverInstallerProcess.WaitForExit();

            // Restore redirection
            Wow64RevertWow64FsRedirection(ptr);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length > 0 && File.Exists(args[0]))
            {
                InfInstallerApp driverInstaller = new InfInstallerApp();
                driverInstaller.InstallInfDriverFile(args[0]);
            }
        }
    }
}
