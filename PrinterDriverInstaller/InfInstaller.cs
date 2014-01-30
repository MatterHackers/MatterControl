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
        [DllImport("Setupapi.dll", EntryPoint = "InstallHinfSection", CallingConvention = CallingConvention.StdCall)]
        public static extern void InstallHinfSection(
            [In] IntPtr hwnd,
            [In] IntPtr ModuleHandle,
            [In, MarshalAs(UnmanagedType.LPWStr)] string CmdLineBuffer,
            int nCmdShow);

        public InfInstallerApp()
        {
        }

        public void InstallInfDriverFile(string pathAndDriverToInstall)
        {
            InstallHinfSection(IntPtr.Zero, IntPtr.Zero, pathAndDriverToInstall, 0);
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
