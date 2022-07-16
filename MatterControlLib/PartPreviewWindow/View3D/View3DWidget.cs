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
#define ENABLE_PERSPECTIVE_PROJECTION_DYNAMIC_NEAR_FAR

using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class View3DWidget : GuiWidget, IDrawable
	{
		// Padded by this amount on each side, in case of unaccounted for scene geometry.
		// For orthographic, this is an offset on either side scaled by far - near.
		// For perspective, this + 1 is used to scale the near and far planes.
		private const double DynamicNearFarBoundsPaddingFactor = 0.1;

		private bool deferEditorTillMouseUp = false;
		private bool expandSelection;

		public int EditButtonHeight { get; set; } = 44;

		public Matrix4X4 TransformOnMouseDown { get; private set; } = Matrix4X4.Identity;

		private readonly TreeView treeView;

		private readonly PrinterConfig printer;

		private readonly ThemeConfig theme;

		public Vector3 BedCenter
		{
			get
			{
				return new Vector3(sceneContext.BedCenter);
			}
		}

		public TrackballTumbleWidgetExtended TrackballTumbleWidget { get; private set; }

		public Object3DControlsLayer Object3DControlLayer { get; }

		public ISceneContext sceneContext;

		public PrinterConfig Printer { get; private set; }

		private readonly PrinterTabPage printerTabPage;
		private RadioIconButton translateButton;
		private RadioIconButton rotateButton;
		private RadioIconButton zoomButton;
		private RadioIconButton partSelectButton;

		public View3DWidget(PrinterConfig printer, ISceneContext sceneContext, ViewToolBarControls viewControls3D, ThemeConfig theme, DesignTabPage printerTabBase, Object3DControlsLayer.EditorType editorType = Object3DControlsLayer.EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.printerTabPage = printerTabBase as PrinterTabPage;
			this.Printer = printer;

			this.Object3DControlLayer = new Object3DControlsLayer(sceneContext, theme, editorType)
			{
				Name = "Object3DControlLayer",
			};
			this.Object3DControlLayer.AnchorAll();

			// Register ourself as an IDrawable
			this.Object3DControlLayer.RegisterDrawable(this);

			this.viewControls3D = viewControls3D;
			this.printer = printer;
			this.theme = theme;
			this.Name = "View3DWidget";
			this.BackgroundColor = theme.BedBackgroundColor;
			this.HAnchor = HAnchor.Stretch; // HAnchor.MaxFitOrStretch,
			this.VAnchor = VAnchor.Stretch; //  VAnchor.MaxFitOrStretch

			viewControls3D.TransformStateChanged += ViewControls3D_TransformStateChanged;

			// MeshViewer
			TrackballTumbleWidget = new TrackballTumbleWidgetExtended(sceneContext.World, this, Object3DControlLayer, theme)
			{
				TransformState = TrackBallTransformType.Rotation
			};

			TrackballTumbleWidget.GetNearFar = GetNearFar;

			TrackballTumbleWidget.AnchorAll();

			this.BoundsChanged += UpdateRenderView;

			// TumbleWidget
			this.Object3DControlLayer.AddChild(TrackballTumbleWidget);

			this.Object3DControlLayer.SetRenderTarget(this);

			// Add splitter support with the InteractionLayer on the left and resize containers on the right
			var splitContainer = new FlowLayoutWidget()
			{
				Name = "SplitContainer",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			splitContainer.AddChild(this.Object3DControlLayer);
			this.AddChild(splitContainer);

			Scene.SelectionChanged += Scene_SelectionChanged;

			this.Scene.Invalidated += Scene_Invalidated;

			this.AnchorAll();

			TrackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;

			selectedObjectPanel = new SelectedObjectPanel(this, sceneContext, theme)
			{
				VAnchor = VAnchor.Stretch,
			};

			modelViewSidePanel = new VerticalResizeContainer(theme, GrabBarSide.Left)
			{
				Width = UserSettings.Instance.SelectedObjectPanelWidth,
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Absolute,
				BackgroundColor = theme.InteractionLayerOverlayColor,
				SplitterBarColor = theme.SplitterBackground,
				SplitterWidth = theme.SplitterWidth,
				MinimumSize = new Vector2(theme.SplitterWidth, 0)
			};
			modelViewSidePanel.BoundsChanged += UpdateRenderView;

			modelViewSidePanel.Resized += ModelViewSidePanel_Resized;

			// add the tree view
			treeView = new TreeView(theme)
			{
				Name = "DesignTree",
				Margin = new BorderDouble(left: theme.DefaultContainerPadding + 12),
			};
			treeView.NodeMouseClick += (s, e) =>
			{
				if (e is MouseEventArgs sourceEvent
					&& s is GuiWidget clickedWidget)
				{
					if (!AggregateSelection(sourceEvent, clickedWidget))
					{
						var object3D = (IObject3D)treeView.SelectedNode.Tag;
						if (object3D != Scene.SelectedItem)
						{
							Scene.SelectedItem = object3D;
						}
					}

					if (sourceEvent.Button == MouseButtons.Right)
					{
						var popupMenu = ApplicationController.Instance.GetActionMenuForSceneItem(true, this);
						popupMenu.ShowMenu(clickedWidget, sourceEvent);
					}
				}
			};
			treeView.ScrollArea.HAnchor = HAnchor.Stretch;

			treeNodeContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(12, 3)
			};
			treeView.AddChild(treeNodeContainer);

			var historyAndProperties = new Splitter()
			{
				Orientation = Orientation.Horizontal,
				Panel1Ratio = sceneContext.ViewState.SceneTreeRatio,
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			historyAndProperties.Panel1.MinimumSize = new Vector2(0, 60);
			historyAndProperties.Panel2.MinimumSize = new Vector2(0, 60);

			modelViewSidePanel.AddChild(historyAndProperties);

			var titleAndTreeView = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			titleAndTreeView.AddChild(workspaceName = new InlineStringEdit(sceneContext.Scene.Name ?? "", theme, "WorkspaceName", editable: false)
			{
				Border = new BorderDouble(top: 1),
				BorderColor = theme.SplitterBackground
			});
			titleAndTreeView.AddChild(treeView);

			workspaceName.ActionArea.AddChild(
				new IconButton(StaticData.Instance.LoadIcon("fa-angle-right_12.png", 12, 12).SetToColor(theme.TextColor), theme)
				{
					Enabled = false
				},
				indexInChildrenList: 0);

			// Remove left margin
			workspaceName.ActionArea.Children<TextWidget>().First().Margin = 0;

			// Resize buttons
			foreach (var iconButton in workspaceName.Descendants<IconButton>())
			{
				iconButton.Height = 26;
				iconButton.Width = 26;
			}

			workspaceName.Margin = workspaceName.Margin.Clone(bottom: 0);

			historyAndProperties.Panel1.AddChild(titleAndTreeView);

			historyAndProperties.DistanceChanged += (s, e) =>
			{
				sceneContext.ViewState.SceneTreeRatio = historyAndProperties.Panel1Ratio;
			};

			historyAndProperties.Panel2.AddChild(selectedObjectPanel);
			splitContainer.AddChild(modelViewSidePanel);

			CreateTumbleCubeAndControls(theme);

			this.Object3DControlLayer.AfterDraw += AfterDraw3DContent;

			Scene.SelectFirstChild();

			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;
			UpdateControlButtons(viewControls3D.ActiveButton);

			sceneContext.SceneLoaded += SceneContext_SceneLoaded;

			if (!AppContext.IsLoading)
			{
				this.RebuildTree();
			}

			if (sceneContext?.Scene?.SelectedItem != null)
			{
				UiThread.RunOnIdle(() =>
				{
					// make sure the selected item is still selected after reload
					using(new SelectionMaintainer(Scene))
                    {
                    }
				});
			}
		}

		private void CreateTumbleCubeAndControls(ThemeConfig theme)
		{
			var controlLayer = this.Object3DControlLayer;
			var scale = GuiWidget.DeviceScale;
			var tumbleCubeControl = new TumbleCubeControl(controlLayer, theme, TrackballTumbleWidget)
			{
				Margin = new BorderDouble(0, 0, 40, 45),
				VAnchor = VAnchor.Top,
				HAnchor = HAnchor.Right,
				Name = "Tumble Cube Control"
			};

			var cubeCenterFromRightTop = new Vector2(tumbleCubeControl.Margin.Right * scale + tumbleCubeControl.Width / 2,
				tumbleCubeControl.Margin.Top * scale + tumbleCubeControl.Height / 2);

			controlLayer.AddChild(tumbleCubeControl);

			var hudBackground = controlLayer.AddChild(new GuiWidget()
			{
				VAnchor = VAnchor.Stretch, 
				HAnchor = HAnchor.Stretch,
				Selectable = false,
				// DoubleBuffer = true
			});

			// hudBackground.BackBuffer.SetRecieveBlender(new BlenderBGRA());

			GuiWidget AddRoundButton(GuiWidget widget, Vector2 offset, bool center = false)
			{
				widget.BackgroundRadius = new RadiusCorners(Math.Min(widget.Width / 2, widget.Height / 2));
				widget.BackgroundOutlineWidth = 1;
				widget.VAnchor = VAnchor.Top;
				widget.HAnchor = HAnchor.Right;
				if (center)
				{
					offset.X -= (widget.Width / 2) / scale;
				}
				
				widget.Margin = new BorderDouble(0, 0, offset.X, offset.Y);
				return controlLayer.AddChild(widget);
			}

			Vector2 RotatedMargin(GuiWidget widget, double angle)
			{
				var radius = 70 * scale;
				var widgetCenter = new Vector2(widget.Width / 2, widget.Height / 2);
				// divide by scale to convert from pixels to margin units
				return (cubeCenterFromRightTop - widgetCenter - new Vector2(0, radius).GetRotated(angle)) / scale;
			}

			// add the view controls
			var buttonGroupA = new ObservableCollection<GuiWidget>();
			partSelectButton = new RadioIconButton(StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "partSelect.png"), 16, 16).SetToColor(theme.TextColor), theme)
			{
				SiblingRadioButtonList = buttonGroupA,
				ToolTipText = "Select Parts".Localize(),
				Margin = theme.ButtonSpacing,
			};

			partSelectButton.MouseEnterBounds += (s, e) => ApplicationController.Instance.UiHint = "Ctrl + A = Select Alll, 'Space' = Clear Selection, 'ESC' = Cancel Drag".Localize();
			partSelectButton.MouseLeaveBounds += (s, e) => ApplicationController.Instance.UiHint = "";
			AddRoundButton(partSelectButton, RotatedMargin(partSelectButton, MathHelper.Tau * .15));
			partSelectButton.Click += (s, e) => viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;
			buttonGroupA.Add(partSelectButton);

			rotateButton = new RadioIconButton(StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "rotate.png"), 16, 16).SetToColor(theme.TextColor), theme)
			{
				SiblingRadioButtonList = buttonGroupA,
				ToolTipText = "Rotate View".Localize(),
				Margin = theme.ButtonSpacing
			};
			rotateButton.MouseEnterBounds += (s, e) => ApplicationController.Instance.UiHint = "Rotate: Right Mouse Button, Ctrl + Left Mouse Button, Arrow Keys".Localize();
			rotateButton.MouseLeaveBounds += (s, e) => ApplicationController.Instance.UiHint = "";
			AddRoundButton(rotateButton, RotatedMargin(rotateButton, MathHelper.Tau * .05));
			rotateButton.Click += (s, e) => viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
			buttonGroupA.Add(rotateButton);

			translateButton = new RadioIconButton(StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "translate.png"), 16, 16).SetToColor(theme.TextColor), theme)
			{
				SiblingRadioButtonList = buttonGroupA,
				ToolTipText = "Move View".Localize(),
				Margin = theme.ButtonSpacing
			};
			translateButton.MouseEnterBounds += (s, e) => ApplicationController.Instance.UiHint = "Move: Middle Mouse Button, Ctrl + Shift + Left Mouse Button, Shift Arrow Keys".Localize();
			translateButton.MouseLeaveBounds += (s, e) => ApplicationController.Instance.UiHint = "";
			AddRoundButton(translateButton, RotatedMargin(translateButton , - MathHelper.Tau * .05));
			translateButton.Click += (s, e) => viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
			buttonGroupA.Add(translateButton);

			zoomButton = new RadioIconButton(StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "scale.png"), 16, 16).SetToColor(theme.TextColor), theme)
			{
				SiblingRadioButtonList = buttonGroupA,
				ToolTipText = "Zoom View".Localize(),
				Margin = theme.ButtonSpacing
			};
			zoomButton.MouseEnterBounds += (s, e) => ApplicationController.Instance.UiHint = "Zoom: Mouse Wheel, Ctrl + Alt + Left Mouse Button, Ctrl + '+' & Ctrl + '-'".Localize();
			zoomButton.MouseLeaveBounds += (s, e) => ApplicationController.Instance.UiHint = "";
			AddRoundButton(zoomButton, RotatedMargin(zoomButton, - MathHelper.Tau * .15));
			zoomButton.Click += (s, e) => viewControls3D.ActiveButton = ViewControls3DButtons.Scale;
			buttonGroupA.Add(zoomButton);

			var bottomButtonOffset = 0;
			var hudBackgroundColor = theme.BedBackgroundColor.WithAlpha(120);
			var hudStrokeColor = theme.TextColor.WithAlpha(120);

			// add the background render for the view controls
			// controlLayer.BeforeDraw += (s, e) => // enable to debug any rendering errors that might be due to double buffered hudBackground
			hudBackground.BeforeDraw += (s, e) =>
			{
				var tumbleCubeRadius = tumbleCubeControl.Width / 2;
				var tumbleCubeCenter = new Vector2(controlLayer.Width - tumbleCubeControl.Margin.Right * scale - tumbleCubeRadius,
					controlLayer.Height - tumbleCubeControl.Margin.Top * scale - tumbleCubeRadius);

				void renderPath(IVertexSource vertexSource, double width)
				{
					var background = new Stroke(vertexSource, width * 2);
					background.LineCap = LineCap.Round;
					e.Graphics2D.Render(background, hudBackgroundColor);
					e.Graphics2D.Render(new Stroke(background, scale), hudStrokeColor);
				}

				void renderRoundedGroup(double spanRatio, double startRatio)
				{
					var angle = MathHelper.Tau * spanRatio;
					var width = 17 * scale;
					var start = MathHelper.Tau * startRatio - angle / 2;
					var end = MathHelper.Tau * startRatio + angle / 2;
					var arc = new Arc(tumbleCubeCenter, tumbleCubeRadius + 12 * scale + width / 2, start, end);

					renderPath(arc, width);
				}

				renderRoundedGroup(.3, .25);
				renderRoundedGroup(.1, .5 + .1);

				// render the perspective and turntable group background
				renderRoundedGroup(.1, 1 - .1); // when we have both ortho and turntable

				void renderRoundedLine(double lineWidth, double heightBelowCenter)
				{
					lineWidth *= scale;
					var width = 17 * scale;
					var height = tumbleCubeCenter.Y - heightBelowCenter * scale;
					var start = tumbleCubeCenter.X - lineWidth;
					var end = tumbleCubeCenter.X + lineWidth;
					var line = new VertexStorage();
					line.MoveTo(start, height);
					line.LineTo(end, height);

					renderPath(line, width);
				}

				tumbleCubeCenter.X += bottomButtonOffset;

				renderRoundedLine(18, 101);

				// e.Graphics2D.Circle(controlLayer.Width - cubeCenterFromRightTop.X, controlLayer.Height - cubeCenterFromRightTop.Y, 150, Color.Cyan);

				// ImageIO.SaveImageData("C:\\temp\\test.png", hudBackground.BackBuffer);
			};

			// add the home button
			var homeButton = new IconButton(StaticData.Instance.LoadIcon("fa-home_16.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Reset View".Localize(),
				Margin = theme.ButtonSpacing
			};
			homeButton.MouseEnterBounds += (s1, e1) => ApplicationController.Instance.UiHint = "W Key";
			homeButton.MouseLeaveBounds += (s1, e1) => ApplicationController.Instance.UiHint = "";
			AddRoundButton(homeButton, RotatedMargin(homeButton, MathHelper.Tau * .3)).Click += (s, e) => viewControls3D.NotifyResetView();

			var zoomToSelectionButton = new IconButton(StaticData.Instance.LoadIcon("select.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Name = "Zoom to selection button",
				ToolTipText = "Zoom to Selection".Localize(),
				Margin = theme.ButtonSpacing
			};
			zoomToSelectionButton.MouseEnterBounds += (s1, e1) => ApplicationController.Instance.UiHint = "Z Key";
			zoomToSelectionButton.MouseLeaveBounds += (s1, e1) => ApplicationController.Instance.UiHint = "";
			void SetZoomEnabled(object s, EventArgs e)
			{
				zoomToSelectionButton.Enabled = this.Scene.SelectedItem != null
					&& (printer == null || printer.ViewState.ViewMode == PartViewMode.Model);
			}

			this.Scene.SelectionChanged += SetZoomEnabled;
			this.Closed += (s, e) => this.Scene.SelectionChanged -= SetZoomEnabled;

			AddRoundButton(zoomToSelectionButton, RotatedMargin(zoomToSelectionButton, MathHelper.Tau * .4)).Click += (s, e) => ZoomToSelection();

			var turntableEnabled = UserSettings.Instance.get(UserSettingsKey.TurntableMode) != "False";
			TrackballTumbleWidget.TurntableEnabled = turntableEnabled;

			var turnTableButton = new RadioIconButton(StaticData.Instance.LoadIcon("spin.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Turntable Mode".Localize(),
				Margin = theme.ButtonSpacing,
				Padding = 2,
				ToggleButton = true,
				SiblingRadioButtonList = new List<GuiWidget>(),
				Checked = turntableEnabled,
				//DoubleBuffer = true,
			};

			AddRoundButton(turnTableButton, RotatedMargin(turnTableButton, -MathHelper.Tau * .4)); // 2 button position
			turnTableButton.CheckedStateChanged += (s, e) =>
			{
				UserSettings.Instance.set(UserSettingsKey.TurntableMode, turnTableButton.Checked.ToString());
				TrackballTumbleWidget.TurntableEnabled = turnTableButton.Checked;
				if (turnTableButton.Checked)
				{
					// Make sure the view has up going the right direction
					// WIP, this should fix the current rotation rather than reset the view
					viewControls3D.NotifyResetView();
				}
			};

			var perspectiveEnabled = UserSettings.Instance.get(UserSettingsKey.PerspectiveMode) != false.ToString();
			TrackballTumbleWidget.ChangeProjectionMode(perspectiveEnabled, false);
			var projectionButton = new RadioIconButton(StaticData.Instance.LoadIcon("perspective.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Name = "Projection mode button",
				ToolTipText = "Perspective Mode".Localize(),
				Margin = theme.ButtonSpacing,
				ToggleButton = true,
				SiblingRadioButtonList = new List<GuiWidget>(),
				Checked = TrackballTumbleWidget.PerspectiveMode,
			};
			AddRoundButton(projectionButton, RotatedMargin(projectionButton, -MathHelper.Tau * .3));
			projectionButton.CheckedStateChanged += (s, e) =>
			{
				UserSettings.Instance.set(UserSettingsKey.PerspectiveMode, projectionButton.Checked.ToString());
				TrackballTumbleWidget.ChangeProjectionMode(projectionButton.Checked, true);
				if (true)
				{
						// Make sure the view has up going the right direction
						// WIP, this should fix the current rotation rather than reset the view
						//ResetView();
				}
				
				Invalidate();
			};

			var startHeight = 180;
			var ySpacing = 40;
			cubeCenterFromRightTop.X -= bottomButtonOffset;

			// put in the bed and build volume buttons
			var bedButton = new RadioIconButton(StaticData.Instance.LoadIcon("bed.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Name = "Bed Button",
				ToolTipText = "Show Print Bed".Localize(),
				Checked = sceneContext.RendererOptions.RenderBed,
				ToggleButton = true,
				SiblingRadioButtonList = new List<GuiWidget>(),
			};
			AddRoundButton(bedButton, new Vector2((cubeCenterFromRightTop.X + 18 * scale - bedButton.Width / 2) / scale, startHeight));
			var printAreaButton = new RadioIconButton(StaticData.Instance.LoadIcon("print_area.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Name = "Bed Button",
				ToolTipText = BuildHeightValid() ? "Show Print Area".Localize() : "Define printer build height to enable",
				Checked = sceneContext.RendererOptions.RenderBuildVolume,
				ToggleButton = true,
				Enabled = BuildHeightValid() && printer?.ViewState.ViewMode != PartViewMode.Layers2D && bedButton.Checked,
				SiblingRadioButtonList = new List<GuiWidget>(),
			};

			bedButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBed = bedButton.Checked;
				printAreaButton.Enabled = BuildHeightValid() && printer?.ViewState.ViewMode != PartViewMode.Layers2D && bedButton.Checked;
			};

			bool BuildHeightValid() => sceneContext.BuildHeight > 0;

			AddRoundButton(printAreaButton, new Vector2((cubeCenterFromRightTop.X - 18 * scale - bedButton.Width / 2) / scale, startHeight));

			printAreaButton.CheckedStateChanged += (s, e) =>
			{
				sceneContext.RendererOptions.RenderBuildVolume = printAreaButton.Checked;
			};

			this.BindBedOptions(controlLayer, bedButton, printAreaButton, sceneContext.RendererOptions);

			if (printer != null)
			{
				// Disable print area button in GCode2D view
				void ViewModeChanged(object s, ViewModeChangedEventArgs e)
				{
					// Button is conditionally created based on BuildHeight, only set enabled if created
					printAreaButton.Enabled = BuildHeightValid() && printer.ViewState.ViewMode != PartViewMode.Layers2D;
				}

				printer.ViewState.ViewModeChanged += ViewModeChanged;

				controlLayer.Closed += (s, e) =>
				{
					printer.ViewState.ViewModeChanged -= ViewModeChanged;
				};
			}

			// declare the grid snap button
			GridOptionsPanel gridSnapButton = null;

			// put in the view list buttons
			var modelViewStyleButton = new ViewStyleButton(sceneContext, theme)
			{
				PopupMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Right, MateEdge.Top)
				}
			};
			modelViewStyleButton.AnchorMate.Mate.VerticalEdge = MateEdge.Bottom;
			modelViewStyleButton.AnchorMate.Mate.HorizontalEdge = MateEdge.Right;
			var marginCenter = cubeCenterFromRightTop.X / scale;
			AddRoundButton(modelViewStyleButton, new Vector2(marginCenter, startHeight + 1 * ySpacing), true);
			modelViewStyleButton.BackgroundColor = hudBackgroundColor;
			modelViewStyleButton.BorderColor = hudStrokeColor;

			void ViewState_ViewModeChanged(object sender, ViewModeChangedEventArgs e)
			{
				modelViewStyleButton.Visible = e.ViewMode == PartViewMode.Model;
				gridSnapButton.Visible = modelViewStyleButton.Visible;
			}

			if (printer?.ViewState != null)
			{
				printer.ViewState.ViewModeChanged += ViewState_ViewModeChanged;
			}

			this.Closed += (s, e) =>
			{
				if (printer?.ViewState != null)
				{
					printer.ViewState.ViewModeChanged -= ViewState_ViewModeChanged;
				}
			};

			// Add the grid snap button
			gridSnapButton = new GridOptionsPanel(Object3DControlLayer, theme)
			{
				PopupMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Right, MateEdge.Top)
				}
			};
			gridSnapButton.AnchorMate.Mate.VerticalEdge = MateEdge.Bottom;
			gridSnapButton.AnchorMate.Mate.HorizontalEdge = MateEdge.Right;
			AddRoundButton(gridSnapButton, new Vector2(marginCenter, startHeight + 2 * ySpacing), true);
			gridSnapButton.BackgroundColor = hudBackgroundColor;
			gridSnapButton.BorderColor = hudStrokeColor;

#if DEBUG
			var renderOptionsButton = new RenderOptionsButton(theme, this.Object3DControlLayer)
			{
				ToolTipText = "Debug Render Options".Localize(),
				PopupMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				AnchorMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				}
			};
			AddRoundButton(renderOptionsButton, new Vector2(marginCenter, startHeight + 3 * ySpacing), true);
