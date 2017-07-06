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
using System.IO;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewGcodeBasic : GuiWidget
	{
		private MeshViewerWidget externalMeshViewer;

		public enum WindowMode { Embeded, StandAlone };

		private SetLayerWidget setLayerWidget;
		private LayerNavigationWidget navigationWidget;

		private TextWidget gcodeProcessingStateInfoText;
		private GCode2DWidget gcode2DWidget;
		private PrintItemWrapper printItem => ApplicationController.Instance.ActivePrintItem;
		private bool startedSliceFromGenerateButton = false;
		private FlowLayoutWidget buttonBottomPanel;
		private FlowLayoutWidget layerSelectionButtonsPanel;

		private ViewControlsToggle viewControlsToggle;

		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;
		private WindowMode windowMode;

		public delegate Vector2 GetSizeFunction();

		private string gcodeLoading = "Loading G-Code".Localize();
		private string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private string fileNotFoundMessage = "File not found on disk.".Localize();
		private string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private Vector2 bedCenter;
		private Vector3 viewerVolume;
		private BedShape bedShape;
		private int sliderWidth;

		private PartViewMode activeViewMode = PartViewMode.Layers3D;

		private View3DConfig options;

		private PrinterConfig printer;
		private ViewControls3D viewControls3D;

		private BedConfig bedPlate;

		private SystemWindow parentSystemWindow;

		private TextImageButtonFactory buttonFactory;

		public ViewGcodeBasic(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowMode, ViewControls3D viewControls3D, ThemeConfig theme, MeshViewerWidget externalMeshViewer)
		{
			this.externalMeshViewer = externalMeshViewer;
			this.externalMeshViewer.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;

			buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;

			options = ApplicationController.Instance.Printer.BedPlate.RendererOptions;
			printer = ApplicationController.Instance.Printer;
			printer.BedPlate.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;

			this.viewControls3D = viewControls3D;
			this.viewerVolume = viewerVolume;
			this.bedShape = bedShape;
			this.bedCenter = bedCenter;
			this.windowMode = windowMode;

			sliderWidth = (UserSettings.Instance.IsTouchScreen) ? 20 : 10;

			RenderOpenGl.GLHelper.WireframeColor = ActiveTheme.Instance.PrimaryAccentColor;

			CreateAndAddChildren();

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == "extruder_offset")
					{
						printer.BedPlate.GCodeRenderer.Clear3DGCode();
					}
				}
			}, ref unregisterEvents);

			bedPlate = ApplicationController.Instance.Printer.BedPlate;

			// TODO: Why do we clear GCode on AdvancedControlsPanelReloading - assume some slice settings should invalidate. If so, code should be more specific and bound to slice settings changed
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => printer.BedPlate.GCodeRenderer?.Clear3DGCode(), ref unregisterEvents);
		}

		private void BedPlate_LoadedGCodeChanged(object sender, EventArgs e)
		{
			// ResetRenderInfo
			printer.BedPlate.RenderInfo = new GCodeRenderInfo(
				0,
				1,
				Agg.Transform.Affine.NewIdentity(),
				1,
				0,
				1,
				new Vector2[]
				{
					ActiveSliceSettings.Instance.Helpers.ExtruderOffset(0),
					ActiveSliceSettings.Instance.Helpers.ExtruderOffset(1)
				},
				this.GetRenderType,
				MeshViewerWidget.GetMaterialColor);
		}

		public override void OnLoad(EventArgs args)
		{
			// Find and hook the parent system window KeyDown event
			if (this.Parents<SystemWindow>().FirstOrDefault() is SystemWindow systemWindow)
			{
				systemWindow.KeyDown += Parent_KeyDown;
				parentSystemWindow = systemWindow;
			}

			base.OnLoad(args);
		}

		private void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (this.Visible)
			{
				switch(keyEvent.KeyCode)
				{
					case Keys.Up:
						bedPlate.ActiveLayerIndex += 1;
						break;
					case Keys.Down:
						bedPlate.ActiveLayerIndex -= 1;
						break;
				}
			}
		}

		private GCodeFile loadedGCode => printer.BedPlate.LoadedGCode;

		internal void CreateAndAddChildren()
		{
			CloseAllChildren();

			gcode2DWidget = null;
			gcodeProcessingStateInfoText = null;

			var mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				VAnchor = VAnchor.Max_FitToChildren_ParentHeight
			};

			buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(3, 3),
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};

			layerSelectionButtonsPanel = new FlowLayoutWidget(FlowDirection.RightToLeft)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = 0,
			};

			gcodeDisplayWidget = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop
			};

			if (printItem != null)
			{
				SetProcessingMessage("Loading G-Code...".Localize());

				bool isGCode = Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE";

				string gcodeFilePath = isGCode ? printItem.FileLocation : printItem.GetGCodePathAndFileName();
				if (File.Exists(gcodeFilePath))
				{
					gcode2DWidget = new GCode2DWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter)
					{
						Visible = (activeViewMode == PartViewMode.Layers2D)
					};
					gcodeDisplayWidget.AddChild(gcode2DWidget);
				}
				else
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, printItem.Name));
				}
			}

			mainContainerTopToBottom.AddChild(gcodeDisplayWidget);

			// add in a spacer
			layerSelectionButtonsPanel.AddChild(new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight
			});
			buttonBottomPanel.AddChild(layerSelectionButtonsPanel);

			mainContainerTopToBottom.AddChild(buttonBottomPanel);
			this.AddChild(mainContainerTopToBottom);

			viewControls3D.ResetView += (sender, e) =>
			{
				if (gcodeDisplayWidget.Visible)
				{
					gcode2DWidget.CenterPartInView();
				}
			};

			viewControlsToggle = new ViewControlsToggle(ApplicationController.Instance.Theme.ViewControlsButtonFactory, activeViewMode)
			{
				Visible = false,
				HAnchor = HAnchor.ParentRight
			};
			viewControlsToggle.ViewModeChanged += (s, e) =>
			{
				// Respond to user driven view mode change events and store and switch to the new mode
				activeViewMode = e.ViewMode;
				SwitchViewModes();
			};
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
			this.AddChild(viewControlsToggle);

			this.AddGCodeFileControls(ApplicationController.Instance.Printer, ApplicationController.Instance.Theme);
		}

		private void AddGCodeFileControls(PrinterConfig printer, ThemeConfig theme)
		{
			SetProcessingMessage("");
			if (gcode2DWidget != null
				&& loadedGCode == null)
			{
				// If we have finished loading the gcode and the source file exists but we don't have any loaded gcode it is because the loader decided to not load it.
				if (File.Exists(printItem.FileLocation))
				{
					SetProcessingMessage(string.Format(fileTooBigToLoad, printItem.Name));
				}
				else
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, Path.GetFileName(printItem.FileLocation)));
				}
			}

			if (loadedGCode?.LineCount > 0)
			{
				gradientWidget = new ColorGradientWidget(loadedGCode)
				{
					Margin = new BorderDouble(top: 55, left: 11),
					HAnchor = HAnchor.FitToChildren | HAnchor.ParentLeft,
					VAnchor = VAnchor.ParentTop,
					Visible = options.RenderSpeeds
				};
				AddChild(gradientWidget);

				viewControlsToggle.Visible = true;

				setLayerWidget = new SetLayerWidget(theme.GCodeLayerButtons, printer.BedPlate);
				setLayerWidget.VAnchor = VAnchor.ParentTop;
				layerSelectionButtonsPanel.AddChild(setLayerWidget);

				navigationWidget = new LayerNavigationWidget(theme.GCodeLayerButtons);
				navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
				layerSelectionButtonsPanel.AddChild(navigationWidget);

				GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;

				this.gcodeDetails = new GCodeDetails(this.loadedGCode);

				this.AddChild(new GCodeDetailsView(gcodeDetails)
				{
					Margin = new BorderDouble(0, 0, 35, 5),
					Padding = new BorderDouble(10),
					BackgroundColor = new RGBA_Bytes(0, 0, 0, ViewControlsBase.overlayAlpha),
					HAnchor = HAnchor.ParentRight | HAnchor.AbsolutePosition,
					VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren,
					Width = 150
				});

				//UiThread.RunOnIdle(SetSyncToPrintVisibility);

				// Switch to the most recent view mode, defaulting to Layers3D
				SwitchViewModes();
			}
		}


		private RenderType GetRenderType()
		{
			var options = ApplicationController.Instance.Printer.BedPlate.RendererOptions;

			RenderType renderType = RenderType.Extrusions;
			if (options.RenderMoves)
			{
				renderType |= RenderType.Moves;
			}
			if (options.RenderRetractions)
			{
				renderType |= RenderType.Retractions;
			}
			if (options.RenderSpeeds)
			{
				renderType |= RenderType.SpeedColors;
			}
			if (options.SimulateExtrusion)
			{
				renderType |= RenderType.SimulateExtrusion;
			}
			if (options.TransparentExtrusion)
			{
				renderType |= RenderType.TransparentExtrusion;
			}
			if (options.HideExtruderOffsets)
			{
				renderType |= RenderType.HideExtruderOffsets;
			}

			return renderType;
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			if (loadedGCode == null || printer.BedPlate.GCodeRenderer == null || !this.Visible)
			{
				return;
			}

			printer.BedPlate.Render3DLayerFeatures();
		}

		private GCodeDetails gcodeDetails;

		private void SwitchViewModes()
		{
			bool inLayers3DMode = activeViewMode == PartViewMode.Layers3D;
			if (inLayers3DMode)
			{
				UserSettings.Instance.set("LayerViewDefault", "3D Layer");
			}
			else
			{
				UserSettings.Instance.set("LayerViewDefault", "2D Layer");

				// HACK: Getting the Layer2D view to show content only works if CenterPartInView is called after the control is visible and after some cycles have passed
				UiThread.RunOnIdle(gcode2DWidget.CenterPartInView);
			}

			if (gcode2DWidget != null)
			{
				gcode2DWidget.Visible = !inLayers3DMode;
			}
		}

		internal void LoadProgress_Changed((double progress0To1, string processingState) progress, CancellationTokenSource continueProcessing)
		{
			SetProcessingMessage(string.Format("{0} {1:0}%...", gcodeLoading, progress.progress0To1 * 100));
			if(this.HasBeenClosed)
			{
				continueProcessing.Cancel();
			}
		}

		private void SetProcessingMessage(string message)
		{
			if (gcodeProcessingStateInfoText == null)
			{
				gcodeProcessingStateInfoText = new TextWidget(message)
				{
					HAnchor = HAnchor.ParentCenter,
					VAnchor = VAnchor.ParentCenter,
					AutoExpandBoundsToText = true
				};

				var labelContainer = new GuiWidget();
				labelContainer.Selectable = false;
				labelContainer.AnchorAll();
				labelContainer.AddChild(gcodeProcessingStateInfoText);

				gcodeDisplayWidget.AddChild(labelContainer);
			}

			if (message == "")
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.Transparent;
			}
			else
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.White;
			}

			gcodeProcessingStateInfoText.Text = message;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			// Find and unhook the parent system window KeyDown event
			if (parentSystemWindow != null)
			{
				parentSystemWindow.KeyDown -= Parent_KeyDown;
			}

			if (externalMeshViewer != null)
			{
				externalMeshViewer.TrackballTumbleWidget.DrawGlContent -= TrackballTumbleWidget_DrawGlContent;
			}

			if (printItem != null)
			{
				if (startedSliceFromGenerateButton && printItem.CurrentlySlicing)
				{
					SlicingQueue.Instance.CancelCurrentSlicing();
				}
			}

			base.OnClosed(e);
		}
	}
}
