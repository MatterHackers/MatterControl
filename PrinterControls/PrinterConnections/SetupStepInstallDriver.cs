/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepInstallDriver : DialogPage
	{
		private static List<string> printerDrivers = null;

		private TextWidget printerDriverMessage;

		private Button installButton;
		private Button skipButton;

		private PrinterConfig printer;

		public SetupStepInstallDriver(PrinterConfig printer)
		{
			this.printer = printer;
			this.HeaderText = "Install Communication Driver";

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 5),
				HAnchor = HAnchor.Stretch
			};

			printerDriverMessage = new TextWidget("This printer requires a driver for communication.".Localize(), 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			};

			var printerDriverMessageTwo = new TextWidget("Driver located. Would you like to install?".Localize(), 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			};

			container.AddChild(printerDriverMessage);
			container.AddChild(printerDriverMessageTwo);

			contentRow.AddChild(container);

			//Construct buttons
			installButton = textImageButtonFactory.Generate("Install Driver".Localize());
			installButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					bool canContinue = this.InstallDriver();
					if (canContinue)
					{
						this.ChangeToSetupBaudOrComPortOne();
					}
				});
			};

			skipButton = textImageButtonFactory.Generate("Skip".Localize());
			skipButton.Click += (s, e) => this.ChangeToSetupBaudOrComPortOne();

			this.AddPageAction(installButton);
			this.AddPageAction(skipButton);
		}

		private void ChangeToSetupBaudOrComPortOne()
		{
			if (string.IsNullOrEmpty(printer.Settings.GetValue(SettingsKey.baud_rate)))
			{
				this.WizardWindow.ChangeToPage(new SetupStepBaudRate(printer));
			}
			else
			{
				this.WizardWindow.ChangeToPage(new SetupStepComPortOne(printer));
			}
		}

		private void InstallDriver(string fileName)
		{
			switch (AggContext.OperatingSystem)
			{
				case OSType.Windows:
					if (File.Exists(fileName))
					{
						if (Path.GetExtension(fileName).ToUpper() == ".INF")
						{
							Process driverInstallerProcess = new Process();
							// Prepare the process to run

							// Enter in the command line arguments, everything you would enter after the executable name itself
							driverInstallerProcess.StartInfo.Arguments = Path.GetFullPath(fileName);

							// Enter the executable to run, including the complete path
							string printerDriverInstallerExePathAndFileName = Path.GetFullPath(Path.Combine(".", "InfInstaller.exe"));

							driverInstallerProcess.StartInfo.CreateNoWindow = true;
							driverInstallerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

							driverInstallerProcess.StartInfo.FileName = Path.GetFullPath(printerDriverInstallerExePathAndFileName);
							driverInstallerProcess.StartInfo.Verb = "runas";
							driverInstallerProcess.StartInfo.UseShellExecute = true;

							driverInstallerProcess.Start();
							driverInstallerProcess.WaitForExit();
						}
						else
						{
							Process.Start(fileName);
						}
					}
					else
					{
						throw new Exception(string.Format("Can't find driver {0}.", fileName));
					}
					break;

				case OSType.Mac:
					break;

				case OSType.X11:
					if (File.Exists(fileName))
					{
						if (Path.GetExtension(fileName).ToUpper() == ".INF")
						{
							var driverInstallerProcess = new Process();
							// Prepare the process to run

							// Enter in the command line arguments, everything you would enter after the executable name itself
							driverInstallerProcess.StartInfo.Arguments = Path.GetFullPath(fileName);

							// Enter the executable to run, including the complete path
							string printerDriverInstallerExePathAndFileName = Path.Combine(".", "InfInstaller.exe");

							driverInstallerProcess.StartInfo.CreateNoWindow = true;
							driverInstallerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

							driverInstallerProcess.StartInfo.FileName = Path.GetFullPath(printerDriverInstallerExePathAndFileName);
							driverInstallerProcess.StartInfo.Verb = "runas";
							driverInstallerProcess.StartInfo.UseShellExecute = true;

							driverInstallerProcess.Start();

							driverInstallerProcess.WaitForExit();

							// Retrieve the app's exit code
							var exitCode = driverInstallerProcess.ExitCode;
						}
						else
						{
							Process.Start(fileName);
						}
					}
					else
					{
						throw new Exception("Can't find driver: " + fileName);
					}
					break;
			}
		}

		public static List<string> PrinterDrivers(PrinterConfig printer)
		{
			if (printerDrivers == null)
			{
				printerDrivers = GetPrintDrivers(printer);
			}

			return printerDrivers;
		}

		private static List<string> GetPrintDrivers(PrinterConfig printer)
		{
			var drivers = new List<string>();

			//Determine what if any drivers are needed
			string infFileNames = printer.Settings.GetValue(SettingsKey.windows_driver);
			if (!string.IsNullOrEmpty(infFileNames))
			{
				foreach (string fileName in infFileNames.Split(','))
				{
					switch (AggContext.OperatingSystem)
					{
						case OSType.Windows:

							string pathForInf = Path.GetFileNameWithoutExtension(fileName);

							// TODO: It's really unexpected that the driver gets copied to the temp folder every time a printer is setup. I'd think this only needs
							// to happen when the infinstaller is run (More specifically - move this to *after* the user clicks Install Driver)

							string infPath = Path.Combine("Drivers", pathForInf);
							string infPathAndFileToInstall = Path.Combine(infPath, fileName);

							if (AggContext.StaticData.FileExists(infPathAndFileToInstall))
							{
								// Ensure the output directory exists
								string destTempPath = Path.GetFullPath(Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "inf", pathForInf));
								if (!Directory.Exists(destTempPath))
								{
									Directory.CreateDirectory(destTempPath);
								}

								string destTempInf = Path.GetFullPath(Path.Combine(destTempPath, fileName));

								// Sync each file from StaticData to the location on disk for serial drivers
								foreach (string file in AggContext.StaticData.GetFiles(infPath))
								{
									using (Stream outstream = File.OpenWrite(Path.Combine(destTempPath, Path.GetFileName(file))))
									using (Stream instream = AggContext.StaticData.OpenSteam(file))
									{
										instream.CopyTo(outstream);
									}
								}

								drivers.Add(destTempInf);
							}
							break;

						default:
							break;
					}
				}
			}

			return drivers;
		}

		private bool InstallDriver()
		{
			try
			{
				printerDriverMessage.Text = "Installing".Localize() + "...";

				foreach (string driverPath in PrinterDrivers(printer))
				{
					InstallDriver(driverPath);
				}

				return true;
			}
			catch (Exception)
			{
				printerDriverMessage.Text = "Sorry, we were unable to install the driver.".Localize();
				return false;
			}
		}
	}
}