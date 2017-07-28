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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterActionsBar : FlowLayoutWidget
	{
		private static EePromMarlinWindow openEePromMarlinWidget = null;
		private static EePromRepetierWindow openEePromRepetierWidget = null;
		private string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
		private string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();

		private OverflowDropdown overflowDropdown;

		private SliceProgressReporter sliceProgressReporter;

		private CancellationTokenSource gcodeLoadCancellationTokenSource;

		public class SliceProgressReporter : IProgress<string>
		{
			private MeshViewerWidget meshViewer;

			public SliceProgressReporter(MeshViewerWidget meshViewer)
			{
				this.meshViewer = meshViewer;
			}

			public void StartReporting()
			{
				meshViewer.BeginProgressReporting("Slicing Part");
			}

			public void EndReporting()
			{
				meshViewer.EndProgressReporting();
			}

			public void Report(string value)
			{
				meshViewer.partProcessingInfo.centeredInfoDescription.Text = value;
			}
		}

		public PrinterActionsBar(View3DWidget modelViewer, PrinterTabPage printerTabPage)
		{
			UndoBuffer undoBuffer = modelViewer.UndoBuffer;

			var defaultMargin = new BorderDouble(8, 0);

			sliceProgressReporter = new SliceProgressReporter(modelViewer.meshViewerWidget);

			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.FitToChildren;

			var buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;

			this.AddChild(new PrinterConnectButton(buttonFactory));

			this.AddChild(new PrintActionRow(buttonFactory, this, defaultMargin));

			this.AddChild(new HorizontalSpacer());

			var initialMargin = buttonFactory.Margin;

			var sliceButton = buttonFactory.Generate("Slice".Localize());
			sliceButton.ToolTipText = "Slice Parts".Localize();
			sliceButton.Name = "Generate Gcode Button";
			sliceButton.Margin = defaultMargin;
			sliceButton.Click += async (s, e) =>
			{
				if (ActiveSliceSettings.Instance.PrinterSelected)
				{
					var printItem = ApplicationController.Instance.ActivePrintItem;

					if (ActiveSliceSettings.Instance.IsValid() && printItem != null)
					{
						sliceButton.Enabled = false;

						try
						{
							sliceProgressReporter.StartReporting();

							// Save any pending changes before starting the print
							await ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded();
							
							await SlicingQueue.SliceFileAsync(printItem, sliceProgressReporter);

							gcodeLoadCancellationTokenSource = new CancellationTokenSource();

							ApplicationController.Instance.Printer.BedPlate.LoadGCode(printItem.GetGCodePathAndFileName(), gcodeLoadCancellationTokenSource.Token, printerTabPage.modelViewer.gcodeViewer.LoadProgress_Changed);
							sliceProgressReporter.EndReporting();

							printerTabPage.ViewMode = PartViewMode.Layers3D;

							// HACK: directly fire method which previously ran on SlicingDone event on PrintItemWrapper
							UiThread.RunOnIdle(printerTabPage.modelViewer.gcodeViewer.CreateAndAddChildren);
						}
						catch (Exception ex)
						{
							Console.WriteLine("Error slicing file: " + ex.Message);
						}

						sliceButton.Enabled = true;
					};
				}
				else
				{
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowMessageBox(null, "Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
					});
				}
			};

			this.AddChild(sliceButton);

			this.AddChild(new TemperatureWidgetExtruder(ApplicationController.Instance.Theme.MenuButtonFactory)
			{
				Margin = new BorderDouble(right: 10)
			});

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				this.AddChild(new TemperatureWidgetBed());
			}

			buttonFactory.Margin = defaultMargin;

			Button undoButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("Undo_grey_16x.png", 16, 16));
			undoButton.Name = "3D View Undo";
			undoButton.ToolTipText = "Undo";
			undoButton.Enabled = false;
			undoButton.Margin = defaultMargin;
			undoButton.Click += (sender, e) =>
			{
				undoBuffer.Undo();
			};
			this.AddChild(undoButton);
			undoButton.VAnchor = VAnchor.ParentCenter;

			Button redoButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("Redo_grey_16x.png", 16, 16));
			redoButton.Name = "3D View Redo";
			redoButton.ToolTipText = "Redo";
			redoButton.Enabled = false;
			redoButton.Click += (sender, e) =>
			{
				undoBuffer.Redo();
			};
			this.AddChild(redoButton);
			redoButton.VAnchor = VAnchor.ParentCenter;

			buttonFactory.Margin = initialMargin;

			undoBuffer.Changed += (sender, e) =>
			{
				undoButton.Enabled = undoBuffer.UndoCount > 0;
				redoButton.Enabled = undoBuffer.RedoCount > 0;
			};

			overflowDropdown = new OverflowDropdown(allowLightnessInvert: true)
			{
				AlignToRightEdge = true,
				Name = "Printer Overflow Menu",
				Margin = defaultMargin
			};
			overflowDropdown.DynamicPopupContent = GeneratePrinterOverflowMenu;

			// Deregister on close
			this.Closed += (s, e) =>
			{
				overflowDropdown.DynamicPopupContent = GeneratePrinterOverflowMenu;
			}; 

			this.AddChild(overflowDropdown);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			gcodeLoadCancellationTokenSource?.Cancel();
			base.OnClosed(e);
		}

		private GuiWidget GeneratePrinterOverflowMenu()
		{
			var printerSettings = ActiveSliceSettings.Instance;


			var widgetToPop = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.FitToChildren,
			};

			var menuActions = new NamedAction[]
			{
				new NamedAction()
				{
					Icon = StaticData.Instance.LoadIcon("memory_16x16.png", 16, 16),
					Title = "Configure EEProm".Localize(),
					Action = configureEePromButton_Click
				},
				new NamedAction()
				{
					Title = "Rename Printer".Localize(),
					Action = () =>
					{
						var renameItemWindow = new RenameItemWindow(
						"Rename Printer".Localize() + ":",
						printerSettings.GetValue(SettingsKey.printer_name),
						(newName) =>
						{
							if (!string.IsNullOrEmpty(newName))
							{
								printerSettings.SetValue(SettingsKey.printer_name, newName);
							}
						});
					}
				},
				new NamedAction() { Title = "----" },
				new NamedAction()
				{
					Title = "Delete Printer".Localize(),
					Action = () =>
					{
						StyledMessageBox.ShowMessageBox(
							(doDelete) =>
							{
								if (doDelete)
								{
									ActiveSliceSettings.Instance.Helpers.SetMarkedForDelete(true);
								}
							},
							"Are you sure you want to delete your currently selected printer?".Localize(),
							"Delete Printer?".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Delete Printer".Localize());
					}
				}
			};
			
			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				MenuItem menuItem;

				if (menuAction.Title == "----")
				{
					menuItem = overflowDropdown.CreateHorizontalLine();
				}
				else
				{
					menuItem = overflowDropdown.CreateMenuItem((string)menuAction.Title);
					menuItem.Name = $"{menuAction.Title} Menu Item";
				}

				menuItem.Enabled = menuAction.Action != null;
				menuItem.ClearRemovedFlag();

				if (menuItem.Enabled)
				{
					menuItem.Click += (s, e) =>
					{
						menuAction.Action();
					};
				}

				widgetToPop.AddChild(menuItem);
			}

			return widgetToPop;
		}

		private void configureEePromButton_Click()
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