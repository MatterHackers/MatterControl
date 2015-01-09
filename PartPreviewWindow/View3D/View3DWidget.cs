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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.Transform;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public partial class View3DWidget : PartPreview3DWidget
    {
        EventHandler unregisterEvents;
        public enum WindowMode { Embeded, StandAlone };
        public enum AutoRotate { Enabled, Disabled };
        public enum OpenMode { Viewing, Editing }
        OpenMode openMode;

        readonly int EditButtonHeight = 44;

        public WindowMode windowType { get; set; }

        Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
        MeshSelectInfo meshSelectInfo;

        EventHandler SelectionChanged;
        UpArrow3D upArrow;
        internal HeightValueDisplay heightDisplay;

        FlowLayoutWidget viewOptionContainer;
        FlowLayoutWidget rotateOptionContainer;
        FlowLayoutWidget scaleOptionContainer;
        FlowLayoutWidget mirrorOptionContainer;
        FlowLayoutWidget materialOptionContainer;

        List<string> pendingPartsToLoad = new List<string>();
        bool DoAddFileAfterCreatingEditData { get; set; }

        ProgressControl processingProgressControl;
        FlowLayoutWidget enterEditButtonsContainer;
        FlowLayoutWidget doEdittingButtonsContainer;

        Dictionary<string, List<GuiWidget>> transformControls = new Dictionary<string, List<GuiWidget>>();

        MHNumberEdit scaleRatioControl;

        CheckBox expandViewOptions;
        CheckBox expandRotateOptions;
        CheckBox expandScaleOptions;
        CheckBox expandMirrorOptions;
        CheckBox expandMaterialOptions;

        Button autoArrangeButton;
		SplitButton saveButtons;
        Button applyScaleButton;

		PrintItemWrapper printItemWrapper;
		
		SaveAsWindow saveAsWindow = null;
        ExportPrintItemWindow exportingWindow = null;

        bool partHasBeenEdited = false;

        List<MeshGroup> asynchMeshGroups = new List<MeshGroup>();
        List<ScaleRotateTranslate> asynchMeshGroupTransforms = new List<ScaleRotateTranslate>();
        List<PlatingMeshGroupData> asynchPlatingDatas = new List<PlatingMeshGroupData>();

        List<PlatingMeshGroupData> MeshGroupExtraData;

        public bool DisplayAllValueData { get; set; }

        public ScaleRotateTranslate SelectedMeshGroupTransform
        {
            get { return meshViewerWidget.SelectedMeshGroupTransform; }
            set { meshViewerWidget.SelectedMeshGroupTransform = value; }
        }

        public MeshGroup SelectedMeshGroup
        {
            get { return meshViewerWidget.SelectedMeshGroup; }
        }

        public bool HaveSelection
        {
            get { return MeshGroups.Count > 0 && SelectedMeshGroupIndex > -1; }
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

        public List<MeshGroup> MeshGroups
        {
            get { return meshViewerWidget.MeshGroups; }
        }

        public List<ScaleRotateTranslate> MeshGroupTransforms
        {
            get { return meshViewerWidget.MeshGroupTransforms; }
        }

        internal struct MeshSelectInfo
        {
            internal bool downOnPart;
            internal PlaneShape hitPlane;
            internal Vector3 planeDownHitPos;
            internal Vector3 lastMoveDelta;
        }

        private bool FindMeshGroupHitPosition(Vector2 screenPosition, out int meshHitIndex)
        {
            meshHitIndex = 0;
            if (MeshGroupExtraData.Count == 0 || MeshGroupExtraData[0].meshTraceableData == null)
            {
                return false;
            }

            List<IRayTraceable> mesheTraceables = new List<IRayTraceable>();
            for (int i = 0; i < MeshGroupExtraData.Count; i++)
            {
                foreach (IRayTraceable traceData in MeshGroupExtraData[i].meshTraceableData)
                {
                    mesheTraceables.Add(new Transform(traceData, MeshGroupTransforms[i].TotalTransform));
                }
            }
            IRayTraceable allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

            Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
            Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
            IntersectInfo info = allObjects.GetClosestIntersection(ray);
            if (info != null)
            {
                meshSelectInfo.planeDownHitPos = info.hitPosition;
                meshSelectInfo.lastMoveDelta = new Vector3();

                for (int i = 0; i < MeshGroupExtraData.Count; i++)
                {
                    List<IRayTraceable> insideBounds = new List<IRayTraceable>();
                    foreach (IRayTraceable traceData in MeshGroupExtraData[i].meshTraceableData)
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

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
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
                        if (FindMeshGroupHitPosition(mouseEvent.Position, out meshGroupHitIndex))
                        {
                            meshSelectInfo.hitPlane = new PlaneShape(Vector3.UnitZ, meshSelectInfo.planeDownHitPos.z, null);
                            SelectedMeshGroupIndex = meshGroupHitIndex;

                            transformOnMouseDown = SelectedMeshGroupTransform.translation;

                            Invalidate();
                            meshSelectInfo.downOnPart = true;
                        }
                        else
                        {
                            SelectedMeshGroupIndex = -1;
                        }

                        UpdateSizeInfo();
                    }
                }
            }
        }

        bool firstDraw = true;
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (firstDraw)
            {
                ClearBedAndLoadPrintItemWrapper(printItemWrapper);
                firstDraw = false;
            }

            if (HaveSelection)
            {
                upArrow.SetPosition();
                heightDisplay.SetPosition();
            }

            hasDrawn = true;
            base.OnDraw(graphics2D);
            DrawStuffForSelectedPart(graphics2D);
        }

        private void DrawStuffForSelectedPart(Graphics2D graphics2D)
        {
            if (SelectedMeshGroup != null)
            {
                AxisAlignedBoundingBox selectedBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox(SelectedMeshGroupTransform.TotalTransform);
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

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None && meshSelectInfo.downOnPart)
            {
                Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));
                Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
                IntersectInfo info = meshSelectInfo.hitPlane.GetClosestIntersection(ray);
                if (info != null)
                {
                    Vector3 delta = info.hitPosition - meshSelectInfo.planeDownHitPos;

                    Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(new Vector3(-meshSelectInfo.lastMoveDelta));
                    totalTransfrom *= Matrix4X4.CreateTranslation(new Vector3(delta));
                    meshSelectInfo.lastMoveDelta = delta;

                    ScaleRotateTranslate translated = SelectedMeshGroupTransform;
                    translated.translation *= totalTransfrom;
                    SelectedMeshGroupTransform = translated;

                    Invalidate();
                }
            }

            base.OnMouseMove(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
                && meshSelectInfo.downOnPart
                && meshSelectInfo.lastMoveDelta != Vector3.Zero)
            {
                PartHasBeenChanged();
            }

            meshSelectInfo.downOnPart = false;

            base.OnMouseUp(mouseEvent);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        public View3DWidget(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape, WindowMode windowType, AutoRotate autoRotate, OpenMode openMode = OpenMode.Viewing)
        {
            this.openMode = openMode;
            this.windowType = windowType;
            allowAutoRotate = (autoRotate == AutoRotate.Enabled);
            autoRotating = allowAutoRotate;
            MeshGroupExtraData = new List<PlatingMeshGroupData>();
            MeshGroupExtraData.Add(new PlatingMeshGroupData());

            this.printItemWrapper = printItemWrapper;

            FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

            FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
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

                // If the window is embeded (in the center pannel) and there is no item loaded then don't show the add button
                enterEditButtonsContainer = new FlowLayoutWidget();
                {
					Button addButton = textImageButtonFactory.Generate("Insert".Localize(), "icon_insert_32x32.png");
                    addButton.Margin = new BorderDouble(right: 0);
                    enterEditButtonsContainer.AddChild(addButton);
                    addButton.Click += (sender, e) =>
                    {
                        UiThread.RunOnIdle((state) =>
                        {
                            DoAddFileAfterCreatingEditData = true;
                            EnterEditAndCreateSelectionData();
                        });
                    };

                    Button enterEdittingButton = textImageButtonFactory.Generate("Edit".Localize(), "icon_edit_32x32.png");
                    enterEdittingButton.Margin = new BorderDouble(right: 4);
                    enterEdittingButton.Click += (sender, e) =>
                    {
                        EnterEditAndCreateSelectionData();
                    };

					Button exportButton = textImageButtonFactory.Generate("Export...".Localize());
					exportButton.Margin = new BorderDouble(right: 10);
					exportButton.Click += (sender, e) => 
					{
						UiThread.RunOnIdle((state) =>
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
                        UiThread.RunOnIdle((state) =>
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
                    doEdittingButtonsContainer.AddChild(ungroupButton);
                    ungroupButton.Click += (sender, e) =>
                    {
                        UngroupSelectedMeshGroup();
                    };

                    Button groupButton = textImageButtonFactory.Generate("Group".Localize());
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

					GuiWidget separatorTwo = new GuiWidget(1, 2);
					separatorTwo.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
					separatorTwo.Margin = new BorderDouble(4, 2);
					separatorTwo.VAnchor = VAnchor.ParentBottomTop;
					doEdittingButtonsContainer.AddChild(separatorTwo);

                    Button copyButton = textImageButtonFactory.Generate("Copy".Localize());
                    doEdittingButtonsContainer.AddChild(copyButton);
                    copyButton.Click += (sender, e) =>
                    {
                        MakeCopyOfGroup();
                    };

                    Button deleteButton = textImageButtonFactory.Generate("Remove".Localize());
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

					Button leaveEditModeButton = textImageButtonFactory.Generate("Cancel".Localize(), centerText: true);
					leaveEditModeButton.Click += (sender, e) =>
						{
							UiThread.RunOnIdle((state) =>
								{
									if (saveButtons.Visible)
									{
										StyledMessageBox.ShowMessageBox(ExitEditingAndSaveIfRequired, "Would you like to save your changes before exiting the editor?", "Save Changes", StyledMessageBox.MessageType.YES_NO);
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
					doEdittingButtonsContainer.AddChild(leaveEditModeButton);

					// put in the save button
					AddSaveAndSaveAs(doEdittingButtonsContainer);
                }

                KeyDown += (sender, e) =>
                {
                    KeyEventArgs keyEvent = e as KeyEventArgs;
                    if (keyEvent != null && !keyEvent.Handled)
                    {
                        if (keyEvent.KeyCode == Keys.Delete || keyEvent.KeyCode == Keys.Back)
                        {
                            DeleteSelectedMesh();
                        }

                        if (keyEvent.KeyCode == Keys.Escape)
                        {
                            if (meshSelectInfo.downOnPart)
                            {
                                meshSelectInfo.downOnPart = false;

                                ScaleRotateTranslate translated = SelectedMeshGroupTransform;
                                translated.translation = transformOnMouseDown;
                                SelectedMeshGroupTransform = translated;

                                Invalidate();
                            }
                        }
                    }
                };

                editToolBar.AddChild(doEdittingButtonsContainer);
                buttonBottomPanel.AddChild(editToolBar);
            }

            GuiWidget buttonRightPanelHolder = new GuiWidget(HAnchor.FitToChildren, VAnchor.ParentBottomTop);
            centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
            buttonRightPanelHolder.AddChild(buttonRightPanel);

            viewControls3D = new ViewControls3D(meshViewerWidget);

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

            AddHandlers();

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

            upArrow = new UpArrow3D(this);
            heightDisplay = new HeightValueDisplay(this);
            heightDisplay.Visible = false;
            meshViewerWidget.interactionVolumes.Add(upArrow);

            // make sure the colors are set correctl
            ThemeChanged(this, null);

            saveButtons.VisibleChanged += (sender, e) =>
            {
                partHasBeenEdited = true;
            };
        }

        private void ClearBedAndLoadPrintItemWrapper(PrintItemWrapper printItemWrapper)
        {
            MeshGroups.Clear();
            MeshGroupExtraData.Clear();
            MeshGroupTransforms.Clear();
            if (printItemWrapper != null)
            {
                // Controls if the part should be automattically centered. Ideally, we should autocenter any time a user has
                // not moved parts around on the bed (as we do now) but skip autocentering if the user has moved and placed
                // parts themselves. For now, simply mock that determination to allow testing of the proposed change and convey
                // when we would want to autocenter (i.e. autocenter when part was loaded outside of the new closed loop system)
                MeshVisualizer.MeshViewerWidget.CenterPartAfterLoad centerOnBed = MeshViewerWidget.CenterPartAfterLoad.DO;
                if (printItemWrapper.FileLocation.Contains(ApplicationDataStorage.Instance.ApplicationLibraryDataPath))
                {
                    centerOnBed = MeshViewerWidget.CenterPartAfterLoad.DONT;
                }

                // don't load the mesh until we get all the rest of the interface built
                meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);
                meshViewerWidget.LoadMesh(printItemWrapper.FileLocation, centerOnBed);
            }

            partHasBeenEdited = false;
        }

        void ExitEditingAndSaveIfRequired(bool response)
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

        void SwitchStateToNotEditing()
        {
            enterEditButtonsContainer.Visible = true;
            autoArrangeButton.Visible = false;
            processingProgressControl.Visible = false;
            buttonRightPanel.Visible = false;
            doEdittingButtonsContainer.Visible = false;
            viewControls3D.PartSelectVisible = false;
            if (viewControls3D.partSelectButton.Checked)
            {
                viewControls3D.rotateButton.ClickButton(null);
            }
            SelectedMeshGroupIndex = -1;
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

        public void ThemeChanged(object sender, EventArgs e)
        {
            processingProgressControl.fillColor = ActiveTheme.Instance.PrimaryAccentColor;

            MeshViewerWidget.SetMaterialColor(1, ActiveTheme.Instance.PrimaryAccentColor);
        }

        void SetEditControlsBasedOnPrinterState(object sender, EventArgs e)
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

        bool hasDrawn = false;
        Stopwatch timeSinceLastSpin = new Stopwatch();
        void AutoSpin(object state)
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

        private void LoadAndAddPartsToPlate(string[] filesToLoad)
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

                BackgroundWorker loadAndAddPartsToPlateBackgroundWorker = null;
                loadAndAddPartsToPlateBackgroundWorker = new BackgroundWorker();

                loadAndAddPartsToPlateBackgroundWorker.DoWork += new DoWorkEventHandler(loadAndAddPartsToPlateBackgroundWorker_DoWork);
                loadAndAddPartsToPlateBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted);

                loadAndAddPartsToPlateBackgroundWorker.RunWorkerAsync(filesToLoad);
            }
        }

        enum TraceInfoOpperation { DONT_COPY, DO_COPY };
        private void PushMeshGroupDataToAsynchLists(TraceInfoOpperation traceInfoOpperation, ReportProgressRatio reportProgress = null)
        {
            UiThread.RunOnIdle((state) =>
            {
                processingProgressControl.ProgressMessage = "Async Copy";
            });
            asynchMeshGroups.Clear();
            asynchMeshGroupTransforms.Clear();
            for (int meshGroupIndex = 0; meshGroupIndex < MeshGroups.Count; meshGroupIndex++)
            {
                MeshGroup meshGroup = MeshGroups[meshGroupIndex];
                MeshGroup newMeshGroup = new MeshGroup();
                for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
                {
                    Mesh mesh = meshGroup.Meshes[meshIndex];
                    newMeshGroup.Meshes.Add(Mesh.Copy(mesh));
                }
                asynchMeshGroups.Add(newMeshGroup);
                asynchMeshGroupTransforms.Add(MeshGroupTransforms[meshGroupIndex]);
            }
            asynchPlatingDatas.Clear();

            for (int meshGroupIndex = 0; meshGroupIndex < MeshGroupExtraData.Count; meshGroupIndex++)
            {
                PlatingMeshGroupData meshData = new PlatingMeshGroupData();
                meshData.currentScale = MeshGroupExtraData[meshGroupIndex].currentScale;
                MeshGroup meshGroup = MeshGroups[meshGroupIndex];
                for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
                {
                    if (traceInfoOpperation == TraceInfoOpperation.DO_COPY)
                    {
                        meshData.meshTraceableData.AddRange(MeshGroupExtraData[meshGroupIndex].meshTraceableData);
                    }
                }
                asynchPlatingDatas.Add(meshData);
            }
            UiThread.RunOnIdle((state) =>
            {
                processingProgressControl.ProgressMessage = "";
            });
        }

        private void PullMeshGroupDataFromAsynchLists()
        {
            if (MeshGroups.Count != asynchMeshGroups.Count)
            {
                PartHasBeenChanged();
            }

            MeshGroups.Clear();
            foreach (MeshGroup meshGroup in asynchMeshGroups)
            {
                MeshGroups.Add(meshGroup);
            }
            MeshGroupTransforms.Clear();
            foreach (ScaleRotateTranslate transform in asynchMeshGroupTransforms)
            {
                MeshGroupTransforms.Add(transform);
            }
            MeshGroupExtraData.Clear();
            foreach (PlatingMeshGroupData meshData in asynchPlatingDatas)
            {
                MeshGroupExtraData.Add(meshData);
            }

            if (MeshGroups.Count != MeshGroupTransforms.Count
                || MeshGroups.Count != MeshGroupExtraData.Count)
            {
                throw new Exception("These all need to remain in sync.");
            }
        }

        void loadAndAddPartsToPlateBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            string[] filesToLoad = e.Argument as string[];
            if (filesToLoad != null && filesToLoad.Length > 0)
            {
                double ratioPerFile = 1.0 / filesToLoad.Length;
                double currentRatioDone = 0;

                for (int i = 0; i < filesToLoad.Length; i++)
                {
                    string loadedFileName = filesToLoad[i];
                    List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(Path.GetFullPath(loadedFileName), (double progress0To1, string processingState, out bool continueProcessing) =>
                    {
                        continueProcessing = !this.WidgetHasBeenClosed;
                        double ratioAvailable = (ratioPerFile * .5);
                        double currentRatio = currentRatioDone + progress0To1 * ratioAvailable;
                        BackgroundWorker_ProgressChanged(currentRatio, processingState, out continueProcessing);
                    });

                    if (WidgetHasBeenClosed)
                    {
                        return;
                    }
                    if (loadedMeshGroups != null)
                    {
                        double ratioPerSubMesh = 1.0 / loadedMeshGroups.Count;
                        double currentPlatingRatioDone = 0;

                        for (int subMeshIndex = 0; subMeshIndex < loadedMeshGroups.Count; subMeshIndex++)
                        {
                            MeshGroup meshGroup = loadedMeshGroups[subMeshIndex];

                            PlatingHelper.FindPositionForGroupAndAddToPlate(meshGroup, ScaleRotateTranslate.Identity(), asynchPlatingDatas, asynchMeshGroups, asynchMeshGroupTransforms);
                            if (WidgetHasBeenClosed)
                            {
                                return;
                            }
                            PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, asynchMeshGroups.Count - 1, (double progress0To1, string processingState, out bool continueProcessing) =>
                            {
                                continueProcessing = !this.WidgetHasBeenClosed;
                                double ratioAvailable = (ratioPerFile * .5);
                                double currentRatio = currentRatioDone + currentPlatingRatioDone + ratioAvailable + progress0To1 * ratioAvailable;
                                BackgroundWorker_ProgressChanged(currentRatio, processingState, out continueProcessing);
                            });

                            currentPlatingRatioDone += ratioPerSubMesh;
                        }
                    }
                }

                currentRatioDone += ratioPerFile;
            }
        }

        void loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (WidgetHasBeenClosed)
            {
                return;
            }
            UnlockEditControls();
            PartHasBeenChanged();

            bool addingOnlyOneItem = asynchMeshGroups.Count == MeshGroups.Count + 1;

            if (MeshGroups.Count > 0)
            {
                PullMeshGroupDataFromAsynchLists();
                if (addingOnlyOneItem)
                {
                    // if we are only adding one part to the plate set the selection to it
                    SelectedMeshGroupIndex = asynchMeshGroups.Count - 1;
                }
            }
        }

        void meshViewerWidget_LoadDone(object sender, EventArgs e)
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

            SelectionChanged(this, null);

            if (openMode == OpenMode.Editing)
            {
                UiThread.RunOnIdle((state) =>
                {
                    EnterEditAndCreateSelectionData();
                });
            }
        }

        bool viewIsInEditModePreLock = false;
        bool wasInSelectMode = false;
        void LockEditControls()
        {
            viewIsInEditModePreLock = doEdittingButtonsContainer.Visible;
            enterEditButtonsContainer.Visible = false;
            doEdittingButtonsContainer.Visible = false;
            buttonRightPanelDisabledCover.Visible = true;
            if (viewControls3D.PartSelectVisible == true)
            {
                viewControls3D.PartSelectVisible = false;
                if (viewControls3D.partSelectButton.Checked)
                {
                    wasInSelectMode = true;
                    viewControls3D.rotateButton.ClickButton(null);
                    viewControls3D.scaleButton.Click += StopReturnToSelectionButton;
                    viewControls3D.translateButton.Click += StopReturnToSelectionButton;
                }
            }
        }

        void StopReturnToSelectionButton(object sender, EventArgs e)
        {
            wasInSelectMode = false;
            RadioButton button = sender as RadioButton;
            button.Click -= StopReturnToSelectionButton;
        }

        void UnlockEditControls()
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
                viewControls3D.partSelectButton.ClickButton(null);
                wasInSelectMode = false;
            }

            viewControls3D.scaleButton.Click -= StopReturnToSelectionButton;
            viewControls3D.translateButton.Click -= StopReturnToSelectionButton;

            UpdateSizeInfo();
        }

        private void DeleteSelectedMesh()
        {
            // don't ever delete the last mesh
            if (SelectedMeshGroupIndex != -1
                && MeshGroups.Count > 1)
            {
                MeshGroups.RemoveAt(SelectedMeshGroupIndex);
                MeshGroupExtraData.RemoveAt(SelectedMeshGroupIndex);
                MeshGroupTransforms.RemoveAt(SelectedMeshGroupIndex);
                SelectedMeshGroupIndex = Math.Min(SelectedMeshGroupIndex, MeshGroups.Count - 1);
                PartHasBeenChanged();
                Invalidate();
            }
        }

        Stopwatch timeSinceReported = new Stopwatch();
        void BackgroundWorker_ProgressChanged(double progress0To1, string processingState, out bool continueProcessing)
        {
            if (!timeSinceReported.IsRunning || timeSinceReported.ElapsedMilliseconds > 100
                || processingState != processingProgressControl.ProgressMessage)
            {
                UiThread.RunOnIdle((state) =>
                {
                    processingProgressControl.PercentComplete = (int)(progress0To1 * 100 + .5);
                    processingProgressControl.ProgressMessage = processingState;
                });
                timeSinceReported.Restart();
            }
            continueProcessing = true;
        }

        private void CreateOptionsContent()
        {
            AddRotateControls(rotateOptionContainer);
            AddScaleControls(scaleOptionContainer);
        }

        private FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

                expandRotateOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Rotate"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandRotateOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandRotateOptions);

                rotateOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rotateOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                rotateOptionContainer.Visible = false;
                buttonRightPanel.AddChild(rotateOptionContainer);

                expandScaleOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Scale"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandScaleOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandScaleOptions);

                scaleOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                scaleOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                scaleOptionContainer.Visible = false;
                buttonRightPanel.AddChild(scaleOptionContainer);

                // put in the mirror options
                {
                    expandMirrorOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Mirror"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandMirrorOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandMirrorOptions);

                    mirrorOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    mirrorOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    mirrorOptionContainer.Visible = false;
                    buttonRightPanel.AddChild(mirrorOptionContainer);

                    AddMirrorControls(mirrorOptionContainer);
                }

                // put in the material options
                int numberOfExtruders = ActiveSliceSettings.Instance.ExtruderCount;
                
                expandMaterialOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Material"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandMaterialOptions.Margin = new BorderDouble(bottom: 2);

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
                    expandViewOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Display"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandViewOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandViewOptions);

                    viewOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    viewOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    viewOptionContainer.Padding = new BorderDouble(left: 4);
                    viewOptionContainer.Visible = false;
                    {
                        CheckBox showBedCheckBox = new CheckBox(LocalizedString.Get("Show Print Bed"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                        showBedCheckBox.Checked = true;
                        showBedCheckBox.CheckedStateChanged += (sender, e) =>
                        {
                            meshViewerWidget.RenderBed = showBedCheckBox.Checked;
                        };
                        viewOptionContainer.AddChild(showBedCheckBox);

                        if (buildHeight > 0)
                        {
                            CheckBox showBuildVolumeCheckBox = new CheckBox(LocalizedString.Get("Show Print Area"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                            showBuildVolumeCheckBox.Checked = false;
                            showBuildVolumeCheckBox.Margin = new BorderDouble(bottom: 5);
                            showBuildVolumeCheckBox.CheckedStateChanged += (sender, e) =>
                            {
                                meshViewerWidget.RenderBuildVolume = showBuildVolumeCheckBox.Checked;
                            };
                            viewOptionContainer.AddChild(showBuildVolumeCheckBox);
                        }

#if __ANDROID__
                        UserSettings.Instance.set("defaultRenderSetting", RenderTypes.Shaded.ToString());
#else
                        CreateRenderTypeRadioButtons(viewOptionContainer);
#endif
                    }
                    buttonRightPanel.AddChild(viewOptionContainer);
                }

                autoArrangeButton = whiteButtonFactory.Generate(LocalizedString.Get("Auto-Arrange"), centerText: true);
                autoArrangeButton.Cursor = Cursors.Hand;
                buttonRightPanel.AddChild(autoArrangeButton);
                autoArrangeButton.Visible = false;
                autoArrangeButton.Click += (sender, e) =>
                {
                    AutoArrangePartsInBackground();
                };

                GuiWidget verticalSpacer = new GuiWidget();
                verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
                buttonRightPanel.AddChild(verticalSpacer);
            }

            buttonRightPanel.Padding = new BorderDouble(6, 6);
            buttonRightPanel.Margin = new BorderDouble(0, 1);
            buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

            return buttonRightPanel;
        }

        private void AddSaveAndSaveAs(FlowLayoutWidget flowToAddTo)
        {
            TupleList<string, Func<bool>> buttonList = new TupleList<string, Func<bool>>();
            buttonList.Add("Save", () =>
            {
                MergeAndSavePartsToCurrentMeshFile();
                return true;
            });
            buttonList.Add("Save As", () =>
            {
                if (saveAsWindow == null)
                {
                    saveAsWindow = new SaveAsWindow(MergeAndSavePartsToNewMeshFile);
                    saveAsWindow.Closed += (sender2, e2) =>
                    {
                        saveAsWindow = null;
                    };
                }
                else
                {
                    saveAsWindow.BringToFront();
                }
                return true;
            });
            SplitButtonFactory splitButtonFactory = new SplitButtonFactory();
            splitButtonFactory.FixedHeight = 40;
			saveButtons = splitButtonFactory.Generate(buttonList, Direction.Up,imageName:"icon_save_32x32.png");
            saveButtons.Visible = false;

            saveButtons.Margin = new BorderDouble();
            saveButtons.VAnchor |= VAnchor.ParentCenter;

            flowToAddTo.AddChild(saveButtons);
        }

        public void PartHasBeenChanged()
        {
            saveButtons.Visible = true;
        }

        private FlowLayoutWidget CreateSaveButtons()
        {
            FlowLayoutWidget saveButtons = new FlowLayoutWidget();

            //Create Save Button
            double oldWidth = whiteButtonFactory.FixedWidth;
            whiteButtonFactory.FixedWidth = 56;
            Button saveButton = whiteButtonFactory.Generate("Save".Localize(), centerText: true);
            saveButton.Cursor = Cursors.Hand;
            saveButtons.AddChild(saveButton);
            saveButton.Click += (sender, e) =>
            {
                MergeAndSavePartsToCurrentMeshFile();
            };

            //Create Save As Button 	
            whiteButtonFactory.FixedWidth = SideBarButtonWidth - whiteButtonFactory.FixedWidth - 2;
            Button saveAsButton = whiteButtonFactory.Generate("Save As".Localize(), centerText: true);
            whiteButtonFactory.FixedWidth = oldWidth;
            saveAsButton.Cursor = Cursors.Hand;
            saveButtons.AddChild(saveAsButton);
            saveAsButton.Click += (sender, e) =>
            {
                if (saveAsWindow == null)
                {
                    saveAsWindow = new SaveAsWindow(MergeAndSavePartsToNewMeshFile);
                    saveAsWindow.Closed += (sender2, e2) =>
                    {
                        saveAsWindow = null;
                    };
                }
                else
                {
                    saveAsWindow.BringToFront();
                }
            };

            saveButtons.Visible = false;

            return saveButtons;
        }

        private void AddScaleControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> scaleControls = new List<GuiWidget>();
            transformControls.Add("Scale", scaleControls);

            // Put in the scale ratio edit field
            {
                FlowLayoutWidget scaleRatioContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
                scaleRatioContainer.HAnchor = HAnchor.ParentLeftRight;
                scaleRatioContainer.Padding = new BorderDouble(5);

                string scaleRatioLabelText = "Ratio".Localize();
                string scaleRatioLabelTextFull = "{0}:".FormatWith(scaleRatioLabelText);
                TextWidget scaleRatioLabel = new TextWidget(scaleRatioLabelTextFull, textColor: ActiveTheme.Instance.PrimaryTextColor);
                scaleRatioLabel.Margin = new BorderDouble(0, 0, 3, 0);
                scaleRatioLabel.VAnchor = VAnchor.ParentCenter;
                scaleRatioContainer.AddChild(scaleRatioLabel);

                scaleRatioContainer.AddChild(new HorizontalSpacer());

                scaleRatioControl = new MHNumberEdit(1, pixelWidth: 50, allowDecimals: true, increment: .05);
                scaleRatioControl.SelectAllOnFocus = true;
                scaleRatioControl.VAnchor = VAnchor.ParentCenter;
                scaleRatioContainer.AddChild(scaleRatioControl);
                scaleRatioControl.ActuallNumberEdit.KeyPressed += (sender, e) =>
                {
                    SetApplyScaleVisability(this, null);
                };

                scaleRatioControl.ActuallNumberEdit.KeyDown += (sender, e) =>
                {
                    SetApplyScaleVisability(this, null);
                };

                scaleRatioControl.ActuallNumberEdit.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
                {
                    ApplyScaleFromEditField();
                };

                scaleRatioContainer.AddChild(CreateScaleDropDownMenu());

                buttonPanel.AddChild(scaleRatioContainer);

                scaleControls.Add(scaleRatioControl);
            }

            applyScaleButton = whiteButtonFactory.Generate("Apply Scale".Localize(), centerText: true);
            applyScaleButton.Visible = false;
            applyScaleButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(applyScaleButton);

            scaleControls.Add(applyScaleButton);
            applyScaleButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                ApplyScaleFromEditField();
            };

            // add in the dimensions
            {
                buttonPanel.AddChild(createAxisScalingControl("x", 0));
                buttonPanel.AddChild(createAxisScalingControl("y", 1));
                buttonPanel.AddChild(createAxisScalingControl("z", 2));

                uniformScale = new CheckBox("Lock Ratio".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
                uniformScale.Checked = true;

                FlowLayoutWidget leftToRight = new FlowLayoutWidget();
                leftToRight.Padding = new BorderDouble(5, 3);

                leftToRight.AddChild(uniformScale);
                buttonPanel.AddChild(leftToRight);
            }

            buttonPanel.AddChild(generateHorizontalRule());
        }

        CheckBox uniformScale;
        EditableNumberDisplay[] sizeDisplay = new EditableNumberDisplay[3];
        private GuiWidget createAxisScalingControl(string axis, int axisIndex)
        {
            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Padding = new BorderDouble(5, 3);

            TextWidget sizeDescription = new TextWidget("{0}:".FormatWith(axis), textColor: ActiveTheme.Instance.PrimaryTextColor);
            sizeDescription.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(sizeDescription);

            sizeDisplay[axisIndex] = new EditableNumberDisplay(textImageButtonFactory, "100", "1000.00");
            sizeDisplay[axisIndex].EditComplete += (sender, e) =>
            {
                if (HaveSelection)
                {
                    SetNewModelSize(sizeDisplay[axisIndex].GetValue(), axisIndex);
                    sizeDisplay[axisIndex].SetDisplayString("{0:0.00}".FormatWith(SelectedMeshGroup.GetAxisAlignedBoundingBox().Size[axisIndex]));
                    UpdateSizeInfo();
                }
                else
                {
                    sizeDisplay[axisIndex].SetDisplayString("---");
                }
            };

            leftToRight.AddChild(sizeDisplay[axisIndex]);

            return leftToRight;
        }

        void SetNewModelSize(double sizeInMm, int axis)
        {
            if (HaveSelection)
            {
                // because we remove any current scale before we change to a new one we only get the size of the base mesh data
                AxisAlignedBoundingBox originalMeshBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox();

                double currentSize = originalMeshBounds.Size[axis];
                double desiredSize = sizeDisplay[axis].GetValue();
                double scaleFactor = 1;
                if (currentSize != 0)
                {
                    scaleFactor = desiredSize / currentSize;
                }

                if (uniformScale.Checked)
                {
                    scaleRatioControl.ActuallNumberEdit.Value = scaleFactor;
                    ApplyScaleFromEditField();
                }
                else
                {
                    ScaleAxis(scaleFactor, axis);
                }
            }
        }

        void UpdateSizeInfo()
        {
            if (sizeDisplay[0] != null
                && SelectedMeshGroup != null)
            {
                AxisAlignedBoundingBox bounds = SelectedMeshGroup.GetAxisAlignedBoundingBox(SelectedMeshGroupTransform.scale);
                sizeDisplay[0].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[0]));
                sizeDisplay[1].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[1]));
                sizeDisplay[2].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[2]));
            }
            else
            {
                sizeDisplay[0].SetDisplayString("---");
                sizeDisplay[1].SetDisplayString("---");
                sizeDisplay[2].SetDisplayString("---");
            }
        }

        private void SetApplyScaleVisability(Object sender, EventArgs e)
        {
            if (HaveSelection)
            {
                double scale = scaleRatioControl.ActuallNumberEdit.Value;
                if (scale != MeshGroupExtraData[SelectedMeshGroupIndex].currentScale[0]
                    || scale != MeshGroupExtraData[SelectedMeshGroupIndex].currentScale[1]
                    || scale != MeshGroupExtraData[SelectedMeshGroupIndex].currentScale[2])
                {
                    applyScaleButton.Visible = true;
                }
                else
                {
                    applyScaleButton.Visible = false;
                }
            }

            UpdateSizeInfo();
        }

        private DropDownMenu CreateScaleDropDownMenu()
        {
            DropDownMenu presetScaleMenu = new DropDownMenu("", Direction.Down);
            presetScaleMenu.NormalArrowColor = ActiveTheme.Instance.PrimaryTextColor;
            presetScaleMenu.HoverArrowColor = ActiveTheme.Instance.PrimaryTextColor;
            presetScaleMenu.MenuAsWideAsItems = false;
            presetScaleMenu.AlignToRightEdge = true;
            //presetScaleMenu.OpenOffset = new Vector2(-50, 0);
            presetScaleMenu.HAnchor = HAnchor.None;
            presetScaleMenu.VAnchor = VAnchor.None;
            presetScaleMenu.Width = 25;
            presetScaleMenu.Height = scaleRatioControl.Height + 2;

            presetScaleMenu.AddItem("mm to in (.0393)");
            presetScaleMenu.AddItem("in to mm (25.4)");
            presetScaleMenu.AddItem("mm to cm (.1)");
            presetScaleMenu.AddItem("cm to mm (10)");
            string resetLable = "reset".Localize();
            string resetLableFull = "{0} (1)".FormatWith(resetLable);
            presetScaleMenu.AddItem(resetLableFull);

            presetScaleMenu.SelectionChanged += (sender, e) =>
            {
                double scale = 1;
                switch (presetScaleMenu.SelectedIndex)
                {
                    case 0:
                        scale = 1.0 / 25.4;
                        break;
                    case 1:
                        scale = 25.4;
                        break;
                    case 2:
                        scale = .1;
                        break;
                    case 3:
                        scale = 10;
                        break;
                    case 4:
                        scale = 1;
                        break;
                }

                scaleRatioControl.ActuallNumberEdit.Value = scale;
                ApplyScaleFromEditField();
            };

            return presetScaleMenu;
        }

        private void CreateRenderTypeRadioButtons(FlowLayoutWidget viewOptionContainer)
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

        private GuiWidget generateHorizontalRule()
        {
            GuiWidget horizontalRule = new GuiWidget();
            horizontalRule.Height = 1;
            horizontalRule.Margin = new BorderDouble(0, 1, 0, 3);
            horizontalRule.HAnchor = HAnchor.ParentLeftRight;
            horizontalRule.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
            return horizontalRule;
        }

        private void ApplyScaleFromEditField()
        {
            if (HaveSelection)
            {
                double scale = scaleRatioControl.ActuallNumberEdit.Value;
                if (scale > 0)
                {
                    ScaleAxis(scale, 0);
                    ScaleAxis(scale, 1);
                    ScaleAxis(scale, 2);
                }
            }
        }

        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension))
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragEnter(fileDropEventArgs);
        }

        public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension))
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragOver(fileDropEventArgs);
        }

        public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
        {
            pendingPartsToLoad.Clear();
            foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(droppedFileName).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension))
                {
                    pendingPartsToLoad.Add(droppedFileName);
                }
            }

            bool enterEditModeBeforeAddingParts = enterEditButtonsContainer.Visible == true;
            if (enterEditModeBeforeAddingParts)
            {
                EnterEditAndCreateSelectionData();
            }
            else
            {
                LoadAndAddPartsToPlate(pendingPartsToLoad.ToArray());
            }

            base.OnDragDrop(fileDropEventArgs);
        }

        private void ScaleAxis(double scaleIn, int axis)
        {
            AxisAlignedBoundingBox originalMeshBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox();
            AxisAlignedBoundingBox scaledBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox(SelectedMeshGroupTransform.scale);

            // first we remove any scale we have applied and then scale to the new value
            Vector3 axisRemoveScalings = new Vector3();
            axisRemoveScalings.x = scaledBounds.Size.x / originalMeshBounds.Size.x;
            axisRemoveScalings.y = scaledBounds.Size.y / originalMeshBounds.Size.y;
            axisRemoveScalings.z = scaledBounds.Size.z / originalMeshBounds.Size.z;

            Matrix4X4 removeScaleMatrix = Matrix4X4.CreateScale(1 / axisRemoveScalings);

            Vector3 newScale = MeshGroupExtraData[SelectedMeshGroupIndex].currentScale;
            newScale[axis] = scaleIn;
            Matrix4X4 totalScale = removeScaleMatrix * Matrix4X4.CreateScale(newScale);

            ScaleRotateTranslate scale = SelectedMeshGroupTransform;
            scale.scale *= totalScale;
            SelectedMeshGroupTransform = scale;

            PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
            PartHasBeenChanged();
            Invalidate();
            MeshGroupExtraData[SelectedMeshGroupIndex].currentScale[axis] = scaleIn;
            SetApplyScaleVisability(this, null);
        }

        private void AddRotateControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> rotateControls = new List<GuiWidget>();
            transformControls.Add("Rotate".Localize(), rotateControls);

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
                if (SelectedMeshGroupIndex != -1)
                {
                    double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                    // rotate it
                    ScaleRotateTranslate rotated = SelectedMeshGroupTransform;
                    rotated.rotation *= Matrix4X4.CreateRotationX(radians);
                    SelectedMeshGroupTransform = rotated;

                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
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
                if (SelectedMeshGroupIndex != -1)
                {
                    double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                    // rotate it
                    ScaleRotateTranslate rotated = SelectedMeshGroupTransform;
                    rotated.rotation *= Matrix4X4.CreateRotationY(radians);
                    SelectedMeshGroupTransform = rotated;
                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
                    saveButtons.Visible = true;
                    Invalidate();
                }
            };

            Button rotateZButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredZ = new TextWidget("Z", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredZ.Margin = new BorderDouble(3, 0, 0, 0); centeredZ.AnchorCenter(); rotateZButton.AddChild(centeredZ);
            rotateButtonContainer.AddChild(rotateZButton);
            rotateControls.Add(rotateZButton);
            rotateZButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                if (SelectedMeshGroupIndex != -1)
                {
                    double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                    // rotate it
                    ScaleRotateTranslate rotated = SelectedMeshGroupTransform;
                    rotated.rotation *= Matrix4X4.CreateRotationZ(radians);
                    SelectedMeshGroupTransform = rotated;

                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);
                    PartHasBeenChanged();
                    Invalidate();
                }
            };

            buttonPanel.AddChild(rotateButtonContainer);

            Button layFlatButton = whiteButtonFactory.Generate("Align to Bed".Localize(), centerText: true);
            layFlatButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(layFlatButton);

            layFlatButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                if (SelectedMeshGroupIndex != -1)
                {
                    MakeLowestFaceFlat(SelectedMeshGroupIndex);

                    PartHasBeenChanged();
                    Invalidate();
                }
            };

            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddMirrorControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> mirrorControls = new List<GuiWidget>();
            transformControls.Add("Mirror", mirrorControls);

            textImageButtonFactory.FixedWidth = EditButtonHeight;

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeftRight;

            Button mirrorXButton = textImageButtonFactory.Generate("X", centerText: true);
            buttonContainer.AddChild(mirrorXButton);
            mirrorControls.Add(mirrorXButton);
            mirrorXButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                if (SelectedMeshGroupIndex != -1)
                {
                    SelectedMeshGroup.ReverseFaceEdges();

                    ScaleRotateTranslate scale = SelectedMeshGroupTransform;
                    scale.scale *= Matrix4X4.CreateScale(-1, 1, 1);
                    SelectedMeshGroupTransform = scale;

                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);

                    PartHasBeenChanged();
                    Invalidate();
                }
            };

            Button mirrorYButton = textImageButtonFactory.Generate("Y", centerText: true);
            buttonContainer.AddChild(mirrorYButton);
            mirrorControls.Add(mirrorYButton);
            mirrorYButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                if (SelectedMeshGroupIndex != -1)
                {
                    SelectedMeshGroup.ReverseFaceEdges();

                    ScaleRotateTranslate scale = SelectedMeshGroupTransform;
                    scale.scale *= Matrix4X4.CreateScale(1, -1, 1);
                    SelectedMeshGroupTransform = scale;

                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);

                    PartHasBeenChanged();
                    Invalidate();
                }
            };

            Button mirrorZButton = textImageButtonFactory.Generate("Z", centerText: true);
            buttonContainer.AddChild(mirrorZButton);
            mirrorControls.Add(mirrorZButton);
            mirrorZButton.Click += (object sender, EventArgs mouseEvent) =>
            {
                if (SelectedMeshGroupIndex != -1)
                {
                    SelectedMeshGroup.ReverseFaceEdges();

                    ScaleRotateTranslate scale = SelectedMeshGroupTransform;
                    scale.scale *= Matrix4X4.CreateScale(1, 1, -1);
                    SelectedMeshGroupTransform = scale;

                    PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);

                    PartHasBeenChanged();
                    Invalidate();
                }
            };
            buttonPanel.AddChild(buttonContainer);
            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        ObservableCollection<GuiWidget> extruderButtons = new ObservableCollection<GuiWidget>();
        void AddMaterialControls(FlowLayoutWidget buttonPanel)
        {
            extruderButtons.Clear();
            for (int extruderIndex = 0; extruderIndex < ActiveSliceSettings.Instance.ExtruderCount; extruderIndex++)
            {
                FlowLayoutWidget colorSelectionContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
                colorSelectionContainer.HAnchor = HAnchor.ParentLeftRight;
                colorSelectionContainer.Padding = new BorderDouble(5);

                string colorLabelText = "Extruder {0}".Localize().FormatWith(extruderIndex + 1);
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

#if false // no color swatch for this build
                GuiWidget colorSwatch = new GuiWidget(15,15);
                colorSwatch.Draw += (sender, e) =>
                {
                    DrawEventArgs drawEvent = e as DrawEventArgs;
                    GuiWidget widget = sender as GuiWidget;
                    if(widget != null && drawEvent != null && drawEvent.graphics2D != null)
                    {
                        drawEvent.graphics2D.Rectangle(widget.LocalBounds, RGBA_Bytes.Black);
                    }
                };
                colorSwatch.BackgroundColor = MeshViewerWidget.GetMaterialColor(extruderIndexLocal+1);
                Button swatchButton = new Button(0, 0, colorSwatch);
                swatchButton.Click += (sender, e) =>
                {
                    nextColor++;
                    RGBA_Bytes color = SelectionColors[nextColor % SelectionColors.Length];
                    MeshViewerWidget.SetMaterialColor(extruderIndexLocal+1, color);
                    colorSwatch.BackgroundColor = color;
                };
                colorSelectionContainer.AddChild(swatchButton);
#endif

                buttonPanel.AddChild(colorSelectionContainer);
            }
        }

        RGBA_Bytes[] SelectionColors = new RGBA_Bytes[] { new RGBA_Bytes(131, 4, 66), new RGBA_Bytes(227, 31, 61), new RGBA_Bytes(255, 148, 1), new RGBA_Bytes(247, 224, 23), new RGBA_Bytes(143, 212, 1) };

        private void AddHandlers()
        {
            expandViewOptions.CheckedStateChanged += expandViewOptions_CheckedStateChanged;
            expandMirrorOptions.CheckedStateChanged += expandMirrorOptions_CheckedStateChanged;
            if (expandMaterialOptions != null)
            {
                expandMaterialOptions.CheckedStateChanged += expandMaterialOptions_CheckedStateChanged;
            }
            expandRotateOptions.CheckedStateChanged += expandRotateOptions_CheckedStateChanged;
            expandScaleOptions.CheckedStateChanged += expandScaleOptions_CheckedStateChanged;

            SelectionChanged += SetApplyScaleVisability;
        }

        private void MergeAndSavePartsToNewMeshFile(SaveAsWindow.SaveAsReturnInfo returnInfo)
        {
            if (MeshGroups.Count > 0)
            {
                string progressSavingPartsLabel = "Saving".Localize();
                string progressSavingPartsLabelFull = "{0}:".FormatWith(progressSavingPartsLabel);
                processingProgressControl.ProcessType = progressSavingPartsLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker mergeAndSavePartsBackgroundWorker = new BackgroundWorker();

                mergeAndSavePartsBackgroundWorker.DoWork += new DoWorkEventHandler(mergeAndSavePartsBackgroundWorker_DoWork);
                mergeAndSavePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mergeAndSavePartsBackgroundWorker_RunWorkerCompleted);

                mergeAndSavePartsBackgroundWorker.RunWorkerAsync(returnInfo);
            }
        }

        public delegate void AfterSaveCallback();
        AfterSaveCallback afterSaveCallback = null;
        private void MergeAndSavePartsToCurrentMeshFile(AfterSaveCallback eventToCallAfterSave = null)
        {
            afterSaveCallback = eventToCallAfterSave;

            if (MeshGroups.Count > 0)
            {
                string progressSavingPartsLabel = "Saving".Localize();
                string progressSavingPartsLabelFull = "{0}:".FormatWith(progressSavingPartsLabel);
                processingProgressControl.ProcessType = progressSavingPartsLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker mergeAndSavePartsBackgroundWorker = new BackgroundWorker();

                mergeAndSavePartsBackgroundWorker.DoWork += new DoWorkEventHandler(mergeAndSavePartsBackgroundWorker_DoWork);
                mergeAndSavePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mergeAndSavePartsBackgroundWorker_RunWorkerCompleted);

                mergeAndSavePartsBackgroundWorker.RunWorkerAsync(printItemWrapper);
            }
        }

        void mergeAndSavePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            SaveAsWindow.SaveAsReturnInfo returnInfo = e.Argument as SaveAsWindow.SaveAsReturnInfo;

            if (returnInfo != null)
            {
                printItemWrapper = returnInfo.printItemWrapper;
            }

            // we sent the data to the asynch lists but we will not pull it back out (only use it as a temp holder).
            PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            try
            {
                // push all the transforms into the meshes
                for (int i = 0; i < asynchMeshGroups.Count; i++)
                {
                    asynchMeshGroups[i].Transform(asynchMeshGroupTransforms[i].TotalTransform);

                    bool continueProcessing;
                    BackgroundWorker_ProgressChanged((i + 1) * .4 / asynchMeshGroups.Count, "", out continueProcessing);
                }

                LibraryData.SaveToLibraryFolder(printItemWrapper, asynchMeshGroups);
            }
            catch (System.UnauthorizedAccessException)
            {
                UiThread.RunOnIdle((state) =>
                {
                    //Do something special when unauthorized?
                    StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
                });
            }
            catch
            {
                UiThread.RunOnIdle((state) =>
                {
                    StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
                });
            }

            e.Result = e.Argument;
        }

        void mergeAndSavePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SaveAsWindow.SaveAsReturnInfo returnInfo = e.Result as SaveAsWindow.SaveAsReturnInfo;

            if (returnInfo != null)
            {
                QueueData.Instance.AddItem(printItemWrapper);
                if (!PrinterConnectionAndCommunication.Instance.PrintIsActive)
                {
                    QueueData.Instance.SelectedIndex = QueueData.Instance.Count-1;
                }

                if (returnInfo.placeInLibrary)
                {
                    LibraryData.Instance.AddItem(printItemWrapper);
                }
            }

            if (WidgetHasBeenClosed)
            {
                return;
            }
            UnlockEditControls();
            // NOTE: we do not pull the data back out of the asynch lists.
            saveButtons.Visible = false;

            if (afterSaveCallback != null)
            {
                afterSaveCallback();
            }
        }

        void expandViewOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            if (viewOptionContainer.Visible != expandViewOptions.Checked)
            {
                if (expandViewOptions.Checked == true)
                {
                    expandScaleOptions.Checked = false;
                    expandRotateOptions.Checked = false;
                    expandMirrorOptions.Checked = false;
                    expandMaterialOptions.Checked = false;
                }
                viewOptionContainer.Visible = expandViewOptions.Checked;
            }
        }

        void expandMirrorOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            if (mirrorOptionContainer.Visible != expandMirrorOptions.Checked)
            {
                if (expandMirrorOptions.Checked == true)
                {
                    expandScaleOptions.Checked = false;
                    expandRotateOptions.Checked = false;
                    expandViewOptions.Checked = false;
                    expandMaterialOptions.Checked = false;
                }
                mirrorOptionContainer.Visible = expandMirrorOptions.Checked;
            }
        }

        void expandMaterialOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            if (expandMaterialOptions.Checked == true)
            {
                expandScaleOptions.Checked = false;
                expandRotateOptions.Checked = false;
                expandViewOptions.Checked = false;
            }
            materialOptionContainer.Visible = expandMaterialOptions.Checked;
        }

        void expandRotateOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            if (rotateOptionContainer.Visible != expandRotateOptions.Checked)
            {
                if (expandRotateOptions.Checked == true)
                {
                    expandViewOptions.Checked = false;
                    expandScaleOptions.Checked = false;
                    expandMirrorOptions.Checked = false;
                    expandMaterialOptions.Checked = false;
                }
                rotateOptionContainer.Visible = expandRotateOptions.Checked;
            }
        }

        void expandScaleOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            if (scaleOptionContainer.Visible != expandScaleOptions.Checked)
            {
                if (expandScaleOptions.Checked == true)
                {
                    expandViewOptions.Checked = false;
                    expandRotateOptions.Checked = false;
                    expandMirrorOptions.Checked = false;
                    expandMaterialOptions.Checked = false;
                }
                scaleOptionContainer.Visible = expandScaleOptions.Checked;
            }
        }

        bool scaleQueueMenu_Click()
        {
            return true;
        }

        bool rotateQueueMenu_Click()
        {
            return true;
        }

        private void MakeLowestFaceFlat(int indexToLayFlat)
        {
            Vertex lowestVertex = MeshGroups[indexToLayFlat].Meshes[0].Vertices[0];
            Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, MeshGroupTransforms[indexToLayFlat].rotation);
            Mesh meshToLayFlat = null;
            foreach (Mesh meshToCheck in MeshGroups[indexToLayFlat].Meshes)
            {
                // find the lowest point on the model
                for (int testIndex = 1; testIndex < meshToCheck.Vertices.Count; testIndex++)
                {
                    Vertex vertex = meshToCheck.Vertices[testIndex];
                    Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshGroupTransforms[indexToLayFlat].rotation);
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
                        Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, MeshGroupTransforms[indexToLayFlat].rotation);
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
                Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshGroupTransforms[indexToLayFlat].rotation);
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
                ScaleRotateTranslate rotated = SelectedMeshGroupTransform;
                rotated.rotation *= partLevelMatrix;
                SelectedMeshGroupTransform = rotated;

                PlatingHelper.PlaceMeshGroupOnBed(MeshGroups, MeshGroupTransforms, SelectedMeshGroupIndex);

                PartHasBeenChanged();
                Invalidate();
            }
        }
    }
}
