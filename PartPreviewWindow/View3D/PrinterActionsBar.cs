/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget : PartPreview3DWidget
	{
		public class PrinterActionsBar : FlowLayoutWidget
		{
			private static EePromMarlinWindow openEePromMarlinWidget = null;
			private static EePromRepetierWindow openEePromRepetierWidget = null;
			private string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
			private string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();

			public PrinterActionsBar()
			{
				this.Padding = new BorderDouble(0, 5);
				this.HAnchor = HAnchor.ParentLeftRight;
				this.VAnchor = VAnchor.FitToChildren;

				var buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;
				
				this.AddChild(new PrinterConnectButton(buttonFactory));

				this.AddChild(new PrintActionRow(buttonFactory, this));

				//ImageBuffer terminalSettingsImage = StaticData.Instance.LoadIcon("terminal-24x24.png", 24, 24).InvertLightness();
				var terminalButton = buttonFactory.Generate("Terminal".Localize().ToUpper());
				terminalButton.Name = "Show Terminal Button";
				terminalButton.Click += (s, e) => UiThread.RunOnIdle(TerminalWindow.Show);
				this.AddChild(terminalButton);

				/*
				ImageBuffer levelingImage = StaticData.Instance.LoadIcon("leveling_32x32.png", 24, 24);
				if (!ActiveTheme.Instance.IsDarkTheme)
				{
					levelingImage.InvertLightness();
				}*/

				Button configureEePromButton = buttonFactory.Generate("EEProm".Localize().ToUpper());
				configureEePromButton.Click += configureEePromButton_Click;
				this.AddChild(configureEePromButton);

				//this.AddChild(new PrintStatusRow());

				this.AddChild(new HorizontalSpacer());

				this.AddChild(GeneratePopupContent());

				var overflowDropdown = new OverflowDropdown(allowLightnessInvert: true)
				{
					AlignToRightEdge = true,
				};
				overflowDropdown.DynamicPopupContent.Add(GeneratePopupContent);

				// Deregister on close
				this.Closed += (s, e) =>
				{
					overflowDropdown.DynamicPopupContent.Add(GeneratePopupContent);
				};

				this.AddChild(overflowDropdown);
			}

			private GuiWidget GeneratePopupContent()
			{
				var widgetToPop = new FlowLayoutWidget();
				widgetToPop.MaximumSize = new VectorMath.Vector2(280, 35);

				widgetToPop.AddChild(new PrinterSelectEditDropdown());
				
				//widgetToPop.AddChild("more stuff...");

				return widgetToPop;
			}

			private void configureEePromButton_Click(object sender, EventArgs mouseEvent)
			{
				UiThread.RunOnIdle(() =>
				{
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
					switch (PrinterConnectionAndCommunication.Instance.FirmwareType)
					{
						case PrinterConnectionAndCommunication.FirmwareTypes.Repetier:
							if (openEePromRepetierWidget != null)
							{
								openEePromRepetierWidget.BringToFront();
							}
							else
							{
								openEePromRepetierWidget = new EePromRepetierWindow();
								openEePromRepetierWidget.Closed += (RepetierWidget, RepetierEvent) =>
								{
									openEePromRepetierWidget = null;
								};
							}
							break;

						case PrinterConnectionAndCommunication.FirmwareTypes.Marlin:
							if (openEePromMarlinWidget != null)
							{
								openEePromMarlinWidget.BringToFront();
							}
							else
							{
								openEePromMarlinWidget = new EePromMarlinWindow();
								openEePromMarlinWidget.Closed += (marlinWidget, marlinEvent) =>
								{
									openEePromMarlinWidget = null;
								};
							}
							break;

						default:
							PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("M115");
							StyledMessageBox.ShowMessageBox(null, noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
							break;
					}
#endif
				});
			}
		}
	}
}