/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceEngineField : UIField
	{
		private DropDownList dropdownList;
		private ThemeConfig theme;
		private PrinterConfig printer;

		public SliceEngineField(PrinterConfig printer, ThemeConfig theme)
		{
			this.theme = theme;
			this.printer = printer;
		}

		public new string Name { get; set; }

		public override void Initialize(int tabIndex)
		{
			EventHandler unregisterEvents = null;

			var panel = new FlowLayoutWidget();

			dropdownList = new MHDropDownList("None".Localize(), theme, maxHeight: 200)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex,
				Name = "slice_engine Field",
				// Prevent droplist interaction when connected
				Enabled = !printer.Connection.Printing,
			};

			dropdownList.Click += (s, e) =>
			{
				// TODO: why doesn't this blow up without runonidle?
				RebuildMenuItems();
			};

			RebuildMenuItems();

			// Prevent droplist interaction when connected
			void CommunicationStateChanged(object s, EventArgs e)
			{
				dropdownList.Enabled = !printer.Connection.Printing;

				if (printer.Connection.ComPort != dropdownList.SelectedLabel)
				{
					dropdownList.SelectedLabel = printer.Connection.ComPort;
				}
			}
			printer.Connection.CommunicationStateChanged += CommunicationStateChanged;
			dropdownList.Closed += (s, e) => printer.Connection.CommunicationStateChanged -= CommunicationStateChanged;

			// Release event listener on close
			dropdownList.Closed += (s, e) =>
			{
				unregisterEvents?.Invoke(null, null);
			};

			var configureIcon = new IconButton(AggContext.StaticData.LoadIcon("fa-cog_16.png", theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing,
				ToolTipText = "Port Wizard".Localize()
			};
			configureIcon.Click += (s, e) =>
			{
				DialogWindow.Show(new SetupStepComPortOne(printer));
			};

			panel.AddChild(configureIcon);

			panel.AddChild(dropdownList);

			this.Content = panel;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			dropdownList.SelectedLabel = this.Value;
			base.OnValueChanged(fieldChangedEventArgs);
		}

		private void RebuildMenuItems()
		{
			dropdownList.MenuItems.Clear();

			foreach (var sliceEngine in PrinterSettings.SliceEngines)
			{
				// Add each serial port to the dropdown list
				MenuItem newItem = dropdownList.AddItem(sliceEngine.Key);

				// When the given menu item is selected, save its value back into settings
				newItem.Selected += (sender, e) =>
				{
					if (sender is MenuItem menuItem)
					{
						if (PrinterSettings.SliceEngines.TryGetValue(menuItem.Text, out IObjectSlicer slicer))
						{
							printer.Settings.Slicer = slicer;
						}

						this.SetValue(
							menuItem.Text,
							userInitiated: true);
					}
				};
			}

			// Set control text
			dropdownList.SelectedLabel = this.Value;
		}
	}

}
