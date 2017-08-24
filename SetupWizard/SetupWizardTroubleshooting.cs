using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
#if __ANDROID__
using Com.Hoho.Android.Usbserial.Driver;
using Android.Hardware.Usb;
using Android.Content;
#endif

namespace MatterHackers.MatterControl
{
	public class SetupWizardTroubleshooting : WizardPage
	{
		private Button nextButton;

		private EventHandler unregisterEvents;

		private CriteriaRow connectToPrinterRow;

		// Used in Android
		private System.Threading.Timer checkForPermissionTimer;

#if __ANDROID__
		private static UsbManager usbManager
		{
			get { return (UsbManager) Android.App.Application.Context.ApplicationContext.GetSystemService(Context.UsbService); }
		}
#endif

		public SetupWizardTroubleshooting()
		{
			this.WindowTitle = "Troubleshooting".Localize();

			RefreshStatus();

			cancelButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				abortCancel = true;
				this.WizardWindow.ChangeToPage<AndroidConnectDevicePage>();
			});
			
			nextButton = textImageButtonFactory.Generate("Continue".Localize());
			nextButton.Click += (sender, e) => UiThread.RunOnIdle(this.WizardWindow.Close);
			nextButton.Visible = false;

			this.AddPageAction(nextButton);

			// Register for connection notifications
			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(ConnectionStatusChanged, ref unregisterEvents);
		}

		public void ConnectionStatusChanged(object test, EventArgs args)
		{
			if(PrinterConnection.Instance.CommunicationState == CommunicationStates.Connected && connectToPrinterRow != null)
			{
				connectToPrinterRow.SetSuccessful();
				nextButton.Visible = true;
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if(checkForPermissionTimer != null)
			{
				checkForPermissionTimer.Dispose();
			}

			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void RefreshStatus()
		{
			CriteriaRow.ResetAll();

			// Clear the main container
			contentRow.CloseAllChildren();

			// Regen and refresh the troubleshooting criteria
			TextWidget printerNameLabel = new TextWidget(string.Format ("{0}:", "Connection Troubleshooting".Localize()), 0, 0, labelFontSize);
			printerNameLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerNameLabel.Margin = new BorderDouble(bottom: 10);

#if __ANDROID__
			IUsbSerialPort serialPort = FrostedSerialPort.LoadSerialDriver(null);

#if ANDROID7
			// Filter out the built-in 002 device and select the first item from the list
			// On the T7 Android device, there is a non-printer device always registered at usb/002/002 that must be ignored
			UsbDevice usbPrintDevice = usbManager.DeviceList.Values.Where(d => d.DeviceName != "/dev/bus/usb/002/002").FirstOrDefault();
#else
			UsbDevice usbPrintDevice = usbManager.DeviceList.Values.FirstOrDefault();
#endif

			UsbStatus usbStatus = new UsbStatus () {
				IsDriverLoadable = (serialPort != null),
				HasUsbDevice = true,
				HasUsbPermission = false,
				AnyUsbDeviceExists = usbPrintDevice != null
			};

			if (!usbStatus.IsDriverLoadable)
			{
				usbStatus.HasUsbDevice = usbPrintDevice != null;

				if (usbStatus.HasUsbDevice) {
					// TODO: Testing specifically for UsbClass.Comm seems fragile but no better alternative exists without more research
					usbStatus.UsbDetails = new UsbDeviceDetails () {
						ProductID = usbPrintDevice.ProductId,
						VendorID = usbPrintDevice.VendorId,
						DriverClass = usbManager.DeviceList.Values.First ().DeviceClass == Android.Hardware.Usb.UsbClass.Comm ? "cdcDriverType" : "ftdiDriverType"
					};
					usbStatus.Summary = string.Format ("No USB device definition found. Click the 'Fix' button to add an override for your device ", usbStatus.UsbDetails.VendorID, usbStatus.UsbDetails.ProductID);
				}
			}

			usbStatus.HasUsbPermission = usbStatus.IsDriverLoadable && FrostedSerialPort.HasPermissionToDevice(serialPort);

			contentRow.AddChild(printerNameLabel);

			contentRow.AddChild(new CriteriaRow(
				"USB Connection", 
				"Retry",
				"No USB device found. Check and reseat cables and try again",
				usbStatus.AnyUsbDeviceExists, 
				() => UiThread.RunOnIdle(RefreshStatus)));

			contentRow.AddChild(new CriteriaRow(
				"USB Driver", 
				"Fix",
				usbStatus.Summary,
				usbStatus.IsDriverLoadable, 
				() => { 
					string overridePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "usboverride.local");
					UsbDeviceDetails usbDetails = usbStatus.UsbDetails;
					File.AppendAllText(overridePath, string.Format("{0},{1},{2}\r\n", usbDetails.VendorID, usbDetails.ProductID, usbDetails.DriverClass));

					UiThread.RunOnIdle(() => RefreshStatus());
				}));

			contentRow.AddChild(new CriteriaRow(
				"USB Permission", 
				"Request Permission",
				"Click the 'Request Permission' button to gain Android access rights",
				usbStatus.HasUsbPermission , 
				() => { 

					if(checkForPermissionTimer == null)
					{
						checkForPermissionTimer = new System.Threading.Timer((state) => {

							if(FrostedSerialPort.HasPermissionToDevice(serialPort))
							{
								UiThread.RunOnIdle(this.RefreshStatus);
								checkForPermissionTimer.Dispose();
							}
						}, null, 200, 200);
					}

					FrostedSerialPort.RequestPermissionToDevice(serialPort);
				}));

#endif
			connectToPrinterRow = new CriteriaRow(
				"Connect to Printer",
				"Connect",
				"Click the 'Connect' button to retry the original connection attempt",
				false,
				() => PrinterConnection.Instance.ConnectToActivePrinter());

			contentRow.AddChild(connectToPrinterRow);

			if (CriteriaRow.ActiveErrorItem != null) {

				FlowLayoutWidget errorText = new FlowLayoutWidget () {
					Padding = new BorderDouble (0, 15)
				};

				errorText.AddChild(new TextWidget(CriteriaRow.ActiveErrorItem.ErrorText) {
					TextColor = ActiveTheme.Instance.PrimaryAccentColor
				});

				contentRow.AddChild(errorText);
			}
		}

		private class CriteriaRow : FlowLayoutWidget
		{
			public static CriteriaRow ActiveErrorItem { get; private set; }

			public string ErrorText { get; private set; }

			private static bool stillSuccessful = true;

			private static int criteriaCount = 0;

			private static RGBA_Bytes disabledTextColor = new RGBA_Bytes(0.35, 0.35, 0.35);
			private static RGBA_Bytes disabledBackColor = new RGBA_Bytes(0.22, 0.22, 0.22);
			private static RGBA_Bytes toggleColor = new RGBA_Bytes(RGBA_Bytes.Gray.red + 2, RGBA_Bytes.Gray.green + 2, RGBA_Bytes.Gray.blue + 2);

			public CriteriaRow (string itemText, string fixitText, string errorText, bool succeeded, Action fixAction) 
				: base(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch;
				VAnchor = VAnchor.Absolute;
				TextImageButtonFactory buttonFactory = new TextImageButtonFactory();

				ErrorText = errorText;

				base.Height = 40;

				base.AddChild(new TextWidget (string.Format("  {0}. {1}", criteriaCount + 1, itemText)){
					TextColor = stillSuccessful ? RGBA_Bytes.White : disabledTextColor,
					VAnchor = VAnchor.Center
				});

				if(stillSuccessful && !succeeded)
				{
					ActiveErrorItem = this;
				}

				base.AddChild(new HorizontalSpacer());

				if(stillSuccessful) {
					if(succeeded)
					{
						// Add checkmark image
						AddSuccessIcon();
					} else {
						// Add Fix button
						Button button  = buttonFactory.Generate(fixitText.Localize());
						button.VAnchor = VAnchor.Center;
						button.Padding = new BorderDouble(3, 8);
						button.Click += (sender, e) => fixAction();
						base.AddChild(button);
					}
				}

				if(stillSuccessful) 
				{
					this.BackgroundColor = (criteriaCount % 2 == 0) ? RGBA_Bytes.Gray : toggleColor;
				}
				else
				{
					this.BackgroundColor = disabledBackColor;
				}

				stillSuccessful &= succeeded;

				criteriaCount++;
			}

			public void SetSuccessful()
			{
				this.RemoveChild (this.Children.Last ());
				ActiveErrorItem = null;
				AddSuccessIcon();
			}

			public static void ResetAll()
			{
				criteriaCount = 0;
				stillSuccessful = true;
				ActiveErrorItem = null;
			}

			private void AddSuccessIcon()
			{
				base.AddChild (new ImageWidget (AggContext.StaticData.LoadImage (Path.Combine ("Icons", "426.png"))) {
					VAnchor = VAnchor.Center
				});
			}

		}

		private class UsbStatus
		{
			public bool HasUsbDevice { get; set; }
			public bool IsDriverLoadable { get; set; }
			public string Summary { get; set; }
			public bool HasUsbPermission { get; set; }
			public bool AnyUsbDeviceExists { get ; set; }
			public UsbDeviceDetails UsbDetails { get; set; }
		}

		private class UsbDeviceDetails
		{
			public int VendorID { get; set; }
			public int ProductID { get; set; }
			public string DriverClass { get; set; }
		}
	}
}
