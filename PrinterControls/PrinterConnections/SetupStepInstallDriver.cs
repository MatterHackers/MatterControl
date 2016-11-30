using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepInstallDriver : ConnectionWizardPage
	{
		private static List<string> printerDrivers = null;

		private FlowLayoutWidget printerDriverContainer;
		private TextWidget printerDriverMessage;

		private Button installButton;
		private Button skipButton;

		public SetupStepInstallDriver()
		{
			headerLabel.Text = string.Format(LocalizedString.Get("Install Communication Driver"));
			printerDriverContainer = createPrinterDriverContainer();
			contentRow.AddChild(printerDriverContainer);
			{
				//Construct buttons
				installButton = textImageButtonFactory.Generate(LocalizedString.Get("Install Driver"));
				installButton.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						bool canContinue = this.InstallDriver();
						if (canContinue)
						{
							WizardWindow.ChangeToSetupBaudOrComPortOne();
						}
					});
				};

				skipButton = textImageButtonFactory.Generate(LocalizedString.Get("Skip"));
				skipButton.Click += (s, e) => WizardWindow.ChangeToSetupBaudOrComPortOne();

				//Add buttons to buttonContainer
				footerRow.AddChild(installButton);
				footerRow.AddChild(skipButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
		}

		private FlowLayoutWidget createPrinterDriverContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			printerDriverMessage = new TextWidget(LocalizedString.Get("This printer requires a driver for communication."), 0, 0, 10);
			printerDriverMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerDriverMessage.HAnchor = HAnchor.ParentLeftRight;
			printerDriverMessage.Margin = elementMargin;

			TextWidget printerDriverMessageTwo = new TextWidget(LocalizedString.Get("Driver located. Would you like to install?"), 0, 0, 10);
			printerDriverMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerDriverMessageTwo.HAnchor = HAnchor.ParentLeftRight;
			printerDriverMessageTwo.Margin = elementMargin;

			container.AddChild(printerDriverMessage);
			container.AddChild(printerDriverMessageTwo);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void InstallDriver(string fileName)
		{
			switch (OsInformation.OperatingSystem)
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

		public static List<string> PrinterDrivers()
		{
			if (printerDrivers == null)
			{
				printerDrivers = GetPrintDrivers();
			}

			return printerDrivers;
		}

		private static List<string> GetPrintDrivers()
		{
			var drivers = new List<string>();

			//Determine what if any drivers are needed
			string infFileNames = ActiveSliceSettings.Instance.GetValue(SettingsKey.windows_driver);
			if (!string.IsNullOrEmpty(infFileNames))
			{
				string[] fileNames = infFileNames.Split(',');
				foreach (string fileName in fileNames)
				{
					switch (OsInformation.OperatingSystem)
					{
						case OSType.Windows:

							string pathForInf = Path.GetFileNameWithoutExtension(fileName);

							// TODO: It's really unexpected that the driver gets copied to the temp folder every time a printer is setup. I'd think this only needs
							// to happen when the infinstaller is run (More specifically - move this to *after* the user clicks Install Driver)

							string infPath = Path.Combine("Drivers", pathForInf);
							string infPathAndFileToInstall = Path.Combine(infPath, fileName);

							if (StaticData.Instance.FileExists(infPathAndFileToInstall))
							{
								// Ensure the output directory exists
								string destTempPath = Path.GetFullPath(Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "inf", pathForInf));
								if (!Directory.Exists(destTempPath))
								{
									Directory.CreateDirectory(destTempPath);
								}

								string destTempInf = Path.GetFullPath(Path.Combine(destTempPath, fileName));

								// Sync each file from StaticData to the location on disk for serial drivers
								foreach (string file in StaticData.Instance.GetFiles(infPath))
								{
									using (Stream outstream = File.OpenWrite(Path.Combine(destTempPath, Path.GetFileName(file))))
									using (Stream instream = StaticData.Instance.OpenSteam(file))
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

				foreach (string driverPath in PrinterDrivers())
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