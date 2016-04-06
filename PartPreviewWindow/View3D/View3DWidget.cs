﻿/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
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
using System.Xml.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public interface IInteractionVolumeCreator
	{
		InteractionVolume CreateInteractionVolume(View3DWidget widget);
	}

	public class DragDropLoadProgress
	{
		IObject3D trackingObject;
		View3DWidget view3DWidget;
		private ProgressBar progressBar;

		public DragDropLoadProgress(View3DWidget view3DWidget, IObject3D trackingObject)
		{
			this.trackingObject = trackingObject;
			this.view3DWidget = view3DWidget;
			view3DWidget.AfterDraw += View3DWidget_AfterDraw;
			progressBar = new ProgressBar(80, 15)
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
			};
		}

		private void View3DWidget_AfterDraw(GuiWidget drawingWidget, DrawEventArgs e)
		{
			if (view3DWidget?.meshViewerWidget?.TrackballTumbleWidget != null)
			{
				AxisAlignedBoundingBox bounds = trackingObject.GetAxisAlignedBoundingBox();
				Vector3 renderPosition = bounds.Center;
				Vector2 cornerScreenSpace = view3DWidget.meshViewerWidget.TrackballTumbleWidget.GetScreenPosition(renderPosition) - new Vector2(40, 20);

				e.graphics2D.PushTransform();
				Affine currentGraphics2DTransform = e.graphics2D.GetTransform();
				Affine accumulatedTransform = currentGraphics2DTransform * Affine.NewTranslation(cornerScreenSpace.x, cornerScreenSpace.y);
				e.graphics2D.SetTransform(accumulatedTransform);

				progressBar.OnDraw(e.graphics2D);
				e.graphics2D.PopTransform();
			}
		}

		public void UpdateLoadProgress(double progress0To1, string processingState, out bool continueProcessing)
		{
			continueProcessing = true;
			progressBar.RatioComplete = progress0To1;
			if (progress0To1 == 1)
			{
				view3DWidget.AfterDraw -= View3DWidget_AfterDraw;
				view3DWidget = null;
			}
        }
	}

	public class InteractionVolumePlugin : IInteractionVolumeCreator
	{
		public virtual InteractionVolume CreateInteractionVolume(View3DWidget widget)
		{
			return null;
		}
	}

	public interface ISideBarToolCreator
	{
		GuiWidget CreateSideBarTool(View3DWidget widget);
	}

	public class SideBarPlugin : ISideBarToolCreator
	{
		public virtual GuiWidget CreateSideBarTool(View3DWidget widget)
		{
			return null;
		}
	}

	public partial class View3DWidget : PartPreview3DWidget
	{
		bool DoBooleanTest = false;
		public FlowLayoutWidget doEdittingButtonsContainer;

		public readonly int EditButtonHeight = 44;

		private UndoBuffer undoBuffer = new UndoBuffer();
		private Action afterSaveCallback = null;
		private bool editorThatRequestedSave = false;
		private FlowLayoutWidget enterEditButtonsContainer;
		private ExportPrintItemWindow exportingWindow = null;
		private ObservableCollection<GuiWidget> extruderButtons = new ObservableCollection<GuiWidget>();
		private bool hasDrawn = false;

		private OpenMode openMode;
		private bool partHasBeenEdited = false;
		private List<string> pendingPartsToLoad = new List<string>();
		private PrintItemWrapper printItemWrapper;
		private ProgressControl processingProgressControl;
		private SaveAsWindow saveAsWindow = null;
		private SplitButton saveButtons;
		private bool saveSucceded = true;
		private EventHandler SelectionChanged;
		private RGBA_Bytes[] SelectionColors = new RGBA_Bytes[] { new RGBA_Bytes(131, 4, 66), new RGBA_Bytes(227, 31, 61), new RGBA_Bytes(255, 148, 1), new RGBA_Bytes(247, 224, 23), new RGBA_Bytes(143, 212, 1) };
		private Stopwatch timeSinceLastSpin = new Stopwatch();
		private Stopwatch timeSinceReported = new Stopwatch();
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private EventHandler unregisterEvents;

		private bool viewIsInEditModePreLock = false;


		private bool wasInSelectMode = false;

		public event EventHandler SelectedTransformChanged;
		public View3DWidgetSidebar Sidebar;

		protected FlowLayoutWidget editPlateButtonsContainer;

		public View3DWidget(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape, WindowMode windowType, AutoRotate autoRotate, OpenMode openMode = OpenMode.Viewing)
		{
			this.openMode = openMode;
			this.windowType = windowType;
			allowAutoRotate = (autoRotate == AutoRotate.Enabled);
			autoRotating = allowAutoRotate;

			this.printItemWrapper = printItemWrapper;
			this.Name = "View3DWidget";

			FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

			FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
			centerPartPreviewAndControls.Name = "centerPartPreviewAndControls";
			centerPartPreviewAndControls.AnchorAll();

			GuiWidget viewArea = new GuiWidget();
			viewArea.AnchorAll();
			{
				meshViewerWidget = new MeshViewerWidget(viewerVolume, bedCenter, bedShape, "Press 'Add' to select an item.".Localize());

				PutOemImageOnBed();

				meshViewerWidget.AnchorAll();
			}
			viewArea.AddChild(meshViewerWidget);

			centerPartPreviewAndControls.AddChild(viewArea);
			mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);

			FlowLayoutWidget buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.Padding = new BorderDouble(3, 3);
			buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			Sidebar = new View3DWidgetSidebar(this, viewerVolume.y, undoBuffer);
			Sidebar.Name = "buttonRightPanel";
			Sidebar.Visible = false;
			Sidebar.InitializeComponents();

			Scene.SelectionChanged += Scene_SelectionChanged;

			CreateOptionsContent();

			// add in the plater tools
			{
				FlowLayoutWidget editToolBar = new FlowLayoutWidget();

				string progressFindPartsLabel = "Entering Editor".Localize();
				string progressFindPartsLabelFull = "{0}:".FormatWith(progressFindPartsLabel);

				processingProgressControl = new ProgressControl(progressFindPartsLabelFull, ActiveTheme.Instance.PrimaryTextColor, ActiveTheme.Instance.PrimaryAccentColor);
				processingProgressControl.VAnchor = Agg.UI.VAnchor.ParentCenter;
				editToolBar.AddChild(processingProgressControl);
				editToolBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
				processingProgressControl.Visible = false;

				// If the window is embedded (in the center panel) and there is no item loaded then don't show the add button
				enterEditButtonsContainer = new FlowLayoutWidget();
				{
					Button addButton = textImageButtonFactory.Generate("Insert".Localize(), "icon_insert_32x32.png");
					addButton.ToolTipText = "Insert an .stl, .amf or .zip file".Localize();
					addButton.Margin = new BorderDouble(right: 0);
					enterEditButtonsContainer.AddChild(addButton);
					addButton.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							DoAddFileAfterCreatingEditData = true;
							EnterEditAndCreateSelectionData();
						});
					};
					if (printItemWrapper != null
						&& printItemWrapper.PrintItem.ReadOnly)
					{
						addButton.Enabled = false;
					}

					Button enterEdittingButton = textImageButtonFactory.Generate("Edit".Localize(), "icon_edit_32x32.png");
					enterEdittingButton.Name = "3D View Edit";
					enterEdittingButton.Margin = new BorderDouble(right: 4);
					enterEdittingButton.Click += (sender, e) =>
					{
						EnterEditAndCreateSelectionData();
					};

					if (printItemWrapper != null
						&& printItemWrapper.PrintItem.ReadOnly)
					{
						enterEdittingButton.Enabled = false;
					}

					Button exportButton = textImageButtonFactory.Generate("Export...".Localize());
					if (printItemWrapper != null &&
						(printItemWrapper.PrintItem.Protected || printItemWrapper.PrintItem.ReadOnly))
					{
						exportButton.Enabled = false;
					}

					exportButton.Margin = new BorderDouble(right: 10);
					exportButton.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							OpenExportWindow();
						});
					};

					enterEditButtonsContainer.AddChild(enterEdittingButton);
					enterEditButtonsContainer.AddChild(exportButton);
				}
				editToolBar.AddChild(enterEditButtonsContainer);

				doEdittingButtonsContainer = new FlowLayoutWidget();
				doEdittingButtonsContainer.Visible = false;

				{
					Button addButton = textImageButtonFactory.Generate("Insert".Localize(), "icon_insert_32x32.png");
					addButton.Margin = new BorderDouble(right: 10);
					doEdittingButtonsContainer.AddChild(addButton);
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

					GuiWidget separator = new GuiWidget(1, 2);
					separator.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
					separator.Margin = new BorderDouble(4, 2);
					separator.VAnchor = VAnchor.ParentBottomTop;
					doEdittingButtonsContainer.AddChild(separator);

					Button ungroupButton = textImageButtonFactory.Generate("Ungroup".Localize());
					ungroupButton.Name = "3D View Ungroup";
					doEdittingButtonsContainer.AddChild(ungroupButton);
					ungroupButton.Click += (sender, e) =>
					{
						UngroupSelectedMeshGroup();
					};

					Button groupButton = textImageButtonFactory.Generate("Group".Localize());
					groupButton.Name = "3D View Group";
					doEdittingButtonsContainer.AddChild(groupButton);
					groupButton.Click += (sender, e) =>
					{
						GroupSelectedMeshs();
					};

					Button alignButton = textImageButtonFactory.Generate("Align".Localize());
					doEdittingButtonsContainer.AddChild(alignButton);
					alignButton.Click += (sender, e) =>
					{
						AlignToSelectedMeshGroup();
					};

					Button arrangeButton = textImageButtonFactory.Generate("Arrange".Localize());
					doEdittingButtonsContainer.AddChild(arrangeButton);
					arrangeButton.Click += (sender, e) =>
					{
						AutoArrangePartsInBackground();
					};

					GuiWidget separatorTwo = new GuiWidget(1, 2);
					separatorTwo.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
					separatorTwo.Margin = new BorderDouble(4, 2);
					separatorTwo.VAnchor = VAnchor.ParentBottomTop;
					doEdittingButtonsContainer.AddChild(separatorTwo);

					Button copyButton = textImageButtonFactory.Generate("Copy".Localize());
					copyButton.Name = "3D View Copy";
					doEdittingButtonsContainer.AddChild(copyButton);
					copyButton.Click += (sender, e) =>
					{
						MakeCopyOfGroup();
					};

					Button deleteButton = textImageButtonFactory.Generate("Remove".Localize());
					deleteButton.Name = "3D View Remove";
					doEdittingButtonsContainer.AddChild(deleteButton);
					deleteButton.Click += (sender, e) =>
					{
						DeleteSelectedMesh();
					};

					GuiWidget separatorThree = new GuiWidget(1, 2);
					separatorThree.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
					separatorThree.Margin = new BorderDouble(4, 1);
					separatorThree.VAnchor = VAnchor.ParentBottomTop;
					doEdittingButtonsContainer.AddChild(separatorThree);

					Button cancelEditModeButton = textImageButtonFactory.Generate("Cancel".Localize(), centerText: true);
					cancelEditModeButton.Name = "3D View Cancel";
					cancelEditModeButton.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							if (saveButtons.Visible)
							{
								StyledMessageBox.ShowMessageBox(ExitEditingAndSaveIfRequired, "Would you like to save your changes before exiting the editor?".Localize(), "Save Changes".Localize(), StyledMessageBox.MessageType.YES_NO);
							}
							else
							{
								if (partHasBeenEdited)
								{
									ExitEditingAndSaveIfRequired(false);
								}
								else
								{
									SwitchStateToNotEditing();
								}
							}
						});
					};

					doEdittingButtonsContainer.AddChild(cancelEditModeButton);

					// put in the save button
					AddSaveAndSaveAs(doEdittingButtonsContainer);
				}

				editToolBar.AddChild(doEdittingButtonsContainer);
				buttonBottomPanel.AddChild(editToolBar);
			}

			GuiWidget buttonRightPanelHolder = new GuiWidget(HAnchor.FitToChildren, VAnchor.ParentBottomTop);
			buttonRightPanelHolder.Name = "buttonRightPanelHolder";
			centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
			buttonRightPanelHolder.AddChild(Sidebar);
			Sidebar.VisibleChanged += (sender, e) =>
			{
				buttonRightPanelHolder.Visible = Sidebar.Visible;
			};

			viewControls3D = new ViewControls3D(meshViewerWidget);
			viewControls3D.ResetView += (sender, e) =>
			{
				meshViewerWidget.ResetView();
			};

			buttonRightPanelDisabledCover = new Cover(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
			buttonRightPanelDisabledCover.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 150);
			buttonRightPanelHolder.AddChild(buttonRightPanelDisabledCover);

			viewControls3D.PartSelectVisible = false;
			LockEditControls();

			GuiWidget leftRightSpacer = new GuiWidget();
			leftRightSpacer.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.AddChild(leftRightSpacer);

			if (windowType == WindowMode.StandAlone)
			{
				Button closeButton = textImageButtonFactory.Generate("Close".Localize());
				buttonBottomPanel.AddChild(closeButton);
				closeButton.Click += (sender, e) =>
				{
					CloseOnIdle();
				};
			}

			mainContainerTopToBottom.AddChild(buttonBottomPanel);

			this.AddChild(mainContainerTopToBottom);
			this.AnchorAll();

			meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
			AddChild(viewControls3D);


			/* TODO: Why doesn't this pattern work but using new SelectedObjectPanel object does?
			selectedObjectPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Width = 215,
				Margin = new BorderDouble(0, 0, buttonRightPanel.Width + 5, 5),
				BackgroundColor = RGBA_Bytes.Red,
				HAnchor = HAnchor.ParentRight,
				VAnchor = VAnchor.ParentTop
			}; */

			selectedObjectPanel = new SelectedObjectPanel()
			{
				Width = 215,
				Margin = new BorderDouble(0, 0, Sidebar.Width + 5, 5),
			};

			AddChild(selectedObjectPanel);

			UiThread.RunOnIdle(AutoSpin);

			if (printItemWrapper == null && windowType == WindowMode.Embeded)
			{
				enterEditButtonsContainer.Visible = false;
			}

			if (windowType == WindowMode.Embeded)
			{
				PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(SetEditControlsBasedOnPrinterState, ref unregisterEvents);
				if (windowType == WindowMode.Embeded)
				{
					// make sure we lock the controls if we are printing or paused
					switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
					{
						case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						case PrinterConnectionAndCommunication.CommunicationStates.Paused:
							LockEditControls();
							break;
					}
				}
			}

			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

			meshViewerWidget.interactionVolumes.Add(new UpArrow3D(this));
			meshViewerWidget.interactionVolumes.Add(new SelectionShadow(this));
			meshViewerWidget.interactionVolumes.Add(new SnappingIndicators(this));

			PluginFinder<InteractionVolumePlugin> InteractionVolumePlugins = new PluginFinder<InteractionVolumePlugin>();
			foreach (InteractionVolumePlugin plugin in InteractionVolumePlugins.Plugins)
			{
				meshViewerWidget.interactionVolumes.Add(plugin.CreateInteractionVolume(this));
			}

			// make sure the colors are set correct
			ThemeChanged(this, null);

			saveButtons.VisibleChanged += (sender, e) =>
			{
				partHasBeenEdited = true;
			};

			meshViewerWidget.ResetView();

			if (DoBooleanTest)
			{
				BeforeDraw += CreateBooleanTestGeometry;
				AfterDraw += RemoveBooleanTestGeometry;
			}
			meshViewerWidget.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;
		}

		private IObject3D dragDropSource; 
		public IObject3D DragDropSource
		{
			get
			{
				return dragDropSource;
			}

			set
			{
				dragDropSource = value;

				// Suppress ui volumes when dragDropSource is not null
				meshViewerWidget.SuppressUiVolumes = (dragDropSource != null);
			}
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			return;
			if (Scene?.TraceData() != null)
			{
				Scene.TraceData().RenderBvhRecursive(Scene.Matrix);
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (activeButtonBeforeKeyOverride == null)
			{
				activeButtonBeforeKeyOverride = viewControls3D.ActiveButton;

				if (keyEvent.Alt)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
				}
				else if (keyEvent.Shift)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
				}
				else if (keyEvent.Control)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.Scale;
				}
			}

			switch (keyEvent.KeyCode)
			{
				case Keys.Z:
					if (keyEvent.Control)
					{
						undoBuffer.Undo();
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
					}
					break;

				case Keys.Y:
					if (keyEvent.Control)
					{
						undoBuffer.Redo();
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
					}
					break;

				case Keys.Delete:
				case Keys.Back:
					DeleteSelectedMesh();
					break;

				case Keys.Escape:
					if (CurrentSelectInfo.DownOnPart)
					{
						CurrentSelectInfo.DownOnPart = false;

						Scene.SelectedItem.Matrix = transformOnMouseDown;

						Invalidate();
					}
					break;
			}

			base.OnKeyDown(keyEvent);
		}

		public bool IsEditing { get; private set; }

		public bool DragingPart
		{
			get { return CurrentSelectInfo.DownOnPart; }
		}

		public void AddUndoOperation(IUndoRedoCommand operation)
		{
			undoBuffer.Add(operation);
		}

