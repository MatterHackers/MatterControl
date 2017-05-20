/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class MenuOptionAction : MenuBase
	{
		private EventHandler unregisterEvents;
		public MenuOptionAction() : base("Actions".Localize())
		{
			Name = "Actions Menu";

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((s,e) => SetEnabledState(), ref unregisterEvents);
		}

		public override void OnLoad(EventArgs args)
		{
			SetEnabledState();
			base.OnLoad(args);
		}

		public override void OnClosed(ClosedEventArgs e)
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