#endif
		}

		public void ZoomToSelection()
		{
			var selectedItem = this.Scene.SelectedItem;
			if (selectedItem != null)
			{
				TrackballTumbleWidget.ZoomToAABB(selectedItem.GetAxisAlignedBoundingBox());
			}
		}

		public void UpdateControlButtons(ViewControls3DButtons activeTransformState)
		{
			switch (activeTransformState)
			{
				case ViewControls3DButtons.Rotate:
					if (rotateButton != null)
					{
						rotateButton.Checked = true;
					}
					break;

				case ViewControls3DButtons.Translate:
					if (translateButton != null)
					{
						translateButton.Checked = true;
					}
					break;

				case ViewControls3DButtons.Scale:
					if (zoomButton != null)
					{
						zoomButton.Checked = true;
					}
					break;

				case ViewControls3DButtons.PartSelect:
					if (partSelectButton != null)
					{
						partSelectButton.Checked = true;
					}
					break;
			}
		}

		public void BindBedOptions(GuiWidget container, ICheckbox bedButton, ICheckbox printAreaButton, View3DConfig renderOptions)
		{
			void SyncProperties(object s, PropertyChangedEventArgs e)
			{
				switch (e.PropertyName)
				{
					case nameof(renderOptions.RenderBed):
						bedButton.Checked = renderOptions.RenderBed;
						break;

					case nameof(renderOptions.RenderBuildVolume) when printAreaButton != null:
						printAreaButton.Checked = renderOptions.RenderBuildVolume;
						break;
				}
			}

			renderOptions.PropertyChanged += SyncProperties;

			container.Closed += (s, e) =>
			{
				renderOptions.PropertyChanged -= SyncProperties;
			};
		}

		private void GetNearFar(WorldView world, out double zNear, out double zFar)
		{
			// All but the near and far planes.
			Plane[] worldspacePlanes = Frustum.FrustumFromProjectionMatrix(world.ProjectionMatrix).Planes.Take(4)
				.Select(p => p.Transform(world.InverseModelviewMatrix)).ToArray();

			// TODO: (Possibly) Compute a dynamic near plane based on visible triangles rather than clipped AABBs.
			//       Currently, the near and far planes are fit to clipped AABBs in the scene.
			//       A rudimentary implementation to start off with. The significant limitation is that zNear is ~zero when inside an AABB.
			//       The resulting frustum can be visualized using the debug menu.
			//       The far plane is less important.
			//       Other ideas are an infinite far plane, depth clamping, multi-pass rendering, AABB feedback from the previous frame, dedicated scene manager for all 3D rendering.
			Tuple<double, double> nearFar = null;
			foreach (var aabb in Object3DControlLayer.MakeListOfObjectControlBoundingBoxes())
			{
				if (!double.IsNaN(aabb.MinXYZ.X) && !double.IsInfinity(aabb.MinXYZ.X))
				{
					nearFar = ExpandNearAndFarToClippedBounds(nearFar, world.IsOrthographic, worldspacePlanes, world.ModelviewMatrix, aabb);
				}
			}
			nearFar = ExpandNearAndFarToClippedBounds(nearFar, world.IsOrthographic, worldspacePlanes, world.ModelviewMatrix, Object3DControlLayer.GetPrinterNozzleAABB());
			nearFar = ExpandNearAndFarToClippedBounds(nearFar, world.IsOrthographic, worldspacePlanes, world.ModelviewMatrix, Scene.GetAxisAlignedBoundingBox());

			zNear = nearFar != null ? nearFar.Item1 : WorldView.DefaultNearZ;
			zFar = nearFar != null ? nearFar.Item2 : WorldView.DefaultFarZ;

			if (world.IsOrthographic)
			{
				WorldView.SanitiseOrthographicNearFar(ref zNear, ref zFar);

				// Add some padding in case of unaccounted geometry.
				double padding = (zFar - zNear) * DynamicNearFarBoundsPaddingFactor;
				zNear -= padding;
				zFar += padding;

				WorldView.SanitiseOrthographicNearFar(ref zNear, ref zFar);
			}
			else
			{
#if ENABLE_PERSPECTIVE_PROJECTION_DYNAMIC_NEAR_FAR
				WorldView.SanitisePerspectiveNearFar(ref zNear, ref zFar);

				zNear /= 1 + DynamicNearFarBoundsPaddingFactor;
				zFar *= 1 + DynamicNearFarBoundsPaddingFactor;
#else
				zNear = WorldView.DefaultNearZ;
				zFar = WorldView.DefaultFarZ;
#endif
				WorldView.SanitisePerspectiveNearFar(ref zNear, ref zFar);
			}

			System.Diagnostics.Debug.Assert(zNear < zFar && zFar < double.PositiveInfinity);
		}

		private static Tuple<double, double> ExpandNearAndFarToClippedBounds(Tuple<double, double> nearFar, bool isOrthographic, Plane[] worldspacePlanes, Matrix4X4 worldToViewspace, AxisAlignedBoundingBox bounds)
		{
			Tuple<double, double> thisNearFar = CameraFittingUtil.ComputeNearFarOfClippedWorldspaceAABB(isOrthographic, worldspacePlanes, worldToViewspace, bounds);
			if (nearFar == null)
			{
				return thisNearFar;
			}
			else if (thisNearFar == null)
			{
				return nearFar;
			}
			else
			{
				return Tuple.Create(Math.Min(nearFar.Item1, thisNearFar.Item1), Math.Max(nearFar.Item2, thisNearFar.Item2));
			}
		}

		private readonly Dictionary<IObject3D, TreeNode> treeNodesByObject = new Dictionary<IObject3D, TreeNode>();

		private bool watingToScroll = false;

		private void RebuildTree()
		{
			rebuildTreePending = false;
			workspaceName.Text = sceneContext.Scene.Name ?? "";

			if (!watingToScroll)
			{
				// This is a mess. The goal is that the tree view will not scroll as we add and remove items.
				// This is better than always reseting to the top, but only a little.
				watingToScroll = true;
				beforeReubildScrollPosition = treeView.TopLeftOffset;

				UiThread.RunOnIdle(() =>
				{
					treeView.TopLeftOffset = beforeReubildScrollPosition;
					watingToScroll = false;
				}, .5);
			}

			// Top level selection only - rebuild tree
			treeNodeContainer.CloseChildren();

			treeNodesByObject.Clear();

			IEnumerable<IObject3D> children = sceneContext.Scene.Children;
			if (children.Any(c => c is SelectionGroupObject3D))
			{
				var selectionChildern = children.Where(c => c is SelectionGroupObject3D).First().Children;
				var notSelectionChildern = children.Where(c => !(c is SelectionGroupObject3D));
				children = selectionChildern.Union(notSelectionChildern);
			}

			foreach (var child in children.OrderBy(i => i.Name))
			{
				if (child.GetType().GetCustomAttributes(typeof(HideFromTreeViewAttribute), true).Any())
				{
					continue;
				}

				var rootNode = Object3DTreeBuilder.BuildTree(child, treeNodesByObject, theme);
				treeNodeContainer.AddChild(rootNode);
				rootNode.TreeView = treeView;
			}

			void HighLightSelection()
			{
				var scene = sceneContext.Scene;
				var nodes = treeNodeContainer.DescendantsAndSelf().Where(t => t is TreeNode);
				foreach (var uncastNode in nodes)
				{
					var node = (TreeNode)uncastNode;
					if (node.Tag is IObject3D item)
					{
						if (scene.SelectedItem == item
							|| (scene.SelectedItem is SelectionGroupObject3D selectionGroup
								&& selectionGroup.Children.Contains(item)))
						{
							node.HighlightRegion.BackgroundColor = theme.AccentMimimalOverlay;
						}
						else
						{
							node.HighlightRegion.BackgroundColor = Color.Transparent;
						}
					}
				}
			}

			// Ensure selectedItem is selected
			var selectedItem = sceneContext.Scene.SelectedItem;
			if (selectedItem != null
				&& treeNodesByObject.TryGetValue(selectedItem, out TreeNode treeNode))
			{
				treeView.SelectedNode = treeNode;
				if (expandSelection)
				{
					expandSelection = false;
					treeNode.Expanded = true;
				}
			}

			treeView.TopLeftOffset = beforeReubildScrollPosition;

			HighLightSelection();

			Invalidate();
		}

		private void ModelViewSidePanel_Resized(object sender, EventArgs e)
		{
			UserSettings.Instance.SelectedObjectPanelWidth = selectedObjectPanel.Width;
		}

		private void UpdateRenderView(object sender, EventArgs e)
		{
			UiThread.RunOnUiThread(() => TrackballTumbleWidget.CenterOffsetX = -modelViewSidePanel.Width);
		}

		private void SceneContext_SceneLoaded(object sender, EventArgs e)
		{
			if (AppContext.IsLoading)
			{
				return;
			}

			if (printerTabPage?.PrinterActionsBar?.sliceButton is GuiWidget sliceButton)
			{
				sliceButton.Enabled = sceneContext.EditableScene;
			}

			if (printerTabPage?.PrinterActionsBar?.modelViewButton is GuiWidget button)
			{
				button.Enabled = sceneContext.EditableScene;

				if (sceneContext.ContentType == "gcode"
					&& printerTabPage?.PrinterActionsBar?.layers3DButton is GuiWidget gcodeButton)
				{
					gcodeButton.InvokeClick();
				}
			}

			this.RebuildTree();

			this.Invalidate();
		}

		public void PushToPrinterAndPrint()
		{
			// If invoked from a printer tab, simply start the print
			if (this.Printer != null)
			{
				// Save any pending changes before starting print operation
				ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), printer, printer.Bed.SaveChanges).ContinueWith(task =>
				{
					ApplicationController.Instance.PrintPart(
						printer.Bed.EditContext,
						printer,
						null,
						CancellationToken.None,
						PrinterConnection.PrintingModes.Normal).ConfigureAwait(false);
				});
			}
			else if (ProfileManager.Instance.ActiveProfiles.Count() <= 0)
			{
				// If no printer profiles exist, show the printer setup wizard
				var window = DialogWindow.Show(new SetupStepMakeModelName(false));
				window.Closed += (s2, e2) =>
				{
					if (ApplicationController.Instance.ActivePrinters.FirstOrDefault() is PrinterConfig printer
						&& printer.Settings.PrinterSelected)
					{
						CopyPlateToPrinter(sceneContext, printer);
					}
				};
			}
			else if (ApplicationController.Instance.ActivePrinters.Count() is int printerCount && printerCount > 0)
			{
				if (printerCount == 1
					&& ApplicationController.Instance.ActivePrinters.FirstOrDefault() is PrinterConfig firstPrinter)
				{
					// If one printer exists, stash plate with undo operation, then load this scene onto the printer bed
					CopyPlateToPrinter(sceneContext, firstPrinter);
				}
				else
				{
					// If multiple active printers exist, show select printer dialog
					DialogWindow.Show(
						new OpenPrinterPage(
							"Next".Localize(),
							(selectedPrinter) =>
							{
								if (selectedPrinter?.Settings.PrinterSelected == true)
								{
									CopyPlateToPrinter(sceneContext, selectedPrinter);
								}
							}));
				}
			}
			else if (ProfileManager.Instance.ActiveProfiles.Any())
			{
				// If no active printer but profiles exist, show select printer
				DialogWindow.Show(
					new OpenPrinterPage(
						"Next".Localize(),
						(loadedPrinter) =>
						{
							if (loadedPrinter is PrinterConfig activePrinter
								&& activePrinter.Settings.PrinterSelected)
							{
								CopyPlateToPrinter(sceneContext, activePrinter);
							}
						}));
			}
		}

		private static void CopyPlateToPrinter(ISceneContext sceneContext, PrinterConfig printer)
		{
			Task.Run(async () =>
			{
				var scene = sceneContext.Scene;

				// Clear bed to get new MCX on disk for this item
				printer.Bed.ClearPlate();

				if (sceneContext.EditContext.ContentStore == null)
                {
					// The scene is on a bed plate that has never been saved.
					// Create a temp version of the scene so that we can plate it
					var filename = ApplicationController.Instance.SanitizeFileName($"{DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss")}.mcx");
					var mcxPath = Path.Combine(ApplicationDataStorage.Instance.PrintHistoryPath, filename);
					File.WriteAllText(mcxPath, await new Object3D().ToJson());
					var historyContainer = ApplicationController.Instance.Library.PlatingHistory;
					sceneContext = new BedConfig(historyContainer);
					sceneContext.EditContext = new EditContext()
					{
						ContentStore = historyContainer,
						SourceItem = new FileSystemFileItem(mcxPath)
					};

					printer.Bed.Scene.Load(scene.Clone());
				}
				else
                {
					await ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer, sceneContext.SaveChanges);

					// Load current scene into new printer scene
					await printer.Bed.LoadIntoCurrent(sceneContext.EditContext, null);
				}

				bool allInBounds = true;
				foreach (var item in printer.Bed.Scene.VisibleMeshes())
				{
					allInBounds &= printer.InsideBuildVolume(item);
				}

				if (!allInBounds)
				{
					var bounds = printer.Bed.Scene.GetAxisAlignedBoundingBox();
					var boundsCenter = bounds.Center;
					// don't move the z of our stuff
					boundsCenter.Z = 0;

					if (bounds.XSize <= printer.Bed.ViewerVolume.X
						&& bounds.YSize <= printer.Bed.ViewerVolume.Y)
					{
						// center the collection of stuff
						var bedCenter = new Vector3(printer.Bed.BedCenter);

						foreach (var item in printer.Bed.Scene.Children)
						{
							item.Matrix *= Matrix4X4.CreateTranslation(-boundsCenter + bedCenter);
						}
					}
					else
					{
						// arrange the stuff the best we can
						await printer.Bed.Scene.AutoArrangeChildren(new Vector3(printer.Bed.BedCenter));
					}
				}

				// Switch to printer
				ApplicationController.Instance.MainView.TabControl.SelectedTabKey = printer.PrinterName;

				// Save any pending changes before starting print operation
				await ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), printer, printer.Bed.SaveChanges);

				// Slice and print
				await ApplicationController.Instance.PrintPart(
					printer.Bed.EditContext,
					printer,
					null,
					CancellationToken.None,
					PrinterConnection.PrintingModes.Normal);
			});
		}

		private void ViewControls3D_TransformStateChanged(object sender, TransformStateChangedEventArgs e)
		{
			switch (e.TransformMode)
			{
				case ViewControls3DButtons.Rotate:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;
					break;

				case ViewControls3DButtons.Translate:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Translation;
					break;

				case ViewControls3DButtons.Scale:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Scale;
					break;

				case ViewControls3DButtons.PartSelect:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.None;
					break;
			}
		}

		public void SelectAll()
		{
			Scene.ClearSelection();

			// Select All - set selection to all scene children
			Scene.SetSelection(Scene.Children.ToList());
		}

		public void AddUndoOperation(IUndoRedoCommand operation)
		{
			Scene.UndoBuffer.Add(operation);
		}

		public bool DisplayAllValueData { get; set; }

		public override void OnClosed(EventArgs e)
		{
			viewControls3D.TransformStateChanged -= ViewControls3D_TransformStateChanged;

			// Release events
			this.Scene.SelectionChanged -= Scene_SelectionChanged;
			this.Scene.Invalidated -= Scene_Invalidated;

			sceneContext.SceneLoaded -= SceneContext_SceneLoaded;
			modelViewSidePanel.Resized -= ModelViewSidePanel_Resized;

			if (this.Object3DControlLayer != null)
			{
				this.Object3DControlLayer.AfterDraw -= AfterDraw3DContent;
			}

			base.OnClosed(e);
		}

		private GuiWidget topMostParent;

		private readonly PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

		public bool DragOperationActive { get; private set; }

		public InsertionGroupObject3D DragDropObject { get; private set; }

		public ILibraryAssetStream SceneReplacement { get; private set; }

		/// <summary>
		/// Provides a View3DWidget specific drag implementation
		/// </summary>
		/// <param name="screenSpaceMousePosition">The screen space mouse position.</param>
		public void ExternalDragOver(Vector2 screenSpaceMousePosition, GuiWidget sourceWidget)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			// If the mouse is within the MeshViewer process the Drag move
			var meshViewerScreenBounds = this.Object3DControlLayer.TransformToScreenSpace(this.Object3DControlLayer.LocalBounds);
			if (meshViewerScreenBounds.Contains(screenSpaceMousePosition))
			{
				// If already started, process drag move
				if (this.DragOperationActive)
				{
					this.DragOver(screenSpaceMousePosition);
				}
				else
				{
					if (this.Printer != null
						&& this.Printer.ViewState.ViewMode != PartViewMode.Model)
					{
						this.Printer.ViewState.ViewMode = PartViewMode.Model;
					}

					IEnumerable<ILibraryItem> selectedItems;

					if (sourceWidget is LibraryListView listView)
					{
						// Project from ListViewItem to ILibraryItem
						selectedItems = listView.SelectedItems.Select(l => l.Model);
					}
					else // Project from ListViewItem to ILibraryItem
					{
						selectedItems = Enumerable.Empty<ILibraryItem>();
					}

					// Otherwise begin an externally started DragDropOperation hard-coded to use LibraryView->SelectedItems
					this.StartDragDrop(selectedItems, screenSpaceMousePosition);
				}
			}
		}

		private void DragOver(Vector2 screenSpaceMousePosition)
		{
			IObject3D selectedItem = Scene.SelectedItem;
			// Move the object being dragged
			if (this.DragOperationActive
				&& this.DragDropObject != null
				&& selectedItem != null)
			{
				// Move the DropDropObject the target item
				DragSelectedObject(selectedItem, localMousePosition: this.Object3DControlLayer.TransformFromScreenSpace(screenSpaceMousePosition));
			}
		}

		private void StartDragDrop(IEnumerable<ILibraryItem> items, Vector2 screenSpaceMousePosition, bool trackSourceFiles = false)
		{
			this.DragOperationActive = true;

			// ContentStore is null for plated gcode, call ClearPlate to exit mode and return to bed mcx
			// Unsaved New Design also have a null ContentStore but they don't have gcode, so test both.
			if (sceneContext.Printer?.Bed?.LoadedGCode != null
				&& sceneContext.EditContext.ContentStore == null)
			{
				this.ClearPlate(); 
			}

			var firstItem = items.FirstOrDefault();

			if ((firstItem is ILibraryAssetStream contentStream
				&& contentStream.ContentType == "gcode")
				|| firstItem is SceneReplacementFileItem)
			{
				DragDropObject = null;
				this.SceneReplacement = firstItem as ILibraryAssetStream;

				// TODO: Figure out a mechanism to disable View3DWidget with dark overlay, displaying something like "Switch to xxx.gcode", make disappear on mouseLeaveBounds and dragfinish
				this.Object3DControlLayer.BackgroundColor = new Color(Color.Black, 200);

				return;
			}

			// Set the hitplane to the bed plane
			CurrentSelectInfo.HitPlane = bedPlane;

			// Add item to scene
			var insertionGroup = sceneContext.AddToPlate(items, Vector2.Zero, moveToOpenPosition: false);

			// Find intersection position of the mouse with the bed plane
			var intersectInfo = GetIntersectPosition(screenSpaceMousePosition);
			if (intersectInfo != null)
			{
				CalculateDragStartPosition(insertionGroup, intersectInfo);
			}
			else
			{
				CurrentSelectInfo.LastMoveDelta = Vector3.PositiveInfinity;
			}

			this.deferEditorTillMouseUp = true;

			Scene.SelectedItem = insertionGroup;

			this.DragDropObject = insertionGroup;
		}

		private void CalculateDragStartPosition(IObject3D insertionGroup, IntersectInfo intersectInfo)
		{
			// Set the initial transform on the inject part to the current transform mouse position
			var sourceItemBounds = insertionGroup.GetAxisAlignedBoundingBox();
			var center = sourceItemBounds.Center;

			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(-center.X, -center.Y, -sourceItemBounds.MinXYZ.Z);
			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(new Vector3(intersectInfo.HitPosition));

			CurrentSelectInfo.PlaneDownHitPos = intersectInfo.HitPosition;
			CurrentSelectInfo.LastMoveDelta = Vector3.Zero;
		}

		internal void FinishDrop(bool mouseUpInBounds)
		{
			if (this.DragOperationActive)
			{
				this.Object3DControlLayer.BackgroundColor = Color.Transparent;
				this.DragOperationActive = false;

				if (mouseUpInBounds)
				{
					if (this.DragDropObject == null
						&& this.SceneReplacement != null)
					{
						// Drop handler for special case of GCode or similar (change loaded scene to new context)
						sceneContext.LoadContent(
							new EditContext()
							{
								SourceItem = this.SceneReplacement,
								// No content store for GCode
								ContentStore = null
							}, null).ConfigureAwait(false);

						this.SceneReplacement = null;
					}
				}
				else
				{
					Scene.Children.Modify(list => list.Remove(this.DragDropObject));
					Scene.ClearSelection();
				}

				this.DragDropObject = null;

				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);

				Scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));

				// Set focus to View3DWidget after drag-drop
				this.Focus();
			}
		}

		public override void OnLoad(EventArgs args)
		{
			topMostParent = this.TopmostParent();

			// Set reference on show
			var dragDropData = ApplicationController.Instance.DragDropData;
			dragDropData.View3DWidget = this;
			dragDropData.SceneContext = sceneContext;

			base.OnLoad(args);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var selectedItem = Scene.SelectedItem;

			if (selectedItem != null)
			{
				foreach (var volume in this.Object3DControlLayer.Object3DControls)
				{
					volume.SetPosition(selectedItem, CurrentSelectInfo);
				}
			}

			TrackballTumbleWidget.RecalculateProjection();

			base.OnDraw(graphics2D);
		}

		private void AfterDraw3DContent(object sender, DrawEventArgs e)
		{
			if (DragSelectionInProgress)
			{
				var selectionRectangle = new RectangleDouble(DragSelectionStartPosition, DragSelectionEndPosition);
				e.Graphics2D.Rectangle(selectionRectangle, Color.Red);
			}
		}

		private bool foundTriangleInSelectionBounds;

		private void DoRectangleSelection(DrawEventArgs e)
		{
			var allResults = new List<BvhIterator>();

			var matchingSceneChildren = Scene.Children.Where(item =>
			{
				foundTriangleInSelectionBounds = false;

				// Filter the IPrimitive trace data finding matches as defined in InSelectionBounds
				var filteredResults = item.GetBVHData().Filter(InSelectionBounds);

				// Accumulate all matching BvhIterator results for debug rendering
				allResults.AddRange(filteredResults);

				return foundTriangleInSelectionBounds;
			});

			// Apply selection
			if (matchingSceneChildren.Any())
			{
				// If we are actually doing the selection rather than debugging the data
				if (e == null)
				{
					Scene.ClearSelection();
					Scene.SetSelection(matchingSceneChildren.ToList());
					if (Scene.SelectedItem is SelectionGroupObject3D selectionGroup)
					{
						selectionGroup.Expanded = true;
					}
				}
				else
				{
					Object3DControlsLayer.RenderBounds(e, sceneContext.World, allResults);
				}
			}
		}

		private bool InSelectionBounds(BvhIterator iterator)
		{
			var world = sceneContext?.World;
			if (world == null 
				|| iterator == null
				|| iterator.Bvh == null)
            {
				return false;
            }

			var selectionRectangle = new RectangleDouble(DragSelectionStartPosition, DragSelectionEndPosition);

			var traceBottoms = new Vector2[4];
			var traceTops = new Vector2[4];

			if (foundTriangleInSelectionBounds)
			{
				return false;
			}

			if (iterator.Bvh is ITriangle tri)
			{
				// check if any vertex in screen rect
				// calculate all the top and bottom screen positions
				for (int i = 0; i < 3; i++)
				{
					Vector3 bottomStartPosition = Vector3Ex.Transform(tri.GetVertex(i), iterator.TransformToWorld);
					traceBottoms[i] = world.GetScreenPosition(bottomStartPosition);
				}

				for (int i = 0; i < 3; i++)
				{
					if (selectionRectangle.ClipLine(traceBottoms[i], traceBottoms[(i + 1) % 3]))
					{
						foundTriangleInSelectionBounds = true;
						return true;
					}
				}
			}
			else
			{
				// calculate all the top and bottom screen positions
				for (int i = 0; i < 4; i++)
				{
					Vector3 bottomStartPosition = Vector3Ex.Transform(iterator.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), iterator.TransformToWorld);
					traceBottoms[i] = world.GetScreenPosition(bottomStartPosition);

					Vector3 topStartPosition = Vector3Ex.Transform(iterator.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), iterator.TransformToWorld);
					traceTops[i] = world.GetScreenPosition(topStartPosition);
				}

				RectangleDouble.OutCode allPoints = RectangleDouble.OutCode.Inside;
				// check if we are inside all the points
				for (int i = 0; i < 4; i++)
				{
					allPoints |= selectionRectangle.ComputeOutCode(traceBottoms[i]);
					allPoints |= selectionRectangle.ComputeOutCode(traceTops[i]);
				}

				if (allPoints == RectangleDouble.OutCode.Surrounded)
				{
					return true;
				}

				for (int i = 0; i < 4; i++)
				{
					if (selectionRectangle.ClipLine(traceBottoms[i], traceBottoms[(i + 1) % 4])
						|| selectionRectangle.ClipLine(traceTops[i], traceTops[(i + 1) % 4])
						|| selectionRectangle.ClipLine(traceTops[i], traceBottoms[i]))
					{
						return true;
					}
				}
			}

			return false;
		}

		private ViewControls3DButtons? activeButtonBeforeMouseOverride = null;

		private Vector2 lastMouseMove;
		private Vector2 mouseDownPositon = Vector2.Zero;
		private Matrix4X4 worldMatrixOnMouseDown;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			using (new QuickTimer("View3DWidget_OnMouseDown", .3))
			{
				var selectedItem = Scene.SelectedItem;
				mouseDownPositon = mouseEvent.Position;
				worldMatrixOnMouseDown = sceneContext.World.GetTransform4X4();
				// Show transform override
				if (activeButtonBeforeMouseOverride == null
					&& (mouseEvent.Button == MouseButtons.Right || Keyboard.IsKeyDown(Keys.Control)))
				{
					if (Keyboard.IsKeyDown(Keys.Shift))
					{
						activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
						viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
					}
					else if (Keyboard.IsKeyDown(Keys.Alt))
					{
						activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
						viewControls3D.ActiveButton = ViewControls3DButtons.Scale;
					}
					else
					{
						activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
						viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
					}
				}
				else if (activeButtonBeforeMouseOverride == null && mouseEvent.Button == MouseButtons.Middle)
				{
					activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
					viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
				}

				if (mouseEvent.Button == MouseButtons.Right ||
					mouseEvent.Button == MouseButtons.Middle)
				{
					this.Object3DControlLayer.SuppressObject3DControls = true;
				}

				base.OnMouseDown(mouseEvent);

				if (TrackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse
					&& sceneContext.ViewState.ModelView)
				{
					if ((mouseEvent.Button == MouseButtons.Left
						&& viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect
						&& ModifierKeys == Keys.Shift)
						|| (TrackballTumbleWidget.TransformState == TrackBallTransformType.None
							&& ModifierKeys != Keys.Control
							&& ModifierKeys != Keys.Alt))
					{
						if (!this.Object3DControlLayer.MouseDownOnObject3DControlVolume)
						{
							this.Object3DControlLayer.SuppressObject3DControls = true;

							IObject3D hitObject = null;
							IntersectInfo info = null;
							using (new QuickTimer("View3DWidget_OnMouseDown_FindHitObject3D", .3))
							{
								hitObject = FindHitObject3D(mouseEvent.Position, out info);
							}

							if (hitObject == null)
							{
								if (selectedItem != null)
								{
									Scene.ClearSelection();
								}

								// start a selection rect
								DragSelectionStartPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
								DragSelectionEndPosition = DragSelectionStartPosition;
								DragSelectionInProgress = true;
							}
							else
							{
								CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.Z, null);

								if (hitObject != selectedItem)
								{
									if (selectedItem == null)
									{
										// No selection exists
										Scene.SelectedItem = hitObject;
									}
									else if ((ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
										&& !selectedItem.Children.Contains(hitObject))
									{
										expandSelection = !(selectedItem is SelectionGroupObject3D);
										Scene.AddToSelection(hitObject);
									}
									else if (selectedItem == hitObject || selectedItem.Children.Contains(hitObject))
									{
										// Selection should not be cleared and drag should occur
									}
									else if (ModifierKeys != Keys.Shift)
									{
										Scene.SelectedItem = hitObject;
									}

									// Selection may have changed, update local reference to current value
									selectedItem = Scene.SelectedItem;

									Invalidate();
								}

								TransformOnMouseDown = selectedItem.Matrix;

								Invalidate();
								CurrentSelectInfo.DownOnPart = true;

								AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

								if (info.HitPosition.X < selectedBounds.Center.X)
								{
									if (info.HitPosition.Y < selectedBounds.Center.Y)
									{
										CurrentSelectInfo.HitQuadrant = HitQuadrant.LB;
									}
									else
									{
										CurrentSelectInfo.HitQuadrant = HitQuadrant.LT;
									}
								}
								else
								{
									if (info.HitPosition.Y < selectedBounds.Center.Y)
									{
										CurrentSelectInfo.HitQuadrant = HitQuadrant.RB;
									}
									else
									{
										CurrentSelectInfo.HitQuadrant = HitQuadrant.RT;
									}
								}
							}
						}
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			IObject3D selectedItem = Scene.SelectedItem;

			lastMouseMove = mouseEvent.Position;
			if (lastMouseMove != mouseDownPositon)
			{
				mouseDownPositon = Vector2.Zero;
			}

			// File system Drop validation
			mouseEvent.AcceptDrop = this.AllowDragDrop()
					&& mouseEvent.DragFiles?.Count > 0
					&& mouseEvent.DragFiles.TrueForAll(filePath =>
					{
						return filePath.StartsWith("html:", StringComparison.OrdinalIgnoreCase)
							|| filePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
							|| filePath.StartsWith("text:", StringComparison.OrdinalIgnoreCase)
							|| ApplicationController.Instance.IsLoadableFile(filePath)
								// Disallow GCode drop in part view
								&& (this.Printer != null || !string.Equals(System.IO.Path.GetExtension(filePath), ".gcode", StringComparison.OrdinalIgnoreCase));
					});

			// View3DWidgets Filesystem DropDrop handler
			if (mouseEvent.AcceptDrop
				&& this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (this.DragOperationActive)
				{
					DragOver(screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position));
				}
				else
				{
					// Project DragFiles to IEnumerable<FileSystemFileItem>
					this.StartDragDrop(
						mouseEvent.DragFiles.Select<string, ILibraryItem>(path =>
						{
							if (path.StartsWith("html:"))
							{
								var html = path;

								int startTagPosition = html.IndexOf("<html");

								html = html.Substring(startTagPosition);

								// Parse HTML into something usable for the scene
								var parser = new HtmlParser();
								var document = parser.ParseDocument(html);

								// TODO: This needs to become much smarter. Ideally it would inject a yet to be built Object3D for HTML
								// snippets which could initially infer the content to use but would allow for interactive selection.
								// There's already a model for this in the experimental SVG tool. For now, find any embedded svg
								if (document.QuerySelector("img") is IElement img)
								{
									path = img.Attributes["src"].Value;
								}
								else
								{
									// If no image was found, extract the text content
									path = "text:" + document.DocumentElement.TextContent;
								}
							}

							if (path.StartsWith("data:"))
							{
								// Basic support for images encoded as Base64 data urls
								var match = Regex.Match(path, @"data:(?<type>.+?);base64,(?<data>.+)");
								var base64Data = match.Groups["data"].Value;
								var contentType = match.Groups["type"].Value;
								var binData = Convert.FromBase64String(base64Data);

								return new BufferLibraryItem(binData, contentType, "unknown");
							}
							else if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
							{
								// Basic support for images via remote urls
								return new RemoteLibraryItem(path, null);
							}
							else if (path.StartsWith("text:"))
							{
								return new OnDemandLibraryItem("xxx")
								{
									Object3DProvider = async () =>
									{
										var text = new TextObject3D()
										{
											NameToWrite = path.Substring(5)
										};

										await text.Rebuild();

										return text;
									}
								};
							}
							else
							{
								return new FileSystemFileItem(path);
							}
						}),
						screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position),
						trackSourceFiles: true);
				}
			}

			if (CurrentSelectInfo.DownOnPart
				&& TrackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& selectedItem != null)
			{
				DragSelectedObject(selectedItem, new Vector2(mouseEvent.X, mouseEvent.Y));
			}

			if (DragSelectionInProgress)
			{
				DragSelectionEndPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
				DragSelectionEndPosition = new Vector2(
					Math.Max(Math.Min((double)DragSelectionEndPosition.X, this.Object3DControlLayer.LocalBounds.Right), this.Object3DControlLayer.LocalBounds.Left),
					Math.Max(Math.Min((double)DragSelectionEndPosition.Y, this.Object3DControlLayer.LocalBounds.Top), this.Object3DControlLayer.LocalBounds.Bottom));
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public IntersectInfo GetIntersectPosition(Vector2 screenSpacePosition)
		{
			// Translate to local
			Vector2 localPosition = this.Object3DControlLayer.TransformFromScreenSpace(screenSpacePosition);

			Ray ray = sceneContext.World.GetRayForLocalBounds(localPosition);

			return CurrentSelectInfo.HitPlane.GetClosestIntersectionWithinRayDistanceRange(ray);
		}

		public void DragSelectedObject(IObject3D selectedItem, Vector2 localMousePosition)
		{
			if (!PositionWithinLocalBounds(localMousePosition.X, localMousePosition.Y))
			{
				var totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
				selectedItem.Matrix *= totalTransform;
				CurrentSelectInfo.LastMoveDelta = Vector3.Zero;
				Invalidate();
				return;
			}

			Vector2 meshViewerWidgetScreenPosition = this.Object3DControlLayer.TransformFromParentSpace(this, localMousePosition);
			Ray ray = sceneContext.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersectionWithinRayDistanceRange(ray);
			if (info != null)
			{
				if (CurrentSelectInfo.LastMoveDelta == Vector3.PositiveInfinity)
				{
					CalculateDragStartPosition(selectedItem, info);
				}

				// move the mesh back to the start position
				{
					var totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					selectedItem.Matrix *= totalTransform;
				}

				Vector3 delta = info.HitPosition - CurrentSelectInfo.PlaneDownHitPos;

				double snapGridDistance = this.Object3DControlLayer.SnapGridDistance;
				if (snapGridDistance > 0)
				{
					// snap this position to the grid
					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					double xSnapOffset = selectedBounds.MinXYZ.X;
					// snap the x position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.RB
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						xSnapOffset = selectedBounds.MaxXYZ.X;
					}
					double xToSnap = xSnapOffset + delta.X;

					double snappedX = Math.Round(xToSnap / snapGridDistance) * snapGridDistance;
					delta.X = snappedX - xSnapOffset;

					double ySnapOffset = selectedBounds.MinXYZ.Y;
					// snap the y position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.LT
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						ySnapOffset = selectedBounds.MaxXYZ.Y;
					}
					double yToSnap = ySnapOffset + delta.Y;

					double snappedY = Math.Round(yToSnap / snapGridDistance) * snapGridDistance;
					delta.Y = snappedY - ySnapOffset;
				}

				// if the shift key is down only move on the major axis of x or y
				if (Keyboard.IsKeyDown(Keys.ShiftKey))
				{
					if (Math.Abs(delta.X) < Math.Abs(delta.Y))
					{
						delta.X = 0;
					}
					else
					{
						delta.Y = 0;
					}
				}

				// move the mesh back to the new position
				{
					var totalTransform = Matrix4X4.CreateTranslation(new Vector3(delta));

					selectedItem.Matrix *= totalTransform;

					CurrentSelectInfo.LastMoveDelta = delta;
				}

				Invalidate();
			}
		}

		Vector2 OffsetToMeshViewerWidget()
		{
			var parents = new List<GuiWidget>();
			GuiWidget parent = this.Object3DControlLayer.Parent;
			while (parent != this)
			{
				parents.Add(parent);
				parent = parent.Parent;
			}
			var offset = default(Vector2);
			for (int i = parents.Count - 1; i >= 0; i--)
			{
				offset += parents[i].OriginRelativeParent;
			}
			return offset;
		}

		Vector3 homeEyePosition = default(Vector3);
		Matrix4X4 homeRotation = default(Matrix4X4);
		public void ResetView()
		{
			var world = sceneContext.World;
			TrackballTumbleWidget.SetRotationCenter(new Vector3(sceneContext.BedCenter));

			void ResetHome()
			{
				world.Reset();
				world.Scale = .03;
				world.Translate(-new Vector3(sceneContext.BedCenter));
				world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, -MathHelper.Tau / 16)));
				world.Rotate(Quaternion.FromEulerAngles(new Vector3(MathHelper.Tau * .19, 0, 0)));

				homeEyePosition = world.EyePosition;
				homeRotation = world.RotationMatrix;
				Invalidate();
			}

			void Rotate()
			{
				TrackballTumbleWidget.AnimateRotation(homeRotation);//, ResetHome);

			}

			void Translate()
			{
				TrackballTumbleWidget.AnimateTranslation(world.EyePosition, homeEyePosition);

			}

			if (homeEyePosition.Z != 0)
			{
				// pan to the center
				//var screenCenter = new Vector2(world.Width / 2 - selectedObjectPanel.Width / 2, world.Height / 2);
				//var centerRay = world.GetRayForLocalBounds(screenCenter);
				//TrackballTumbleWidget.AnimateTranslation(new Vector3(sceneContext.BedCenter, 0), centerRay.origin + centerRay.directionNormal * 80);
				ResetHome();
			}
			else
			{
				ResetHome();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			var selectedItem = Scene.SelectedItem;
			if (this.DragOperationActive)
			{
				this.FinishDrop(mouseUpInBounds: true);
			}

			if (TrackballTumbleWidget.TransformState == TrackBallTransformType.None)
			{
				if (selectedItem != null
					&& CurrentSelectInfo.DownOnPart
					&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
				{
					this.Scene.AddTransformSnapshot(TransformOnMouseDown);
				}
				else if (DragSelectionInProgress)
				{
					DoRectangleSelection(null);
					DragSelectionInProgress = false;
				}
			}

			this.Object3DControlLayer.SuppressObject3DControls = false;

			CurrentSelectInfo.DownOnPart = false;

			if (activeButtonBeforeMouseOverride != null)
			{
				viewControls3D.ActiveButton = (ViewControls3DButtons)activeButtonBeforeMouseOverride;
				activeButtonBeforeMouseOverride = null;
			}

			// if we had a down and an up that did not move the view
			if (worldMatrixOnMouseDown == sceneContext.World.GetTransform4X4())
			{
				// and we are the first under mouse
				if (TrackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse)
				{
					// and the control key is pressed
					if (ModifierKeys == Keys.Control)
					{
						// find the think we clicked on
						var hitObject = FindHitObject3D(mouseEvent.Position, out IntersectInfo info);
						if (hitObject != null)
						{
							if (selectedItem == hitObject
								&& !(selectedItem is SelectionGroupObject3D))
							{
								Scene.SelectedItem = null;
							}
							else
							{
								IObject3D selectedHitItem = null;
								if (selectedItem != null)
								{
									foreach (Object3D object3D in selectedItem.Children)
									{
										if (object3D.GetBVHData().Contains(info.HitPosition))
										{
											CurrentSelectInfo.PlaneDownHitPos = info.HitPosition;
											CurrentSelectInfo.LastMoveDelta = default(Vector3);
											selectedHitItem = object3D;
											break;
										}
									}
								}

								if (selectedHitItem != null)
								{
									selectedItem.Children.Remove(selectedHitItem);
									if (selectedItem.Children.Count == 0)
									{
										Scene.SelectedItem = null;
									}

									Scene.Children.Add(selectedHitItem);
								}
								else
								{
									Scene.AddToSelection(hitObject);
								}

								// Selection may have changed, update local reference to current value
								selectedItem = Scene.SelectedItem;
							}
						}
					}
				}
			}

			if (mouseEvent.Button == MouseButtons.Right
				&& mouseDownPositon == mouseEvent.Position
				&& this.TrackballTumbleWidget.FirstWidgetUnderMouse)
			{
				if (FindHitObject3D(mouseEvent.Position, out _) is IObject3D hitObject
					&& (this.Printer == null // Allow Model -> Right Click in Part view
						|| this.Printer?.ViewState.ViewMode == PartViewMode.Model)) // Disallow Model -> Right Click in GCode views
				{
					// Object3D/hit item context menu
					if (hitObject != selectedItem)
					{
						Scene.SelectedItem = null;
						Scene.SelectedItem = hitObject;
						selectedItem = hitObject;
					}

					this.ShowPartContextMenu(mouseEvent, selectedItem);
				}
				else // Allow right click on bed in all modes
				{
					this.ShowBedContextMenu(mouseEvent.Position);
				}
			}

			base.OnMouseUp(mouseEvent);

			if (deferEditorTillMouseUp)
			{
				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);
			}
		}

		private void ShowPartContextMenu(MouseEventArgs mouseEvent, IObject3D selectedItem)
		{
			var popupMenu = ApplicationController.Instance.GetActionMenuForSceneItem(true, this);
			popupMenu.ShowMenu(this, mouseEvent);
		}

		public void ShowBedContextMenu(Vector2 position)
		{
			// Workspace/plate context menu
			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			var workspaceActions = ApplicationController.Instance.GetWorkspaceActions(this);

			var actions = new[]
			{
					new ActionSeparator(),
					new NamedAction()
					{
						Title = "Paste".Localize(),
						Action = () =>
						{
							sceneContext.Paste();
						},
						IsEnabled = () => Clipboard.Instance.ContainsImage || Clipboard.Instance.GetText() == "!--IObjectSelection--!"
					},
					workspaceActions["Save"],
					workspaceActions["SaveAs"],
					workspaceActions["Export"],
					new ActionSeparator(),
					workspaceActions["Print"],
					new ActionSeparator(),
					workspaceActions["ArrangeAll"],
					workspaceActions["ClearBed"],
				};

			theme.CreateMenuItems(popupMenu, actions);

			var popupBounds = new RectangleDouble(position.X + 1, position.Y + 1, position.X + 1, position.Y + 1);

			var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
			systemWindow.ShowPopup(
                theme,
				new MatePoint(this)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
				},
				new MatePoint(popupMenu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
				},
				altBounds: popupBounds);
		}

		// TODO: Consider if we should always allow DragDrop or if we should prevent during printer or other scenarios
		private bool AllowDragDrop() => true;

		private bool rebuildTreePending = false;

		private void Scene_Invalidated(object sender, InvalidateArgs e)
		{
			if (Scene.Descendants().Count() != lastSceneDescendantsCount
				&& !rebuildTreePending)
			{
				rebuildTreePending = true;
				UiThread.RunOnIdle(this.RebuildTree);
			}

			if (e.InvalidateType.HasFlag(InvalidateType.Children)
				&& !rebuildTreePending)
			{
				rebuildTreePending = true;
				UiThread.RunOnIdle(this.RebuildTree);
			}

			if (e.InvalidateType.HasFlag(InvalidateType.Name))
			{
				// clear and restore the selection so we have the name change
				using (new SelectionMaintainer(Scene))
				{
					if (!rebuildTreePending)
					{
						rebuildTreePending = true;
						UiThread.RunOnIdle(this.RebuildTree);
					}
				}
			}

			lastSceneDescendantsCount = Scene.Descendants().Count();

			// Invalidate widget on scene invalidate
			this.Invalidate();
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if (deferEditorTillMouseUp)
			{
				selectedObjectPanel.SetActiveItem(null);
			}
			else
			{
				var selectedItem = Scene.SelectedItem;

				// Change tree selection to current node
				if (selectedItem != null
					&& treeNodesByObject.TryGetValue(selectedItem, out TreeNode treeNode))
				{
					treeView.SelectedNode = treeNode;
				}
				else
				{
					// Clear the TreeView and release node references when no item is selected
					treeView.SelectedNode = null;
				}

				selectedObjectPanel.SetActiveItem(this.sceneContext);
			}
		}

		public void ClearPlate()
		{
			if (Printer != null)
			{
				selectedObjectPanel.SetActiveItem(null);
				sceneContext.ClearPlate();
				sceneContext.Scene.UndoBuffer.ClearHistory();
			}
			else
            {
				this.SelectAll();
				SceneActions.DeleteSelection(Scene);
			}

			this.Invalidate();
		}

		public static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)", RegexOptions.Compiled);

		private readonly SelectedObjectPanel selectedObjectPanel;

		internal VerticalResizeContainer modelViewSidePanel;

		public Vector2 DragSelectionStartPosition { get; private set; }

		public bool DragSelectionInProgress { get; private set; }

		public Vector2 DragSelectionEndPosition { get; private set; }

		internal GuiWidget ShowOverflowMenu(PopupMenu popupMenu)
		{
			this.ShowBedViewOptions(popupMenu);

			return popupMenu;
		}

		internal void ShowBedViewOptions(PopupMenu popupMenu)
		{
			// TODO: Extend popup menu if applicable
			// popupMenu.CreateHorizontalLine();
		}

		private readonly bool assigningTreeNode;
		private readonly FlowLayoutWidget treeNodeContainer;
		
		private readonly InlineStringEdit workspaceName;
		private int lastSceneDescendantsCount;
		private Vector2 beforeReubildScrollPosition;

		public InteractiveScene Scene => sceneContext.Scene;

		protected ViewToolBarControls viewControls3D { get; }

		public MeshSelectInfo CurrentSelectInfo { get; } = new MeshSelectInfo();


		private IObject3D FindHitObject3D(Vector2 screenPosition, out IntersectInfo intersectionInfo)
		{
			Vector2 meshViewerWidgetScreenPosition = this.Object3DControlLayer.TransformFromParentSpace(this, screenPosition);
			Ray ray = sceneContext.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			intersectionInfo = Scene.GetBVHData().GetClosestIntersection(ray);
			if (intersectionInfo != null)
			{
				foreach (var object3D in Scene.Children)
				{
					if (object3D.GetBVHData().Contains(intersectionInfo.HitPosition))
					{
						CurrentSelectInfo.PlaneDownHitPos = intersectionInfo.HitPosition;
						CurrentSelectInfo.LastMoveDelta = default(Vector3);
						return object3D;
					}
				}
			}

			return null;
		}

		public void Save()
		{
			ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer, sceneContext.SaveChanges);
		}

		void IDrawable.Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (CurrentSelectInfo.DownOnPart
				&& TrackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& Keyboard.IsKeyDown(Keys.ShiftKey))
			{
				// draw marks on the bed to show that the part is constrained to x and y
				AxisAlignedBoundingBox selectedBounds = Scene.SelectedItem.GetAxisAlignedBoundingBox();

				var drawCenter = CurrentSelectInfo.PlaneDownHitPos;
				var drawColor = Color.Red.WithAlpha(20);
				bool zBuffer = false;

				for (int i = 0; i < 2; i++)
				{
					sceneContext.World.Render3DLine(
						drawCenter - new Vector3(-50, 0, 0),
						drawCenter - new Vector3(50, 0, 0),
						drawColor,
						zBuffer,
						2);

					sceneContext.World.Render3DLine(
						drawCenter - new Vector3(0, -50, 0),
						drawCenter - new Vector3(0, 50, 0),
						drawColor,
						zBuffer,
						2);

					drawColor = Color.Black;
					drawCenter.Z = 0;
					zBuffer = true;
				}

				GL.Enable(EnableCap.Lighting);
			}

			TrackballTumbleWidget.OnDraw3D();

			// Render 3D GCode if applicable
			if (sceneContext.LoadedGCode != null
				&& sceneContext.GCodeRenderer != null
				&& printerTabPage?.Printer.ViewState.ViewMode == PartViewMode.Layers3D)
			{
				printerTabPage.Printer.Bed.RenderGCode3D(e);
			}
		}

		AxisAlignedBoundingBox IDrawable.GetWorldspaceAABB()
		{
			AxisAlignedBoundingBox box = AxisAlignedBoundingBox.Empty();

			if (CurrentSelectInfo.DownOnPart
				&& TrackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& Keyboard.IsKeyDown(Keys.ShiftKey))
			{
				var drawCenter = CurrentSelectInfo.PlaneDownHitPos;

				for (int i = 0; i < 2; i++)
				{
					box.ExpandToInclude(drawCenter - new Vector3(-50, 0, 0));
					box.ExpandToInclude(drawCenter - new Vector3(50, 0, 0));
					box.ExpandToInclude(drawCenter - new Vector3(0, -50, 0));
					box.ExpandToInclude(drawCenter - new Vector3(0, 50, 0));
					drawCenter.Z = 0;
				}
			}

			// Render 3D GCode if applicable
			if (sceneContext.LoadedGCode != null
				&& sceneContext.GCodeRenderer != null
				&& printerTabPage?.Printer.ViewState.ViewMode == PartViewMode.Layers3D)
			{
				box = AxisAlignedBoundingBox.Union(box, printerTabPage.Printer.Bed.GetAabbOfRenderGCode3D());
			}

			return box;
		}

		string IDrawable.Title { get; } = "View3DWidget Extensions";

		string IDrawable.Description { get; } = "Render axis indicators for shift drag and 3D GCode view";

		DrawStage IDrawable.DrawStage { get; } = DrawStage.OpaqueContent;

		private bool AggregateSelection(MouseEventArgs sourceEvent, GuiWidget clickedWidget)
		{
			if (!Keyboard.IsKeyDown(Keys.Control))
			{
				return false;
			}

			if (sourceEvent.Button != MouseButtons.Left)
			{
				return false;
			}

			if (Scene.SelectedItem == null)
			{
				return false;
			}

			var node = (TreeNode)clickedWidget.Parent;
			var object3D = (IObject3D)node.Tag;
			if (object3D == Scene.SelectedItem)
			{
				// Control-click on the existing selection doesn't change anything.
				return true;
			}

			if (object3D.Parent == Scene.SelectedItem && Scene.SelectedItem is SelectionGroupObject3D)
			{
				// Part is in a selection group so remove it.
				var remainingParts = Scene.SelectedItem.Children.Except(new [] {object3D}).ToList();
				Scene.ClearSelection();
				Scene.SetSelection(remainingParts);
				expandSelection = true;
				return true;
			}

			if (treeNodesByObject.TryGetValue(Scene.SelectedItem, out var priorSelectedNode))
			{
				if (priorSelectedNode.NodeParent != null)
				{
					// The existing selection isn't a top-level node so don't create a
					// new selection group. Restore visuals to prior selected node.
					treeView.SelectedNode = priorSelectedNode;
					return true;
				}
			}

			if (node.NodeParent != null)
			{
				// Only top-level nodes can be aggregated into a selection.
				treeView.SelectedNode = priorSelectedNode;
				return true;
			}

			Scene.AddToSelection(object3D);
			if (!treeNodesByObject.TryGetValue(Scene.SelectedItem, out _))
			{
				// A new selection group has been created and should be expanded by default.
				// However, the expansion has to be deferred until after tree gets rebuilt
				// so the new selection group object is included in the tree.
				expandSelection = true;
			}

			return true;
		}
	}

	public enum HitQuadrant
	{
		LB,
		LT,
		RB,
		RT
	}

	public class MeshSelectInfo
	{
		public HitQuadrant HitQuadrant { get; set; }

		public bool DownOnPart { get; set; }

		public PlaneShape HitPlane { get; set; }

		public Vector3 LastMoveDelta { get; set; }

		public Vector3 PlaneDownHitPos { get; set; }
	}
}