#region DoBooleanTest
        Object3D booleanObject;
		Vector3 offset = new Vector3();
		Vector3 direction = new Vector3(.11, .12, .13);
		Vector3 rotCurrent = new Vector3();
		Vector3 rotChange = new Vector3(.011, .012, .013);
		Vector3 scaleChange = new Vector3(.0011, .0012, .0013);
		Vector3 scaleCurrent = new Vector3(1, 1, 1);
		private void CreateBooleanTestGeometry(GuiWidget drawingWidget, DrawEventArgs e)
		{
			try
			{
				IObject3D booleanGroup = new Object3D { ItemType = Object3DTypes.Group };


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
			catch(Exception e2)
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
			Debug.WriteLine("t"+offsetB.ToString() + " r" + rotCurrent.ToString() + " s" + scaleCurrent.ToString() + " " + opp);
			Matrix4X4 transformB = Matrix4X4.CreateScale(scaleCurrent) * Matrix4X4.CreateRotation(rotCurrent) * Matrix4X4.CreateTranslation(offsetB);
            boxB.Transform(transformB);

			Mesh meshToAdd = meshOpperation(boxA, boxB);
			meshToAdd.CleanAndMergMesh();

			if(aabbOpperation != null)
			{
				AxisAlignedBoundingBox boundsA = boxA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsB = boxB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsAdd = meshToAdd.GetAxisAlignedBoundingBox();

				AxisAlignedBoundingBox boundsResult = aabbOpperation(boundsA, boundsB);

				if(!boundsAdd.Equals(boundsResult, .0001))
				{
					int a = 0;
				}
			}

			int nonManifoldEdges = meshToAdd.GetNonManifoldEdges().Count;
			if (nonManifoldEdges > 0)
			{
				// shoud be manifold
				int a = 0;
			}

			return meshToAdd;
		}

		private void RemoveBooleanTestGeometry(GuiWidget drawingWidget, DrawEventArgs e)
        {
			if (meshViewerWidget.Scene.Children.Contains(booleanObject))
			{
				meshViewerWidget.Scene.Children.Remove(booleanObject);
				UiThread.RunOnIdle(() => Invalidate(), 1.0 / 30.0);
			}
        }
		#endregion DoBooleanTest

		public enum AutoRotate { Enabled, Disabled };

		public enum OpenMode { Viewing, Editing }

		public enum WindowMode { Embeded, StandAlone };

		public bool DisplayAllValueData { get; set; }

		public WindowMode windowType { get; set; }
		private bool DoAddFileAfterCreatingEditData { get; set; }

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			base.OnClosed(e);
		}

		// TODO: Just realized we don't implement DragLeave, meaning that injected items can't be removed. Must implement
		public override void OnDragDrop(FileDropEventArgs fileDropArgs)
		{
			if (AllowDragDrop() && fileDropArgs.DroppedFiles.Count == 1)
			{
				// Item is already in the scene
				DragDropSource = null;
			}
			else if (AllowDragDrop())
			{
				// Items need to be added to the scene
				pendingPartsToLoad.Clear();
				foreach (string droppedFileName in fileDropArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(droppedFileName).ToLower();
					if (extension != "" && ApplicationSettings.OpenDesignFileParams.Contains(extension))
					{
						pendingPartsToLoad.Add(droppedFileName);
					}
				}

				if (pendingPartsToLoad.Count > 0)
				{
					bool enterEditModeBeforeAddingParts = enterEditButtonsContainer.Visible == true;
					if (enterEditModeBeforeAddingParts)
					{
						EnterEditAndCreateSelectionData();
					}
					else
					{
						LoadAndAddPartsToPlate(pendingPartsToLoad.ToArray());
						pendingPartsToLoad.Clear();
					}
				}
			}

			base.OnDragDrop(fileDropArgs);
		}

		public override void OnDragEnter(FileDropEventArgs fileDropArgs)
		{
			if (AllowDragDrop())
			{
				foreach (string file in fileDropArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(file).ToLower();
					if (extension != "" && ApplicationSettings.OpenDesignFileParams.Contains(extension))
					{
						fileDropArgs.AcceptDrop = true;
					}
				}

				if(fileDropArgs.AcceptDrop)
				{
					DragDropSource = new Object3D
					{
						ItemType = Object3DTypes.Model,
						Mesh = PlatonicSolids.CreateCube(10, 10, 10)
					};
				}
			}
			base.OnDragEnter(fileDropArgs);
		}

		public override async void OnDragOver(FileDropEventArgs fileDropArgs)
		{
			if (AllowDragDrop() && fileDropArgs.DroppedFiles.Count == 1) 
			{
				var screenSpaceMousePosition = this.TransformToScreenSpace(new Vector2(fileDropArgs.X, fileDropArgs.Y));

				// If the DragDropSource was added to the scene on this DragOver call, we start a task to replace 
				// the "loading" mesh with the actual file contents
				if (AltDragOver(screenSpaceMousePosition))
				{
					DragDropSource.MeshPath = fileDropArgs.DroppedFiles.First();

					// Run the rest of the OnDragOver pipeline since we're starting a new thread and won't finish for an unknown time
					base.OnDragOver(fileDropArgs);

					LoadDragSource();

					// Don't fall through to the base.OnDragOver because we preemptively invoked it above
					return;
				}
			}

			// AcceptDrop anytime a DropSource has been queued
			fileDropArgs.AcceptDrop = DragDropSource != null;

			base.OnDragOver(fileDropArgs);
		}

		private GuiWidget topMostParent;

		private PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

		public override void OnLoad(EventArgs args)
		{
			ClearBedAndLoadPrintItemWrapper(printItemWrapper);
			topMostParent = this.TopmostParent();

			base.OnLoad(args);
		}

		/// <summary>
		/// Provides a View3DWidget specific drag implementation
		/// </summary>
		/// <param name="screenSpaceMousePosition">The screen space mouse position.</param>
		/// <returns>A value indicating in the DragDropSource was added to the scene</returns>
		public bool AltDragOver(Vector2 screenSpaceMousePosition)
		{
			if (WidgetHasBeenClosed)
			{
				return false;
			}

			bool itemAddedToScene = false;

			var meshViewerPosition = this.meshViewerWidget.TransformToScreenSpace(meshViewerWidget.LocalBounds);

			if (meshViewerPosition.Contains(screenSpaceMousePosition) && DragDropSource != null)
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
					var sourceItemBounds = DragDropSource.GetAxisAlignedBoundingBox();
					var center = sourceItemBounds.Center;

					DragDropSource.Matrix *= Matrix4X4.CreateTranslation(-center.x, -center.y, -sourceItemBounds.minXYZ.z);
					DragDropSource.Matrix *= Matrix4X4.CreateTranslation(new Vector3(intersectInfo.hitPosition));

					CurrentSelectInfo.PlaneDownHitPos = intersectInfo.hitPosition;
					CurrentSelectInfo.LastMoveDelta = Vector3.Zero;

					// Add item to scene and select it
					Scene.ModifyChildren(children =>
					{
						children.Add(DragDropSource);
					});
					Scene.Select(DragDropSource);

					itemAddedToScene = true;
				}

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


		/// <summary>
		/// Loads the referenced DragDropSource object.
		/// </summary>
		/// <param name="dragSource">The drag source at the original time of invocation.</param>
		/// <returns></returns>
		public async Task LoadDragSource()
		{
			// The drag source at the original time of invocation.</param>
			IObject3D dragSource = DragDropSource;
			if (dragSource == null)
			{
				return;
			}

			IObject3D loadedItem = await Task.Run(() =>
			{
				return Object3D.Load(
					dragSource.MeshPath,
					meshViewerWidget.CachedMeshes,
					new DragDropLoadProgress(this, dragSource).UpdateLoadProgress);
			});

			if (loadedItem != null)
			{
				// TODO: Changing an item in the scene has a risk of collection modified during enumeration errors. This approach works as 
				// a proof of concept but needs to take the more difficult route of managing state and swapping the dragging instance with 
				// the new loaded item data
				Vector3 meshGroupCenter = loadedItem.GetAxisAlignedBoundingBox().Center;
				dragSource.Mesh = loadedItem.Mesh;
				dragSource.Children.AddRange(loadedItem.Children);
				dragSource.Matrix *= Matrix4X4.CreateTranslation(-meshGroupCenter.x, -meshGroupCenter.y, -dragSource.GetAxisAlignedBoundingBox().minXYZ.z);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (Scene.HasSelection)
			{
				foreach (InteractionVolume volume in meshViewerWidget.interactionVolumes)
				{
					volume.SetPosition();
				}
			}

			hasDrawn = true;
			base.OnDraw(graphics2D);
		}

		private ViewControls3DButtons? activeButtonBeforeMouseOverride = null;
		private ViewControls3DButtons? activeButtonBeforeKeyOverride = null;

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			if (activeButtonBeforeKeyOverride != null)
			{
				viewControls3D.ActiveButton = (ViewControls3DButtons)activeButtonBeforeKeyOverride;
				activeButtonBeforeKeyOverride = null;
			}

			base.OnKeyUp(keyEvent);
		}

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

			if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
			{
				if (mouseEvent.Button == MouseButtons.Left
					&&
					ModifierKeys == Keys.Shift ||
					(
					meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
					&& ModifierKeys != Keys.Control
					&& ModifierKeys != Keys.Alt))
				{
					if (!meshViewerWidget.MouseDownOnInteractionVolume)
					{
						meshViewerWidget.SuppressUiVolumes = true;

						IntersectInfo info = new IntersectInfo();

						IObject3D hitObject = FindHitObject3D(mouseEvent.Position, ref info);
						if (hitObject != null)
						{
							CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.z, null);

							if (hitObject != Scene.SelectedItem)
							{
								if (Scene.SelectedItem == null)
								{
									// No selection exists
									Scene.Select(hitObject);
								}
								else if (ModifierKeys == Keys.Shift && !Scene.SelectedItem.Children.Contains(hitObject))
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

							AxisAlignedBoundingBox selectedBounds = meshViewerWidget.Scene.SelectedItem.GetAxisAlignedBoundingBox();

							if (info.hitPosition.x < selectedBounds.Center.x)
							{
								if (info.hitPosition.y < selectedBounds.Center.y)
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
								if (info.hitPosition.y < selectedBounds.Center.y)
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RB;
								}
								else
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RT;
								}
							}
						}
						else
						{
							if(!Scene.HasSelection)
							{
								return;
							}

							if(Scene.SelectedItem.ItemType == Object3DTypes.SelectionGroup)
							{
								Scene.ModifyChildren(ClearSelectionApplyChanges);
							}
							else
							{
								Scene.ClearSelection();
							}
						}

						SelectedTransformChanged?.Invoke(this, null);
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

			Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayForLocalBounds(localPosition);

			return CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
		}

		public void DragSelectedObject(Vector2 localMousePostion)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, localMousePostion);
			Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
			if (info != null)
			{
				// move the mesh back to the start position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					Scene.SelectedItem.Matrix *= totalTransform;
				}

				Vector3 delta = info.hitPosition - CurrentSelectInfo.PlaneDownHitPos;

				double snapGridDistance = meshViewerWidget.SnapGridDistance;
				if (snapGridDistance > 0)
				{
					// snap this position to the grid
					AxisAlignedBoundingBox selectedBounds = meshViewerWidget.Scene.SelectedItem.GetAxisAlignedBoundingBox();

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
			if (CurrentSelectInfo.DownOnPart && meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				DragSelectedObject(new Vector2(mouseEvent.X, mouseEvent.Y));
			}

			base.OnMouseMove(mouseEvent);
		}

		public void AddUndoForSelectedMeshGroupTransform(Matrix4X4 undoTransform)
		{
			if (Scene.HasSelection && undoTransform != Scene.SelectedItem?.Matrix)
			{
				undoBuffer.Add(new TransformUndoCommand(this, Scene.SelectedItem, undoTransform, Scene.SelectedItem.Matrix));
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
				&& CurrentSelectInfo.DownOnPart
				&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
			{
				if (Scene.SelectedItem.Matrix != transformOnMouseDown)
				{
					AddUndoForSelectedMeshGroupTransform(transformOnMouseDown);
					PartHasBeenChanged();
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
		}

		public void PartHasBeenChanged()
		{
			saveButtons.Visible = true;
			SelectedTransformChanged?.Invoke(this, null);
			Invalidate();
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			processingProgressControl.FillColor = ActiveTheme.Instance.PrimaryAccentColor;

			MeshViewerWidget.SetMaterialColor(1, ActiveTheme.Instance.PrimaryAccentColor);
		}

		internal void AddMaterialControls(FlowLayoutWidget buttonPanel)
		{
			extruderButtons.Clear();
			for (int extruderIndex = 0; extruderIndex < ActiveSliceSettings.Instance.ExtruderCount; extruderIndex++)
			{
				FlowLayoutWidget colorSelectionContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
				colorSelectionContainer.HAnchor = HAnchor.ParentLeftRight;
				colorSelectionContainer.Padding = new BorderDouble(5);

				string colorLabelText = "Material {0}".Localize().FormatWith(extruderIndex + 1);
				RadioButton extruderSelection = new RadioButton(colorLabelText, textColor: ActiveTheme.Instance.PrimaryTextColor);
				extruderButtons.Add(extruderSelection);
				extruderSelection.SiblingRadioButtonList = extruderButtons;
				colorSelectionContainer.AddChild(extruderSelection);
				colorSelectionContainer.AddChild(new HorizontalSpacer());
				int extruderIndexLocal = extruderIndex;
				extruderSelection.Click += (sender, e) =>
				{
					if (Scene.HasSelection)
					{
						// TODO: In the new model, we probably need to iterate this object and all its children, setting 
						// some state along the way or modify the tree processing to pass the parent value down the chain
						MeshMaterialData material = MeshMaterialData.Get(Scene.SelectedItem.Mesh);
						if (material.MaterialIndex != extruderIndexLocal + 1)
						{
							material.MaterialIndex = extruderIndexLocal + 1;
							PartHasBeenChanged();
						}
					}
				};

				this.SelectionChanged += (sender, e) =>
				{
					if (Scene.HasSelection)
					{
						// TODO: Likely needs to be reviewed as described above and in the context of the scene graph
						MeshMaterialData material = MeshMaterialData.Get(Scene.SelectedItem.Mesh);
						for (int i = 0; i < extruderButtons.Count; i++)
						{
							if (material.MaterialIndex - 1 == i)
							{
								((RadioButton)extruderButtons[i]).Checked = true;
							}
						}
					}
				};

				buttonPanel.AddChild(colorSelectionContainer);
			}
		}

		private void AddRotateControls(FlowLayoutWidget buttonPanel)
		{
			List<GuiWidget> rotateControls = new List<GuiWidget>();

			textImageButtonFactory.FixedWidth = EditButtonHeight;

			FlowLayoutWidget degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			degreesContainer.HAnchor = HAnchor.ParentLeftRight;
			degreesContainer.Padding = new BorderDouble(5);

			string degreesLabelText = "Degrees".Localize();
			string degreesLabelTextFull = "{0}:".FormatWith(degreesLabelText);
			TextWidget degreesLabel = new TextWidget(degreesLabelText, textColor: ActiveTheme.Instance.PrimaryTextColor);
			degreesContainer.AddChild(degreesLabel);
			degreesContainer.AddChild(new HorizontalSpacer());

			MHNumberEdit degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, allowDecimals: true, increment: 5, minValue: -360, maxValue: 360);
			degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
			degreesContainer.AddChild(degreesControl);
			rotateControls.Add(degreesControl);

			buttonPanel.AddChild(degreesContainer);

			FlowLayoutWidget rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rotateButtonContainer.HAnchor = HAnchor.ParentLeftRight;

			Button rotateXButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
			TextWidget centeredX = new TextWidget("X", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredX.Margin = new BorderDouble(3, 0, 0, 0); centeredX.AnchorCenter(); rotateXButton.AddChild(centeredX);
			rotateButtonContainer.AddChild(rotateXButton);
			rotateControls.Add(rotateXButton);
			rotateXButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (Scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationX(radians);
					Matrix4X4 undoTransform = Scene.SelectedItem.Matrix;
                    Scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(Scene.SelectedItem, rotation);
					undoBuffer.Add(new TransformUndoCommand(this, Scene.SelectedItem, undoTransform, Scene.SelectedItem.Matrix));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			Button rotateYButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
			TextWidget centeredY = new TextWidget("Y", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredY.Margin = new BorderDouble(3, 0, 0, 0); centeredY.AnchorCenter(); rotateYButton.AddChild(centeredY);
			rotateButtonContainer.AddChild(rotateYButton);
			rotateControls.Add(rotateYButton);
			rotateYButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (Scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationY(radians);
					Matrix4X4 undoTransform = Scene.SelectedItem.Matrix;
					Scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(Scene.SelectedItem, rotation);
					undoBuffer.Add(new TransformUndoCommand(this, Scene.SelectedItem, undoTransform, Scene.SelectedItem.Matrix));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			Button rotateZButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
			TextWidget centeredZ = new TextWidget("Z", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredZ.Margin = new BorderDouble(3, 0, 0, 0); centeredZ.AnchorCenter(); rotateZButton.AddChild(centeredZ);
			rotateButtonContainer.AddChild(rotateZButton);
			rotateControls.Add(rotateZButton);
			rotateZButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (Scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationZ(radians);
					Matrix4X4 undoTransform = Scene.SelectedItem.Matrix;
					Scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(Scene.SelectedItem, rotation);
					undoBuffer.Add(new TransformUndoCommand(this, Scene.SelectedItem, undoTransform, Scene.SelectedItem.Matrix));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			buttonPanel.AddChild(rotateButtonContainer);

			Button layFlatButton = WhiteButtonFactory.Generate("Align to Bed".Localize(), centerText: true);
			layFlatButton.Cursor = Cursors.Hand;
			buttonPanel.AddChild(layFlatButton);

			layFlatButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (Scene.HasSelection)
				{
					Matrix4X4 undoTransform = Scene.SelectedItem.Matrix;
					MakeLowestFaceFlat(Scene.SelectedItem);
					undoBuffer.Add(new TransformUndoCommand(this, Scene.SelectedItem, undoTransform, Scene.SelectedItem.Matrix));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			buttonPanel.AddChild(GenerateHorizontalRule());
			textImageButtonFactory.FixedWidth = 0;
		}

		private void AddSaveAndSaveAs(FlowLayoutWidget flowToAddTo)
		{
			TupleList<string, Func<bool>> buttonList = new TupleList<string, Func<bool>>();
			buttonList.Add("Save".Localize(), () =>
			{
				MergeAndSavePartsToCurrentMeshFile();
				return true;
			});

			buttonList.Add("Save As".Localize(), () =>
			{
				UiThread.RunOnIdle(OpenSaveAsWindow);
				return true;
			});

			SplitButtonFactory splitButtonFactory = new SplitButtonFactory();
			splitButtonFactory.FixedHeight = 40 * TextWidget.GlobalPointSizeScaleRatio;
			saveButtons = splitButtonFactory.Generate(buttonList, Direction.Up, imageName: "icon_save_32x32.png");
			saveButtons.Visible = false;

			saveButtons.Margin = new BorderDouble();
			saveButtons.VAnchor |= VAnchor.ParentCenter;

			flowToAddTo.AddChild(saveButtons);
		}

		private bool AllowDragDrop()
		{
			if ((!enterEditButtonsContainer.Visible
				&& !doEdittingButtonsContainer.Visible)
				|| printItemWrapper == null || printItemWrapper.PrintItem.ReadOnly)
			{
				return false;
			}

			return true;
		}

		private void AutoSpin()
		{
			if (!WidgetHasBeenClosed && autoRotating)
			{
				// add it back in to keep it running.
				UiThread.RunOnIdle(AutoSpin, .04);

				if ((!timeSinceLastSpin.IsRunning || timeSinceLastSpin.ElapsedMilliseconds > 50)
					&& hasDrawn)
				{
					hasDrawn = false;
					timeSinceLastSpin.Restart();

					Quaternion currentRotation = meshViewerWidget.TrackballTumbleWidget.TrackBallController.CurrentRotation.GetRotation();
					Quaternion invertedRotation = Quaternion.Invert(currentRotation);

					Quaternion rotateAboutZ = Quaternion.FromEulerAngles(new Vector3(0, 0, .01));
					rotateAboutZ = invertedRotation * rotateAboutZ * currentRotation;
					meshViewerWidget.TrackballTumbleWidget.TrackBallController.Rotate(rotateAboutZ);
					Invalidate();
				}
			}
		}

		private void ReportProgressChanged(double progress0To1, string processingState)
		{
			bool continueProcessing;
			ReportProgressChanged(progress0To1, processingState, out continueProcessing);
		}

		private void ReportProgressChanged(double progress0To1, string processingState, out bool continueProcessing)
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
			continueProcessing = true;
		}

		private async Task ClearBedAndLoadPrintItemWrapper(PrintItemWrapper printItemWrapper)
		{
			SwitchStateToNotEditing();

			Scene.ModifyChildren(children => children.Clear());

			if (printItemWrapper != null)
			{
				// remove it first to make sure we don't double add it
				PrintItemWrapper.FileHasChanged.UnregisterEvent(ReloadMeshIfChangeExternaly, ref unregisterEvents);
				PrintItemWrapper.FileHasChanged.RegisterEvent(ReloadMeshIfChangeExternaly, ref unregisterEvents); ;

				// don't load the mesh until we get all the rest of the interface built
				meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);

				Vector2 bedCenter = new Vector2();
				MeshViewerWidget.CenterPartAfterLoad doCentering = MeshViewerWidget.CenterPartAfterLoad.DONT;

				if (ActiveSliceSettings.Instance != null
				&& ActiveSliceSettings.Instance.CenterOnBed())
				{
					doCentering = MeshViewerWidget.CenterPartAfterLoad.DO;
					bedCenter = ActiveSliceSettings.Instance.BedCenter;
				}

				await meshViewerWidget.LoadMesh(printItemWrapper.FileLocation, doCentering, bedCenter);

				PartHasBeenChanged();
				Invalidate();
			}

			partHasBeenEdited = false;
		}

		private void CreateOptionsContent()
		{
			AddRotateControls(Sidebar.rotateOptionContainer);
		}

		internal void CreateRenderTypeRadioButtons(FlowLayoutWidget viewOptionContainer)
		{
			string renderTypeString = UserSettings.Instance.get("defaultRenderSetting");
			if (renderTypeString == null)
			{
				if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
				{
					renderTypeString = "Shaded";
				}
				else
				{
					renderTypeString = "Outlines";
				}
				UserSettings.Instance.set("defaultRenderSetting", renderTypeString);
			}
			RenderOpenGl.RenderTypes renderType;
			bool canParse = Enum.TryParse<RenderOpenGl.RenderTypes>(renderTypeString, out renderType);
			if (canParse)
			{
				meshViewerWidget.RenderType = renderType;
			}

			{
				RadioButton renderTypeShaded = new RadioButton("Shaded".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				renderTypeShaded.Checked = (meshViewerWidget.RenderType == RenderTypes.Shaded);

				renderTypeShaded.CheckedStateChanged += (sender, e) =>
				{
					meshViewerWidget.RenderType = RenderTypes.Shaded;
					UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypeShaded);
			}

			{
				RadioButton renderTypeOutlines = new RadioButton("Outlines".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				renderTypeOutlines.Checked = (meshViewerWidget.RenderType == RenderTypes.Outlines);
				renderTypeOutlines.CheckedStateChanged += (sender, e) =>
				{
					meshViewerWidget.RenderType = RenderTypes.Outlines;
					UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypeOutlines);
			}

			{
				RadioButton renderTypePolygons = new RadioButton("Polygons".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				renderTypePolygons.Checked = (meshViewerWidget.RenderType == RenderTypes.Polygons);
				renderTypePolygons.CheckedStateChanged += (sender, e) =>
				{
					meshViewerWidget.RenderType = RenderTypes.Polygons;
					UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypePolygons);
			}
		}

		public List<IObject3DEditor> objectEditors = new List<IObject3DEditor>();

		public Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if(!Scene.HasSelection)
			{
				selectedObjectPanel.RemoveAllChildren();
				return;
			}

			var selectedItem = Scene.SelectedItem;

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItem.GetType(), out mappedEditors);

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom, vAnchor: VAnchor.FitToChildren);

			if (mappedEditors != null)
			{
				AnchoredDropDownList dropDownList = new AnchoredDropDownList("", maxHeight: 300)
				{
					Margin = new BorderDouble(0, 3)
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
				IObject3DEditor activeEditor = (from editor in mappedEditors
								   let type = editor.GetType()
								   where type.Name == selectedItem.ActiveEditor
								   select editor).FirstOrDefault();

				if(activeEditor == null)
				{
					activeEditor = mappedEditors.First();
				}

				int selectedIndex = 0;
				for(int i = 0; i < dropDownList.MenuItems.Count; i++)
				{
					if(dropDownList.MenuItems[i].Text == activeEditor.Name)
					{
						selectedIndex = i;
						break;
					}
				}

				dropDownList.SelectedIndex = selectedIndex;

				ShowObjectEditor(activeEditor);
			}
		}

		private void ShowObjectEditor(IObject3DEditor editor)
		{

			editorPanel.CloseAllChildren();
			var newEditor = editor.Create(Scene.SelectedItem, this);
			editorPanel.AddChild(newEditor);
		}

		private void DeleteSelectedMesh()
		{
			if (Scene.HasSelection && Scene.Children.Count > 1)
			{
				// Create and perform the delete operation 
				var deleteOperation = new DeleteCommand(this, Scene.SelectedItem);
				deleteOperation.Do();

				// Store the operation for undo/redo
				undoBuffer.Add(deleteOperation);
			}
		}

		private void DrawStuffForSelectedPart(Graphics2D graphics2D)
		{
			if (Scene.HasSelection)
			{
				AxisAlignedBoundingBox selectedBounds = Scene.SelectedItem.GetAxisAlignedBoundingBox(Scene.SelectedItem.Matrix);
				Vector3 boundsCenter = selectedBounds.Center;
				Vector3 centerTop = new Vector3(boundsCenter.x, boundsCenter.y, selectedBounds.maxXYZ.z);

				Vector2 centerTopScreenPosition = meshViewerWidget.TrackballTumbleWidget.GetScreenPosition(centerTop);
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

		private void ExitEditingAndSaveIfRequired(bool response)
		{
			if (response == true)
			{
				MergeAndSavePartsToCurrentMeshFile(SwitchStateToNotEditing);
			}
			else
			{
				SwitchStateToNotEditing();
				// and reload the part
				ClearBedAndLoadPrintItemWrapper(printItemWrapper);
			}
		}

		private async void LoadAndAddPartsToPlate(string[] filesToLoad)
		{
			if (Scene.HasChildren && filesToLoad != null && filesToLoad.Length > 0)
			{
				string loadingPartLabel = "Loading Parts".Localize();
				string loadingPartLabelFull = "{0}:".FormatWith(loadingPartLabel);
				processingProgressControl.ProcessType = loadingPartLabelFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => loadAndAddPartsToPlate(filesToLoad));

				if (WidgetHasBeenClosed)
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

			List<string> filesToLoad = new List<string>();
			if (filesToLoadIncludingZips != null && filesToLoadIncludingZips.Length > 0)
			{
				for (int i = 0; i < filesToLoadIncludingZips.Length; i++)
				{
					string loadedFileName = filesToLoadIncludingZips[i];
					string extension = Path.GetExtension(loadedFileName).ToUpper();
					if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
					{
						filesToLoad.Add(loadedFileName);
					}
					else if (extension == ".ZIP")
					{
						ProjectFileHandler project = new ProjectFileHandler(null);
						List<PrintItem> partFiles = project.ImportFromProjectArchive(loadedFileName);
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
				for (int i = 0; i < filesToLoad.Count; i++)
				{
					string loadedFileName = filesToLoad[i];
					List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(Path.GetFullPath(loadedFileName), (double progress0To1, string processingState, out bool continueProcessing) =>
					{
						continueProcessing = !this.WidgetHasBeenClosed;
						double ratioAvailable = (ratioPerFile * .5);
						double currentRatio = currentRatioDone + progress0To1 * ratioAvailable;
						ReportProgressChanged(currentRatio, progressMessage, out continueProcessing);
					});

					if (WidgetHasBeenClosed)
					{
						return;
					}
					if (loadedMeshGroups != null)
					{
						double ratioPerSubMesh = ratioPerFile / loadedMeshGroups.Count;
						double subMeshRatioDone = 0;

						var tempScene = new Object3D();

						// During startup we load and reload the main control multiple times. When this occurs, sometimes the reportProgress0to100 will set
						// continueProcessing to false and MeshFileIo.LoadAsync will return null. In those cases, we need to exit rather than process the loaded MeshGroup
						if (loadedMeshGroups == null)
						{
							return;
						}

						var loadedGroup = new Object3D {
							MeshPath = loadedFileName
						};

						// TODO: Shouldn't Object3D.Load already cover this task?
						foreach (var meshGroup in loadedMeshGroups)
						{
							foreach(var mesh in meshGroup.Meshes)
							{
								loadedGroup.Children.Add(new Object3D() { Mesh = mesh });
							}
						}

						tempScene.Children.Add(loadedGroup);

						PlatingHelper.MoveToOpenPosition(tempScene, this.Scene);

						this.InsertNewItem(tempScene);
					}

					currentRatioDone += ratioPerFile;
				}
			}
		}

		public void LockEditControls()
		{
			viewIsInEditModePreLock = doEdittingButtonsContainer.Visible;
			enterEditButtonsContainer.Visible = false;
			doEdittingButtonsContainer.Visible = false;
			buttonRightPanelDisabledCover.Visible = true;
			if (viewControls3D.PartSelectVisible == true)
			{
				viewControls3D.PartSelectVisible = false;
				if (viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect)
				{
					wasInSelectMode = true;
					viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
				}
			}
		}

		private void MakeLowestFaceFlat(IObject3D objectToLayFlatGroup)
		{
			Matrix4X4 objectToWold = objectToLayFlatGroup.Matrix;
			IObject3D objectToLayFlat = objectToLayFlatGroup.Children[0];

			Vertex lowestVertex = objectToLayFlat.Mesh.Vertices[0];

			Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, objectToWold);

			IObject3D itemToLayFlat = null;

			// Process each child, checking for the lowest vertex
			foreach (IObject3D itemToCheck in objectToLayFlat.Children.Where(child => child.Mesh != null))
			{
				// find the lowest point on the model
				for (int testIndex = 1; testIndex < itemToCheck.Mesh.Vertices.Count; testIndex++)
				{
					Vertex vertex = itemToCheck.Mesh.Vertices[testIndex];
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
				foreach (Vertex faceVertex in face.Vertices())
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
			foreach (Vertex vertex in faceToLayFlat.Vertices())
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

		private GuiWidget selectedObjectPanel;
		private FlowLayoutWidget editorPanel;

		public void ProcessTree(IObject3D object3D)
		{

		}

		public void SaveScene(string filePath)
		{
			// TODO: Drop children of items containing populated MeshPath attributes
			var itemsToSave = from object3D in Scene.Descendants()
							  where object3D.MeshPath == null &&
									object3D.Mesh != null
							  select object3D;

			foreach(var item in itemsToSave)
			{
				SimpleSave(item);
			}

			File.WriteAllText(filePath, JsonConvert.SerializeObject(Scene, Formatting.Indented));
		}

		private void SimpleSave(IObject3D object3D)
		{
			string[] metaData = { "Created By", "MatterControl", "BedPosition", "Absolute" };

			MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);

			try
			{
				// TODO: jlewin - temp path works for testing, still need to build library path and fix cache purge to be scene aware 
				//
				// get a new location to save to
				string tempFileNameToSaveTo = ApplicationDataStorage.Instance.GetTempFileName("amf");

				// save to the new temp location
				bool savedSuccessfully = MeshFileIo.Save(
					new List<MeshGroup> { new MeshGroup(object3D.Mesh) }, 
					tempFileNameToSaveTo, 
					outputInfo, 
					ReportProgressChanged);

				if (savedSuccessfully && File.Exists(tempFileNameToSaveTo))
				{
					object3D.MeshPath = tempFileNameToSaveTo;
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		private void MergeAndSavePartsDoWork(SaveAsWindow.SaveAsReturnInfo returnInfo)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			string mcxPath = Path.ChangeExtension(printItemWrapper.FileLocation, ".mcx");

			try
			{
				// If null we are replacing a file from the current print item wrapper
				if (returnInfo == null)
				{
					// Only save as .mcx
					if (Path.GetExtension(printItemWrapper.FileLocation) != ".mcx")
					{
						printItemWrapper.FileLocation = mcxPath;
					}
				}
				else // we are saving a new file and it will not exist until we are done
				{
					PrintItem printItem = new PrintItem();
					printItem.Name = returnInfo.newName;
					printItem.FileLocation = Path.GetFullPath(mcxPath);

					printItemWrapper = new PrintItemWrapper(printItem, returnInfo.destinationLibraryProvider.GetProviderLocator());
				}

				SaveScene(mcxPath);

				printItemWrapper.PrintItem.Commit();

				// Wait for a second to report the file changed to give the OS a chance to finish closing it.
				UiThread.RunOnIdle(printItemWrapper.ReportFileChange, 3);

				if (returnInfo?.destinationLibraryProvider != null)
				{
					// save this part to correct library provider
					LibraryProvider libraryToSaveTo = returnInfo.destinationLibraryProvider;
					if (libraryToSaveTo != null)
					{
						libraryToSaveTo.AddItem(printItemWrapper);
						libraryToSaveTo.Dispose();
					}
				}
				else // we have already saved it and the library should pick it up
				{
				}

				saveSucceded = true;
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}

			return;

			///* Classic save implementation temporarily disabled */

			//if (returnInfo != null)
			//{
			//	PrintItem printItem = new PrintItem();
			//	printItem.Name = returnInfo.newName;
			//	printItem.FileLocation = Path.GetFullPath(returnInfo.fileNameAndPath);
			//	printItemWrapper = new PrintItemWrapper(printItem, returnInfo.destinationLibraryProvider.GetProviderLocator());
			//}

			//// we sent the data to the async lists but we will not pull it back out (only use it as a temp holder).
			////PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

			//Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			//try
			//{
			//	string[] metaData = { "Created By", "MatterControl", "BedPosition", "Absolute" };

			//	MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);

			//	// If null we are replacing a file from the current print item wrapper
			//	if (returnInfo == null)
			//	{
			//		var fileInfo = new FileInfo(printItemWrapper.FileLocation);

			//		string altFilePath = Path.ChangeExtension(fileInfo.FullName, ".mcx");
			//		SaveScene(altFilePath);

			//		bool requiresTypeChange = !fileInfo.Extension.Equals(".amf", StringComparison.OrdinalIgnoreCase);
			//		if (requiresTypeChange && !printItemWrapper.UseIncrementedNameDuringTypeChange)
			//		{
			//			// Not using incremented file name, simply change to AMF
			//			printItemWrapper.FileLocation = Path.ChangeExtension(printItemWrapper.FileLocation, ".amf");
			//		}
			//		else if (requiresTypeChange)
			//		{
			//			string newFileName;
			//			string incrementedFileName;

			//			// Switching from .stl, .obj or similar to AMF. Save the file and update the
			//			// the filename with an incremented (n) value to reflect the extension change in the UI 
			//			string fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);

			//			// Drop bracketed number sections from our source filename to ensure we don't generate something like "file (1) (1).amf"
			//			if (fileName.Contains("("))
			//			{
			//				fileName = fileNameNumberMatch.Replace(fileName, "").Trim();
			//			}

			//			// Generate and search for an incremented file name until no match is found at the target directory
			//			int foundCount = 0;
			//			do
			//			{
			//				newFileName = string.Format("{0} ({1})", fileName, ++foundCount);
			//				incrementedFileName = Path.Combine(fileInfo.DirectoryName, newFileName + ".amf");

			//				// Continue incrementing while any matching file exists
			//			} while (Directory.GetFiles(fileInfo.DirectoryName, newFileName + ".*").Any());

			//			// Change the FileLocation to the new AMF file
			//			printItemWrapper.FileLocation = incrementedFileName;
			//		}

			//		try
			//		{
			//			// get a new location to save to
			//			string tempFileNameToSaveTo = ApplicationDataStorage.Instance.GetTempFileName("amf");

			//			// save to the new temp location
			//			bool savedSuccessfully = false; // = MeshFileIo.Save(MeshGroups, tempFileNameToSaveTo, outputInfo, ReportProgressChanged);

			//			// Swap out the files if the save operation completed successfully 
			//			if (savedSuccessfully && File.Exists(tempFileNameToSaveTo))
			//			{
			//				// Ensure the target path is clear
			//				if(File.Exists(printItemWrapper.FileLocation))
			//				{
			//					File.Delete(printItemWrapper.FileLocation);
			//				}

			//				// Move the newly saved file back into place
			//				File.Move(tempFileNameToSaveTo, printItemWrapper.FileLocation);

			//				// Once the file is swapped back into place, update the PrintItem to account for extension change
			//				printItemWrapper.PrintItem.Commit();
			//			}
			//		}
			//		catch(Exception ex)
			//		{
			//			Trace.WriteLine("Error saving file: ", ex.Message);
			//		}
			//	}
			//	else // we are saving a new file and it will not exist until we are done
			//	{
			//		//MeshFileIo.Save(MeshGroups, printItemWrapper.FileLocation, outputInfo, ReportProgressChanged);
			//	}

			//	// Wait for a second to report the file changed to give the OS a chance to finish closing it.
			//	UiThread.RunOnIdle(printItemWrapper.ReportFileChange, 3);

			//	if (returnInfo != null
			//		&& returnInfo.destinationLibraryProvider != null)
			//	{
			//		// save this part to correct library provider
			//		LibraryProvider libraryToSaveTo = returnInfo.destinationLibraryProvider;
			//		if (libraryToSaveTo != null)
			//		{
			//			libraryToSaveTo.AddItem(printItemWrapper);
			//			libraryToSaveTo.Dispose();
			//		}
			//	}
			//	else // we have already saved it and the library should pick it up
			//	{
			//	}

			//	saveSucceded = true;
			//}
			//catch (System.UnauthorizedAccessException e2)
			//{
			//	Debug.Print(e2.Message);
			//	GuiWidget.BreakInDebugger();
			//	saveSucceded = false;
			//	UiThread.RunOnIdle(() =>
			//	{
			//		//Do something special when unauthorized?
			//		StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
			//	});
			//}
			//catch (Exception e)
			//{
			//	Debug.Print(e.Message);
			//	GuiWidget.BreakInDebugger();
			//	saveSucceded = false;
			//	UiThread.RunOnIdle(() =>
			//	{
			//		StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
			//	});
			//}
		}

		private void MergeAndSavePartsDoCompleted()
		{
			if (WidgetHasBeenClosed)
			{
				return;
			}
			UnlockEditControls();

			// NOTE: we do not pull the data back out of the async lists.
			if (saveSucceded)
			{
				saveButtons.Visible = false;
			}

			if (afterSaveCallback != null)
			{
				afterSaveCallback();
			}
		}

		private async void MergeAndSavePartsToCurrentMeshFile(Action eventToCallAfterSave = null)
		{
			editorThatRequestedSave = true;
			afterSaveCallback = eventToCallAfterSave;

			if (Scene.HasChildren)
			{
				string progressSavingPartsLabel = "Saving".Localize();
				string progressSavingPartsLabelFull = "{0}:".FormatWith(progressSavingPartsLabel);
				processingProgressControl.ProcessType = progressSavingPartsLabelFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => MergeAndSavePartsDoWork(null));
				MergeAndSavePartsDoCompleted();
			}
		}

		private async void MergeAndSavePartsToNewMeshFile(SaveAsWindow.SaveAsReturnInfo returnInfo)
		{
			editorThatRequestedSave = true;
			if (Scene.HasChildren)
			{
				string progressSavingPartsLabel = "Saving".Localize();
				string progressSavingPartsLabelFull = "{0}:".FormatWith(progressSavingPartsLabel);
				processingProgressControl.ProcessType = progressSavingPartsLabelFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => MergeAndSavePartsDoWork(returnInfo));
				MergeAndSavePartsDoCompleted();
			}
		}

		private void meshViewerWidget_LoadDone(object sender, EventArgs e)
		{
			if (windowType == WindowMode.Embeded)
			{
				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
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

			SelectionChanged?.Invoke(this, null);

			if (openMode == OpenMode.Editing)
			{
				UiThread.RunOnIdle(EnterEditAndCreateSelectionData);
			}

			meshViewerWidget.ResetView();
		}

		private bool PartsAreInPrintVolume()
		{
			if (ActiveSliceSettings.Instance != null
				&& !ActiveSliceSettings.Instance.CenterOnBed()
				&& ActivePrinterProfile.Instance.ActivePrinter != null)
			{
				AxisAlignedBoundingBox allBounds = AxisAlignedBoundingBox.Empty;
				foreach(var aabb in Scene.Children.Select(item => item.GetAxisAlignedBoundingBox()))
				{
					allBounds += aabb;
				}

				bool onBed = allBounds.minXYZ.z > -.001 && allBounds.minXYZ.z < .001; // really close to the bed
				RectangleDouble bedRect = new RectangleDouble(0, 0, ActiveSliceSettings.Instance.BedSize.x, ActiveSliceSettings.Instance.BedSize.y);
				bedRect.Offset(ActiveSliceSettings.Instance.BedCenter - ActiveSliceSettings.Instance.BedSize / 2);

				bool inBounds = bedRect.Contains(new Vector2(allBounds.minXYZ)) && bedRect.Contains(new Vector2(allBounds.maxXYZ));

				return onBed && inBounds;
			}

			return true;
		}

		private void OpenExportWindow()
		{
			if (exportingWindow == null)
			{
				exportingWindow = new ExportPrintItemWindow(this.printItemWrapper);
				exportingWindow.Closed += (sender, e) =>
				{
					exportingWindow = null;
				};
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				exportingWindow.BringToFront();
			}
		}

		private void OpenSaveAsWindow()
		{
			if (saveAsWindow == null)
			{
				List<ProviderLocatorNode> providerLocator = null;
				if (printItemWrapper.SourceLibraryProviderLocator != null)
				{
					providerLocator = printItemWrapper.SourceLibraryProviderLocator;
				}
				saveAsWindow = new SaveAsWindow(MergeAndSavePartsToNewMeshFile, providerLocator, true, true);
				saveAsWindow.Closed += new EventHandler(SaveAsWindow_Closed);
			}
			else
			{
				saveAsWindow.BringToFront();
			}
		}

		private void ReloadMeshIfChangeExternaly(Object sender, EventArgs e)
		{
			PrintItemWrapper senderItem = sender as PrintItemWrapper;
			if (senderItem != null
				&& senderItem.FileLocation == printItemWrapper.FileLocation)
			{
				if (!editorThatRequestedSave)
				{
					ClearBedAndLoadPrintItemWrapper(printItemWrapper);
				}

				editorThatRequestedSave = false;
			}
		}

		private bool rotateQueueMenu_Click()
		{
			return true;
		}

		private void SaveAsWindow_Closed(object sender, EventArgs e)
		{
			this.saveAsWindow = null;
		}

		private bool scaleQueueMenu_Click()
		{
			return true;
		}

		private void SetEditControlsBasedOnPrinterState(object sender, EventArgs e)
		{
			if (windowType == WindowMode.Embeded)
			{
				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
						LockEditControls();
						break;

					default:
						UnlockEditControls();
						break;
				}
			}
		}

		private void SwitchStateToNotEditing()
		{
			IsEditing = false;

			if (!enterEditButtonsContainer.Visible)
			{
				enterEditButtonsContainer.Visible = true;
				processingProgressControl.Visible = false;
				Sidebar.Visible = false;
				doEdittingButtonsContainer.Visible = false;
				viewControls3D.PartSelectVisible = false;
				if (viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
				}

				Scene.ModifyChildren(ClearSelectionApplyChanges);
			}
		}

		public void UnlockEditControls()
		{
			buttonRightPanelDisabledCover.Visible = false;
			processingProgressControl.Visible = false;

			if (viewIsInEditModePreLock)
			{
				if (!enterEditButtonsContainer.Visible)
				{
					viewControls3D.PartSelectVisible = true;
					doEdittingButtonsContainer.Visible = true;
				}
			}
			else
			{
				enterEditButtonsContainer.Visible = true;
			}

			if (wasInSelectMode)
			{
				viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;
				wasInSelectMode = false;
			}

			SelectedTransformChanged?.Invoke(this, null);
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