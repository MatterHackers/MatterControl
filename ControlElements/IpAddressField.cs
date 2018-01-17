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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ControlElements
{
	static class IpAddressField
	{
		public static async void RebuildMenuItems(Button refreshButton, DropDownList dropDownList)
		{
			refreshButton.Enabled = false;
			IReadOnlyList<Zeroconf.IZeroconfHost> possibleHosts = await ProbeForNetworkedTelenetConnections();
			dropDownList.MenuItems.Clear();

			MenuItem defaultOption = dropDownList.AddItem("Manual", "127.0.0.1:23");
			defaultOption.Selected += (sender, e) =>
			{
				ActiveSliceSettings.Instance.SetValue(SettingsKey.selector_ip_address, defaultOption.Text);
			};

			foreach (Zeroconf.IZeroconfHost host in possibleHosts)
			{
				// Add each found telnet host to the dropdown list 
				IService service;
				bool exists = host.Services.TryGetValue("_telnet._tcp.local.", out service);
				int port = exists ? service.Port : 23;
				MenuItem newItem = dropDownList.AddItem(host.DisplayName, $"{host.IPAddress}:{port}"); //The port may be unnecessary
																									   // When the given menu item is selected, save its value back into settings
				newItem.Selected += (sender, e) =>
				{
					if (sender is MenuItem menuItem)
					{
						//this.SetValue(
						//	menuItem.Text,
						//	userInitiated: true);
						string[] ipAndPort = menuItem.Value.Split(':');
						ActiveSliceSettings.Instance.SetValue(SettingsKey.ip_address, ipAndPort[0]);
						ActiveSliceSettings.Instance.SetValue(SettingsKey.ip_port, ipAndPort[1]);
						ActiveSliceSettings.Instance.SetValue(SettingsKey.selector_ip_address, menuItem.Text);
					}
				};
			}
			refreshButton.Enabled = true;
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
