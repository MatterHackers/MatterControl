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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DWidget : GuiWidget
	{
		private bool DoBooleanTest = false;
		private bool deferEditorTillMouseUp = false;

		public FlowLayoutWidget selectionActionBar;
		public UndoBuffer UndoBuffer { get; } = new UndoBuffer();
		public readonly int EditButtonHeight = 44;

		private ObservableCollection<GuiWidget> extruderButtons = new ObservableCollection<GuiWidget>();
		private bool hasDrawn = false;

		private OpenMode openMode;
		internal bool partHasBeenEdited = false;
		private PrintItemWrapper printItemWrapper { get; set; }
		internal ProgressControl processingProgressControl;
		private SaveAsWindow saveAsWindow = null;
		private SplitButton saveButtons;
		private RGBA_Bytes[] SelectionColors = new RGBA_Bytes[] { new RGBA_Bytes(131, 4, 66), new RGBA_Bytes(227, 31, 61), new RGBA_Bytes(255, 148, 1), new RGBA_Bytes(247, 224, 23), new RGBA_Bytes(143, 212, 1) };
		private Stopwatch timeSinceLastSpin = new Stopwatch();
		private Stopwatch timeSinceReported = new Stopwatch();
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private EventHandler unregisterEvents;

		internal bool viewIsInEditModePreLock = false;

		private bool wasInSelectMode = false;

		public event EventHandler SelectedTransformChanged;

		public static ImageBuffer ArrowRight
		{
			get
			{
				if (ActiveTheme.Instance.IsDarkTheme)
				{
					return StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32, 32).InvertLightness();
				}
				else
				{
					return StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32, 32);
				}
			}
		}

		public static ImageBuffer ArrowDown
		{
			get
			{
				if (ActiveTheme.Instance.IsDarkTheme)
				{
					return StaticData.Instance.LoadIcon("icon_arrow_down_no_border_32x32.png", 32, 32).InvertLightness();
				}
				else
				{
					return StaticData.Instance.LoadIcon("icon_arrow_down_no_border_32x32.png", 32, 32);
				}
			}
		}

		private ThemeConfig theme;

		private PrinterConfig printer;

		// TODO: Make dynamic
		public WorldView World { get; } = ApplicationController.Instance.Printer.BedPlate.World;

		public TrackballTumbleWidget TrackballTumbleWidget { get; }

		private Vector2 bedCenter;

		internal ViewGcodeBasic gcodeViewer;

		public InteractionLayer InteractionLayer { get; }

		public View3DWidget(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowType, AutoRotate autoRotate, ViewControls3D viewControls3D, ThemeConfig theme, OpenMode openMode = OpenMode.Viewing)
		{
			this.printer = ApplicationController.Instance.Printer;
			this.bedCenter = bedCenter;

			this.TrackballTumbleWidget = new TrackballTumbleWidget(ApplicationController.Instance.Printer.BedPlate.World)
			{
				TransformState = TrackBallController.MouseDownType.Rotation
			};
			this.TrackballTumbleWidget.AnchorAll();

			this.InteractionLayer = new InteractionLayer(this.World, this.UndoBuffer, this.PartHasBeenChanged)
			{
				Name = "InteractionLayer",
			};
			this.InteractionLayer.AnchorAll();

			this.viewControls3D = viewControls3D;
			this.theme = theme;
			this.openMode = openMode;
			allowAutoRotate = (autoRotate == AutoRotate.Enabled);
			meshViewerWidget = new MeshViewerWidget(viewerVolume, bedCenter, bedShape, this.TrackballTumbleWidget, this.InteractionLayer);
			this.printItemWrapper = printItemWrapper;

			ActiveSliceSettings.SettingChanged.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent(CheckSettingChanged, ref unregisterEvents);

			this.windowType = windowType;
			autoRotating = allowAutoRotate;

			this.Name = "View3DWidget";

			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;

			viewControls3D.TransformStateChanged += ViewControls3D_TransformStateChanged;

			var mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				VAnchor = VAnchor.MaxFitOrStretch
			};

			var smallMarginButtonFactory = ApplicationController.Instance.Theme.SmallMarginButtonFactory;

			PutOemImageOnBed();

			meshViewerWidget.AnchorAll();
			this.InteractionLayer.AddChild(meshViewerWidget);

			// The slice layers view
			gcodeViewer = new ViewGcodeBasic(
				viewerVolume,
				bedCenter,
				bedShape,
				viewControls3D);
			gcodeViewer.AnchorAll();
			this.gcodeViewer.Visible = false;

			this.InteractionLayer.AddChild(gcodeViewer);
			this.InteractionLayer.AddChild(this.TrackballTumbleWidget);

			mainContainerTopToBottom.AddChild(this.InteractionLayer);

			var buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = 3,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
			};

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// TODO: Consider only loading once into a static
			var objectEditors = PluginFinder.CreateInstancesOf<IObject3DEditor>();
			foreach (IObject3DEditor editor in objectEditors)
			{
				foreach (Type type in editor.SupportedTypes())
				{
					if (!objectEditorsByType.TryGetValue(type, out mappedEditors))
					{
						mappedEditors = new HashSet<IObject3DEditor>();
						objectEditorsByType.Add(type, mappedEditors);
					}

					mappedEditors.Add(editor);
				}
			}

			Scene.SelectionChanged += Scene_SelectionChanged;

			// add in the plater tools
			{
				selectionActionBar = new FlowLayoutWidget();
				selectionActionBar.VAnchor |= VAnchor.Center;

				processingProgressControl = new ProgressControl("", ActiveTheme.Instance.PrimaryTextColor, ActiveTheme.Instance.PrimaryAccentColor)
				{
					VAnchor = VAnchor.Center,
					Visible = false
				};

				selectionActionBar = new FlowLayoutWidget();
				selectionActionBar.Visible = false;

				var buttonSpacing = ApplicationController.Instance.Theme.ButtonSpacing;

				Button addButton = smallMarginButtonFactory.Generate("Insert".Localize(), StaticData.Instance.LoadIcon("AddAzureResource_16x.png", 16, 16));
				addButton.Margin = buttonSpacing;
				addButton.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						FileDialog.OpenFileDialog(
							new OpenFileDialogParams(ApplicationSettings.OpenDesignFileParams, multiSelect: true),
							(openParams) =>
							{
								LoadAndAddPartsToPlate(openParams.FileNames);
							});
					});
				};
				selectionActionBar.AddChild(addButton);

				CreateActionSeparator(selectionActionBar);

				Button ungroupButton = smallMarginButtonFactory.Generate("Ungroup".Localize());
				ungroupButton.Name = "3D View Ungroup";
				ungroupButton.Margin = buttonSpacing;
				ungroupButton.Click += (sender, e) =>
				{
					this.Scene.UngroupSelection(this);
				};
				selectionActionBar.AddChild(ungroupButton);

				Button groupButton = smallMarginButtonFactory.Generate("Group".Localize());
				groupButton.Name = "3D View Group";
				groupButton.Margin = buttonSpacing;
				groupButton.Click += (sender, e) =>
				{
					this.Scene.GroupSelection(this);
				};
				selectionActionBar.AddChild(groupButton);

				Button alignButton = smallMarginButtonFactory.Generate("Align".Localize());
				alignButton.Margin = buttonSpacing;
				alignButton.Click += (sender, e) =>
				{
					this.Scene.AlignToSelection(this);
				};
				selectionActionBar.AddChild(alignButton);

				CreateActionSeparator(selectionActionBar);

				Button copyButton = smallMarginButtonFactory.Generate("Copy".Localize());
				copyButton.Name = "3D View Copy";
				copyButton.Margin = buttonSpacing;
				copyButton.Click += (sender, e) =>
				{
					this.Scene.DuplicateSelection(this);
				};
				selectionActionBar.AddChild(copyButton);

				Button deleteButton = smallMarginButtonFactory.Generate("Remove".Localize());
				deleteButton.Name = "3D View Remove";
				deleteButton.Margin = buttonSpacing;
				deleteButton.Click += (sender, e) =>
				{
					this.Scene.DeleteSelection(this);
				};
				selectionActionBar.AddChild(deleteButton);

				CreateActionSeparator(selectionActionBar);

				// put in the save button
				AddSaveAndSaveAs(selectionActionBar, buttonSpacing);

				var mirrorView = smallMarginButtonFactory.Generate("Mirror".Localize());
				mirrorView.Margin = buttonSpacing;

				var mirrorButton = new PopupButton(mirrorView)
				{
					PopDirection = Direction.Up,
					PopupContent = new MirrorControls(this, smallMarginButtonFactory),
					//Margin = buttonSpacing,
				};
				selectionActionBar.AddChild(mirrorButton);

				var menuActions = new[]
				{
					new NamedAction()
					{
						Title = "Export".Localize() + "...",
						Action = () =>
						{
							UiThread.RunOnIdle(OpenExportWindow);
						}
					},
					new NamedAction()
					{
						Title = "Arrange All Parts".Localize(),
						Action = () =>
						{
							this.Scene.AutoArrangeChildren(this);
						}
					},
					new NamedAction() { Title = "----" },
					new NamedAction()
					{
						Title = "Clear Bed".Localize(),
						Action = () =>
						{
							UiThread.RunOnIdle(ApplicationController.Instance.ClearPlate);
						}
					}
				};

				// Bed menu
				selectionActionBar.AddChild(new PopupButton(smallMarginButtonFactory.Generate("Bed".Localize()))
				{
					PopDirection = Direction.Up,
					PopupContent = ApplicationController.Instance.Theme.CreatePopupMenu(menuActions),
					AlignToRightEdge = true,
					Margin = buttonSpacing
				});

				// put in the material options
				var materialsButton = new PopupButton(smallMarginButtonFactory.Generate("Materials".Localize()))
				{
					PopDirection = Direction.Up,
					PopupContent = this.AddMaterialControls(),
					AlignToRightEdge = true,
					Margin = buttonSpacing
				};
				selectionActionBar.AddChild(materialsButton);
			}

			selectionActionBar.AddChild(processingProgressControl);
			buttonBottomPanel.AddChild(selectionActionBar);

			LockEditControls();

			mainContainerTopToBottom.AddChild(buttonBottomPanel);

			this.AddChild(mainContainerTopToBottom);

			this.AnchorAll();

			this.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;

			selectedObjectPanel = new SelectedObjectPanel()
			{
				Margin = 5,
				BackgroundColor = new RGBA_Bytes(0, 0, 0, ViewControlsBase.overlayAlpha)
			};
			AddChild(selectedObjectPanel);

			UiThread.RunOnIdle(AutoSpin);

			if (windowType == WindowMode.Embeded)
			{
				PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(SetEditControlsBasedOnPrinterState, ref unregisterEvents);
				if (windowType == WindowMode.Embeded)
				{
					// make sure we lock the controls if we are printing or paused
					switch (PrinterConnection.Instance.CommunicationState)
					{
						case CommunicationStates.Printing:
						case CommunicationStates.Paused:
							LockEditControls();
							break;
					}
				}
			}

			ActiveTheme.ThemeChanged.RegisterEvent((s, e) =>
			{
				processingProgressControl.FillColor = ActiveTheme.Instance.PrimaryAccentColor;
			}, ref unregisterEvents);

			var interactionVolumes = this.InteractionLayer.InteractionVolumes;
			interactionVolumes.Add(new MoveInZControl(this.InteractionLayer));
			interactionVolumes.Add(new SelectionShadow(this.InteractionLayer));
			interactionVolumes.Add(new SnappingIndicators(this.InteractionLayer, this.CurrentSelectInfo));

			var interactionVolumePlugins = PluginFinder.CreateInstancesOf<InteractionVolumePlugin>();
			foreach (InteractionVolumePlugin plugin in interactionVolumePlugins)
			{
				interactionVolumes.Add(plugin.CreateInteractionVolume(this.InteractionLayer));
			}

			if (DoBooleanTest)
			{
				BeforeDraw += CreateBooleanTestGeometry;
				AfterDraw += RemoveBooleanTestGeometry;
			}

			meshViewerWidget.AfterDraw += AfterDraw3DContent;

			this.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;
		}

		private void CreateActionSeparator(GuiWidget container)
		{
			container.AddChild(new VerticalLine(20)
			{
				Margin = new BorderDouble(3, 4, 0, 4),
			});
		}

		private void ViewControls3D_TransformStateChanged(object sender, TransformStateChangedEventArgs e)
		{
			switch (e.TransformMode)
			{
				case ViewControls3DButtons.Rotate:
					this.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
					break;

				case ViewControls3DButtons.Translate:
					this.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
					break;

				case ViewControls3DButtons.Scale:
					this.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
					break;

				case ViewControls3DButtons.PartSelect:
					this.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
					break;
			}
		}

		public void SelectAll()
		{
			Scene.ClearSelection();
			foreach (var child in Scene.Children)
			{
				Scene.AddToSelection(child);
			}
		}

		public ILibraryContentStream DragSourceModel { get; set; }

		// TODO: Rename to DragDropItem

		private IObject3D dragDropSource;
		public IObject3D DragDropSource
		{
			get
			{
				return dragDropSource;
			}

			set
			{
				// <IObject3D>
				dragDropSource = value;

				// Clear the DragSourceModel - <ILibraryItem>
				DragSourceModel = null;

				// Suppress ui volumes when dragDropSource is not null
				meshViewerWidget.SuppressUiVolumes = (dragDropSource != null);
			}
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			// This shows the BVH as rects around the scene items
			//Scene?.TraceData().RenderBvhRecursive(0, 3);

			if (gcodeViewer?.loadedGCode == null || printer.BedPlate.GCodeRenderer == null || !gcodeViewer.Visible)
			{
				return;
			}

			printer.BedPlate.Render3DLayerFeatures();
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

					case Keys.Z:
						if (keyEvent.Control)
						{
							UndoBuffer.Undo();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.Y:
						if (keyEvent.Control)
						{
							UndoBuffer.Redo();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}
						break;

					case Keys.Delete:
					case Keys.Back:
						this.Scene.DeleteSelection(this);
						break;

					case Keys.Escape:
						if (CurrentSelectInfo.DownOnPart)
						{
							CurrentSelectInfo.DownOnPart = false;

							Scene.SelectedItem.Matrix = transformOnMouseDown;

							Invalidate();
						}
						break;
					case Keys.Space:
						this.Scene.ClearSelection();
						break;
				}
			}
		}

		public bool DragingPart
		{
			get { return CurrentSelectInfo.DownOnPart; }
		}

		public void AddUndoOperation(IUndoRedoCommand operation)
		{
			UndoBuffer.Add(operation);
		}

		#region DoBooleanTest
		Object3D booleanGroup;
		Vector3 offset = new Vector3();
		Vector3 direction = new Vector3(.11, .12, .13);
		Vector3 rotCurrent = new Vector3();
		Vector3 rotChange = new Vector3(.011, .012, .013);
		Vector3 scaleChange = new Vector3(.0011, .0012, .0013);
		Vector3 scaleCurrent = new Vector3(1, 1, 1);

		private void CreateBooleanTestGeometry(object sender, DrawEventArgs e)
		{
			try
			{
				booleanGroup = new Object3D { ItemType = Object3DTypes.Group };

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(PolygonMesh.Csg.CsgOperations.Union, AxisAlignedBoundingBox.Union, new Vector3(100, 0, 20), "U")
				});

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(PolygonMesh.Csg.CsgOperations.Subtract, null, new Vector3(100, 100, 20), "S")
				});

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(PolygonMesh.Csg.CsgOperations.Intersect, AxisAlignedBoundingBox.Intersection, new Vector3(100, 200, 20), "I")
				});

				offset += direction;
				rotCurrent += rotChange;
				scaleCurrent += scaleChange;

				Scene.ModifyChildren(children =>
				{
					children.Add(booleanGroup);
				});
			}
			catch (Exception e2)
			{
				string text = e2.Message;
				int a = 0;
			}
		}

		private Mesh ApplyBoolean(Func<Mesh, Mesh, Mesh> meshOpperation, Func<AxisAlignedBoundingBox, AxisAlignedBoundingBox, AxisAlignedBoundingBox> aabbOpperation, Vector3 centering, string opp)
		{
			Mesh boxA = PlatonicSolids.CreateCube(40, 40, 40);
			//boxA = PlatonicSolids.CreateIcosahedron(35);
			boxA.Translate(centering);
			Mesh boxB = PlatonicSolids.CreateCube(40, 40, 40);
			//boxB = PlatonicSolids.CreateIcosahedron(35);

			for (int i = 0; i < 3; i++)
			{
				if (Math.Abs(direction[i] + offset[i]) > 10)
				{
					direction[i] = direction[i] * -1.00073112;
				}
			}

			for (int i = 0; i < 3; i++)
			{
				if (Math.Abs(rotChange[i] + rotCurrent[i]) > 6)
				{
					rotChange[i] = rotChange[i] * -1.000073112;
				}
			}

			for (int i = 0; i < 3; i++)
			{
				if (scaleChange[i] + scaleCurrent[i] > 1.1 || scaleChange[i] + scaleCurrent[i] < .9)
				{
					scaleChange[i] = scaleChange[i] * -1.000073112;
				}
			}

			Vector3 offsetB = offset + centering;
			// switch to the failing offset
			//offsetB = new Vector3(105.240172225344, 92.9716306394062, 18.4619570261172);
			//rotCurrent = new Vector3(4.56890223673623, -2.67874102322035, 1.02768848238523);
			//scaleCurrent = new Vector3(1.07853517569753, 0.964980885267323, 1.09290934544604);
			Debug.WriteLine("t" + offsetB.ToString() + " r" + rotCurrent.ToString() + " s" + scaleCurrent.ToString() + " " + opp);
			Matrix4X4 transformB = Matrix4X4.CreateScale(scaleCurrent) * Matrix4X4.CreateRotation(rotCurrent) * Matrix4X4.CreateTranslation(offsetB);
			boxB.Transform(transformB);

			Mesh meshToAdd = meshOpperation(boxA, boxB);
			meshToAdd.CleanAndMergMesh(CancellationToken.None);

			if (aabbOpperation != null)
			{
				AxisAlignedBoundingBox boundsA = boxA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsB = boxB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsAdd = meshToAdd.GetAxisAlignedBoundingBox();

				AxisAlignedBoundingBox boundsResult = aabbOpperation(boundsA, boundsB);
			}

			return meshToAdd;
		}

		private void RemoveBooleanTestGeometry(object sender, DrawEventArgs e)
		{
			if (meshViewerWidget.Scene.Children.Contains(booleanGroup))
			{
				meshViewerWidget.Scene.Children.Remove(booleanGroup);
				UiThread.RunOnIdle(() => Invalidate(), 1.0 / 30.0);
			}
		}
		#endregion DoBooleanTest

		public enum AutoRotate { Enabled, Disabled };

		public enum OpenMode { Viewing, Editing }

		public enum WindowMode { Embeded, StandAlone };

		public bool DisplayAllValueData { get; set; }

		public WindowMode windowType { get; set; }

		public override void OnClosed(ClosedEventArgs e)
		{
			// Not needed but safer than without
			viewControls3D.TransformStateChanged -= ViewControls3D_TransformStateChanged;

			if (meshViewerWidget != null)
			{
				meshViewerWidget.AfterDraw -= AfterDraw3DContent;
			}

			this.TrackballTumbleWidget.DrawGlContent -= TrackballTumbleWidget_DrawGlContent;
			
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				if (AllowDragDrop())
				{
					mouseEvent.AcceptDrop = mouseEvent.DragFiles.TrueForAll(filePath => ApplicationController.Instance.IsLoadableFile(filePath));

					if (mouseEvent.AcceptDrop)
					{
						string filePath = mouseEvent.DragFiles.FirstOrDefault();
						string extensionWithoutPeriod = Path.GetExtension(filePath).Trim('.');

						IContentProvider contentProvider;
						if (!string.IsNullOrEmpty(filePath)
							&& ApplicationController.Instance.Library.ContentProviders.TryGetValue(extensionWithoutPeriod, out contentProvider)
							&& contentProvider is ISceneContentProvider)
						{
							var sceneProvider = contentProvider as ISceneContentProvider;
							this.DragDropSource = sceneProvider.CreateItem(new FileSystemFileItem(filePath), null).Object3D;
						}
						else
						{
							this.DragDropSource = new Object3D
							{
								ItemType = Object3DTypes.Model,
								Mesh = PlatonicSolids.CreateCube(10, 10, 10)
							};
						}
					}
				}
			}

			base.OnMouseEnterBounds(mouseEvent);
		}

		private GuiWidget topMostParent;

		private PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

		/// <summary>
		/// Provides a View3DWidget specific drag implementation
		/// </summary>
		/// <param name="screenSpaceMousePosition">The screen space mouse position.</param>
		/// <returns>A value indicating if a new item was generated for the DragDropSource and added to the scene</returns>
		public bool AltDragOver(Vector2 screenSpaceMousePosition)
		{
			if (this.HasBeenClosed || this.DragDropSource == null)
			{
				return false;
			}

			bool itemAddedToScene = false;

			var meshViewerPosition = this.meshViewerWidget.TransformToScreenSpace(meshViewerWidget.LocalBounds);

			// If the mouse is within this control
			if (meshViewerPosition.Contains(screenSpaceMousePosition)
				&& this.DragDropSource != null)
			{
				var localPosition = this.TransformFromParentSpace(topMostParent, screenSpaceMousePosition);

				// Inject the DragDropSource if it's missing from the scene, using the default "loading" mesh
				if (!Scene.Children.Contains(DragDropSource))
				{
					// Set the hitplane to the bed plane
					CurrentSelectInfo.HitPlane = bedPlane;

					// Find intersection position of the mouse with the bed plane
					var intersectInfo = GetIntersectPosition(screenSpaceMousePosition);
					if (intersectInfo == null)
					{
						return false;
					}

					// Set the initial transform on the inject part to the current transform mouse position
					var sourceItemBounds = DragDropSource.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
					var center = sourceItemBounds.Center;

					DragDropSource.Matrix *= Matrix4X4.CreateTranslation(-center.x, -center.y, -sourceItemBounds.minXYZ.z);
					DragDropSource.Matrix *= Matrix4X4.CreateTranslation(new Vector3(intersectInfo.HitPosition));

					CurrentSelectInfo.PlaneDownHitPos = intersectInfo.HitPosition;
					CurrentSelectInfo.LastMoveDelta = Vector3.Zero;

					this.deferEditorTillMouseUp = true;

					// Add item to scene and select it
					Scene.ModifyChildren(children =>
					{
						children.Add(DragDropSource);
					});
					Scene.Select(DragDropSource);

					itemAddedToScene = true;
				}

				// Move the object being dragged
				if (Scene.HasSelection)
				{
					// Pass the mouse position, transformed to local cords, through to the view3D widget to move the target item
					localPosition = meshViewerWidget.TransformFromScreenSpace(screenSpaceMousePosition);
					DragSelectedObject(localPosition);
				}

				return itemAddedToScene;
			}

			return false;
		}

		internal void FinishDrop()
		{
			this.DragDropSource = null;
			this.DragSourceModel = null;

			this.deferEditorTillMouseUp = false;
			Scene_SelectionChanged(null, null);

			this.PartHasBeenChanged();

			// Set focus to View3DWidget after drag-drop
			UiThread.RunOnIdle(this.Focus);
		}

		public override void OnLoad(EventArgs args)
		{
			topMostParent = this.TopmostParent();
			base.OnLoad(args);
		}

		/// <summary>
		/// Loads the referenced DragDropSource object.
		/// </summary>
		/// <param name="dragSource">The drag source at the original time of invocation.</param>
		/// <returns></returns>
		public async Task LoadDragSource(ListViewItem sourceListItem)
		{
			// Hold initial reference
			IObject3D dragDropItem = DragDropSource;
			if (dragDropItem == null)
			{
				return;
			}

			this.DragSourceModel = sourceListItem?.Model as ILibraryContentStream;

			IObject3D loadedItem = await Task.Run(async () =>
			{
				if (File.Exists(dragDropItem.MeshPath))
				{
					string extensionWithoutPeriod = Path.GetExtension(dragDropItem.MeshPath).Trim('.');

					if (ApplicationController.Instance.Library.ContentProviders.ContainsKey(extensionWithoutPeriod))
					{
						return null;
					}
					else
					{
						return Object3D.Load(dragDropItem.MeshPath, CancellationToken.None, progress: new DragDropLoadProgress(this, dragDropItem).ProgressReporter);
					}
				}
				else if (DragSourceModel != null)
				{
					var loadProgress = new DragDropLoadProgress(this, dragDropItem);

					ContentResult contentResult;

					if (sourceListItem == null)
					{
						contentResult = DragSourceModel.CreateContent(loadProgress.ProgressReporter);
						await contentResult.MeshLoaded;
					}
					else
					{
						sourceListItem.StartProgress();

						contentResult = DragSourceModel.CreateContent((double ratio, string state) =>
						{
							sourceListItem.ProgressReporter(ratio, state);
							loadProgress.ProgressReporter(ratio, state);
						});

						await contentResult.MeshLoaded;

						sourceListItem.EndProgress();

						loadProgress.ProgressReporter(1, "");
					}

					return contentResult?.Object3D;
				}

				return null;
			});

			if (loadedItem != null)
			{
				Vector3 meshGroupCenter = loadedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;

				dragDropItem.Mesh = loadedItem.Mesh;
				dragDropItem.Children = loadedItem.Children;

				// TODO: jlewin - also need to apply the translation to the scale/rotation from the source (loadedItem.Matrix)
				dragDropItem.Matrix = loadedItem.Matrix * dragDropItem.Matrix;
				dragDropItem.Matrix *= Matrix4X4.CreateTranslation(-meshGroupCenter.x, -meshGroupCenter.y, -dragDropItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity).minXYZ.z);
			}

			this.PartHasBeenChanged();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (needToRecreateBed)
			{
				needToRecreateBed = false;
				RecreateBed();
			}

			if (Scene.HasSelection)
			{
				var selectedItem = Scene.SelectedItem;

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
				e.graphics2D.Rectangle(selectionRectangle, RGBA_Bytes.Red);
			}
		}

		bool foundTriangleInSelectionBounds;
		private void DoRectangleSelection(DrawEventArgs e)
		{
			var allResults = new List<BvhIterator>();

			var matchingSceneChildren = Scene.Children.Where(item =>
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
					Scene.ClearSelection();

					foreach (var sceneItem in matchingSceneChildren)
					{
						Scene.AddToSelection(sceneItem);
					}
				}
				else
				{
					RenderBounds(e, allResults);
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

		private void RenderBounds(DrawEventArgs e, IEnumerable<BvhIterator> allResults)
		{
			foreach (var x in allResults)
			{
				for (int i = 0; i < 4; i++)
				{
					Vector3 bottomStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), x.TransformToWorld);
					var bottomStartScreenPos = this.World.GetScreenPosition(bottomStartPosition);

					Vector3 bottomEndPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner((i + 1) % 4), x.TransformToWorld);
					var bottomEndScreenPos = this.World.GetScreenPosition(bottomEndPosition);

					Vector3 topStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), x.TransformToWorld);
					var topStartScreenPos = this.World.GetScreenPosition(topStartPosition);

					Vector3 topEndPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner((i + 1) % 4), x.TransformToWorld);
					var topEndScreenPos = this.World.GetScreenPosition(topEndPosition);

					e.graphics2D.Line(bottomStartScreenPos, bottomEndScreenPos, RGBA_Bytes.Black);
					e.graphics2D.Line(topStartScreenPos, topEndScreenPos, RGBA_Bytes.Black);
					e.graphics2D.Line(topStartScreenPos, bottomStartScreenPos, RGBA_Bytes.Black);
				}

				TriangleShape tri = x.Bvh as TriangleShape;
				if (tri != null)
				{
					for (int i = 0; i < 3; i++)
					{
						var vertexPos = tri.GetVertex(i);
						var screenCenter = Vector3.Transform(vertexPos, x.TransformToWorld);
						var screenPos = this.World.GetScreenPosition(screenCenter);

						e.graphics2D.Circle(screenPos, 3, RGBA_Bytes.Red);
					}
				}
				else
				{
					var center = x.Bvh.GetCenter();
					var worldCenter = Vector3.Transform(center, x.TransformToWorld);
					var screenPos2 = this.World.GetScreenPosition(worldCenter);
					e.graphics2D.Circle(screenPos2, 3, RGBA_Bytes.Yellow);
					e.graphics2D.DrawString($"{x.Depth},", screenPos2.x + 12 * x.Depth, screenPos2.y);
				}
			}
		}

		private void RendereSceneTraceData(DrawEventArgs e)
		{
			var bvhIterator = new BvhIterator(Scene?.TraceData(), decentFilter: (x) =>
			{
				var center = x.Bvh.GetCenter();
				var worldCenter = Vector3.Transform(center, x.TransformToWorld);
				if (worldCenter.z > 0)
				{
					return true;
				}

				return false;
			});

			RenderBounds(e, bvhIterator);
		}

		private ViewControls3DButtons? activeButtonBeforeMouseOverride = null;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Show transform override
			if (activeButtonBeforeMouseOverride == null && mouseEvent.Button == MouseButtons.Right)
			{
				activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
				viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
			}
			else if (activeButtonBeforeMouseOverride == null && mouseEvent.Button == MouseButtons.Middle)
			{
				activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
				viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
			}

			if(mouseEvent.Button == MouseButtons.Right ||
				mouseEvent.Button == MouseButtons.Middle)
			{
				meshViewerWidget.SuppressUiVolumes = true;
			}

			autoRotating = false;
			base.OnMouseDown(mouseEvent);

			if (this.TrackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				if (mouseEvent.Button == MouseButtons.Left
					&&
					(ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
					|| (
						this.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
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
							if (Scene.HasSelection)
							{
								if (Scene.SelectedItem.ItemType == Object3DTypes.SelectionGroup)
								{
									Scene.ModifyChildren(ClearSelectionApplyChanges);
								}
								else
								{
									Scene.ClearSelection();
								}
								SelectedTransformChanged?.Invoke(this, null);
							}

							// start a selection rect
							DragSelectionStartPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
							DragSelectionEndPosition = DragSelectionStartPosition;
							DragSelectionInProgress = true;
						}
						else
						{
							CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.z, null);

							if (hitObject != Scene.SelectedItem)
							{
								if (Scene.SelectedItem == null)
								{
									// No selection exists
									Scene.Select(hitObject);
								}
								else if ((ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
									&& !Scene.SelectedItem.Children.Contains(hitObject))
								{
									Scene.AddToSelection(hitObject);
								}
								else if (Scene.SelectedItem == hitObject || Scene.SelectedItem.Children.Contains(hitObject))
								{
									// Selection should not be cleared and drag should occur
								}
								else if (ModifierKeys != Keys.Shift)
								{
									Scene.ModifyChildren(children =>
									{
										ClearSelectionApplyChanges(children);
									});

									Scene.Select(hitObject);
								}

								PartHasBeenChanged();
							}

							transformOnMouseDown = Scene.SelectedItem.Matrix;

							Invalidate();
							CurrentSelectInfo.DownOnPart = true;

							AxisAlignedBoundingBox selectedBounds = meshViewerWidget.Scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

							if (info.HitPosition.x < selectedBounds.Center.x)
							{
								if (info.HitPosition.y < selectedBounds.Center.y)
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
								if (info.HitPosition.y < selectedBounds.Center.y)
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RB;
								}
								else
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RT;
								}
							}

							SelectedTransformChanged?.Invoke(this, null);
						}
					}
				}
			}
		}

		public void ClearSelectionApplyChanges(List<IObject3D> target)
		{
			Scene.SelectedItem.CollapseInto(target);
			Scene.ClearSelection();
		}

		public IntersectInfo GetIntersectPosition(Vector2 screenSpacePosition)
		{
			//Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));

			// Translate to local
			Vector2 localPosition = this.TransformFromScreenSpace(screenSpacePosition);

			Ray ray = this.World.GetRayForLocalBounds(localPosition);

			return CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
		}

		public void DragSelectedObject(Vector2 localMousePostion)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, localMousePostion);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
			if (info != null)
			{
				// move the mesh back to the start position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					Scene.SelectedItem.Matrix *= totalTransform;
				}

				Vector3 delta = info.HitPosition - CurrentSelectInfo.PlaneDownHitPos;

				double snapGridDistance = this.InteractionLayer.SnapGridDistance;
				if (snapGridDistance > 0)
				{
					// snap this position to the grid
					AxisAlignedBoundingBox selectedBounds = meshViewerWidget.Scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					double xSnapOffset = selectedBounds.minXYZ.x;
					// snap the x position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.RB
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						xSnapOffset = selectedBounds.maxXYZ.x;
					}
					double xToSnap = xSnapOffset + delta.x;

					double snappedX = ((int)((xToSnap / snapGridDistance) + .5)) * snapGridDistance;
					delta.x = snappedX - xSnapOffset;

					double ySnapOffset = selectedBounds.minXYZ.y;
					// snap the y position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.LT
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						ySnapOffset = selectedBounds.maxXYZ.y;
					}
					double yToSnap = ySnapOffset + delta.y;

					double snappedY = ((int)((yToSnap / snapGridDistance) + .5)) * snapGridDistance;
					delta.y = snappedY - ySnapOffset;
				}

				// move the mesh back to the new position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(delta));

					Scene.SelectedItem.Matrix *= totalTransform;

					CurrentSelectInfo.LastMoveDelta = delta;
				}

				Invalidate();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (AllowDragDrop() && mouseEvent.DragFiles?.Count == 1)
			{
				var screenSpaceMousePosition = this.TransformToScreenSpace(new Vector2(mouseEvent.X, mouseEvent.Y));

				// If the DragDropSource was added to the scene on this DragOver call, we start a task to replace 
				// the "loading" mesh with the actual file contents
				if (AltDragOver(screenSpaceMousePosition))
				{
					this.DragDropSource.MeshPath = mouseEvent.DragFiles.FirstOrDefault();

					// Run the rest of the OnDragOver pipeline since we're starting a new thread and won't finish for an unknown time
					base.OnMouseMove(mouseEvent);

					LoadDragSource(null);

					// Don't fall through to the base.OnDragOver because we preemptively invoked it above
					return;
				}
			}

			// AcceptDrop anytime a DropSource has been queued
			mouseEvent.AcceptDrop = this.DragDropSource != null;

			if (CurrentSelectInfo.DownOnPart && this.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				DragSelectedObject(new Vector2(mouseEvent.X, mouseEvent.Y));
			}

			if (DragSelectionInProgress)
			{
				DragSelectionEndPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
				DragSelectionEndPosition = new Vector2(
					Math.Max(Math.Min(DragSelectionEndPosition.x, meshViewerWidget.LocalBounds.Right), meshViewerWidget.LocalBounds.Left),
					Math.Max(Math.Min(DragSelectionEndPosition.y, meshViewerWidget.LocalBounds.Top), meshViewerWidget.LocalBounds.Bottom));
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
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
			for(int i=parents.Count-1; i>=0; i--)
			{
				offset += parents[i].OriginRelativeParent;
			}
			return offset;
		}

		public void ResetView()
		{
			this.TrackballTumbleWidget.ZeroVelocity();

			var world = this.World;

			world.Reset();
			world.Scale = .03;
			world.Translate(-new Vector3(bedCenter));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				if (AllowDragDrop() && mouseEvent.DragFiles.Count == 1)
				{
					// Item is already in the scene
					this.DragDropSource = null;
				}
				else if (AllowDragDrop())
				{
					// Items need to be added to the scene
					var partsToAdd = mouseEvent.DragFiles.Where(filePath => ApplicationController.Instance.IsLoadableFile(filePath)).ToArray();

					if (partsToAdd.Length > 0)
					{
						loadAndAddPartsToPlate(partsToAdd);
					}
				}
			}

			if (this.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				if (Scene.SelectedItem != null
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

		public void PartHasBeenChanged()
		{
			partHasBeenEdited = true;
			saveButtons.Visible = true;
			SelectedTransformChanged?.Invoke(this, null);
			Invalidate();
		}

		internal GuiWidget AddMaterialControls()
		{
			FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Fit,
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				Padding = 15
			};

			extruderButtons.Clear();
			int extruderCount = 4;
			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				FlowLayoutWidget colorSelectionContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					HAnchor = HAnchor.Fit,
					Padding = new BorderDouble(5)
				};
				buttonPanel.AddChild(colorSelectionContainer);

				string extruderLabelText = string.Format("{0} {1}", "Extruder".Localize(), extruderIndex + 1);

				RadioButton extruderSelection = new RadioButton(extruderLabelText, textColor: ActiveTheme.Instance.PrimaryTextColor);
				extruderButtons.Add(extruderSelection);
				extruderSelection.SiblingRadioButtonList = extruderButtons;
				colorSelectionContainer.AddChild(extruderSelection);
				colorSelectionContainer.AddChild(new HorizontalSpacer());
				int extruderIndexCanPassToClick = extruderIndex;
				extruderSelection.Click += (sender, e) =>
				{
					if (Scene.HasSelection)
					{
						Scene.SelectedItem.MaterialIndex = extruderIndexCanPassToClick;
						PartHasBeenChanged();
					}
				};

				colorSelectionContainer.AddChild(new GuiWidget(16, 16)
				{
					BackgroundColor = MatterialRendering.Color(extruderIndex),
					Margin = new BorderDouble(5, 0, 0, 0)
				});
			}

			return buttonPanel;
		}

		private void AddSaveAndSaveAs(FlowLayoutWidget flowToAddTo, BorderDouble margin)
		{
			var buttonList = new List<NamedAction>()
			{
				{
					"Save".Localize(),
					async () =>
					{
						if (printItemWrapper == null)
						{
							UiThread.RunOnIdle(OpenSaveAsWindow);
						}
						else
						{
							await this.SaveChanges();
						}
					}
				},
				{
					"Save As".Localize(),
					() => UiThread.RunOnIdle(OpenSaveAsWindow)
				}
			};

			var splitButtonFactory = new SplitButtonFactory()
			{
				FixedHeight = ApplicationController.Instance.Theme.SmallMarginButtonFactory.FixedHeight
			};

			saveButtons = splitButtonFactory.Generate(buttonList, Direction.Up, imageName: "icon_save_32x32.png");
			saveButtons.Visible = false;
			saveButtons.Margin = margin;
			saveButtons.VAnchor |= VAnchor.Center;
			flowToAddTo.AddChild(saveButtons);
		}

		// Indicates if MatterControl is in a mode that allows DragDrop  - true if printItem not null and not ReadOnly
		private bool AllowDragDrop() => !printItemWrapper?.PrintItem.ReadOnly ?? false;

		private void AutoSpin()
		{
			if (!HasBeenClosed && autoRotating)
			{
				// add it back in to keep it running.
				UiThread.RunOnIdle(AutoSpin, .04);

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

		internal void ReportProgressChanged(double progress0To1, string processingState)
		{
			if (!timeSinceReported.IsRunning || timeSinceReported.ElapsedMilliseconds > 100
				|| processingState != processingProgressControl.ProgressMessage)
			{
				UiThread.RunOnIdle(() =>
				{
					processingProgressControl.RatioComplete = progress0To1;
					processingProgressControl.ProgressMessage = processingState;
				});
				timeSinceReported.Restart();
			}
		}

		public async Task ClearBedAndLoadPrintItemWrapper(PrintItemWrapper newPrintItem, bool switchToEditingMode = false)
		{
			SwitchStateToEditing();

			Scene.ModifyChildren(children => children.Clear());

			if (newPrintItem != null)
			{
				// don't load the mesh until we get all the rest of the interface built
				meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);

				Vector2 bedCenter = new Vector2();

				await meshViewerWidget.LoadItemIntoScene(newPrintItem.FileLocation, bedCenter, newPrintItem.Name);

				Invalidate();
			}

			this.printItemWrapper = newPrintItem;

			PartHasBeenChanged();
			partHasBeenEdited = false;
		}

		public List<IObject3DEditor> objectEditors = new List<IObject3DEditor>();

		public Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

		public IObject3DEditor ActiveSelectionEditor { get; set; }

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if (!Scene.HasSelection)
			{
				selectedObjectPanel.RemoveAllChildren();
				return;
			}

			if (deferEditorTillMouseUp)
			{
				return;
			}

			var selectedItem = Scene.SelectedItem;

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItem.GetType(), out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var editor in objectEditorsByType)
				{
					if (selectedItem.GetType().IsSubclassOf(editor.Key))
					{
						mappedEditors = editor.Value;
						break;
					}
				}
			}

			// Add any editor mapped to Object3D to the list
			if (objectEditorsByType.TryGetValue(typeof(Object3D), out HashSet<IObject3DEditor> globalEditors))
			{
				foreach(var editor in globalEditors)
				{
					mappedEditors.Add(editor);
				}
			}

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
			};

			if (mappedEditors != null)
			{
				var dropDownList = new DropDownList("", maxHeight: 300)
				{
					Margin = 3
				};

				foreach (IObject3DEditor editor in mappedEditors)
				{
					MenuItem menuItem = dropDownList.AddItem(editor.Name);
					menuItem.Selected += (s, e2) =>
					{
						ShowObjectEditor(editor);
					};
				}

				selectedObjectPanel.RemoveAllChildren();
				selectedObjectPanel.AddChild(dropDownList);
				selectedObjectPanel.AddChild(editorPanel);

				// Select the active editor or fall back to the first if not found
				this.ActiveSelectionEditor = (from editor in mappedEditors
											  let type = editor.GetType()
											  where type.Name == selectedItem.ActiveEditor
											  select editor).FirstOrDefault();

				// Fall back to default editor?
				if (this.ActiveSelectionEditor == null)
				{
					this.ActiveSelectionEditor = mappedEditors.First();
				}

				int selectedIndex = 0;
				for (int i = 0; i < dropDownList.MenuItems.Count; i++)
				{
					if (dropDownList.MenuItems[i].Text == this.ActiveSelectionEditor.Name)
					{
						selectedIndex = i;
						break;
					}
				}

				dropDownList.SelectedIndex = selectedIndex;

				ShowObjectEditor(this.ActiveSelectionEditor);
			}

			if (extruderButtons?.Count > 0)
			{
				bool setSelection = false;
				// Set the material selector to have the correct material button selected
				for (int i = 0; i < extruderButtons.Count; i++)
				{
					if (selectedItem.MaterialIndex == i)
					{
						((RadioButton)extruderButtons[i]).Checked = true;
						setSelection = true;
					}
				}

				if(!setSelection)
				{
					((RadioButton)extruderButtons[0]).Checked = true;
				}
			}
		}

		private void ShowObjectEditor(IObject3DEditor editor)
		{
			editorPanel.CloseAllChildren();

			var newEditor = editor.Create(Scene.SelectedItem, this, this.theme);
			editorPanel.AddChild(newEditor);
		}

		private void DrawStuffForSelectedPart(Graphics2D graphics2D)
		{
			if (Scene.HasSelection)
			{
				AxisAlignedBoundingBox selectedBounds = Scene.SelectedItem.GetAxisAlignedBoundingBox(Scene.SelectedItem.Matrix);
				Vector3 boundsCenter = selectedBounds.Center;
				Vector3 centerTop = new Vector3(boundsCenter.x, boundsCenter.y, selectedBounds.maxXYZ.z);

				Vector2 centerTopScreenPosition = this.World.GetScreenPosition(centerTop);
				centerTopScreenPosition = meshViewerWidget.TransformToParentSpace(this, centerTopScreenPosition);
				//graphics2D.Circle(screenPosition.x, screenPosition.y, 5, RGBA_Bytes.Cyan);

				PathStorage zArrow = new PathStorage();
				zArrow.MoveTo(-6, -2);
				zArrow.curve3(0, -4);
				zArrow.LineTo(6, -2);
				zArrow.LineTo(0, 12);
				zArrow.LineTo(-6, -2);

				VertexSourceApplyTransform translate = new VertexSourceApplyTransform(zArrow, Affine.NewTranslation(centerTopScreenPosition));

				//graphics2D.Render(translate, RGBA_Bytes.Black);
			}
		}

		private async void LoadAndAddPartsToPlate(string[] filesToLoad)
		{
			if (Scene.HasChildren && filesToLoad != null && filesToLoad.Length > 0)
			{
				processingProgressControl.ProcessType = "Loading Parts".Localize() + ":";
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => loadAndAddPartsToPlate(filesToLoad));

				if (HasBeenClosed)
				{
					return;
				}

				UnlockEditControls();
				PartHasBeenChanged();

				bool addingOnlyOneItem = Scene.Children.Count == Scene.Children.Count + 1;

				if (Scene.HasChildren)
				{
					if (addingOnlyOneItem)
					{
						// if we are only adding one part to the plate set the selection to it
						Scene.SelectLastChild();
					}
				}
			}
		}

		private void loadAndAddPartsToPlate(string[] filesToLoadIncludingZips)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			if (filesToLoadIncludingZips?.Any() == true)
			{
				List<string> filesToLoad = new List<string>();
				foreach (string loadedFileName in filesToLoadIncludingZips)
				{
					string extension = Path.GetExtension(loadedFileName).ToUpper();
					if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
					{
						filesToLoad.Add(loadedFileName);
					}
					else if (extension == ".ZIP")
					{
						List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(loadedFileName);
						if (partFiles != null)
						{
							foreach (PrintItem part in partFiles)
							{
								filesToLoad.Add(part.FileLocation);
							}
						}
					}
				}

				string progressMessage = "Loading Parts...".Localize();

				double ratioPerFile = 1.0 / filesToLoad.Count;
				double currentRatioDone = 0;

				var itemCache = new Dictionary<string, IObject3D>();

				foreach (string filePath in filesToLoad)
				{
					var libraryItem = new FileSystemFileItem(filePath);

					var contentResult = libraryItem.CreateContent((double progress0To1, string processingState) =>
					{
						double ratioAvailable = (ratioPerFile * .5);
						double currentRatio = currentRatioDone + progress0To1 * ratioAvailable;

						ReportProgressChanged(currentRatio, progressMessage);
					});

					contentResult?.MeshLoaded.ContinueWith((task) =>
					{
						if (contentResult != null && contentResult.Object3D != null)
						{
							Scene.ModifyChildren(children => children.Add(contentResult.Object3D));

							PlatingHelper.MoveToOpenPosition(contentResult.Object3D, this.Scene.Children);

							// TODO: There should be a batch insert so you can undo large 'add to scene' operations in one go
							//this.InsertNewItem(tempScene);
						}
					});

					if (HasBeenClosed)
					{
						return;
					}

					currentRatioDone += ratioPerFile;
				}
			}

			this.PartHasBeenChanged();
		}

		public void LockEditControls()
		{
			viewIsInEditModePreLock = selectionActionBar.Visible;
			selectionActionBar.Visible = false;
		}

		internal void MakeLowestFaceFlat(IObject3D objectToLayFlatGroup)
		{
			Matrix4X4 objectToWold = objectToLayFlatGroup.Matrix;
			IObject3D objectToLayFlat = objectToLayFlatGroup.Children[0];

			var lowestVertex = objectToLayFlat.Mesh.Vertices[0];

			Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, objectToWold);

			IObject3D itemToLayFlat = null;

			// Process each child, checking for the lowest vertex
			foreach (IObject3D itemToCheck in objectToLayFlat.Children.Where(child => child.Mesh != null))
			{
				// find the lowest point on the model
				for (int testIndex = 1; testIndex < itemToCheck.Mesh.Vertices.Count; testIndex++)
				{
					var vertex = itemToCheck.Mesh.Vertices[testIndex];
					Vector3 vertexPosition = Vector3.Transform(vertex.Position, objectToWold);
					if (vertexPosition.z < lowestVertexPosition.z)
					{
						lowestVertex = itemToCheck.Mesh.Vertices[testIndex];
						lowestVertexPosition = vertexPosition;
						itemToLayFlat = itemToCheck;
					}
				}
			}

			Face faceToLayFlat = null;
			double lowestAngleOfAnyFace = double.MaxValue;
			// Check all the faces that are connected to the lowest point to find out which one to lay flat.
			foreach (Face face in lowestVertex.ConnectedFaces())
			{
				double biggestAngleToFaceVertex = double.MinValue;
				foreach (IVertex faceVertex in face.Vertices())
				{
					if (faceVertex != lowestVertex)
					{
						Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, objectToWold);
						Vector3 pointRelLowest = faceVertexPosition - lowestVertexPosition;
						double xLeg = new Vector2(pointRelLowest.x, pointRelLowest.y).Length;
						double yLeg = pointRelLowest.z;
						double angle = Math.Atan2(yLeg, xLeg);
						if (angle > biggestAngleToFaceVertex)
						{
							biggestAngleToFaceVertex = angle;
						}
					}
				}
				if (biggestAngleToFaceVertex < lowestAngleOfAnyFace)
				{
					lowestAngleOfAnyFace = biggestAngleToFaceVertex;
					faceToLayFlat = face;
				}
			}

			double maxDistFromLowestZ = 0;
			List<Vector3> faceVertexes = new List<Vector3>();
			foreach (IVertex vertex in faceToLayFlat.Vertices())
			{
				Vector3 vertexPosition = Vector3.Transform(vertex.Position, objectToWold);
				faceVertexes.Add(vertexPosition);
				maxDistFromLowestZ = Math.Max(maxDistFromLowestZ, vertexPosition.z - lowestVertexPosition.z);
			}

			if (maxDistFromLowestZ > .001)
			{
				Vector3 xPositive = (faceVertexes[1] - faceVertexes[0]).GetNormal();
				Vector3 yPositive = (faceVertexes[2] - faceVertexes[0]).GetNormal();
				Vector3 planeNormal = Vector3.Cross(xPositive, yPositive).GetNormal();

				// this code takes the minimum rotation required and looks much better.
				Quaternion rotation = new Quaternion(planeNormal, new Vector3(0, 0, -1));
				Matrix4X4 partLevelMatrix = Matrix4X4.CreateRotation(rotation);

				// rotate it
				objectToLayFlatGroup.Matrix = PlatingHelper.ApplyAtCenter(objectToLayFlatGroup, partLevelMatrix);

				PartHasBeenChanged();
				Invalidate();
			}
		}

		public static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)", RegexOptions.Compiled);

		internal GuiWidget selectedObjectPanel;
		private FlowLayoutWidget editorPanel;

		private async Task SaveChanges()
		{
			if (Scene.HasChildren)
			{
				processingProgressControl.ProcessType = "Saving".Localize() + ":";
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;

				LockEditControls();

				// Perform the actual save operation
				await Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					try
					{
						// Force to .mcx
						if (Path.GetExtension(printItemWrapper.FileLocation) != ".mcx")
						{
							printItemWrapper.FileLocation = Path.ChangeExtension(printItemWrapper.FileLocation, ".mcx");
						}

						// TODO: Hook up progress reporting
						Scene.Save(printItemWrapper.FileLocation, ApplicationDataStorage.Instance.ApplicationLibraryDataPath);

						printItemWrapper.PrintItem.Commit();
					}
					catch (Exception ex)
					{
						Trace.WriteLine("Error saving file: ", ex.Message);
					}
				});

				// Post Save cleanup
				if (this.HasBeenClosed)
				{
					return;
				}

				UnlockEditControls();
				saveButtons.Visible = true;
				partHasBeenEdited = false;
			}
		}

		private void meshViewerWidget_LoadDone(object sender, EventArgs e)
		{
			if (windowType == WindowMode.Embeded)
			{
				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.Printing:
					case CommunicationStates.Paused:
						break;

					default:
						UnlockEditControls();
						break;
				}
			}
			else
			{
				UnlockEditControls();
			}

			if (openMode == OpenMode.Editing)
			{
				UiThread.RunOnIdle(SwitchStateToEditing);
			}
		}

		private bool PartsAreInPrintVolume()
		{
			AxisAlignedBoundingBox allBounds = AxisAlignedBoundingBox.Empty;
			foreach (var aabb in Scene.Children.Select(item => item.GetAxisAlignedBoundingBox(Matrix4X4.Identity)))
			{
				allBounds += aabb;
			}

			bool onBed = allBounds.minXYZ.z > -.001 && allBounds.minXYZ.z < .001; // really close to the bed
			RectangleDouble bedRect = new RectangleDouble(0, 0, ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size).x, ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size).y);
			bedRect.Offset(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center) - ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size) / 2);

			bool inBounds = bedRect.Contains(new Vector2(allBounds.minXYZ)) && bedRect.Contains(new Vector2(allBounds.maxXYZ));

			return onBed && inBounds;
		}

		private void OpenExportWindow()
		{
			var exportPage = new ExportPrintItemPage(new[] { new FileSystemFileItem(this.printItemWrapper.FileLocation) });
			string windowTitle = "MatterControl".Localize() + ": " + "Export File".Localize();
			WizardWindow.Show("/ExportPrintItemPage", windowTitle, exportPage);
		}

		private void OpenSaveAsWindow()
		{
			if (saveAsWindow == null)
			{
				saveAsWindow = new SaveAsWindow(
					async (returnInfo) =>
					{
						// TODO: The PrintItemWrapper seems unnecessary in the new LibraryContainer model. Couldn't we just pass the scene to the LibraryContainer via it's add function, no need to perist to disk?
						// Create a new PrintItemWrapper
						printItemWrapper = new PrintItemWrapper(
						new PrintItem()
						{
							Name = returnInfo.newName,
							FileLocation = Path.ChangeExtension(returnInfo.fileNameAndPath, ".mcx")
						},
						returnInfo.DestinationContainer);

						// Save the scene to disk
						await this.SaveChanges();

						// Save to the destination provider
						if (returnInfo?.DestinationContainer != null)
						{
							// save this part to correct library provider
							if (returnInfo.DestinationContainer is ILibraryWritableContainer writableContainer)
							{
								writableContainer.Add(new[]
								{
									new FileSystemFileItem(printItemWrapper.FileLocation)
									{
										Name = returnInfo.newName
									}
								});

								returnInfo.DestinationContainer.Dispose();
							}
						}
					}, 
					printItemWrapper?.SourceLibraryProviderLocator, 
					true, 
					true);

				saveAsWindow.Closed += SaveAsWindow_Closed;
			}
			else
			{
				saveAsWindow.BringToFront();
			}
		}

		private bool rotateQueueMenu_Click()
		{
			return true;
		}

		private void SaveAsWindow_Closed(object sender, ClosedEventArgs e)
		{
			this.saveAsWindow = null;
		}

		private void SetEditControlsBasedOnPrinterState(object sender, EventArgs e)
		{
			if (windowType == WindowMode.Embeded)
			{
				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.Printing:
					case CommunicationStates.Paused:
						LockEditControls();
						break;

					default:
						UnlockEditControls();
						break;
				}
			}
		}

		public Vector2 DragSelectionStartPosition { get; private set; }
		public bool DragSelectionInProgress { get; private set; }
		public Vector2 DragSelectionEndPosition { get; private set; }

		internal async void SwitchStateToEditing()
		{
			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

			processingProgressControl.Visible = true;
			LockEditControls();
			viewIsInEditModePreLock = true;

			if (Scene.HasChildren)
			{
				// CreateSelectionData()
				await Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
					processingProgressControl.ProcessType = "Preparing Meshes".Localize() + ":";

					// Force trace data generation
					foreach (var object3D in Scene.Children)
					{
						object3D.TraceData();
					}

					// TODO: Why were we recreating GLData on edit? 
					//bool continueProcessing2;
					//ReportProgressChanged(1, "Creating GL Data", continueProcessing2);
					//meshViewerWidget.CreateGlDataForMeshes(Scene.Children);
				});

				if (this.HasBeenClosed)
				{
					return;
				}

				Scene.SelectFirstChild();
			}

			UnlockEditControls();
			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

			Invalidate();
		}

		// Before printing persist any changes to disk
		internal async Task PersistPlateIfNeeded()
		{
			if (partHasBeenEdited)
			{
				await this.SaveChanges();
			}
		}

		public void UnlockEditControls()
		{
			processingProgressControl.Visible = false;

			if (viewIsInEditModePreLock)
			{
				viewControls3D.PartSelectVisible = true;
				selectionActionBar.Visible = true;
			}

			if (wasInSelectMode)
			{
				viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;
				wasInSelectMode = false;
			}

			SelectedTransformChanged?.Invoke(this, null);
		}

		// ViewControls3D {{
		internal GuiWidget ShowOverflowMenu()
		{
			var popupContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = 12,
				BackgroundColor = RGBA_Bytes.White
			};

			var meshViewer = meshViewerWidget;

			popupContainer.AddChild(
				this.theme.CreateCheckboxMenuItem(
					"Show Print Bed".Localize(),
					"ShowPrintBed",
					meshViewer.RenderBed,
					5,
					(s, e) =>
					{
						if (s is CheckBox checkbox)
						{
							meshViewer.RenderBed = checkbox.Checked;
						}
					}));

			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);
			if (buildHeight > 0)
			{
				popupContainer.AddChild(
					this.theme.CreateCheckboxMenuItem(
						"Show Print Area".Localize(),
						"ShowPrintArea",
						meshViewer.RenderBuildVolume,
						5,
						(s, e) =>
						{
							if (s is CheckBox checkbox)
							{
								meshViewer.RenderBuildVolume = checkbox.Checked;
							}
						}));
			}
		
			popupContainer.AddChild(new HorizontalLine());

			var renderOptions = CreateRenderTypeRadioButtons();
			popupContainer.AddChild(renderOptions);

			popupContainer.AddChild(new GridOptionsPanel(this.InteractionLayer));

			return popupContainer;
		}

		private GuiWidget CreateRenderTypeRadioButtons()
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5, 5, 5, 0)
			};

			string renderTypeString = UserSettings.Instance.get(UserSettingsKey.defaultRenderSetting);
			if (renderTypeString == null)
			{
				if (UserSettings.Instance.IsTouchScreen)
				{
					renderTypeString = "Shaded";
				}
				else
				{
					renderTypeString = "Outlines";
				}
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderTypeString);
			}

			//var itemTextColor = ActiveTheme.Instance.PrimaryTextColor;
			var itemTextColor = RGBA_Bytes.Black;

			RenderTypes renderType;
			bool canParse = Enum.TryParse(renderTypeString, out renderType);
			if (canParse)
			{
				meshViewerWidget.RenderType = renderType;
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Shaded".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Shaded);

				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Shaded;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				container.AddChild(renderTypeCheckBox);
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Outlines".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Outlines);
				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Outlines;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				container.AddChild(renderTypeCheckBox);
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Polygons".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Polygons);
				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Polygons;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				container.AddChild(renderTypeCheckBox);
			}

			// Materials option
			{
				RadioButton materialsCheckBox = new RadioButton("Materials".Localize(), textColor: itemTextColor);
				materialsCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Materials);

				materialsCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (materialsCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Materials;
						UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
					}
				};

				container.AddChild(materialsCheckBox);
			}


			// overhang setting
			{
				RadioButton renderTypeCheckBox = new RadioButton("Overhang".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Overhang);

				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						// TODO: Determine if Scene is available in scope
						var scene = this.Scene;

						meshViewerWidget.RenderType = RenderTypes.Overhang;

						UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
						foreach (var meshRenderData in scene.VisibleMeshes(Matrix4X4.Identity))
						{
							meshRenderData.Mesh.MarkAsChanged();
							// change the color to be the right thing
							GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshRenderData.Mesh, (faceEdge) =>
							{
								Vector3 normal = faceEdge.ContainingFace.normal;
								normal = Vector3.TransformVector(normal, meshRenderData.Matrix).GetNormal();
								VertexColorData colorData = new VertexColorData();

								double startColor = 223.0 / 360.0;
								double endColor = 5.0 / 360.0;
								double delta = endColor - startColor;

								RGBA_Bytes color = RGBA_Floats.FromHSL(startColor, .99, .49).GetAsRGBA_Bytes();
								if (normal.z < 0)
								{
									color = RGBA_Floats.FromHSL(startColor - delta * normal.z, .99, .49).GetAsRGBA_Bytes();
								}

								colorData.red = color.red;
								colorData.green = color.green;
								colorData.blue = color.blue;
								return colorData;
							});
						}
					}
				};

				container.AddChild(renderTypeCheckBox);
			}

			return container;
		}

		protected bool autoRotating = false;
		protected bool allowAutoRotate = false;

		public MeshViewerWidget meshViewerWidget;

		// Proxy to MeshViewerWidget
		public InteractiveScene Scene => meshViewerWidget.Scene;

		protected ViewControls3D viewControls3D { get; }

		private bool needToRecreateBed = false;

		public MeshSelectInfo CurrentSelectInfo { get; } = new MeshSelectInfo();

		protected IObject3D FindHitObject3D(Vector2 screenPosition, ref IntersectInfo intersectionInfo)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			intersectionInfo = Scene.TraceData().GetClosestIntersection(ray);
			if (intersectionInfo != null)
			{
				foreach (Object3D object3D in Scene.Children)
				{
					if (object3D.TraceData().Contains(intersectionInfo.closestHitObject))
					{
						CurrentSelectInfo.PlaneDownHitPos = intersectionInfo.HitPosition;
						CurrentSelectInfo.LastMoveDelta = new Vector3();
						return object3D;
					}
				}
			}

			return null;
		}

		private void CheckSettingChanged(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					needToRecreateBed = true;
				}
			}
		}

		private void RecreateBed()
		{
			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);

			UiThread.RunOnIdle((Action)(() =>
			{
				meshViewerWidget.CreatePrintBed(
					new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center),
					ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape));
				PutOemImageOnBed();
			}));
		}

		static ImageBuffer wattermarkImage = null;
		protected void PutOemImageOnBed()
		{
			// this is to add an image to the bed
			string imagePathAndFile = Path.Combine("OEMSettings", "bedimage.png");
			if (StaticData.Instance.FileExists(imagePathAndFile))
			{
				if (wattermarkImage == null)
				{
					wattermarkImage = StaticData.Instance.LoadImage(imagePathAndFile);
				}

				ImageBuffer bedImage = MeshViewerWidget.BedImage;
				Graphics2D bedGraphics = bedImage.NewGraphics2D();
				bedGraphics.Render(wattermarkImage, new Vector2((bedImage.Width - wattermarkImage.Width) / 2, (bedImage.Height - wattermarkImage.Height) / 2));
			}
		}

		// ViewControls3D }}
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