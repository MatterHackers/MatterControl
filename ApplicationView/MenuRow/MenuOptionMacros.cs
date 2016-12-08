using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.AboutPage;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
	public class MenuOptionMacros : MenuBase
	{
		private event EventHandler unregisterEvents;
		public MenuOptionMacros() : base("Macros".Localize())
		{
			Name = "Macro Menu";

			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) => SetEnabledState(), ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((s,e) => SetEnabledState(), ref unregisterEvents);
		}

		public override void OnLoad(EventArgs args)
		{
			SetEnabledState();
			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void SetEnabledState()
		{
			for(int i=0; i<MenuDropList.MenuItems.Count-1; i++)
			{
				MenuDropList.MenuItems[i].Enabled = ActiveSliceSettings.Instance.PrinterSelected 
					&& PrinterConnectionAndCommunication.Instance.PrinterIsConnected
					&& !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting;
			}

			// and set the edit menu item
			MenuDropList.MenuItems[MenuDropList.MenuItems.Count-1].Enabled = ActiveSliceSettings.Instance.PrinterSelected;
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			var list = new List<MenuItemAction>();

			if (ActiveSliceSettings.Instance.Macros.Count > 0)
			{
				foreach (GCodeMacro macro in ActiveSliceSettings.Instance.Macros)
				{
					list.Add(new MenuItemAction(MacroControls.FixMacroName(macro.Name), macro.Run));
				}
			}

			list.Add(new MenuItemAction(
				//StaticData.Instance.LoadIcon("icon_plus.png", 32, 32),
				"Edit Macros...",
				() =>
				{
					if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
					{
						UiThread.RunOnIdle(() =>
							StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't edit macros while printing".Localize())
							);
					}
					else
					{
						UiThread.RunOnIdle(() => EditMacrosWindow.Show());
					}

				}));


			return list;
		}
	}
}