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
using System.Linq;

namespace MatterHackers.MatterControl
{
	public class MenuOptionAction : MenuBase
	{
		private event EventHandler unregisterEvents;
		public MenuOptionAction() : base("Actions".Localize())
		{
			Name = "Actions Menu";

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
			for(int i=0; i<MenuDropList.MenuItems.Count; i++)
			{
				MenuDropList.MenuItems[i].Enabled = ActiveSliceSettings.Instance.PrinterSelected 
					&& PrinterConnectionAndCommunication.Instance.PrinterIsConnected
					&& !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting;
			}
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			var list = new List<MenuItemAction>();

			if (ActiveSliceSettings.Instance.ActionMacros().Any())
			{
				foreach (GCodeMacro macro in ActiveSliceSettings.Instance.ActionMacros())
				{
					list.Add(new MenuItemAction(GCodeMacro.FixMacroName(macro.Name), macro.Run));
				}
			}

			return list;
		}
	}
}