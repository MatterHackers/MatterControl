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
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using static MatterHackers.MatterControl.StyledMessageBox;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabPage : PartTabPage
	{
		private GCode2DWidget gcode2DWidget;

		private View3DConfig gcodeOptions;

		private readonly DoubleSolidSlider layerRenderRatioSlider;

		private GCodePanel gcodePanel;
		private VerticalResizeContainer gcodeContainer;

		public PrinterActionsBar PrinterActionsBar { get; set; }

		private DockingTabControl sideBar;
		private SliceSettingsWidget sliceSettingsWidget;

		public PrinterTabPage(PartWorkspace workspace, ThemeConfig theme, string tabTitle)
			: base(workspace, theme, tabTitle)
		{
			gcodeOptions = sceneContext.RendererOptions;

			view3DWidget.InteractionLayer.EditorMode = InteractionLayer.EditorType.Printer;

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

			var opaqueTrackColor = theme.ResolveColor(theme.BedBackgroundColor, theme.SlightShade);

			LayerScrollbar = new SliceLayerSelector(printer, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right | HAnchor.Fit,
				Margin = new BorderDouble(0, 4, 4, 4),
				Maximum = sceneContext.LoadedGCode?.LayerCount ?? 1
			};
			LayerScrollbar.SolidSlider.View.TrackColor = opaqueTrackColor;
			view3DWidget.InteractionLayer.AddChild(LayerScrollbar);

			layerRenderRatioSlider = new DoubleSolidSlider(default(Vector2), SliceLayerSelector.SliderWidth, theme);
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

			AddSettingsTabBar(leftToRight, view3DWidget);

			view3DWidget.InteractionLayer.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			PrinterActionsBar = new PrinterActionsBar(printer, this, theme);
			theme.ApplyBottomBorder(PrinterActionsBar);
			PrinterActionsBar.modelViewButton.Enabled = sceneContext.EditableScene;

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(PrinterActionsBar, 0);

			var trackball = view3DWidget.InteractionLayer.Children<TrackballTumbleWidget>().FirstOrDefault();

			tumbleCubeControl = view3DWidget.InteractionLayer.Children<TumbleCubeControl>().FirstOrDefault();

			var position = view3DWidget.InteractionLayer.Children.IndexOf(trackball);

			gcodePanel = new GCodePanel(this, printer, sceneContext, theme)
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

			var splitContainer = view3DWidget.FindDescendant("SplitContainer");

			splitContainer.AddChild(gcodeContainer);

			view3DContainer.AddChild(new RunningTasksWidget(theme, printer)
			{
				MinimumSize = new Vector2(100, 0),
				Margin = new BorderDouble(top: PrinterActionsBar.Height + 1, left: favoritesBar.LocalBounds.Width + favoritesBar.DeviceMarginAndBorder.Width + 1),
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Fit,
			});

			// Create and append new widget
			gcode2DWidget = new GCode2DWidget(printer, theme)
			{
				Visible = printer.ViewState.ViewMode == PartViewMode.Layers2D
			};
			view3DWidget.InteractionLayer.AddChild(gcode2DWidget, position + 1);

			SetSliderSizes();

			this.SetViewMode(printer.ViewState.ViewMode);

			this.LayerScrollbar.Margin = LayerScrollbar.Margin.Clone(top: tumbleCubeControl.Height + tumbleCubeControl.Margin.Height + 4);

			// On load, switch to gcode view if previously editing gcode file. Listeners would normally do this but workspace loads before this UI widget
			if (this?.PrinterActionsBar?.modelViewButton is GuiWidget button)
			{
				button.Enabled = sceneContext.EditableScene;

				if (sceneContext.ContentType == "gcode"
					&& this?.PrinterActionsBar?.layers3DButton is GuiWidget gcodeButton)
				{
					gcodeButton.InvokeClick();
				}
			}

			// Register listeners
			printer.ViewState.VisibilityChanged += ProcessOptionalTabs;
			printer.ViewState.ViewModeChanged += ViewState_ViewModeChanged;

			printer.Bed.RendererOptions.PropertyChanged += RendererOptions_PropertyChanged;

			// register for communication messages
			printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
			printer.Connection.PauseOnLayer += Connection_PauseOnLayer;
			printer.Connection.FilamentRunout += Connection_FilamentRunout;

			ApplicationController.Instance.ApplicationError += ApplicationController_ApplicationError;
			ApplicationController.Instance.ApplicationEvent += ApplicationController_ApplicationEvent;

			sceneContext.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;
		}

		public SliceLayerSelector LayerScrollbar { get; private set; }

		public DoubleSolidSlider LayerFeaturesScrollbar => layerRenderRatioSlider;

		public int LayerFeaturesIndex
		{
			get
			{
				var renderInfo = sceneContext.RenderInfo;
				int layerIndex = renderInfo.EndLayerIndex - 1;
				int featuresOnLayer = sceneContext.GCodeRenderer.GetNumFeatures(layerIndex);
				int featureIndex = (int)(featuresOnLayer * renderInfo.FeatureToEndOnRatio0To1 + .5);

				return Math.Max(0, Math.Min(featureIndex, featuresOnLayer));
			}

			set
			{
				var renderInfo = sceneContext.RenderInfo;
				int layerIndex = renderInfo.EndLayerIndex - 1;
				int featuresOnLayer = sceneContext.GCodeRenderer.GetNumFeatures(layerIndex);

				var factor = (double)value / featuresOnLayer;

				layerRenderRatioSlider.SecondValue = renderInfo.FeatureToEndOnRatio0To1 = Math.Max(0, Math.Min(factor, 1));
			}
		}

		private readonly string pauseCaption = "Printer Paused".Localize();

		private void ResumePrint(bool clickedOk)
		{
			// They clicked either Resume or Ok
			if (clickedOk && printer.Connection.Paused)
			{
				printer.Connection.Resume();
			}
		}

		protected GuiWidget CreateTextField(string text)
		{
			return new WrappedTextWidget(text)
			{
				Margin = new BorderDouble(left: 10, top: 10),
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch
			};
		}

		private void Connection_FilamentRunout(object sender, PrintPauseEventArgs e)
		{
			if (e is PrintPauseEventArgs printePauseEventArgs)
			{
				if (printePauseEventArgs.FilamentRunout)
				{
					UiThread.RunOnIdle(() =>
					{
						var unloadFilamentButton = new TextButton("Unload Filament".Localize(), theme)
						{
							Name = "unload Filament",
							BackgroundColor = theme.MinimalShade,
							VAnchor = Agg.UI.VAnchor.Absolute,
							HAnchor = Agg.UI.HAnchor.Fit | Agg.UI.HAnchor.Left,
							Margin = new BorderDouble(10, 10, 0, 15)
						};

						unloadFilamentButton.Click += (s, e2) =>
						{
							unloadFilamentButton.Parents<SystemWindow>().First().Close();
							DialogWindow.Show(new UnloadFilamentWizard(printer, extruderIndex: 0));
						};

						theme.ApplyPrimaryActionStyle(unloadFilamentButton);

						string filamentPauseMessage = "Your 3D print has been paused.\n\nOut of filament, or jam, detected. Please load more filament or clear the jam.".Localize();

						var messageBox = new MessageBoxPage(ResumePrint,
							filamentPauseMessage.FormatWith(printePauseEventArgs.LayerNumber),
							pauseCaption,
							StyledMessageBox.MessageType.YES_NO_WITHOUT_HIGHLIGHT,
							null,
							500,
							400,
							"Resume".Localize(),
							"OK".Localize(),
							ApplicationController.Instance.Theme,
							false);

						messageBox.AddPageAction(unloadFilamentButton);

						DialogWindow.Show(messageBox);
					});
				}
			}
		}

		private void Connection_PauseOnLayer(object sender, EventArgs e)
		{
			if (e is PrintPauseEventArgs printePauseEventArgs)
			{
				string layerPauseMessage = "Your 3D print has been auto-paused.\n\nLayer {0} reached.".Localize();

				UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(ResumePrint,
					layerPauseMessage.FormatWith(printePauseEventArgs.LayerNumber),
					pauseCaption,
					StyledMessageBox.MessageType.YES_NO,
					"Resume".Localize(),
					"OK".Localize()));
			}
		}

		private void ApplicationController_ApplicationEvent(object sender, string e)
		{
			printer.TerminalLog.WriteLine(e);
		}

		private void ApplicationController_ApplicationError(object sender, string e)
		{
			printer.TerminalLog.WriteLine(e);
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

			view3DWidget.InteractionLayer.DrawOpenGLContent = printer?.ViewState.ViewMode != PartViewMode.Layers2D;

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
			bool printerIsRunningPrint = printer.Connection.Paused || printer.Connection.Printing;

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

			double currentPosition = printer.Connection.RatioIntoCurrentLayerInstructions;
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
			bool printerIsRunningPrint = printer.Connection.Paused || printer.Connection.Printing;
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

			printer.Connection.PauseOnLayer -= Connection_PauseOnLayer;
			printer.Connection.FilamentRunout -= Connection_FilamentRunout;

			printer.ViewState.VisibilityChanged -= ProcessOptionalTabs;
			printer.ViewState.ViewModeChanged -= ViewState_ViewModeChanged;
			printer.Bed.RendererOptions.PropertyChanged -= RendererOptions_PropertyChanged;
			ApplicationController.Instance.ApplicationError -= ApplicationController_ApplicationError;
			ApplicationController.Instance.ApplicationEvent -= ApplicationController_ApplicationEvent;

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
				// BackgroundColor = new Color(theme.Colors.PrimaryBackgroundColor, 128),
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

			var resliceMessageRow = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = false
			};
			progressContainer.AddChild(resliceMessageRow);

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
						bool doSlicing = !activelySlicing && printer.Bed.EditContext.SourceItem != null;
						if (doSlicing)
						{
							var errors = printer.ValidateSettings();
							if (errors.Any(err => err.ErrorLevel == ValidationErrorLevel.Error))
							{
								doSlicing = false;
								ApplicationController.Instance.ShowValidationErrors("Slicing Error".Localize(), errors);
							}
						}

						if (doSlicing)
						{
							activelySlicing = true;
							if (bottomRow.Name == null)
							{
								bottomRow.Name = printer.Bed.EditContext.GCodeFilePath(printer);
							}

							await ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer, printer.Bed.SaveChanges);

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

							resliceMessageRow.Visible = true;
							resliceMessageRow.VAnchor = VAnchor.Absolute;
							resliceMessageRow.VAnchor = VAnchor.Fit;
						}
						else
						{
							resliceButton.Enabled = true;
						}
					});
				};
				bottomRow.AddChild(resliceButton);

				// setup the message row
				{
					// when it is done queue it to the change to gcode stream
					var switchMessage = "Switch to new G-Code?\n\nBefore you switch, check that your are seeing the changes you expect.".Localize();
					resliceMessageRow.AddChild(new WrappedTextWidget(switchMessage, theme.DefaultFontSize, textColor: theme.TextColor)
					{
						Margin = new BorderDouble(7, 3)
					});

					var switchButtonRow = new FlowLayoutWidget(FlowDirection.RightToLeft)
					{
						HAnchor = HAnchor.Stretch
					};

					resliceMessageRow.AddChild(switchButtonRow);

					var switchButton = new TextButton("Switch", theme)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(5),
						Name = "Switch Button"
					};
					switchButtonRow.AddChild(switchButton);
					switchButton.Click += (s, e) =>
					{
						if (printer.Connection != null
							&& (printer.Connection.Printing || printer.Connection.Paused))
						{
							printer.Connection.SwitchToGCode(printer.Bed.EditContext.GCodeFilePath(printer));
							bottomRow.Name = printer.Bed.EditContext.GCodeFilePath(printer);
						}

						activelySlicing = false;
						resliceButton.Enabled = true;
						resliceMessageRow.Visible = false;
					};

					var cancelButton = new TextButton("Cancel", theme)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(5),
						Name = "Cancel Re-Slice Button"
					};
					switchButtonRow.AddChild(cancelButton);
					cancelButton.Click += async (s, e) =>
					{
						await ApplicationController.Instance.SliceItemLoadOutput(
							printer,
							printer.Bed.Scene,
							bottomRow.Name);

						activelySlicing = false;
						resliceButton.Enabled = true;
						resliceMessageRow.Visible = false;
					};
				}
			}

			timeContainer.AddChild(new ImageWidget(AggContext.StaticData.LoadIcon("fa-clock_24.png", theme.InvertIcons))
			{
				VAnchor = VAnchor.Center
			});

			var timeStack = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(10, 0, 0, 0),
				Padding = new BorderDouble(5, 0, 0, 0),
				VAnchor = VAnchor.Center | VAnchor.Fit
			};
			timeContainer.AddChild(timeStack);

			var timePrinted = new TextWidget("", pointSize: 16, textColor: theme.TextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
			};

			timeStack.AddChild(timePrinted);

			var timeToEnd = new TextWidget("", pointSize: 9, textColor: theme.TextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
			};

			timeStack.AddChild(timeToEnd);

			var runningInterval = UiThread.SetInterval(() =>
				{
					int totalSecondsPrinted = printer.Connection.SecondsPrinted;

					int hoursPrinted = totalSecondsPrinted / (60 * 60);
					int minutesPrinted = totalSecondsPrinted / 60 - hoursPrinted * 60;
					var secondsPrinted = totalSecondsPrinted % 60;

					// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
					timePrinted.Text = GetFormatedTime(hoursPrinted, minutesPrinted, secondsPrinted);

					int totalSecondsToEnd = printer.Connection.SecondsToEnd;

					int hoursToEnd = totalSecondsToEnd / (60 * 60);
					int minutesToEnd = totalSecondsToEnd / 60 - hoursToEnd * 60;
					var secondsToEnd = totalSecondsToEnd % 60;

					timeToEnd.Text = GetFormatedTime(hoursToEnd, minutesToEnd, secondsToEnd);

					progressDial.LayerIndex = printer.Connection.CurrentlyPrintingLayer;
					progressDial.LayerCompletedRatio = printer.Connection.RatioIntoCurrentLayerSeconds;
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

		private static string GetFormatedTime(int hoursPrinted, int minutesPrinted, int secondsPrinted)
		{
			if (hoursPrinted == 0 && minutesPrinted == 0 && secondsPrinted == 0)
			{
				return "";
			}

			return (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";
		}
	}
}
