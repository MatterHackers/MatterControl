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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MatterHackers.Agg;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DWidget : GuiWidget
	{
		private bool deferEditorTillMouseUp = false;

		public readonly int EditButtonHeight = 44;

		private bool hasDrawn = false;

		private Color[] SelectionColors = new Color[] { new Color(131, 4, 66), new Color(227, 31, 61), new Color(255, 148, 1), new Color(247, 224, 23), new Color(143, 212, 1) };
		private Stopwatch timeSinceLastSpin = new Stopwatch();
		private Stopwatch timeSinceReported = new Stopwatch();
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;

		private ThemeConfig theme;

		public Vector3 BedCenter
		{
			get
			{
				return new Vector3(sceneContext.BedCenter);
			}
		}

		private WorldView World => sceneContext.World;

		private TrackballTumbleWidget trackballTumbleWidget;

		public InteractionLayer InteractionLayer { get; }

		public BedConfig sceneContext;

		private PrinterConfig printer;

		private PrinterTabPage printerTabPage;

		public View3DWidget(PrinterConfig printer, BedConfig sceneContext, AutoRotate autoRotate, ViewControls3D viewControls3D, ThemeConfig theme, PartTabPage printerTabBase, MeshViewerWidget.EditorType editorType = MeshViewerWidget.EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.printerTabPage = printerTabBase as PrinterTabPage;
			this.printer = printer;

			this.InteractionLayer = new InteractionLayer(this.World, scene.UndoBuffer, scene)
			{
				Name = "InteractionLayer",
			};
			this.InteractionLayer.AnchorAll();

			this.viewControls3D = viewControls3D;
			this.theme = theme;
			this.Name = "View3DWidget";
			this.BackgroundColor = theme.ActiveTabColor;
			this.Border = new BorderDouble(top: 1);
			this.BorderColor = theme.MinimalShade;
			this.HAnchor = HAnchor.Stretch; //	HAnchor.MaxFitOrStretch,
			this.VAnchor = VAnchor.Stretch; //  VAnchor.MaxFitOrStretch

			autoRotating = allowAutoRotate;
			allowAutoRotate = (autoRotate == AutoRotate.Enabled);

			viewControls3D.TransformStateChanged += ViewControls3D_TransformStateChanged;

			// MeshViewer
			meshViewerWidget = new MeshViewerWidget(sceneContext, this.InteractionLayer, editorType: editorType);
			meshViewerWidget.AnchorAll();
			this.AddChild(meshViewerWidget);

			trackballTumbleWidget = new TrackballTumbleWidget(sceneContext.World, meshViewerWidget)
			{
				TransformState = TrackBallTransformType.Rotation
			};
			trackballTumbleWidget.AnchorAll();

			// TumbleWidget
			this.InteractionLayer.AddChild(trackballTumbleWidget);

			this.InteractionLayer.SetRenderTarget(this.meshViewerWidget);

			// Add splitter support with the InteractionLayer on the left and resize containers on the right
			var splitContainer = new FlowLayoutWidget()
			{
				Name = "SplitContainer",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			splitContainer.AddChild(this.InteractionLayer);
			this.AddChild(splitContainer);

			scene.SelectionChanged += Scene_SelectionChanged;

			// if the scene is invalidated invalidate the widget
			scene.Invalidated += (s, e) => Invalidate();

			this.AnchorAll();

			trackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;

			selectedObjectPanel = new SelectedObjectPanel(this, scene, theme, printer)
			{
				VAnchor = VAnchor.Stretch,
			};

			modelViewSidePanel = new ResizeContainer(selectedObjectPanel)
			{
				Width = printer?.ViewState.SelectedObjectPanelWidth ?? 200,
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Absolute,
				BackgroundColor = theme.InteractionLayerOverlayColor,
				SpliterBarColor = theme.SplitterBackground,
				SplitterWidth = theme.SplitterWidth,
			};

			modelViewSidePanel.AddChild(
				new SectionWidget(
					"Options".Localize(),
					new ModelOptionsPanel(sceneContext, meshViewerWidget, theme)
					{
						Padding = new BorderDouble(10, 10, 10, 0)
					},
					theme,
					ApplicationController.Instance.GetViewOptionButtons(sceneContext, printer, theme))
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					Margin = new BorderDouble(right: 5)
				});

			modelViewSidePanel.AddChild(selectedObjectPanel);
			splitContainer.AddChild(modelViewSidePanel);

			this.InteractionLayer.AddChild(new TumbleCubeControl(this.InteractionLayer)
			{
				Margin = new BorderDouble(50, 0, 0, 50),
				VAnchor = VAnchor.Top,
				HAnchor = HAnchor.Left,
			});

			UiThread.RunOnIdle(AutoSpin);

			var interactionVolumes = this.InteractionLayer.InteractionVolumes;
			interactionVolumes.Add(new MoveInZControl(this.InteractionLayer));
			interactionVolumes.Add(new SelectionShadow(this.InteractionLayer));
			interactionVolumes.Add(new SnappingIndicators(this.InteractionLayer, this.CurrentSelectInfo));

			var interactionVolumePlugins = PluginFinder.CreateInstancesOf<InteractionVolumePlugin>();
			foreach (InteractionVolumePlugin plugin in interactionVolumePlugins)
			{
				interactionVolumes.Add(plugin.CreateInteractionVolume(this.InteractionLayer));
			}

			meshViewerWidget.AfterDraw += AfterDraw3DContent;

			scene.SelectFirstChild();

			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

			// Make sure the render mode is set correctly
			string renderTypeString = UserSettings.Instance.get(UserSettingsKey.defaultRenderSetting);
			if (renderTypeString == null)
			{
				renderTypeString = (UserSettings.Instance.IsTouchScreen) ? "Shaded" : "Outlines";
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderTypeString);
			}

			RenderTypes renderType;
			bool canParse = Enum.TryParse(renderTypeString, out renderType);
			if (canParse)
			{
				meshViewerWidget.RenderType = renderType;
			}

			this.InteractionLayer.DrawGlOpaqueContent += Draw_GlOpaqueContent;

			this.sceneContext.SceneLoaded += SceneContext_SceneLoaded;
		}

		private void SceneContext_SceneLoaded(object sender, EventArgs e)
		{
			if (this.printerTabPage?.printerActionsBar?.sliceButton is GuiWidget sliceButton)
			{
				sliceButton.Enabled = sceneContext.EditableScene;
			}

			if (this.printerTabPage?.printerActionsBar?.modelViewButton is GuiWidget button)
			{
				button.Enabled = sceneContext.EditableScene;
			}

			this.Invalidate();
		}

		private void ViewControls3D_TransformStateChanged(object sender, TransformStateChangedEventArgs e)
		{
			switch (e.TransformMode)
			{
				case ViewControls3DButtons.Rotate:
					trackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;
					break;

				case ViewControls3DButtons.Translate:
					trackballTumbleWidget.TransformState = TrackBallTransformType.Translation;
					break;

				case ViewControls3DButtons.Scale:
					trackballTumbleWidget.TransformState = TrackBallTransformType.Scale;
					break;

				case ViewControls3DButtons.PartSelect:
					trackballTumbleWidget.TransformState = TrackBallTransformType.None;
					break;
			}
		}

		public void SelectAll()
		{
			scene.ClearSelection();
			foreach (var child in scene.Children.ToList())
			{
				scene.AddToSelection(child);
			}
		}

		private void Draw_GlOpaqueContent(object sender, DrawEventArgs e)
		{
			if (CurrentSelectInfo.DownOnPart
				&& trackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& Keyboard.IsKeyDown(Keys.ShiftKey))
			{
				// draw marks on the bed to show that the part is constrained to x and y
				AxisAlignedBoundingBox selectedBounds = scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				var drawCenter = CurrentSelectInfo.PlaneDownHitPos;
				var drawColor = new Color(Color.Red, 20);
				bool zBuffer = false;

				for (int i = 0; i < 2; i++)
				{
					GLHelper.Render3DLine(World,
						drawCenter - new Vector3(-50, 0, 0),
						drawCenter - new Vector3(50, 0, 0), drawColor, zBuffer, 2);

					GLHelper.Render3DLine(World,
						drawCenter - new Vector3(0, -50, 0),
						drawCenter - new Vector3(0, 50, 0), drawColor, zBuffer, 2);

					drawColor = Color.Black;
					drawCenter.Z = 0;
					zBuffer = true;
				}
			}

			// Render 3D GCode if applicable
			if (sceneContext.LoadedGCode != null
				&& sceneContext.GCodeRenderer != null
				&& printerTabPage?.printer.ViewState.ViewMode == PartViewMode.Layers3D)
			{
				sceneContext.RenderGCode3D(e);
			}

			// This shows the BVH as rects around the scene items
			//Scene?.TraceData().RenderBvhRecursive(0, 3);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.A:
						if (keyEvent.Control)
						{
							SelectAll();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.C:
						if (keyEvent.Control)
						{
							scene.Copy();

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.S:
						if (keyEvent.Control)
						{
							ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer.Bed.SaveChanges);

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.V:
						if (keyEvent.Control)
						{
							scene.Paste();

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.X:
						if (keyEvent.Control)
						{
							scene.Cut();

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.Y:
						if (keyEvent.Control)
						{
							scene.UndoBuffer.Redo();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.Z:
						if (keyEvent.Control)
						{
							scene.UndoBuffer.Undo();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.Delete:
					case Keys.Back:
						scene.DeleteSelection();
						break;

					case Keys.Escape:
						if (CurrentSelectInfo.DownOnPart)
						{
							CurrentSelectInfo.DownOnPart = false;

							scene.SelectedItem.Matrix = transformOnMouseDown;

							scene.Invalidate();
						}
						break;
					case Keys.Space:
						scene.ClearSelection();
						break;
				}
			}
		}

		public void AddUndoOperation(IUndoRedoCommand operation)
		{
			scene.UndoBuffer.Add(operation);
		}

		public enum AutoRotate { Enabled, Disabled };

		public bool DisplayAllValueData { get; set; }

		public override void OnClosed(ClosedEventArgs e)
		{
			if (printer != null)
			{
				printer.ViewState.SelectedObjectPanelWidth = selectedObjectPanel.Width;
				printer.ViewState.GCodePanelWidth = printerTabPage.gcodeContainer.Width;
			}

			viewControls3D.TransformStateChanged -= ViewControls3D_TransformStateChanged;
			scene.SelectionChanged -= Scene_SelectionChanged;
			this.InteractionLayer.DrawGlOpaqueContent -= Draw_GlOpaqueContent;
			this.sceneContext.SceneLoaded -= SceneContext_SceneLoaded;

			if (meshViewerWidget != null)
			{
				meshViewerWidget.AfterDraw -= AfterDraw3DContent;
			}

			base.OnClosed(e);
		}

		private GuiWidget topMostParent;

		private PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

		public bool DragOperationActive { get; private set; }

		public InsertionGroup DragDropObject { get; private set; }
		public ILibraryAssetStream SceneReplacement { get; private set; }

		/// <summary>
		/// Provides a View3DWidget specific drag implementation
		/// </summary>
		/// <param name="screenSpaceMousePosition">The screen space mouse position.</param>
		public void ExternalDragOver(Vector2 screenSpaceMousePosition)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			// If the mouse is within the MeshViewer process the Drag move
			var meshViewerPosition = this.meshViewerWidget.TransformToScreenSpace(meshViewerWidget.LocalBounds);
			if (meshViewerPosition.Contains(screenSpaceMousePosition))
			{
				// If already started, process drag move
				if (this.DragOperationActive)
				{
					this.DragOver(screenSpaceMousePosition);
				}
				else
				{
					// Otherwise begin an externally started DragDropOperation hard-coded to use LibraryView->SelectedItems

					this.StartDragDrop(
						// Project from ListViewItem to ILibraryItem
						ApplicationController.Instance.Library.ActiveViewWidget.SelectedItems.Select(l => l.Model),
						screenSpaceMousePosition);
				}
			}
		}

		private void DragOver(Vector2 screenSpaceMousePosition)
		{
			// Move the object being dragged
			if (this.DragOperationActive
				&& this.DragDropObject != null)
			{
				// Move the DropDropObject the target item
				DragSelectedObject(localMousePosition: this.TransformFromParentSpace(topMostParent, screenSpaceMousePosition));
			}
		}

		private void StartDragDrop(IEnumerable<ILibraryItem> items, Vector2 screenSpaceMousePosition, bool trackSourceFiles = false)
		{
			this.DragOperationActive = true;

			var firstItem = items.FirstOrDefault();

			if ((firstItem is ILibraryAssetStream contentStream
				&& contentStream.ContentType == "gcode")
				|| firstItem is SceneReplacementFileItem)
			{
				DragDropObject = null;
				this.SceneReplacement = firstItem as ILibraryAssetStream;

				// TODO: Figure out a mechanism to disable View3DWidget with dark overlay, displaying something like "Switch to xxx.gcode", make disappear on mouseLeaveBounds and dragfinish
				this.InteractionLayer.BackgroundColor = new Color(Color.Black, 200);

				return;
			}

			// Set the hitplane to the bed plane
			CurrentSelectInfo.HitPlane = bedPlane;

			var insertionGroup = new InsertionGroup(
				items,
				this,
				scene,
				sceneContext.BedCenter,
				() => this.DragOperationActive,
				trackSourceFiles);

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

			// Add item to scene and select it
			scene.Children.Modify(list =>
			{
				list.Add(insertionGroup);
			});
			scene.SelectedItem = insertionGroup;

			this.DragDropObject = insertionGroup;
		}

		private void CalculateDragStartPosition(IObject3D insertionGroup, IntersectInfo intersectInfo)
		{
			// Set the initial transform on the inject part to the current transform mouse position
			var sourceItemBounds = insertionGroup.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			var center = sourceItemBounds.Center;

			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(-center.X, -center.Y, -sourceItemBounds.minXYZ.Z);
			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(new Vector3(intersectInfo.HitPosition));

			CurrentSelectInfo.PlaneDownHitPos = intersectInfo.HitPosition;
			CurrentSelectInfo.LastMoveDelta = Vector3.Zero;
		}

		internal void FinishDrop(bool mouseUpInBounds)
		{
			if (this.DragOperationActive)
			{
				this.InteractionLayer.BackgroundColor = Color.Transparent;
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
								// No content store for GCode, otherwise PlatingHistory
								ContentStore = (this.SceneReplacement.ContentType == "gcode") ? null : ApplicationController.Instance.Library.PlatingHistory
							}).ConfigureAwait(false);

						this.SceneReplacement = null;
					}
					else if (this.DragDropObject.ContentAcquired)
					{
						// TODO: Unclear when this is needed and how it would be enabled if the content hadn't loaded by FinishDrop (i.e. how would long running InsertionGroup operations be doing the same thing?)
						//this.viewControls3D.modelViewButton.Enabled = true;

						// Drop handler for InsertionGroup - all normal content
						this.DragDropObject.Collapse();
					}
				}
				else
				{
					scene.Children.Modify(list => list.Remove(this.DragDropObject));
					scene.ClearSelection();
				}

				this.DragDropObject = null;

				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);

				scene.Invalidate();

				// Set focus to View3DWidget after drag-drop
				UiThread.RunOnIdle(this.Focus);

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
			var selectedItem = scene.SelectedItem;

			if (scene.HasSelection
				&& selectedItem != null)
			{

				foreach (InteractionVolume volume in this.InteractionLayer.InteractionVolumes)
				{
					volume.SetPosition(selectedItem);
				}
			}

			hasDrawn = true;

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

		bool foundTriangleInSelectionBounds;
		private void DoRectangleSelection(DrawEventArgs e)
		{
			var allResults = new List<BvhIterator>();

			var matchingSceneChildren = scene.Children.Where(item =>
			{
				foundTriangleInSelectionBounds = false;

				// Filter the IPrimitive trace data finding matches as defined in InSelectionBounds
				var filteredResults = item.TraceData().Filter(InSelectionBounds);

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
					scene.ClearSelection();

					foreach (var sceneItem in matchingSceneChildren.ToList())
					{
						scene.AddToSelection(sceneItem);
					}
				}
				else
				{
					InteractionLayer.RenderBounds(e, World, allResults);
				}
			}
		}

		private bool InSelectionBounds(BvhIterator x)
		{
			var selectionRectangle = new RectangleDouble(DragSelectionStartPosition, DragSelectionEndPosition);

			Vector2[] traceBottoms = new Vector2[4];
			Vector2[] traceTops = new Vector2[4];

			if (foundTriangleInSelectionBounds)
			{
				return false;
			}
			if (x.Bvh is TriangleShape tri)
			{
				// check if any vertex in screen rect
				// calculate all the top and bottom screen positions
				for (int i = 0; i < 3; i++)
				{
					Vector3 bottomStartPosition = Vector3.Transform(tri.GetVertex(i), x.TransformToWorld);
					traceBottoms[i] = this.World.GetScreenPosition(bottomStartPosition);
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
					Vector3 bottomStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), x.TransformToWorld);
					traceBottoms[i] = this.World.GetScreenPosition(bottomStartPosition);

					Vector3 topStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), x.TransformToWorld);
					traceTops[i] = this.World.GetScreenPosition(topStartPosition);
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

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
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
				meshViewerWidget.SuppressUiVolumes = true;
			}

			autoRotating = false;
			base.OnMouseDown(mouseEvent);

			if (trackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				if (mouseEvent.Button == MouseButtons.Left
					&& viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect
					&&
					(ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
					|| (
						trackballTumbleWidget.TransformState == TrackBallTransformType.None
						&& ModifierKeys != Keys.Control
						&& ModifierKeys != Keys.Alt))
				{
					if (!this.InteractionLayer.MouseDownOnInteractionVolume)
					{
						meshViewerWidget.SuppressUiVolumes = true;

						IntersectInfo info = new IntersectInfo();

						IObject3D hitObject = FindHitObject3D(mouseEvent.Position, ref info);
						if (hitObject == null)
						{
							if (scene.HasSelection)
							{
								scene.ClearSelection();
							}

							// start a selection rect
							DragSelectionStartPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
							DragSelectionEndPosition = DragSelectionStartPosition;
							DragSelectionInProgress = true;
						}
						else
						{
							CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.Z, null);

							if (hitObject != scene.SelectedItem)
							{
								if (scene.SelectedItem == null)
								{
									// No selection exists
									scene.SelectedItem = hitObject;
								}
								else if ((ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
									&& !scene.SelectedItem.Children.Contains(hitObject))
								{
									scene.AddToSelection(hitObject);
								}
								else if (scene.SelectedItem == hitObject || scene.SelectedItem.Children.Contains(hitObject))
								{
									// Selection should not be cleared and drag should occur
								}
								else if (ModifierKeys != Keys.Shift)
								{
									scene.SelectedItem = hitObject;
								}

								Invalidate();
							}

							transformOnMouseDown = scene.SelectedItem.Matrix;

							Invalidate();
							CurrentSelectInfo.DownOnPart = true;

							AxisAlignedBoundingBox selectedBounds = scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

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

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// File system Drop validation
			mouseEvent.AcceptDrop = this.AllowDragDrop()
					&& mouseEvent.DragFiles?.Count > 0
					&& mouseEvent.DragFiles.TrueForAll(filePath => ApplicationController.Instance.IsLoadableFile(filePath));

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
						mouseEvent.DragFiles.Select(path => new FileSystemFileItem(path)),
						screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position),
						trackSourceFiles: true);
				}
			}

			if (CurrentSelectInfo.DownOnPart && trackballTumbleWidget.TransformState == TrackBallTransformType.None)
			{
				DragSelectedObject(new Vector2(mouseEvent.X, mouseEvent.Y));
			}

			if (DragSelectionInProgress)
			{
				DragSelectionEndPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
				DragSelectionEndPosition = new Vector2(
					Math.Max(Math.Min((double)DragSelectionEndPosition.X, meshViewerWidget.LocalBounds.Right), meshViewerWidget.LocalBounds.Left),
					Math.Max(Math.Min((double)DragSelectionEndPosition.Y, meshViewerWidget.LocalBounds.Top), meshViewerWidget.LocalBounds.Bottom));
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public IntersectInfo GetIntersectPosition(Vector2 screenSpacePosition)
		{
			//Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));

			// Translate to local
			Vector2 localPosition = this.TransformFromScreenSpace(screenSpacePosition);

			Ray ray = this.World.GetRayForLocalBounds(localPosition);

			return CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
		}

		public void DragSelectedObject(Vector2 localMousePosition)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, localMousePosition);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
			if (info != null)
			{
				if (CurrentSelectInfo.LastMoveDelta == Vector3.PositiveInfinity)
				{
					CalculateDragStartPosition(scene.SelectedItem, info);
				}

				// move the mesh back to the start position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					scene.SelectedItem.Matrix *= totalTransform;

					// Invalidate the item to account for the position change
					scene.SelectedItem.Invalidate();
				}

				Vector3 delta = info.HitPosition - CurrentSelectInfo.PlaneDownHitPos;

				double snapGridDistance = this.InteractionLayer.SnapGridDistance;
				if (snapGridDistance > 0)
				{
					// snap this position to the grid
					AxisAlignedBoundingBox selectedBounds = scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					double xSnapOffset = selectedBounds.minXYZ.X;
					// snap the x position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.RB
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						xSnapOffset = selectedBounds.maxXYZ.X;
					}
					double xToSnap = xSnapOffset + delta.X;

					double snappedX = ((int)((xToSnap / snapGridDistance) + .5)) * snapGridDistance;
					delta.X = snappedX - xSnapOffset;

					double ySnapOffset = selectedBounds.minXYZ.Y;
					// snap the y position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.LT
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						ySnapOffset = selectedBounds.maxXYZ.Y;
					}
					double yToSnap = ySnapOffset + delta.Y;

					double snappedY = ((int)((yToSnap / snapGridDistance) + .5)) * snapGridDistance;
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
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(delta));

					scene.SelectedItem.Matrix *= totalTransform;

					CurrentSelectInfo.LastMoveDelta = delta;
				}

				Invalidate();
			}
		}

		Vector2 OffsetToMeshViewerWidget()
		{
			List<GuiWidget> parents = new List<GuiWidget>();
			GuiWidget parent = meshViewerWidget.Parent;
			while (parent != this)
			{
				parents.Add(parent);
				parent = parent.Parent;
			}
			Vector2 offset = new Vector2();
			for (int i = parents.Count - 1; i >= 0; i--)
			{
				offset += parents[i].OriginRelativeParent;
			}
			return offset;
		}

		public void ResetView()
		{
			trackballTumbleWidget.ZeroVelocity();

			var world = this.World;

			world.Reset();
			world.Scale = .03;
			world.Translate(-new Vector3(sceneContext.BedCenter));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, -MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(MathHelper.Tau * .19, 0, 0)));
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (this.DragOperationActive)
			{
				this.FinishDrop(mouseUpInBounds: true);
			}

			if (trackballTumbleWidget.TransformState == TrackBallTransformType.None)
			{
				if (scene.SelectedItem != null
					&& CurrentSelectInfo.DownOnPart
					&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
				{
					InteractionLayer.AddTransformSnapshot(transformOnMouseDown);
				}
				else if (DragSelectionInProgress)
				{
					DoRectangleSelection(null);
					DragSelectionInProgress = false;
				}
			}

			meshViewerWidget.SuppressUiVolumes = false;

			CurrentSelectInfo.DownOnPart = false;

			if (activeButtonBeforeMouseOverride != null)
			{
				viewControls3D.ActiveButton = (ViewControls3DButtons)activeButtonBeforeMouseOverride;
				activeButtonBeforeMouseOverride = null;
			}

			base.OnMouseUp(mouseEvent);

			if (deferEditorTillMouseUp)
			{
				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);
			}
		}

		// TODO: Consider if we should always allow DragDrop or if we should prevent during printer or other scenarios
		private bool AllowDragDrop() => true;

		private void AutoSpin()
		{
			if (!HasBeenClosed && autoRotating)
			{
				if ((!timeSinceLastSpin.IsRunning || timeSinceLastSpin.ElapsedMilliseconds > 50)
					&& hasDrawn)
				{
					hasDrawn = false;
					timeSinceLastSpin.Restart();

					Quaternion currentRotation = this.World.RotationMatrix.GetRotation();
					Quaternion invertedRotation = Quaternion.Invert(currentRotation);

					Quaternion rotateAboutZ = Quaternion.FromEulerAngles(new Vector3(0, 0, .01));
					rotateAboutZ = invertedRotation * rotateAboutZ * currentRotation;
					this.World.Rotate(rotateAboutZ);
					Invalidate();
				}
			}
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			selectedObjectPanel.Enabled = scene.HasSelection;

			if (!scene.HasSelection)
			{
				if (printer != null)
				{
					printer.ViewState.SelectedObjectPanelWidth = selectedObjectPanel.Width;
				}

				return;
			}

			if (deferEditorTillMouseUp)
			{
				return;
			}

			var selectedItem = scene.SelectedItem;

			selectedObjectPanel.SetActiveItem(selectedItem);
		}

		public static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)", RegexOptions.Compiled);

		private SelectedObjectPanel selectedObjectPanel;

		internal GuiWidget modelViewSidePanel;

		public Vector2 DragSelectionStartPosition { get; private set; }
		public bool DragSelectionInProgress { get; private set; }
		public Vector2 DragSelectionEndPosition { get; private set; }

		internal GuiWidget ShowOverflowMenu(PopupMenu popupMenu)
		{
			this.ShowBedViewOptions(popupMenu);

			popupMenu.AddChild(new GridOptionsPanel(this.InteractionLayer));

			return popupMenu;
		}

		internal void ShowBedViewOptions(PopupMenu popupMenu)
		{
			// TODO: Extend popup menu if applicable
			// popupMenu.CreateHorizontalLine();
		}

		protected bool autoRotating = false;
		protected bool allowAutoRotate = false;

		public MeshViewerWidget meshViewerWidget;

		private InteractiveScene scene => sceneContext.Scene;

		protected ViewControls3D viewControls3D { get; }

		public MeshSelectInfo CurrentSelectInfo { get; } = new MeshSelectInfo();

		protected IObject3D FindHitObject3D(Vector2 screenPosition, ref IntersectInfo intersectionInfo)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			intersectionInfo = scene.TraceData().GetClosestIntersection(ray);
			if (intersectionInfo != null)
			{
				foreach (Object3D object3D in scene.Children)
				{
					if (object3D.TraceData().Contains(intersectionInfo.HitPosition))
					{
						CurrentSelectInfo.PlaneDownHitPos = intersectionInfo.HitPosition;
						CurrentSelectInfo.LastMoveDelta = new Vector3();
						return object3D;
					}
				}
			}

			return null;
		}

	}

	public enum HitQuadrant { LB, LT, RB, RT }

	public class MeshSelectInfo
	{
		public HitQuadrant HitQuadrant;
		public bool DownOnPart;
		public PlaneShape HitPlane;
		public Vector3 LastMoveDelta;
		public Vector3 PlaneDownHitPos;
	}
}
