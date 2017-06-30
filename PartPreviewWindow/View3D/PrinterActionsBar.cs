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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterActionsBar : FlowLayoutWidget
	{
		private static EePromMarlinWindow openEePromMarlinWidget = null;
		private static EePromRepetierWindow openEePromRepetierWidget = null;
		private string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
		private string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();

		private OverflowDropdown overflowDropdown;

		public PrinterActionsBar(View3DWidget modelViewer)
		{
			UndoBuffer undoBuffer = modelViewer.UndoBuffer;

			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.FitToChildren;

			var buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;

			this.AddChild(new PrinterConnectButton(buttonFactory));

			this.AddChild(new PrintActionRow(buttonFactory, this));

			this.AddChild(new HorizontalSpacer());

			var initialMargin = buttonFactory.Margin;

			this.AddChild(new TemperatureWidgetExtruder(ApplicationController.Instance.Theme.MenuButtonFactory)
			{
				Margin = new BorderDouble(right: 10)
			});

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				this.AddChild(new TemperatureWidgetBed());
			}

			buttonFactory.Margin = new BorderDouble(8, 0);

			Button configureEePromButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("chip_24x24.png", 16, 16));
			configureEePromButton.ToolTipText = "EEProm";
			configureEePromButton.Click += configureEePromButton_Click;
			this.AddChild(configureEePromButton);

			Button undoButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("undo_24x24.png", 16, 16));
			undoButton.Name = "3D View Undo";
			undoButton.ToolTipText = "Undo";
			undoButton.Enabled = false;
			undoButton.Margin = new BorderDouble(8, 0);
			undoButton.Click += (sender, e) =>
			{
				undoBuffer.Undo();
			};
			this.AddChild(undoButton);
			undoButton.VAnchor = VAnchor.ParentCenter;
			undoButton.Margin = 3;

			Button redoButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("redo_24x24.png", 16, 16));
			redoButton.Name = "3D View Redo";
			redoButton.ToolTipText = "Redo";
			redoButton.Enabled = false;
			redoButton.Click += (sender, e) =>
			{
				undoBuffer.Redo();
			};
			this.AddChild(redoButton);
			redoButton.VAnchor = VAnchor.ParentCenter;
			redoButton.Margin = 3;

			buttonFactory.Margin = initialMargin;

			undoBuffer.Changed += (sender, e) =>
			{
				undoButton.Enabled = undoBuffer.UndoCount > 0;
				redoButton.Enabled = undoBuffer.RedoCount > 0;
			};

			var editPrinterButton = PrinterSelectEditDropdown.CreatePrinterEditButton();
			this.AddChild(editPrinterButton);

			overflowDropdown = new OverflowDropdown(allowLightnessInvert: true)
			{
				AlignToRightEdge = true,
				Name = "Printer Overflow Menu"
			};
			overflowDropdown.DynamicPopupContent = GeneratePrinterOverflowMenu;

			// Deregister on close
			this.Closed += (s, e) =>
			{
				overflowDropdown.DynamicPopupContent = GeneratePrinterOverflowMenu;
			};

			this.AddChild(overflowDropdown);
		}

		private GuiWidget GeneratePrinterOverflowMenu()
		{
			var widgetToPop = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.FitToChildren,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};

			widgetToPop.AddChild(new PrinterSelectEditDropdown()
			{
				Margin = 10
			});

			return widgetToPop;
		}

		private void configureEePromButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
					switch (PrinterConnection.Instance.FirmwareType)
				{
					case FirmwareTypes.Repetier:
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

					case FirmwareTypes.Marlin:
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
						PrinterConnection.Instance.SendLineToPrinterNow("M115");
						StyledMessageBox.ShowMessageBox(null, noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
						break;
				}
#endif
				});
		}
	}
}