using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using Zeroconf;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	class IpAddessField : UIField
	{
		private DropDownList dropdownList;
		private IconButton refreshButton;

		private PrinterConfig printer;
		private ThemeConfig theme;

		public IpAddessField(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.theme = theme;
		}

		public override void Initialize(int tabIndex)
		{
			base.Initialize(tabIndex);
			bool canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;
			//This setting defaults to Manual
			var selectedMachine = printer.Settings.GetValue(SettingsKey.selector_ip_address);
			dropdownList = new MHDropDownList(selectedMachine, theme, maxHeight: 200)
			{
				ToolTipText = HelpText,
				Margin = new BorderDouble(),
				TabIndex = tabIndex,

				Enabled = canChangeComPort,
				TextColor = canChangeComPort ? theme.TextColor : new Color(theme.TextColor, 150),
			};

			//Create default option
			MenuItem defaultOption = dropdownList.AddItem("Manual", "127.0.0.1:23");
			defaultOption.Selected += (sender, e) =>
			{
				printer.Settings.SetValue(SettingsKey.selector_ip_address, defaultOption.Text);
			};
			UiThread.RunOnIdle(RebuildMenuItems);

			// Prevent droplist interaction when connected
			void CommunicationStateChanged(object s, EventArgs e)
			{
				canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;
				dropdownList.TextColor = theme.TextColor;
				dropdownList.Enabled = canChangeComPort;
			}
			printer.Connection.CommunicationStateChanged += CommunicationStateChanged;
			dropdownList.Closed += (s, e) => printer.Connection.CommunicationStateChanged -= CommunicationStateChanged;

			var widget = new FlowLayoutWidget();
			widget.AddChild(dropdownList);
			refreshButton = new IconButton(AggContext.StaticData.LoadIcon("fa-refresh_14.png", theme.InvertIcons), theme)
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
