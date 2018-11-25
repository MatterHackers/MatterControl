/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabPage : PartTabPage
	{
		internal GCode2DWidget gcode2DWidget;

		private View3DConfig gcodeOptions;
		private DoubleSolidSlider layerRenderRatioSlider;
		public SliceLayerSelector LayerScrollbar { get; private set; }
		private GCodePanel gcodePanel;
		internal VerticalResizeContainer gcodeContainer;
		internal PrinterActionsBar printerActionsBar;
		private DockingTabControl sideBar;
		private SliceSettingsWidget sliceSettingsWidget;

		public PrinterTabPage(PrinterConfig printer, ThemeConfig theme, string tabTitle)
			: base(printer, printer.Bed, theme, tabTitle)
		{
			gcodeOptions = sceneContext.RendererOptions;

			view3DWidget.meshViewerWidget.EditorMode = MeshViewerWidget.EditorType.Printer;

			viewControls3D.TransformStateChanged += (s, e) =>
			{
				switch (e.TransformMode)
				{
					case ViewControls3DButtons.Translate:
						if (gcode2DWidget != null)
						{
							gcode2DWidget.TransformState = GCode2DWidget.ETransformState.Move;
						}
						break;

					case ViewControls3DButtons.Scale:
						if (gcode2DWidget != null)
						{
							gcode2DWidget.TransformState = GCode2DWidget.ETransformState.Scale;
						}
						break;
				}
			};

			viewControls3D.ResetView += (sender, e) =>
			{
				if (gcode2DWidget?.Visible == true)
				{
					gcode2DWidget.CenterPartInView();
				}
			};

			printer.ViewState.ViewModeChanged += ViewState_ViewModeChanged;

			var opaqueTrackColor = theme.ResolveColor(theme.BedBackgroundColor, theme.SlightShade);

			LayerScrollbar = new SliceLayerSelector(printer, sceneContext, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right | HAnchor.Fit,
				Margin = new BorderDouble(0, 4, 4, 4),
				Maximum = sceneContext.LoadedGCode?.LayerCount ?? 1
			};
			LayerScrollbar.SolidSlider.View.TrackColor = opaqueTrackColor;
			view3DWidget.InteractionLayer.AddChild(LayerScrollbar);

			layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), SliceLayerSelector.SliderWidth, theme);
			layerRenderRatioSlider.FirstValue = 0;
			layerRenderRatioSlider.FirstValueChanged += (s, e) =>
			{
				sceneContext.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				sceneContext.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				this.Invalidate();
			};
			layerRenderRatioSlider.SecondValue = 1;
			layerRenderRatioSlider.SecondValueChanged += (s, e) =>
			{
				if (printer?.Bed?.RenderInfo != null)
				{
					sceneContext.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
					sceneContext.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
				}

				this.Invalidate();
			};
			view3DWidget.InteractionLayer.AddChild(layerRenderRatioSlider);
			theme.ApplySliderStyle(layerRenderRatioSlider);

			layerRenderRatioSlider.View.TrackColor = opaqueTrackColor;

			sceneContext.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;

			AddSettingsTabBar(leftToRight, view3DWidget);

			view3DWidget.InteractionLayer.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			printerActionsBar = new PrinterActionsBar(printer, this, theme);
			theme.ApplyBottomBorder(printerActionsBar);
			printerActionsBar.modelViewButton.Enabled = sceneContext.EditableScene;

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(printerActionsBar, 0);

			var trackball = view3DWidget.InteractionLayer.Children<TrackballTumbleWidget>().FirstOrDefault();

			tumbleCubeControl = view3DWidget.InteractionLayer.Children<TumbleCubeControl>().FirstOrDefault();

			var position = view3DWidget.InteractionLayer.Children.IndexOf(trackball);

			gcodePanel = new GCodePanel(printer, sceneContext, theme)
			{
				Name = "GCode3DWidget",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = theme.InteractionLayerOverlayColor,
			};

			var modelViewSidePanel = view3DWidget.Descendants<VerticalResizeContainer>().FirstOrDefault();

			gcodeContainer = new VerticalResizeContainer(theme, GrabBarSide.Left)
			{
				Width = printer?.ViewState.SelectedObjectPanelWidth ?? 200,
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Absolute,
				SplitterBarColor = theme.SplitterBackground,
				SplitterWidth = theme.SplitterWidth,
				Visible = false,
			};

			gcodeContainer.AddChild(gcodePanel);
			gcodeContainer.Resized += (s, e) =>
			{
				if (printer != null)
				{
					printer.ViewState.SelectedObjectPanelWidth = gcodeContainer.Width;
				}
			};

			modelViewSidePanel.BoundsChanged += (s, e) =>
			{
				gcodeContainer.Width = modelViewSidePanel.Width;
			};

			gcodeContainer.BoundsChanged += (s, e) =>
			{
				modelViewSidePanel.Width = gcodeContainer.Width;
			};

			var splitContainer = view3DWidget.FindNamedChildRecursive("SplitContainer");

			splitContainer.AddChild(gcodeContainer);

			view3DContainer.AddChild(new RunningTasksWidget(theme)
			{
				MinimumSize = new Vector2(100, 0),
				Margin = new BorderDouble(top: printerActionsBar.Height + 1, left: favoritesBar.LocalBounds.Width + favoritesBar.DeviceMarginAndBorder.Width + 1),
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Fit,
			});

			// Create and append new widget
			gcode2DWidget = new GCode2DWidget(printer, theme)
			{
				Visible = (printer.ViewState.ViewMode == PartViewMode.Layers2D)
			};
			view3DWidget.InteractionLayer.AddChild(gcode2DWidget, position + 1);

			SetSliderSizes();

			this.SetViewMode(printer.ViewState.ViewMode);

			this.LayerScrollbar.Margin = LayerScrollbar.Margin.Clone(top: tumbleCubeControl.Height + tumbleCubeControl.Margin.Height + 4);

			printer.ViewState.VisibilityChanged += ProcessOptionalTabs;

			printer.Bed.RendererOptions.PropertyChanged += RendererOptions_PropertyChanged;

			printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
		}

		private void RendererOptions_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(printer.Bed.RendererOptions.SyncToPrint))
			{
				this.SetSliderVisibility();
			}
		}

		private void ViewState_ViewModeChanged(object sender, ViewModeChangedEventArgs e)
		{
			this.SetViewMode(e.ViewMode);
		}

		private void ProcessOptionalTabs(object sender, EventArgs e)
		{
			this.ProcessOptionalTabs();
		}

		private void SetViewMode(PartViewMode viewMode)
		{
			if (gcodePanel == null || gcode2DWidget == null)
			{
				// Wait for controls to initialize
				return;
			}

			switch (viewMode)
			{
				case PartViewMode.Layers2D:
					UserSettings.Instance.set(UserSettingsKey.LayerViewDefault, "2D Layer");
					gcode2DWidget.Visible = true;
					break;

				case PartViewMode.Layers3D:
					UserSettings.Instance.set(UserSettingsKey.LayerViewDefault, "3D Layer");
					break;

				case PartViewMode.Model:
					break;
			}

			gcode2DWidget.Visible = viewMode == PartViewMode.Layers2D;
			view3DWidget.meshViewerWidget.Visible = !gcode2DWidget.Visible;

			sceneContext.ViewState.ModelView = viewMode == PartViewMode.Model;

			gcodeContainer.Visible = viewMode != PartViewMode.Model;

			tumbleCubeControl.Visible = !gcode2DWidget.Visible;

			if (viewMode == PartViewMode.Layers3D)
			{
				printer.Bed.Scene.ClearSelection();
			}

			this.SetSliderVisibility();

			view3DWidget.modelViewSidePanel.Visible = printer?.ViewState.ViewMode == PartViewMode.Model;
		}

		private void BedPlate_LoadedGCodeChanged(object sender, EventArgs e)
		{
			this.SetSliderVisibility();

			if (sceneContext.LoadedGCode == null)
			{
				return;
			}

			LayerScrollbar.Maximum = sceneContext.LoadedGCode.LayerCount;
		}

		private void SetSliderVisibility()
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;

			if (gcodeOptions.SyncToPrint && printerIsRunningPrint)
			{
				SetAnimationPosition();
				layerRenderRatioSlider.Visible = false;
				LayerScrollbar.Visible = false;
			}
			else
			{
				if (layerRenderRatioSlider != null)
				{
					layerRenderRatioSlider.FirstValue = 0;
					layerRenderRatioSlider.SecondValue = 1;
				}

				bool hasLayers = printer.Bed.LoadedGCode?.LayerCount > 0;

				layerRenderRatioSlider.Visible = hasLayers && !sceneContext.ViewState.ModelView;
				LayerScrollbar.Visible = hasLayers && !sceneContext.ViewState.ModelView;
			}
		}

		private void SetSliderSizes()
		{
			if (LayerScrollbar == null || view3DWidget == null)
			{
				return;
			}

			layerRenderRatioSlider.OriginRelativeParent = new Vector2(4, 13);
			layerRenderRatioSlider.TotalWidthInPixels = view3DWidget.InteractionLayer.Width - 32;
		}

		private double lastPosition = 0;
		private TumbleCubeControl tumbleCubeControl;

		private bool SetAnimationPosition()
		{
			LayerScrollbar.Value = printer.Connection.CurrentlyPrintingLayer;

			double currentPosition = printer.Connection.RatioIntoCurrentLayer;
			layerRenderRatioSlider.FirstValue = 0;

			if (lastPosition != currentPosition)
			{
				layerRenderRatioSlider.SecondValue = currentPosition;
				lastPosition = currentPosition;
				return true;
			}

			return false;
		}

		internal void ShowGCodeOverflowMenu(PopupMenu popupMenu)
		{
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;
			if (gcodeOptions.SyncToPrint
				&& printerIsRunningPrint
				&& printer.ViewState.ViewMode != PartViewMode.Model)
			{
				if (this.SetAnimationPosition())
				{
					this.Invalidate();
				}
			}

			base.OnDraw(graphics2D);
		}

		protected override void GetViewControls3DOverflowMenu(PopupMenu popupMenu)
		{
			if (printer?.ViewState.ViewMode != PartViewMode.Model)
			{
				view3DWidget.ShowBedViewOptions(popupMenu);
				this.ShowGCodeOverflowMenu(popupMenu);
			}
			else
			{
				view3DWidget.ShowOverflowMenu(popupMenu);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			sceneContext.LoadedGCodeChanged -= BedPlate_LoadedGCodeChanged;
			printer.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;
			printer.ViewState.VisibilityChanged -= ProcessOptionalTabs;
			printer.ViewState.ViewModeChanged -= ViewState_ViewModeChanged;
			printer.Bed.RendererOptions.PropertyChanged -= RendererOptions_PropertyChanged;

			base.OnClosed(e);
		}

		private void AddSettingsTabBar(GuiWidget parent, GuiWidget widgetTodockTo)
		{
			sideBar = new DockingTabControl(widgetTodockTo, DockSide.Right, printer, theme)
			{
				Name = "DockingTabControl",
				ControlIsPinned = printer.ViewState.SliceSettingsTabPinned,
				MinDockingWidth = 400 * (int)GuiWidget.DeviceScale
			};
			sideBar.PinStatusChanged += (s, e) =>
			{
				printer.ViewState.SliceSettingsTabPinned = sideBar.ControlIsPinned;
			};
			parent.AddChild(sideBar);

			sideBar.AddPage(
				"Slice Settings",
				"Slice Settings".Localize(),
				sliceSettingsWidget = new SliceSettingsWidget(
					printer,
					new SettingsContext(
						printer,
						null,
						NamedSettingsLayers.All),
					theme));

			this.ProcessOptionalTabs();
		}

		private void ProcessOptionalTabs()
		{
			sideBar.RemovePage("Controls", false);
			sideBar.RemovePage("Terminal", false);
			sideBar.RemovePage("Printer", false);

			if (printer.ViewState.ControlsVisible)
			{
				sideBar.AddPage("Controls", "Controls".Localize(), new ManualPrinterControls(printer, theme), false);
			}

			if (printer.ViewState.TerminalVisible)
			{
				sideBar.AddPage("Terminal",
					"Terminal".Localize(),
					new TerminalWidget(printer, theme)
					{
						VAnchor = VAnchor.Stretch,
						HAnchor = HAnchor.Stretch
					},
					false);
			}

			if (printer.ViewState.ConfigurePrinterVisible)
			{
				sideBar.AddPage(
					"Printer",
					"Printer".Localize(),
					new ConfigurePrinterWidget(sliceSettingsWidget.settingsContext, printer, theme)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Stretch,
					},
					false);
			}

			sideBar.Rebuild();
		}

		private void Connection_CommunicationStateChanged(object s, EventArgs e)
		{
			this.SetSliderVisibility();
		}

		public static GuiWidget PrintProgressWidget(PrinterConfig printer, ThemeConfig theme)
		{
			var bodyRow = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top | VAnchor.Fit,
				//BackgroundColor = new Color(theme.Colors.PrimaryBackgroundColor, 128),
				MinimumSize = new Vector2(275, 140),
			};

			// Progress section
			var expandingContainer = new HorizontalSpacer()
			{
				VAnchor = VAnchor.Fit | VAnchor.Center
			};
			bodyRow.AddChild(expandingContainer);

			var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
			};
			expandingContainer.AddChild(progressContainer);

			var progressDial = new ProgressDial(theme)
			{
				HAnchor = HAnchor.Center,
				Height = 200 * DeviceScale,
				Width = 200 * DeviceScale
			};
			progressContainer.AddChild(progressDial);

			var bottomRow = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			progressContainer.AddChild(bottomRow);

			var timeContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = 3
			};
			bottomRow.AddChild(timeContainer);

			// we can only reslice on 64 bit, because in 64 bit we always have the gcode loaded
			if (IntPtr.Size == 8)
			{
				var resliceButton = new TextButton("Re-Slice", theme)
				{
					HAnchor = HAnchor.Right,
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(0, 0, 7, 0),
					Name = "Re-Slice Button"
				};
				bool activelySlicing = false;
				resliceButton.Click += (s, e) =>
				{
					resliceButton.Enabled = false;
					UiThread.RunOnIdle(async () =>
					{
						if (!activelySlicing
							&& SettingsValidation.SettingsValid(printer)
							&& printer.Bed.EditContext.SourceItem != null)
						{
							activelySlicing = true;
							if (bottomRow.Name == null)
							{
								bottomRow.Name = printer.Bed.EditContext.GCodeFilePath(printer);
							}

							await ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer.Bed.SaveChanges);

							// start up a new slice on a background thread
							await ApplicationController.Instance.SliceItemLoadOutput(
								printer,
								printer.Bed.Scene,
								printer.Bed.EditContext.GCodeFilePath(printer));

							// Switch to the 3D layer view if on Model view
							if (printer.ViewState.ViewMode == PartViewMode.Model)
							{
								printer.ViewState.ViewMode = PartViewMode.Layers3D;
							}

							// when it is done queue it to the change to gcode stream
							var message2 = "Would you like to switch to the new G-Code? Before you switch, check that your are seeing the changes you expect.".Localize();
							var caption2 = "Switch to new G-Code?".Localize();
							StyledMessageBox.ShowMessageBox(async (clickedOk2) =>
							{
								if (clickedOk2)
								{
									if (printer.Connection != null
										&& (printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused))
									{
										printer.Connection.SwitchToGCode(printer.Bed.EditContext.GCodeFilePath(printer));
										bottomRow.Name = printer.Bed.EditContext.GCodeFilePath(printer);
									}
								}
								else
								{
									await ApplicationController.Instance.SliceItemLoadOutput(
										printer,
										printer.Bed.Scene,
										bottomRow.Name);
								}
								activelySlicing = false;
								resliceButton.Enabled = true;
							}, message2, caption2, StyledMessageBox.MessageType.YES_NO, "Switch".Localize(), "Cancel".Localize());
						}
						else
						{
							resliceButton.Enabled = true;
						}
					});
				};
				bottomRow.AddChild(resliceButton);
			}

			timeContainer.AddChild(new ImageWidget(AggContext.StaticData.LoadIcon("fa-clock_24.png", theme.InvertIcons))
			{
				VAnchor = VAnchor.Center
			});

			var timeWidget = new TextWidget("", pointSize: 22, textColor: theme.TextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(10, 0, 0, 0),
				VAnchor = VAnchor.Center,
			};

			timeContainer.AddChild(timeWidget);

			var runningInterval = UiThread.SetInterval(
				() =>
				{
					int secondsPrinted = printer.Connection.SecondsPrinted;
					int hoursPrinted = (int)(secondsPrinted / (60 * 60));
					int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);

					secondsPrinted = secondsPrinted % 60;

					// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
					timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

					progressDial.LayerIndex = printer.Connection.CurrentlyPrintingLayer;
					progressDial.LayerCompletedRatio = printer.Connection.RatioIntoCurrentLayer;
					progressDial.CompletedRatio = printer.Connection.PercentComplete / 100;

					switch (printer.Connection.CommunicationState)
					{
						case CommunicationStates.PreparingToPrint:
						case CommunicationStates.Printing:
						case CommunicationStates.Paused:
							bodyRow.Visible = true;
							break;

						default:
							bodyRow.Visible = false;
							break;
					}
				}, 1);
			bodyRow.Closed += (s, e) => UiThread.ClearInterval(runningInterval);

			bodyRow.Visible = false;

			return bodyRow;
		}
	}
}
