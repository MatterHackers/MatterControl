using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class SetupStepInstallDriver : SetupConnectionWidgetBase
    {
        FlowLayoutWidget printerDriverContainer;
        TextWidget printerDriverMessage;
        string printerDriverFilePath;

        //bool driverInstallFinished;

        Button installButton;
        Button skipButton;

        public SetupStepInstallDriver(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinterStatus)
            : base(windowController, containerWindowToClose, setupPrinterStatus)
        {
            this.printerDriverFilePath = this.PrinterSetupStatus.DriverFilePath;

			headerLabel.Text = string.Format(LocalizedString.Get("Install Communication Driver"));
            printerDriverContainer = createPrinterDriverContainer();
            contentRow.AddChild(printerDriverContainer);
            {
                //Construct buttons
				installButton = textImageButtonFactory.Generate(LocalizedString.Get("Install Driver"));
                installButton.Click += new ButtonBase.ButtonEventHandler(installButton_Click);

				skipButton = textImageButtonFactory.Generate(LocalizedString.Get("Skip"));
                skipButton.Click += new ButtonBase.ButtonEventHandler(skipButton_Click);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                //Add buttons to buttonContainer
                footerRow.AddChild(installButton);
                footerRow.AddChild(skipButton);
                footerRow.AddChild(hSpacer);

                footerRow.AddChild(cancelButton);
            }
        }

        void installButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            bool canContinue = this.OnSave();
            if (canContinue)
            {
                UiThread.RunOnIdle(MoveToNextWidget);
            }
        }

        void skipButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(MoveToNextWidget);
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
			container.AddChild (printerDriverMessageTwo);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        void MoveToNextWidget(object state)
        {
            // you can call this like this
            //             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));

            if (this.ActivePrinter.BaudRate == null)
            {
                Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else
            {
                Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
        }

        void InstallDriver(string fileName)
        {
            switch (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType())
            {
                case Agg.UI.WindowsFormsAbstract.OSType.Windows:
                    if (File.Exists(fileName))
                    {
                        if (Path.GetExtension(fileName).ToUpper() == ".INF")
                        {
                            var driverInstallerProcess = new Process();
                            // Prepare the process to run
                            // Enter in the command line arguments, everything you would enter after the executable name itself
                            driverInstallerProcess.StartInfo.Arguments = System.IO.Path.GetFullPath(fileName);
                            // Enter the executable to run, including the complete path
                            string printerDriverInstallerExePathAndFileName = Path.Combine(".", "InfInstaller.exe");

                            driverInstallerProcess.StartInfo.CreateNoWindow = true;
                            driverInstallerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                            driverInstallerProcess.StartInfo.FileName = System.IO.Path.GetFullPath(printerDriverInstallerExePathAndFileName);
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
                        throw new Exception(string.Format("Can't find dirver {0}.", fileName));
                    }
                    break;

                case Agg.UI.WindowsFormsAbstract.OSType.Mac:
                    break;
            }
        }

        bool OnSave()
        {
            try
            {
				string printerDriverMessageLabel = LocalizedString.Get("Installing");
				string printerDriverMessageLabelFull = string.Format("{0}...", printerDriverMessageLabel);
				printerDriverMessage.Text = printerDriverMessageLabelFull;
                InstallDriver(this.printerDriverFilePath);
                return true;
            }
            catch
            {
				printerDriverMessage.Text = LocalizedString.Get("Sorry, we were unable to install the driver.");
                return false;
            }
        }

    }
}
