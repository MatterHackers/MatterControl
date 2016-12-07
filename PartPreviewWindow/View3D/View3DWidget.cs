/*
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
//#define DoBooleanTest

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
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

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public interface IInteractionVolumeCreator
	{
		InteractionVolume CreateInteractionVolume(View3DWidget widget);
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
		public UndoBuffer UndoBuffer { get; private set; } = new UndoBuffer();
		public readonly int EditButtonHeight = 44;
		private Action afterSaveCallback = null;
		private List<MeshGroup> asyncMeshGroups = new List<MeshGroup>();
		private List<Matrix4X4> asyncMeshGroupTransforms = new List<Matrix4X4>();
		private List<PlatingMeshGroupData> asyncPlatingDatas = new List<PlatingMeshGroupData>();
		private FlowLayoutWidget doEdittingButtonsContainer;
		private bool editorThatRequestedSave = false;
		private FlowLayoutWidget enterEditButtonsContainer;
		private CheckBox expandMaterialOptions;
		private CheckBox expandRotateOptions;
		private CheckBox expandViewOptions;
		private ExportPrintItemWindow exportingWindow = null;
		private ObservableCollection<GuiWidget> extruderButtons = new ObservableCollection<GuiWidget>();
		private bool hasDrawn = false;
		private FlowLayoutWidget materialOptionContainer;
		public List<PlatingMeshGroupData> MeshGroupExtraData { get; private set; }
		public MeshSelectInfo CurrentSelectInfo { get; private set; } = new MeshSelectInfo();
		private OpenMode openMode;
		private bool partHasBeenEdited = false;
		private List<string> pendingPartsToLoad = new List<string>();
		private PrintItemWrapper printItemWrapper;
		private ProgressControl processingProgressControl;
		private FlowLayoutWidget rotateOptionContainer;
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

		private FlowLayoutWidget viewOptionContainer;

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

		public View3DWidget(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowType, AutoRotate autoRotate, OpenMode openMode = OpenMode.Viewing)
		{
			this.openMode = openMode;
			this.windowType = windowType;
			allowAutoRotate = (autoRotate == AutoRotate.Enabled);
			autoRotating = allowAutoRotate;
			MeshGroupExtraData = new List<PlatingMeshGroupData>();
			MeshGroupExtraData.Add(new PlatingMeshGroupData());

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

			buttonRightPanel = CreateRightButtonPanel(viewerVolume.y);
			buttonRightPanel.Name = "buttonRightPanel";
			buttonRightPanel.Visible = false;

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

					ImageBuffer normalImage = StaticData.Instance.LoadIcon("icon_edit.png", 14, 14);

					Button enterEdittingButton = textImageButtonFactory.Generate("Edit".Localize(), normalImage);
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
						UndoBuffer.ClearHistory();
					};

					Button groupButton = textImageButtonFactory.Generate("Group".Localize());
					groupButton.Name = "3D View Group";
					doEdittingButtonsContainer.AddChild(groupButton);
					groupButton.Click += (sender, e) =>
					{
						GroupSelectedMeshs();
						UndoBuffer.ClearHistory();
					};

					Button alignButton = textImageButtonFactory.Generate("Align".Localize());
					doEdittingButtonsContainer.AddChild(alignButton);
					alignButton.Click += (sender, e) =>
					{
						AlignToSelectedMeshGroup();
						UndoBuffer.ClearHistory();
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
			buttonRightPanelHolder.AddChild(buttonRightPanel);
			buttonRightPanel.VisibleChanged += (sender, e) =>
			{
				buttonRightPanelHolder.Visible = buttonRightPanel.Visible;
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

			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

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

#if DoBooleanTest
            DrawBefore += CreateBooleanTestGeometry;
            DrawAfter += RemoveBooleanTestGeometry;
#endif
			meshViewerWidget.TrackballTumbleWidget.DrawGlContent += trackballTumbleWidget_DrawGlContent;

		}

		private void trackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			if(allObjects != null)
			{
				//DebugBvh.Render(allObjects, Matrix4X4.Identity);
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
					DeleteSelectedMesh();
					break;

				case Keys.Escape:
					if (CurrentSelectInfo.DownOnPart)
					{
						CurrentSelectInfo.DownOnPart = false;

						SelectedMeshGroupTransform = transformOnMouseDown;

						Invalidate();
					}
					break;
			}

			base.OnKeyDown(keyEvent);
		}

		public bool DragingPart
		{
			get { return CurrentSelectInfo.DownOnPart; }
		}

		private void AddGridSnapSettings(GuiWidget widgetToAddTo)
		{
			FlowLayoutWidget container = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(5, 0),
			};

			TextWidget snapGridLabel = new TextWidget("Snap Grid".Localize())
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(3, 0, 0, 0),
			};

			container.AddChild(snapGridLabel);

			DropDownList selectableOptions = new DropDownList("Custom", Direction.Up)
			{
				VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
			};

			Dictionary<double, string> snapSettings = new Dictionary<double, string>()
			{
				{ 0, "Off" },
				{ .1, "0.1" },
				{ .25, "0.25" },
				{ .5, "0.5" },
				{ 1, "1" },
				{ 2, "2" },
				{ 5, "5" },
			};

			foreach (KeyValuePair<double, string> snapSetting in snapSettings)
			{
				double valueLocal = snapSetting.Key;

				MenuItem newItem = selectableOptions.AddItem(snapSetting.Value);
				if (meshViewerWidget.SnapGridDistance == valueLocal)
				{
					selectableOptions.SelectedLabel = snapSetting.Value;
				}

				newItem.Selected += (sender, e) =>
				{
					meshViewerWidget.SnapGridDistance = snapSetting.Key;
				};
			}

			container.AddChild(selectableOptions);

			widgetToAddTo.AddChild(container);
		}

#if DoBooleanTest
        MeshGroup booleanGroup;
        Matrix4X4 groupTransform;
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
				booleanGroup = new MeshGroup();

				booleanGroup.Meshes.Add(ApplyBoolean(PolygonMesh.Csg.CsgOperations.Union, AxisAlignedBoundingBox.Union, new Vector3(100, 0, 20), "U"));
				booleanGroup.Meshes.Add(ApplyBoolean(PolygonMesh.Csg.CsgOperations.Subtract, null, new Vector3(100, 100, 20), "S"));
				booleanGroup.Meshes.Add(ApplyBoolean(PolygonMesh.Csg.CsgOperations.Intersect, AxisAlignedBoundingBox.Intersection , new Vector3(100, 200, 20), "I"));

				offset += direction;
				rotCurrent += rotChange;
				scaleCurrent += scaleChange;
				meshViewerWidget.MeshGroups.Add(booleanGroup);

				groupTransform = Matrix4X4.Identity;
				meshViewerWidget.MeshGroupTransforms.Add(groupTransform);
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
			boxA = PlatonicSolids.CreateIcosahedron(35);
			boxA.Translate(centering);
			Mesh boxB = PlatonicSolids.CreateCube(40, 40, 40);
			boxB = PlatonicSolids.CreateIcosahedron(35);

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
			if (meshViewerWidget.MeshGroups.Contains(booleanGroup))
			{
				meshViewerWidget.MeshGroups.Remove(booleanGroup);
				meshViewerWidget.MeshGroupTransforms.Remove(groupTransform);
				UiThread.RunOnIdle(() => Invalidate(), 1.0 / 30.0);
			}
        }
#endif

		public enum AutoRotate { Enabled, Disabled };

		public enum OpenMode { Viewing, Editing }

		public enum WindowMode { Embeded, StandAlone };

		private enum TraceInfoOpperation { DONT_COPY, DO_COPY };

		public bool DisplayAllValueData { get; set; }

		public bool HaveSelection
		{
			get { return MeshGroups.Count > 0 && SelectedMeshGroupIndex > -1; }
		}

		public List<MeshGroup> MeshGroups
		{
			get { return meshViewerWidget.MeshGroups; }
		}

		public List<Matrix4X4> MeshGroupTransforms
		{
			get { return meshViewerWidget.MeshGroupTransforms; }
		}

		public MeshGroup SelectedMeshGroup
		{
			get { return meshViewerWidget.SelectedMeshGroup; }
		}

		public int SelectedMeshGroupIndex
		{
			get
			{
				return meshViewerWidget.SelectedMeshGroupIndex;
			}
			set
			{
				if (value != SelectedMeshGroupIndex)
				{
					meshViewerWidget.SelectedMeshGroupIndex = value;
					if (SelectionChanged != null)
					{
						SelectionChanged(this, null);
					}
					Invalidate();
				}
			}
		}

		public Matrix4X4 SelectedMeshGroupTransform
		{
			get { return meshViewerWidget.SelectedMeshGroupTransform; }
			set { meshViewerWidget.SelectedMeshGroupTransform = value; }
		}

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

		public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			if (AllowDragDrop())
			{
				pendingPartsToLoad.Clear();
				foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
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

			base.OnDragDrop(fileDropEventArgs);
		}

		public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
		{
			if (AllowDragDrop())
			{
				foreach (string file in fileDropEventArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(file).ToLower();
					if (extension != "" && ApplicationSettings.OpenDesignFileParams.Contains(extension))
					{
						fileDropEventArgs.AcceptDrop = true;
					}
				}
			}
			base.OnDragEnter(fileDropEventArgs);
		}

		public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
		{
			if (AllowDragDrop())
			{
				foreach (string file in fileDropEventArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(file).ToLower();
					if (extension != "" && ApplicationSettings.OpenDesignFileParams.Contains(extension))
					{
						fileDropEventArgs.AcceptDrop = true;
					}
				}
			}
			base.OnDragOver(fileDropEventArgs);
		}

		public override void OnLoad(EventArgs args)
		{
			ClearBedAndLoadPrintItemWrapper(printItemWrapper);
			base.OnLoad(args);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (HaveSelection)
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

			autoRotating = false;
			base.OnMouseDown(mouseEvent);
			if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
			{
				if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
					&& mouseEvent.Button == MouseButtons.Left
					&& ModifierKeys != Keys.Shift
					&& ModifierKeys != Keys.Control
					&& ModifierKeys != Keys.Alt)
				{
					if (!meshViewerWidget.MouseDownOnInteractionVolume)
					{
						int meshGroupHitIndex;
						IntersectInfo info = new IntersectInfo();
						if (FindMeshGroupHitPosition(mouseEvent.Position, out meshGroupHitIndex, ref info))
						{
							CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.z, null);
							SelectedMeshGroupIndex = meshGroupHitIndex;

							transformOnMouseDown = SelectedMeshGroupTransform;

							Invalidate();
							CurrentSelectInfo.DownOnPart = true;

							AxisAlignedBoundingBox selectedBounds = meshViewerWidget.GetBoundsForSelection();

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
							SelectedMeshGroupIndex = -1;
						}

						SelectedTransformChanged?.Invoke(this, null);
					}
				}
			}
		}

		public Vector3 LastHitPosition { get; private set; }

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None && CurrentSelectInfo.DownOnPart)
			{
				Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));
				Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
				IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
				if (info != null)
				{
					// move the mesh back to the start position
					{
						Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
						SelectedMeshGroupTransform *= totalTransform;
					}

					Vector3 delta = info.hitPosition - CurrentSelectInfo.PlaneDownHitPos;

					double snapGridDistance = meshViewerWidget.SnapGridDistance;
					if (snapGridDistance > 0)
					{
						// snap this position to the grid
						AxisAlignedBoundingBox selectedBounds = meshViewerWidget.GetBoundsForSelection();

						double xSnapOffset = selectedBounds.minXYZ.x;
						// snap the x position
						if (CurrentSelectInfo.HitQuadrant == HitQuadrant.RB
							|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
						{
							// switch to the other side
							xSnapOffset = selectedBounds.maxXYZ.x;
						}
						double xToSnap = xSnapOffset + delta.x;

						double snappedX = (Math.Round((xToSnap / snapGridDistance))) * snapGridDistance;
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

						double snappedY = (Math.Round((yToSnap / snapGridDistance))) * snapGridDistance;
						delta.y = snappedY - ySnapOffset;
					}

					// move the mesh back to the new position
					{
						Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(delta));

						SelectedMeshGroupTransform *= totalTransform;

						CurrentSelectInfo.LastMoveDelta = delta;
					}

					LastHitPosition = info.hitPosition;

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public void AddUndoForSelectedMeshGroupTransform(Matrix4X4 undoTransform)
		{
			if (undoTransform != SelectedMeshGroupTransform)
			{
				UndoBuffer.Add(new TransformUndoCommand(this, SelectedMeshGroupIndex, undoTransform, SelectedMeshGroupTransform));
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
				&& CurrentSelectInfo.DownOnPart
				&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
			{
				if (SelectedMeshGroupTransform != transformOnMouseDown)
				{
					AddUndoForSelectedMeshGroupTransform(transformOnMouseDown);
					PartHasBeenChanged();
				}
			}

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

		private void AddMaterialControls(FlowLayoutWidget buttonPanel)
		{
			extruderButtons.Clear();
			for (int extruderIndex = 0; extruderIndex < ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count); extruderIndex++)
			{
				FlowLayoutWidget colorSelectionContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
				colorSelectionContainer.HAnchor = HAnchor.ParentLeftRight;
				colorSelectionContainer.Padding = new BorderDouble(5);

				string colorLabelText = string.Format("{0} {1}", "Material".Localize(), extruderIndex + 1);

				RadioButton extruderSelection = new RadioButton(colorLabelText, textColor: ActiveTheme.Instance.PrimaryTextColor);
				extruderButtons.Add(extruderSelection);
				extruderSelection.SiblingRadioButtonList = extruderButtons;
				colorSelectionContainer.AddChild(extruderSelection);
				colorSelectionContainer.AddChild(new HorizontalSpacer());
				int extruderIndexLocal = extruderIndex;
				extruderSelection.Click += (sender, e) =>
				{
					if (SelectedMeshGroupIndex != -1)
					{
						foreach (Mesh mesh in SelectedMeshGroup.Meshes)
						{
							MeshMaterialData material = MeshMaterialData.Get(mesh);
							if (material.MaterialIndex != extruderIndexLocal + 1)
							{
								material.MaterialIndex = extruderIndexLocal + 1;
								PartHasBeenChanged();
							}
						}
					}
				};

				this.SelectionChanged += (sender, e) =>
				{
					if (SelectedMeshGroup != null)
					{
						Mesh mesh = SelectedMeshGroup.Meshes[0];
						MeshMaterialData material = MeshMaterialData.Get(mesh);

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

			ImageBuffer rotateImage = StaticData.Instance.LoadIcon("icon_rotate_32x32.png", 32, 32);
			Button rotateXButton = textImageButtonFactory.Generate("", rotateImage);
			TextWidget centeredX = new TextWidget("X", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredX.Margin = new BorderDouble(3, 0, 0, 0); centeredX.AnchorCenter(); rotateXButton.AddChild(centeredX);
			rotateButtonContainer.AddChild(rotateXButton);
			rotateControls.Add(rotateXButton);
			rotateXButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (SelectedMeshGroupIndex != -1)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationX(radians);
					Matrix4X4 undoTransform = SelectedMeshGroupTransform;
					SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(SelectedMeshGroup, SelectedMeshGroupTransform, rotation);
					PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
					UndoBuffer.Add(new TransformUndoCommand(this, SelectedMeshGroupIndex, undoTransform, SelectedMeshGroupTransform));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			Button rotateYButton = textImageButtonFactory.Generate("", rotateImage);
			TextWidget centeredY = new TextWidget("Y", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredY.Margin = new BorderDouble(3, 0, 0, 0); centeredY.AnchorCenter(); rotateYButton.AddChild(centeredY);
			rotateButtonContainer.AddChild(rotateYButton);
			rotateControls.Add(rotateYButton);
			rotateYButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (SelectedMeshGroupIndex != -1)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationY(radians);
					Matrix4X4 undoTransform = SelectedMeshGroupTransform;
					SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(SelectedMeshGroup, SelectedMeshGroupTransform, rotation);
					PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
					UndoBuffer.Add(new TransformUndoCommand(this, SelectedMeshGroupIndex, undoTransform, SelectedMeshGroupTransform));
					PartHasBeenChanged();
					Invalidate();
				}
			};

			Button rotateZButton = textImageButtonFactory.Generate("", rotateImage);
			TextWidget centeredZ = new TextWidget("Z", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredZ.Margin = new BorderDouble(3, 0, 0, 0); centeredZ.AnchorCenter(); rotateZButton.AddChild(centeredZ);
			rotateButtonContainer.AddChild(rotateZButton);
			rotateControls.Add(rotateZButton);
			rotateZButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (SelectedMeshGroupIndex != -1)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationZ(radians);
					Matrix4X4 undoTransform = SelectedMeshGroupTransform;
					SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(SelectedMeshGroup, SelectedMeshGroupTransform, rotation);
					PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
					UndoBuffer.Add(new TransformUndoCommand(this, SelectedMeshGroupIndex, undoTransform, SelectedMeshGroupTransform));
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
				if (SelectedMeshGroupIndex != -1)
				{
					Matrix4X4 undoTransform = SelectedMeshGroupTransform;
					MakeLowestFaceFlat(SelectedMeshGroupIndex);
					PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
					UndoBuffer.Add(new TransformUndoCommand(this, SelectedMeshGroupIndex, undoTransform, SelectedMeshGroupTransform));
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
			splitButtonFactory.FixedHeight = 40 * GuiWidget.DeviceScale;
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
			if (!HasBeenClosed && autoRotating)
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

		private void ClearBedAndLoadPrintItemWrapper(PrintItemWrapper printItemWrapper)
		{
			SwitchStateToNotEditing();

			MeshGroups.Clear();
			MeshGroupExtraData.Clear();
			MeshGroupTransforms.Clear();
			if (printItemWrapper != null)
			{
				// remove it first to make sure we don't double add it
				PrintItemWrapper.FileHasChanged.UnregisterEvent(ReloadMeshIfChangeExternaly, ref unregisterEvents);
				PrintItemWrapper.FileHasChanged.RegisterEvent(ReloadMeshIfChangeExternaly, ref unregisterEvents);

				// don't load the mesh until we get all the rest of the interface built
				meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);

				Vector2 bedCenter = new Vector2();
				MeshViewerWidget.CenterPartAfterLoad doCentering = MeshViewerWidget.CenterPartAfterLoad.DONT;

				if (ActiveSliceSettings.Instance?.GetValue<bool>(SettingsKey.center_part_on_bed) == true)
				{
					doCentering = MeshViewerWidget.CenterPartAfterLoad.DO;
					bedCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);
				}

				meshViewerWidget.LoadMesh(printItemWrapper.FileLocation, doCentering, bedCenter);
			}

			partHasBeenEdited = false;
		}

		private void CreateOptionsContent()
		{
			AddRotateControls(rotateOptionContainer);
		}

		private void CreateRenderTypeRadioButtons(FlowLayoutWidget viewOptionContainer)
		{
			string renderTypeString = UserSettings.Instance.get(UserSettingsKey.defaultRenderSetting);
			if (renderTypeString == null)
			{
				if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
				{
					renderTypeString = "Shaded";
				}
				else
				{
					renderTypeString = "Outlines";
				}
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderTypeString);
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
					UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypeShaded);
			}

			{
				RadioButton renderTypeOutlines = new RadioButton("Outlines".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				renderTypeOutlines.Checked = (meshViewerWidget.RenderType == RenderTypes.Outlines);
				renderTypeOutlines.CheckedStateChanged += (sender, e) =>
				{
					meshViewerWidget.RenderType = RenderTypes.Outlines;
					UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypeOutlines);
			}

			{
				RadioButton renderTypePolygons = new RadioButton("Polygons".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				renderTypePolygons.Checked = (meshViewerWidget.RenderType == RenderTypes.Polygons);
				renderTypePolygons.CheckedStateChanged += (sender, e) =>
				{
					meshViewerWidget.RenderType = RenderTypes.Polygons;
					UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
				};
				viewOptionContainer.AddChild(renderTypePolygons);
			}
		}

		private FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
		{
			FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRightPanel.Width = 200;

			// put in undo redo
			if(true) // this will not be enabled until the new scene_bundle gets merged
			{
				FlowLayoutWidget undoRedoButtons = new FlowLayoutWidget()
				{
					VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop,
					HAnchor = HAnchor.FitToChildren | HAnchor.ParentCenter,
				};
				double oldWidth = WhiteButtonFactory.FixedWidth;
				WhiteButtonFactory.FixedWidth = WhiteButtonFactory.FixedWidth / 2;
                Button undoButton = WhiteButtonFactory.Generate("Undo".Localize(), centerText: true);
				undoButton.Name = "3D View Undo";
				undoButton.Enabled = false;
				undoButton.Click += (sender, e) =>
				{
					UndoBuffer.Undo();
				};
				undoRedoButtons.AddChild(undoButton);

				Button redoButton = WhiteButtonFactory.Generate("Redo".Localize(), centerText: true);
				redoButton.Name = "3D View Redo";
				redoButton.Enabled = false;
				redoButton.Click += (sender, e) =>
				{
					UndoBuffer.Redo();
				};
				undoRedoButtons.AddChild(redoButton);
				buttonRightPanel.AddChild(undoRedoButtons);

				UndoBuffer.Changed += (sender, e) =>
				{
					undoButton.Enabled = UndoBuffer.UndoCount > 0;
					redoButton.Enabled = UndoBuffer.RedoCount > 0;
				};
				WhiteButtonFactory.FixedWidth = oldWidth;
			}

			{
				BorderDouble buttonMargin = new BorderDouble(top: 3);

				expandRotateOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton(
					"Rotate".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandRotateOptions.Margin = new BorderDouble(bottom: 2);
				buttonRightPanel.AddChild(expandRotateOptions);
				expandRotateOptions.CheckedStateChanged += expandRotateOptions_CheckedStateChanged;

				rotateOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				rotateOptionContainer.HAnchor = HAnchor.ParentLeftRight;
				rotateOptionContainer.Visible = false;
				buttonRightPanel.AddChild(rotateOptionContainer);

				buttonRightPanel.AddChild(new ScaleControls(this));

				buttonRightPanel.AddChild(new MirrorControls(this));

				PluginFinder<SideBarPlugin> SideBarPlugins = new PluginFinder<SideBarPlugin>();
				foreach (SideBarPlugin plugin in SideBarPlugins.Plugins)
				{
					buttonRightPanel.AddChild(plugin.CreateSideBarTool(this));
				}

				// put in the material options
				int numberOfExtruders = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);

				expandMaterialOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Materials".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandMaterialOptions.Margin = new BorderDouble(bottom: 2);
				expandMaterialOptions.CheckedStateChanged += expandMaterialOptions_CheckedStateChanged;

				if (numberOfExtruders > 1)
				{
					buttonRightPanel.AddChild(expandMaterialOptions);

					materialOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					materialOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					materialOptionContainer.Visible = false;

					buttonRightPanel.AddChild(materialOptionContainer);
					AddMaterialControls(materialOptionContainer);
				}

				// put in the view options
				{
					expandViewOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Display".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
					expandViewOptions.Margin = new BorderDouble(bottom: 2);
					buttonRightPanel.AddChild(expandViewOptions);
					expandViewOptions.CheckedStateChanged += expandViewOptions_CheckedStateChanged;

					viewOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					viewOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					viewOptionContainer.Padding = new BorderDouble(left: 4);
					viewOptionContainer.Visible = false;
					{
						CheckBox showBedCheckBox = new CheckBox("Show Print Bed".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
						showBedCheckBox.Checked = true;
						showBedCheckBox.CheckedStateChanged += (sender, e) =>
						{
							meshViewerWidget.RenderBed = showBedCheckBox.Checked;
						};
						viewOptionContainer.AddChild(showBedCheckBox);

						if (buildHeight > 0)
						{
							CheckBox showBuildVolumeCheckBox = new CheckBox("Show Print Area".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
							showBuildVolumeCheckBox.Checked = false;
							showBuildVolumeCheckBox.Margin = new BorderDouble(bottom: 5);
							showBuildVolumeCheckBox.CheckedStateChanged += (sender, e) =>
							{
								meshViewerWidget.RenderBuildVolume = showBuildVolumeCheckBox.Checked;
							};
							viewOptionContainer.AddChild(showBuildVolumeCheckBox);
						}

						if (UserSettings.Instance.IsTouchScreen)
						{
							UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, RenderTypes.Shaded.ToString());
						}
						else
						{
							CreateRenderTypeRadioButtons(viewOptionContainer);
						}
					}
					buttonRightPanel.AddChild(viewOptionContainer);
				}

				GuiWidget verticalSpacer = new GuiWidget();
				verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
				buttonRightPanel.AddChild(verticalSpacer);

				AddGridSnapSettings(buttonRightPanel);
			}

			buttonRightPanel.Padding = new BorderDouble(6, 6);
			buttonRightPanel.Margin = new BorderDouble(0, 1);
			buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

			return buttonRightPanel;
		}

		private void DeleteSelectedMesh()
		{
			// don't ever delete the last mesh
			if (SelectedMeshGroupIndex != -1
				&& MeshGroups.Count > 1)
			{
				int removingIndex = SelectedMeshGroupIndex;
				UndoBuffer.Add(new DeleteUndoCommand(this, removingIndex));

				MeshGroups.RemoveAt(removingIndex);
				MeshGroupExtraData.RemoveAt(removingIndex);
				MeshGroupTransforms.RemoveAt(removingIndex);
				this.SelectedMeshGroupIndex = -1;
				PartHasBeenChanged();
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

		private void expandMaterialOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (expandMaterialOptions.Checked == true)
			{
				expandRotateOptions.Checked = false;
				expandViewOptions.Checked = false;
			}
			materialOptionContainer.Visible = expandMaterialOptions.Checked;
		}

		private void expandRotateOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (rotateOptionContainer.Visible != expandRotateOptions.Checked)
			{
				if (expandRotateOptions.Checked == true)
				{
					expandViewOptions.Checked = false;
					expandMaterialOptions.Checked = false;
				}
				rotateOptionContainer.Visible = expandRotateOptions.Checked;
			}
		}

		private void expandViewOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (viewOptionContainer.Visible != expandViewOptions.Checked)
			{
				if (expandViewOptions.Checked == true)
				{
					expandRotateOptions.Checked = false;
					expandMaterialOptions.Checked = false;
				}
				viewOptionContainer.Visible = expandViewOptions.Checked;
			}
		}

		IPrimitive allObjects;
        private bool FindMeshGroupHitPosition(Vector2 screenPosition, out int meshHitIndex, ref IntersectInfo info)
		{
			meshHitIndex = 0;
			if (MeshGroupExtraData.Count == 0 || MeshGroupExtraData[0].meshTraceableData == null)
			{
				return false;
			}

			List<IPrimitive> mesheTraceables = new List<IPrimitive>();
			for (int i = 0; i < MeshGroupExtraData.Count; i++)
			{
				foreach (IPrimitive traceData in MeshGroupExtraData[i].meshTraceableData)
				{
					mesheTraceables.Add(new Transform(traceData, MeshGroupTransforms[i]));
				}
			}
			allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables, 0);

			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
			info = allObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				CurrentSelectInfo.PlaneDownHitPos = info.hitPosition;
				CurrentSelectInfo.LastMoveDelta = new Vector3();

				for (int i = 0; i < MeshGroupExtraData.Count; i++)
				{
					List<IPrimitive> insideBounds = new List<IPrimitive>();
					foreach (IPrimitive traceData in MeshGroupExtraData[i].meshTraceableData)
					{
						traceData.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
					}
					if (insideBounds.Contains(info.closestHitObject))
					{
						meshHitIndex = i;
						return true;
					}
				}
			}

			return false;
		}

		public GuiWidget GenerateHorizontalRule()
		{
			GuiWidget horizontalRule = new GuiWidget();
			horizontalRule.Height = 1;
			horizontalRule.Margin = new BorderDouble(0, 1, 0, 3);
			horizontalRule.HAnchor = HAnchor.ParentLeftRight;
			horizontalRule.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
			return horizontalRule;
		}

		private async void LoadAndAddPartsToPlate(string[] filesToLoad)
		{
			if (MeshGroups.Count > 0 && filesToLoad != null && filesToLoad.Length > 0)
			{
				string loadingPartLabel = "Loading Parts".Localize();
				string loadingPartLabelFull = "{0}:".FormatWith(loadingPartLabel);
				processingProgressControl.ProcessType = loadingPartLabelFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

				await Task.Run(() => loadAndAddPartsToPlate(filesToLoad));

				if (HasBeenClosed)
				{
					return;
				}

				UnlockEditControls();
				PartHasBeenChanged();

				bool addingOnlyOneItem = asyncMeshGroups.Count == MeshGroups.Count + 1;

				if (MeshGroups.Count > 0)
				{
					PullMeshGroupDataFromAsynchLists();
					if (addingOnlyOneItem)
					{
						// if we are only adding one part to the plate set the selection to it
						SelectedMeshGroupIndex = asyncMeshGroups.Count - 1;
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
						continueProcessing = !this.HasBeenClosed;
						double ratioAvailable = (ratioPerFile * .5);
						double currentRatio = currentRatioDone + progress0To1 * ratioAvailable;
						ReportProgressChanged(currentRatio, progressMessage, out continueProcessing);
					});

					if (HasBeenClosed)
					{
						return;
					}
					if (loadedMeshGroups != null)
					{
						double ratioPerSubMesh = ratioPerFile / loadedMeshGroups.Count;
						double subMeshRatioDone = 0;

						for (int subMeshIndex = 0; subMeshIndex < loadedMeshGroups.Count; subMeshIndex++)
						{
							MeshGroup meshGroup = loadedMeshGroups[subMeshIndex];

							PlatingHelper.FindPositionForGroupAndAddToPlate(meshGroup, Matrix4X4.Identity, asyncPlatingDatas, asyncMeshGroups, asyncMeshGroupTransforms);
							if (HasBeenClosed)
							{
								return;
							}
							PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, asyncMeshGroups.Count - 1, (double progress0To1, string processingState, out bool continueProcessing) =>
							{
								continueProcessing = !this.HasBeenClosed;
								double ratioAvailable = (ratioPerFile * .5);
								//                    done outer loop  +  done this loop  +first 1/2 (load)+  this part * ratioAvailable
								double currentRatio = currentRatioDone + subMeshRatioDone + ratioAvailable + progress0To1 * ratioPerSubMesh;
								ReportProgressChanged(currentRatio, progressMessage, out continueProcessing);
							});

							subMeshRatioDone += ratioPerSubMesh;
						}
					}

					currentRatioDone += ratioPerFile;
				}
			}
		}

		private void LockEditControls()
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

		private void MakeLowestFaceFlat(int indexToLayFlat)
		{
			Vertex lowestVertex = MeshGroups[indexToLayFlat].Meshes[0].Vertices[0];

			Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, MeshGroupTransforms[indexToLayFlat]);
			Mesh meshToLayFlat = null;
			foreach (Mesh meshToCheck in MeshGroups[indexToLayFlat].Meshes)
			{
				// find the lowest point on the model
				for (int testIndex = 1; testIndex < meshToCheck.Vertices.Count; testIndex++)
				{
					Vertex vertex = meshToCheck.Vertices[testIndex];
					Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshGroupTransforms[indexToLayFlat]);
					if (vertexPosition.z < lowestVertexPosition.z)
					{
						lowestVertex = meshToCheck.Vertices[testIndex];
						lowestVertexPosition = vertexPosition;
						meshToLayFlat = meshToCheck;
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
						Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, MeshGroupTransforms[indexToLayFlat]);
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
				Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshGroupTransforms[indexToLayFlat]);
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
				SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(SelectedMeshGroup, SelectedMeshGroupTransform, partLevelMatrix);

				PartHasBeenChanged();
				Invalidate();
			}
		}

		public static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)", RegexOptions.Compiled);

		private void MergeAndSavePartsDoWork(SaveAsWindow.SaveAsReturnInfo returnInfo)
		{
			if (returnInfo != null)
			{
				PrintItem printItem = new PrintItem();
				printItem.Name = returnInfo.newName;
				printItem.FileLocation = Path.GetFullPath(returnInfo.fileNameAndPath);
				printItemWrapper = new PrintItemWrapper(printItem, returnInfo.destinationLibraryProvider.GetProviderLocator());
			}

			// we sent the data to the async lists but we will not pull it back out (only use it as a temp holder).
			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			try
			{
				// push all the transforms into the meshes
				for (int i = 0; i < asyncMeshGroups.Count; i++)
				{
					asyncMeshGroups[i].Transform(asyncMeshGroupTransforms[i]);

					bool continueProcessing;
					ReportProgressChanged((i + 1) * .4 / asyncMeshGroups.Count, "", out continueProcessing);
				}

				string[] metaData = { "Created By", "MatterControl", "BedPosition", "Absolute" };

				MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);

				// If null we are replacing a file from the current print item wrapper
				if (returnInfo == null)
				{
					var fileInfo = new FileInfo(printItemWrapper.FileLocation);

					bool requiresTypeChange = !fileInfo.Extension.Equals(".amf", StringComparison.OrdinalIgnoreCase);
					if (requiresTypeChange && !printItemWrapper.UseIncrementedNameDuringTypeChange)
					{
						// Not using incremented file name, simply change to AMF
						printItemWrapper.FileLocation = Path.ChangeExtension(printItemWrapper.FileLocation, ".amf");
					}
					else if (requiresTypeChange)
					{
						string newFileName;
						string incrementedFileName;

						// Switching from .stl, .obj or similar to AMF. Save the file and update the
						// the filename with an incremented (n) value to reflect the extension change in the UI 
						string fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);

						// Drop bracketed number sections from our source filename to ensure we don't generate something like "file (1) (1).amf"
						if (fileName.Contains("("))
						{
							fileName = fileNameNumberMatch.Replace(fileName, "").Trim();
						}

						// Generate and search for an incremented file name until no match is found at the target directory
						int foundCount = 0;
						do
						{
							newFileName = string.Format("{0} ({1})", fileName, ++foundCount);
							incrementedFileName = Path.Combine(fileInfo.DirectoryName, newFileName + ".amf");

							// Continue incrementing while any matching file exists
						} while (Directory.GetFiles(fileInfo.DirectoryName, newFileName + ".*").Any());

						// Change the FileLocation to the new AMF file
						printItemWrapper.FileLocation = incrementedFileName;
					}

					try
					{
						// get a new location to save to
						string tempFileNameToSaveTo = ApplicationDataStorage.Instance.GetTempFileName("amf");

						// save to the new temp location
						bool savedSuccessfully = MeshFileIo.Save(asyncMeshGroups, tempFileNameToSaveTo, outputInfo, ReportProgressChanged);

						// Swap out the files if the save operation completed successfully 
						if (savedSuccessfully && File.Exists(tempFileNameToSaveTo))
						{
							// Ensure the target path is clear
							if(File.Exists(printItemWrapper.FileLocation))
							{
								File.Delete(printItemWrapper.FileLocation);
							}

							// Move the newly saved file back into place
							File.Move(tempFileNameToSaveTo, printItemWrapper.FileLocation);

							// Once the file is swapped back into place, update the PrintItem to account for extension change
							printItemWrapper.PrintItem.Commit();
						}
					}
					catch(Exception ex)
					{
						Trace.WriteLine("Error saving file: ", ex.Message);
					}
				}
				else // we are saving a new file and it will not exist until we are done
				{
					MeshFileIo.Save(asyncMeshGroups, printItemWrapper.FileLocation, outputInfo, ReportProgressChanged);
				}

				// Wait for a second to report the file changed to give the OS a chance to finish closing it.
				UiThread.RunOnIdle(printItemWrapper.ReportFileChange, 3);

				if (returnInfo != null
					&& returnInfo.destinationLibraryProvider != null)
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
			catch (System.UnauthorizedAccessException e2)
			{
				Debug.Print(e2.Message);
				GuiWidget.BreakInDebugger();
				saveSucceded = false;
				UiThread.RunOnIdle(() =>
				{
					//Do something special when unauthorized?
					StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
				});
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				saveSucceded = false;
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
				});
			}
		}

		private void MergeAndSavePartsDoCompleted()
		{
			if (HasBeenClosed)
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

			if (MeshGroups.Count > 0)
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
			if (MeshGroups.Count > 0)
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
			if (ActiveSliceSettings.Instance?.GetValue<bool>(SettingsKey.center_part_on_bed) == false)
			{
				AxisAlignedBoundingBox allBounds = MeshViewerWidget.GetAxisAlignedBoundingBox(MeshGroups);
				bool onBed = allBounds.minXYZ.z > -.001 && allBounds.minXYZ.z < .001; // really close to the bed
				RectangleDouble bedRect = new RectangleDouble(0, 0, ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size).x, ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size).y);
				bedRect.Offset(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center) - ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size) / 2);

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

		private void PullMeshGroupDataFromAsynchLists()
		{
			if (MeshGroups.Count != asyncMeshGroups.Count)
			{
				PartHasBeenChanged();
			}

			MeshGroups.Clear();
			foreach (MeshGroup meshGroup in asyncMeshGroups)
			{
				MeshGroups.Add(meshGroup);
			}
			MeshGroupTransforms.Clear();
			foreach (Matrix4X4 transform in asyncMeshGroupTransforms)
			{
				MeshGroupTransforms.Add(transform);
			}
			MeshGroupExtraData.Clear();
			foreach (PlatingMeshGroupData meshData in asyncPlatingDatas)
			{
				MeshGroupExtraData.Add(meshData);
			}

			if (MeshGroups.Count != MeshGroupTransforms.Count
				|| MeshGroups.Count != MeshGroupExtraData.Count)
			{
				throw new Exception("These all need to remain in sync.");
			}
		}

		private void PushMeshGroupDataToAsynchLists(TraceInfoOpperation traceInfoOpperation, ReportProgressRatio reportProgress = null)
		{
			UiThread.RunOnIdle(() =>
			{
				processingProgressControl.ProgressMessage = "Async Copy";
			});
			asyncMeshGroups.Clear();
			asyncMeshGroupTransforms.Clear();
			for (int meshGroupIndex = 0; meshGroupIndex < MeshGroups.Count; meshGroupIndex++)
			{
				MeshGroup meshGroup = MeshGroups[meshGroupIndex];
				MeshGroup newMeshGroup = new MeshGroup();
				for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
				{
					Mesh mesh = meshGroup.Meshes[meshIndex];
					newMeshGroup.Meshes.Add(Mesh.Copy(mesh));
				}
				asyncMeshGroups.Add(newMeshGroup);
				asyncMeshGroupTransforms.Add(MeshGroupTransforms[meshGroupIndex]);
			}
			asyncPlatingDatas.Clear();

			for (int meshGroupIndex = 0; meshGroupIndex < MeshGroupExtraData.Count; meshGroupIndex++)
			{
				PlatingMeshGroupData meshData = new PlatingMeshGroupData();
				MeshGroup meshGroup = MeshGroups[meshGroupIndex];

				if (traceInfoOpperation == TraceInfoOpperation.DO_COPY)
				{
					meshData.meshTraceableData.AddRange(MeshGroupExtraData[meshGroupIndex].meshTraceableData);
				}

				asyncPlatingDatas.Add(meshData);
			}
			UiThread.RunOnIdle(() =>
			{
				processingProgressControl.ProgressMessage = "";
			});
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

		public override bool InEditMode
		{
			get { return buttonRightPanel.Visible; }
		}

		private void SwitchStateToNotEditing()
		{
			if (!enterEditButtonsContainer.Visible)
			{
				enterEditButtonsContainer.Visible = true;
				processingProgressControl.Visible = false;
				buttonRightPanel.Visible = false;
				doEdittingButtonsContainer.Visible = false;
				viewControls3D.PartSelectVisible = false;
				if (viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
				}
				SelectedMeshGroupIndex = -1;
			}
		}

		private void UnlockEditControls()
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