using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using Zeroconf;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Agg.Platform;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	class IpAddessField : UIField
	{
		private DropDownList dropdownList;
		private IconButton refreshButton;

		private PrinterConfig printer;

		public IpAddessField(PrinterConfig printer)
		{
			this.printer = printer;
		}

		public override void Initialize(int tabIndex)
		{
			EventHandler unregisterEvents = null;

			var theme = ApplicationController.Instance.Theme;

			base.Initialize(tabIndex);
			bool canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;
			//This setting defaults to Manual
			var selectedMachine = printer.Settings.GetValue(SettingsKey.selector_ip_address);
			dropdownList = new DropDownList(selectedMachine, theme.Colors.PrimaryTextColor, maxHeight: 200, pointSize: theme.DefaultFontSize)
			{
				ToolTipText = HelpText,
				Margin = new BorderDouble(),
				TabIndex = tabIndex,

				Enabled = canChangeComPort,
				TextColor = canChangeComPort ? theme.Colors.PrimaryTextColor : new Color(theme.Colors.PrimaryTextColor, 150),
				BorderColor = canChangeComPort ? theme.Colors.SecondaryTextColor : new Color(theme.Colors.SecondaryTextColor, 150),


			};

			//Create default option
			MenuItem defaultOption = dropdownList.AddItem("Manual", "127.0.0.1:23");
			defaultOption.Selected += (sender, e) =>
			{
				printer.Settings.SetValue(SettingsKey.selector_ip_address, defaultOption.Text);
			};
			UiThread.RunOnIdle(RebuildMenuItems);

			// Prevent droplist interaction when connected
			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;
				dropdownList.Enabled = canChangeComPort;
				dropdownList.TextColor = canChangeComPort ? theme.Colors.PrimaryTextColor : new Color(theme.Colors.PrimaryTextColor, 150);
				dropdownList.BorderColor = canChangeComPort ? theme.Colors.SecondaryTextColor : new Color(theme.Colors.SecondaryTextColor, 150);
			}, ref unregisterEvents);

			// Release event listener on close
			dropdownList.Closed += (s, e) =>
			{
				unregisterEvents?.Invoke(null, null);
			};

			var widget = new FlowLayoutWidget();
			widget.AddChild(dropdownList);
			refreshButton = new IconButton(AggContext.StaticData.LoadIcon("fa-refresh_14.png", IconColor.Theme), ApplicationController.Instance.Theme)
			{
				Margin = new BorderDouble(left: 5)
			};

			refreshButton.Click += (s, e) => RebuildMenuItems();
			widget.AddChild(refreshButton);

			this.Content = widget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			dropdownList.SelectedLabel = this.Value;
			base.OnValueChanged(fieldChangedEventArgs);
		}

		private async void RebuildMenuItems()
		{
			refreshButton.Enabled = false;
			IReadOnlyList<Zeroconf.IZeroconfHost> possibleHosts = await ProbeForNetworkedTelenetConnections();
			dropdownList.MenuItems.Clear();

			MenuItem defaultOption = dropdownList.AddItem("Manual", "127.0.0.1:23");
			defaultOption.Selected += (sender, e) =>
			{
				printer.Settings.SetValue(SettingsKey.selector_ip_address,defaultOption.Text);
			};

			foreach (Zeroconf.IZeroconfHost host in possibleHosts)
			{
				// Add each found telnet host to the dropdown list 
				IService service;
				bool exists = host.Services.TryGetValue("_telnet._tcp.local.", out service);
				int port = exists ? service.Port:23;
				MenuItem newItem = dropdownList.AddItem(host.DisplayName, $"{host.IPAddress}:{port}"); //The port may be unnecessary
				// When the given menu item is selected, save its value back into settings
				newItem.Selected += (sender, e) =>
				{
					if (sender is MenuItem menuItem)
					{
						//this.SetValue(
						//	menuItem.Text,
						//	userInitiated: true);
						string[] ipAndPort = menuItem.Value.Split(':');
						printer.Settings.SetValue(SettingsKey.ip_address, ipAndPort[0]);
						printer.Settings.SetValue(SettingsKey.ip_port, ipAndPort[1]);
						printer.Settings.SetValue(SettingsKey.selector_ip_address, menuItem.Text);
					}
				};
			}
			refreshButton.Enabled = true;
		}

		private void DefaultOption_Selected(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		public static async Task<IReadOnlyList<Zeroconf.IZeroconfHost>> ProbeForNetworkedTelenetConnections()
		{
			return await ZeroconfResolver.ResolveAsync("_telnet._tcp.local.");
		}

		public static async Task<IReadOnlyList<Zeroconf.IZeroconfHost>> EnumerateAllServicesFromAllHosts()
		{
			ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync();
			return await ZeroconfResolver.ResolveAsync(domains.Select(g => g.Key));
		}
	}
}
