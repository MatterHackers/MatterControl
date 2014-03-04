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
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;


using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.MarchingSquares;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.Localizations; //Added Namespace


using ClipperLib;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class Cover : GuiWidget
    {
        public Cover(HAnchor hAnchor = HAnchor.None, VAnchor vAnchor = VAnchor.None)
            : base(hAnchor, vAnchor)
        {
        }
    }

    public class View3DTransformPart : PartPreviewBaseWidget
    {
        MeshViewerWidget meshViewerWidget;
        Cover buttonRightPanelDisabledCover;
        FlowLayoutWidget buttonRightPanel;

        FlowLayoutWidget viewOptionContainer;
        FlowLayoutWidget rotateOptionContainer;
        FlowLayoutWidget scaleOptionContainer;

        ProgressControl processingProgressControl;
        FlowLayoutWidget editPlateButtonsContainer;
        RadioButton rotateViewButton;
        Button editPlateButton;
        GuiWidget viewControlsSeparator;
        RadioButton partSelectButton;

        Dictionary<string, List<GuiWidget>> transformControls = new Dictionary<string, List<GuiWidget>>();

        CheckBox showBedCheckBox;
        CheckBox showBuildVolumeCheckBox;
        CheckBox showWireframeCheckBox;

        CheckBox expandViewOptions;
        CheckBox expandRotateOptions;
        CheckBox expandScaleOptions;

        Button autoArrangeButton;
        Button saveButton;
        Button closeButton;
        Button applyScaleButton;

        PrintItemWrapper printItemWrapper;

        List<Mesh> asynchMeshesList = new List<Mesh>();
        List<Matrix4X4> asynchMeshTransforms = new List<Matrix4X4>();
        List<PlatingMeshData> asynchPlatingDataList = new List<PlatingMeshData>();

        List<PlatingMeshData> MeshExtraData;

        public Matrix4X4 SelectedMeshTransform
        {
            get { return meshViewerWidget.SelectedMeshTransform; }
            set { meshViewerWidget.SelectedMeshTransform = value; }
        }

        public Mesh SelectedMesh
        {
            get { return meshViewerWidget.SelectedMesh; }
        }

        public int SelectedMeshIndex
        {
            get { return meshViewerWidget.SelectedMeshIndex; }
            set { meshViewerWidget.SelectedMeshIndex = value; }
        }

        public List<Mesh> Meshes
        {
            get { return meshViewerWidget.Meshes; }
        }

        public List<Matrix4X4> MeshTransforms
        {
            get { return meshViewerWidget.MeshTransforms; }
        }

        internal struct MeshSelectInfo
        {
            internal bool downOnPart;
            internal PlaneShape hitPlane;
            internal Vector3 planeDownHitPos;
            internal Vector3 lastMoveDelta;
        }

        private bool FindMeshHitPosition(Vector2 screenPosition, out int meshHitIndex)
        {
            meshHitIndex = 0;
            if (MeshExtraData.Count == 0 || MeshExtraData[0].traceableData == null)
            {
                return false;
            }

            List<IRayTraceable> mesheTraceables = new List<IRayTraceable>();
            for (int i = 0; i < MeshExtraData.Count; i++)
            {
                mesheTraceables.Add(new Transform(MeshExtraData[i].traceableData, MeshTransforms[i]));
            }
            IRayTraceable allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

            Ray ray = meshViewerWidget.TrackballTumbleWidget.LastScreenRay;
            IntersectInfo info = allObjects.GetClosestIntersection(ray);
            if (info != null)
            {
                meshSelectInfo.planeDownHitPos = info.hitPosition;
                meshSelectInfo.lastMoveDelta = new Vector3();

                for (int i = 0; i < MeshExtraData.Count; i++)
                {
                    List<IRayTraceable> insideBounds = new List<IRayTraceable>();
                    MeshExtraData[i].traceableData.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
                    if (insideBounds.Contains(info.closestHitObject))
                    {
                        meshHitIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
        MeshSelectInfo meshSelectInfo;
        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
            if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
            {
                if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
                {
                    partSelectButton.ClickButton(null);
                    int meshHitIndex;
                    if (FindMeshHitPosition(mouseEvent.Position, out meshHitIndex))
                    {
                        meshSelectInfo.hitPlane = new PlaneShape(Vector3.UnitZ, meshSelectInfo.planeDownHitPos.z, null);
                        SelectedMeshIndex = meshHitIndex;
                        transformOnMouseDown = SelectedMeshTransform;
                        Invalidate();
                        meshSelectInfo.downOnPart = true;

                        double scale = scaleRatioControl.ActuallNumberEdit.Value;
                        if (MeshExtraData[meshHitIndex].currentScale == scale)
                        {
                            applyScaleButton.Visible = false;
                        }
                        else
                        {
                            applyScaleButton.Visible = true;
                        }
                    }
                }
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None && meshSelectInfo.downOnPart)
            {
                Ray ray = meshViewerWidget.TrackballTumbleWidget.LastScreenRay;
                IntersectInfo info = meshSelectInfo.hitPlane.GetClosestIntersection(ray);
                if (info != null)
                {
                    Vector3 delta = info.hitPosition - meshSelectInfo.planeDownHitPos;

                    Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(new Vector3(-meshSelectInfo.lastMoveDelta));
                    totalTransfrom *= Matrix4X4.CreateTranslation(new Vector3(delta));
                    meshSelectInfo.lastMoveDelta = delta;
                    SelectedMeshTransform *= totalTransfrom;
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
                saveButton.Visible = true;
            }

            meshSelectInfo.downOnPart = false;

            base.OnMouseUp(mouseEvent);
        }

        public View3DTransformPart(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, MeshViewerWidget.BedShape bedShape)
        {
            MeshExtraData = new List<PlatingMeshData>();
            MeshExtraData.Add(new PlatingMeshData());

            this.printItemWrapper = printItemWrapper;

            FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

            FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
            centerPartPreviewAndControls.AnchorAll();

            GuiWidget viewArea = new GuiWidget();
            viewArea.AnchorAll();
            {
                meshViewerWidget = new MeshViewerWidget(viewerVolume, 1, bedShape);
                SetMeshViewerDisplayTheme();
                meshViewerWidget.AnchorAll();
            }
            viewArea.AddChild(meshViewerWidget);

            centerPartPreviewAndControls.AddChild(viewArea);
            mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);

            FlowLayoutWidget buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
            buttonBottomPanel.Padding = new BorderDouble(3, 3);
            buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            buttonRightPanel = CreateRightButtonPannel(viewerVolume.y);

            CreateOptionsContent();

            // add in the plater tools
            {
                FlowLayoutWidget editToolBar = new FlowLayoutWidget();

				string progressFindPartsLbl = new LocalizedString ("Finding Parts").Translated;
				string progressFindPartsLblFull = string.Format ("{0}:", progressFindPartsLbl);

				processingProgressControl = new ProgressControl(progressFindPartsLblFull);
                processingProgressControl.VAnchor = Agg.UI.VAnchor.ParentCenter;
                editToolBar.AddChild(processingProgressControl);
                editToolBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                processingProgressControl.Visible = false;

				editPlateButton = textImageButtonFactory.Generate(new LocalizedString("Edit").Translated);
                editToolBar.AddChild(editPlateButton);

                editPlateButtonsContainer = new FlowLayoutWidget();
                editPlateButtonsContainer.Visible = false;

				Button addButton = textImageButtonFactory.Generate(new LocalizedString("Add").Translated, "icon_circle_plus.png");
                addButton.Margin = new BorderDouble(right: 10);
                editPlateButtonsContainer.AddChild(addButton);
                addButton.Click += (sender, e) =>
                {
                    UiThread.RunOnIdle((state) =>
                    {
                        OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file|*.stl", multiSelect: true);

                        FileDialog.OpenFileDialog(ref openParams);
                        LoadAndAddPartsToPlate(openParams.FileNames);
                    });
                };

				Button copyButton = textImageButtonFactory.Generate(new LocalizedString("Copy").Translated);
                editPlateButtonsContainer.AddChild(copyButton);
                copyButton.Click += (sender, e) =>
                {
                    MakeCopyOfMesh();
                };

				Button deleteButton = textImageButtonFactory.Generate(new LocalizedString("Delete").Translated);
                deleteButton.Margin = new BorderDouble(left: 20);
                editPlateButtonsContainer.AddChild(deleteButton);
                deleteButton.Click += (sender, e) =>
                {
                    DeleteSelectedMesh();
                };

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
                                SelectedMeshTransform = transformOnMouseDown;
                                Invalidate();
                            }
                        }
                    }
                };

                editToolBar.AddChild(editPlateButtonsContainer);
                buttonBottomPanel.AddChild(editToolBar);

                editPlateButton.Click += (sender, e) =>
                {
                    editPlateButton.Visible = false;

                    EnterEditAndSplitIntoMeshes();
                };
            }

            autoArrangeButton.Click += (sender, e) =>
            {
                AutoArangePartsInBackground();
            };

            GuiWidget buttonRightPanelHolder = new GuiWidget(HAnchor.FitToChildren, VAnchor.ParentBottomTop);
            centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
            buttonRightPanelHolder.AddChild(buttonRightPanel);

            buttonRightPanelDisabledCover = new Cover(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
            buttonRightPanelDisabledCover.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 150);
            buttonRightPanelHolder.AddChild(buttonRightPanelDisabledCover);
            LockEditControls();

            GuiWidget leftRightSpacer = new GuiWidget();
            leftRightSpacer.HAnchor = HAnchor.ParentLeftRight;
            buttonBottomPanel.AddChild(leftRightSpacer);

			closeButton = textImageButtonFactory.Generate(new LocalizedString("Close").Translated);
            buttonBottomPanel.AddChild(closeButton);

            mainContainerTopToBottom.AddChild(buttonBottomPanel);

            this.AddChild(mainContainerTopToBottom);
            this.AnchorAll();

            meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
            AddViewControls();

            AddHandlers();

            if (printItemWrapper != null)
            {
                // don't load the mesh until we get all the rest of the interface built
                meshViewerWidget.LoadMesh(printItemWrapper.FileLocation);
                meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);
            }
        }

        private void MakeCopyOfMesh()
        {
            if (Meshes.Count > 0)
            {
				string makingCopyLabel = new LocalizedString("Making Copy").Translated;
				string makingCopyLabelFull = string.Format ("{0}:", makingCopyLabel);
				processingProgressControl.textWidget.Text = makingCopyLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker copyPartBackgroundWorker = null;
                copyPartBackgroundWorker = new BackgroundWorker();
                copyPartBackgroundWorker.WorkerReportsProgress = true;

                copyPartBackgroundWorker.DoWork += new DoWorkEventHandler(copyPartBackgroundWorker_DoWork);
                copyPartBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                copyPartBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(copyPartBackgroundWorker_RunWorkerCompleted);

                copyPartBackgroundWorker.RunWorkerAsync();
            }
        }

        void copyPartBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            PullMeshDataFromAsynchLists();
            saveButton.Visible = true;
            partSelectButton.ClickButton(null);

            // now set the selection to the new copy
            MeshExtraData[Meshes.Count - 1].currentScale = MeshExtraData[SelectedMeshIndex].currentScale;
            SelectedMeshIndex = Meshes.Count - 1;
        }

        void copyPartBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            PushMeshDataToAsynchLists(true);

            Mesh copyMesh = new Mesh();

            int faceCount = asynchMeshesList[SelectedMeshIndex].Faces.Count;
            for (int i = 0; i < faceCount; i++)
            {
                Face face = asynchMeshesList[SelectedMeshIndex].Faces[i];
                List<Vertex> faceVertices = new List<Vertex>();
                foreach (FaceEdge faceEdgeToAdd in face.FaceEdgeIterator())
                {
                    Vertex newVertex = copyMesh.CreateVertex(faceEdgeToAdd.vertex.Position, true);
                    faceVertices.Add(newVertex);
                }

                int nextPercent = (i + 1) * 80 / faceCount;
                backgroundWorker.ReportProgress(nextPercent);

                copyMesh.CreateFace(faceVertices.ToArray(), true);
            }

            PlatingHelper.FindPositionForPartAndAddToPlate(copyMesh, SelectedMeshTransform, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);
            PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, asynchMeshesList.Count-1);

            backgroundWorker.ReportProgress(95);
        }

        private void AutoArangePartsInBackground()
        {
            if (Meshes.Count > 0)
            {
				string progressArrangeParts = new LocalizedString ("Arranging Parts").Translated;
				string progressArrangePartsFull = string.Format ("{0}:", progressArrangeParts);
				processingProgressControl.textWidget.Text = progressArrangePartsFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                PushMeshDataToAsynchLists(false);

                BackgroundWorker arrangePartsBackgroundWorker = null;
                arrangePartsBackgroundWorker = new BackgroundWorker();
                arrangePartsBackgroundWorker.WorkerReportsProgress = true;

                arrangePartsBackgroundWorker.DoWork += new DoWorkEventHandler(arrangePartsBackgroundWorker_DoWork);
                arrangePartsBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                arrangePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(arrangePartsBackgroundWorker_RunWorkerCompleted);

                arrangePartsBackgroundWorker.RunWorkerAsync();
            }
        }

        void arrangePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            if (asynchMeshesList.Count > 0)
            {
                BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

                // move them all out of the way
                foreach (Mesh mesh in asynchMeshesList)
                {
                    mesh.Translate(new Vector3(1000, 1000, 0));
                }

                // sort them by size
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    AxisAlignedBoundingBox iAABB = asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]);
                    for (int j = i + 1; j < asynchMeshesList.Count; j++)
                    {
                        AxisAlignedBoundingBox jAABB = asynchMeshesList[j].GetAxisAlignedBoundingBox(asynchMeshTransforms[j]);
                        if (Math.Max(iAABB.XSize, iAABB.YSize) < Math.Max(jAABB.XSize, jAABB.YSize))
                        {
                            PlatingMeshData tempData = asynchPlatingDataList[i];
                            asynchPlatingDataList[i] = asynchPlatingDataList[j];
                            asynchPlatingDataList[j] = tempData;

                            Mesh tempMesh = asynchMeshesList[i];
                            asynchMeshesList[i] = asynchMeshesList[j];
                            asynchMeshesList[j] = tempMesh;

                            Matrix4X4 tempTransform = asynchMeshTransforms[i];
                            asynchMeshTransforms[i] = asynchMeshTransforms[j];
                            asynchMeshTransforms[j] = tempTransform;

                            iAABB = jAABB;
                        }
                    }
                }

                // put them onto the plate (try the center) starting with the biggest and moving down
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    Mesh mesh = asynchMeshesList[i];
                    Vector3 center = mesh.GetAxisAlignedBoundingBox(asynchMeshTransforms[i]).Center;
                    asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(-center);
                    PlatingHelper.MoveMeshToOpenPosition(i, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);

                    // and create the trace info so we can select it
                    PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, i);

                    // and put it on the bed
                    {
                        AxisAlignedBoundingBox bounds = asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]);
                        Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                        asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
                    }

                    int nextPercent = (i + 1) * 100 / asynchMeshesList.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                // and finally center whatever we have as a group
                {
                    AxisAlignedBoundingBox bounds = asynchMeshesList[0].GetAxisAlignedBoundingBox(asynchMeshTransforms[0]);
                    for (int i = 1; i < asynchMeshesList.Count; i++)
                    {
                        bounds = AxisAlignedBoundingBox.Union(bounds, asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]));
                    }

                    Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                    for (int i = 0; i < asynchMeshesList.Count; i++)
                    {
                        asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
                    }
                }
            }
        }

        private void LoadAndAddPartsToPlate(string[] filesToLoad)
        {
            if (Meshes.Count > 0)
            {
				string loadingPartLabel = new LocalizedString("Loading Parts").Translated;
				string loadingPartLabelFull = string.Format("{0}:", loadingPartLabel);
				processingProgressControl.textWidget.Text = loadingPartLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                PushMeshDataToAsynchLists(true);

                BackgroundWorker loadAndAddPartsToPlateBackgroundWorker = null;
                loadAndAddPartsToPlateBackgroundWorker = new BackgroundWorker();
                loadAndAddPartsToPlateBackgroundWorker.WorkerReportsProgress = true;

                loadAndAddPartsToPlateBackgroundWorker.DoWork += new DoWorkEventHandler(loadAndAddPartsToPlateBackgroundWorker_DoWork);
                loadAndAddPartsToPlateBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                loadAndAddPartsToPlateBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted);

                loadAndAddPartsToPlateBackgroundWorker.RunWorkerAsync(filesToLoad);
            }
        }

        private void PushMeshDataToAsynchLists(bool copyTraceInfo)
        {
            asynchMeshesList.Clear();
            asynchMeshTransforms.Clear();
            for (int i = 0; i < Meshes.Count; i++)
            {
                Mesh mesh = Meshes[i];
                asynchMeshesList.Add(new Mesh(mesh));
                asynchMeshTransforms.Add(MeshTransforms[i]);
            }
            asynchPlatingDataList.Clear();
            for (int i = 0; i < MeshExtraData.Count; i++)
            {
                PlatingMeshData meshData = new PlatingMeshData();
                meshData.currentScale = MeshExtraData[i].currentScale;
                if (copyTraceInfo)
                {
                    meshData.traceableData = MeshExtraData[i].traceableData;
                }
                asynchPlatingDataList.Add(meshData);
            }
        }

        void arrangePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            saveButton.Visible = true;
            partSelectButton.ClickButton(null);

            PullMeshDataFromAsynchLists();
        }

        void loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            saveButton.Visible = true;
            partSelectButton.ClickButton(null);

            if (asynchMeshesList.Count == Meshes.Count + 1)
            {
                // if we are only adding one part to the plate set the selection to it
                SelectedMeshIndex = asynchMeshesList.Count - 1;
            }

            PullMeshDataFromAsynchLists();
        }

        private void PullMeshDataFromAsynchLists()
        {
            Meshes.Clear();
            foreach (Mesh mesh in asynchMeshesList)
            {
                Meshes.Add(mesh);
            }
            MeshTransforms.Clear();
            foreach (Matrix4X4 transform in asynchMeshTransforms)
            {
                MeshTransforms.Add(transform);
            }
            MeshExtraData.Clear();
            foreach (PlatingMeshData meshData in asynchPlatingDataList)
            {
                MeshExtraData.Add(meshData);
            }
        }

        void loadAndAddPartsToPlateBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            string[] filesToLoad = e.Argument as string[];
            if (filesToLoad != null && filesToLoad.Length > 0)
            {
                int lastPercent = 0;
                for (int i = 0; i < filesToLoad.Length; i++)
                {
                    int nextPercent = i + 1 * 100 / filesToLoad.Length;
                    int subLength = nextPercent - lastPercent;

                    string loadedFileName = filesToLoad[i];
                    Mesh copyMesh = StlProcessing.Load(System.IO.Path.GetFullPath(loadedFileName));
                    if (copyMesh != null)
                    {
                        int halfNextPercent = (nextPercent - lastPercent) / 2;
                        Mesh[] subMeshes = CreateDiscreteMeshes.SplitIntoMeshes(copyMesh, meshViewerWidget.DisplayVolume, backgroundWorker, lastPercent, halfNextPercent);
                        lastPercent = halfNextPercent;

                        for (int subMeshIndex = 0; subMeshIndex < subMeshes.Length; subMeshIndex++)
                        {
                            Mesh subMesh = subMeshes[subMeshIndex];
                            Vector3 center = subMesh.GetAxisAlignedBoundingBox().Center;
                            center.z = 0;
                            subMesh.Translate(-center);
                            PlatingHelper.FindPositionForPartAndAddToPlate(subMesh, Matrix4X4.Identity, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);
                            PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, asynchMeshesList.Count - 1);

                            backgroundWorker.ReportProgress(lastPercent + subMeshIndex + 1 * subLength / subMeshes.Length);
                        }

                        backgroundWorker.ReportProgress(nextPercent);
                        lastPercent = nextPercent;
                    }
                }
            }
        }

        void meshViewerWidget_LoadDone(object sender, EventArgs e)
        {
            UnlockEditControls();
        }

        void LockEditControls()
        {
            editPlateButtonsContainer.Visible = false;
            buttonRightPanelDisabledCover.Visible = true;
            if (viewControlsSeparator != null)
            {
                viewControlsSeparator.Visible = false;
                partSelectButton.Visible = false;
                if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
                {
                    rotateViewButton.ClickButton(null);
                }
            }
        }

        void UnlockEditControls()
        {
            buttonRightPanelDisabledCover.Visible = false;
            processingProgressControl.Visible = false;

            if (!editPlateButton.Visible)
            {
                viewControlsSeparator.Visible = true;
                partSelectButton.Visible = true;
                editPlateButtonsContainer.Visible = true;
            }
        }

        private void DeleteSelectedMesh()
        {
            // don't ever delet the last mesh
            if (Meshes.Count > 1)
            {
                Meshes.RemoveAt(SelectedMeshIndex);
                MeshExtraData.RemoveAt(SelectedMeshIndex);
                MeshTransforms.RemoveAt(SelectedMeshIndex);
                SelectedMeshIndex = Math.Min(SelectedMeshIndex, Meshes.Count - 1);
                saveButton.Visible = true;
                Invalidate();
            }
        }

        void EnterEditAndSplitIntoMeshes()
        {
            if (Meshes.Count > 0)
            {
                processingProgressControl.Visible = true;
                LockEditControls();

                BackgroundWorker createDiscreteMeshesBackgroundWorker = null;
                createDiscreteMeshesBackgroundWorker = new BackgroundWorker();
                createDiscreteMeshesBackgroundWorker.WorkerReportsProgress = true;

                createDiscreteMeshesBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                createDiscreteMeshesBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(createDiscreteMeshesBackgroundWorker_RunWorkerCompleted);
                createDiscreteMeshesBackgroundWorker.DoWork += new DoWorkEventHandler(createDiscreteMeshesBackgroundWorker_DoWork);

                createDiscreteMeshesBackgroundWorker.RunWorkerAsync();
            }
        }

        void createDiscreteMeshesBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            Mesh[] meshes = CreateDiscreteMeshes.SplitIntoMeshes(SelectedMesh, meshViewerWidget.DisplayVolume, backgroundWorker, 0, 50);

            asynchMeshesList.Clear();
            asynchPlatingDataList.Clear();
            asynchMeshTransforms.Clear();
            for (int i = 0; i < meshes.Length; i++)
            {
                PlatingMeshData newInfo = new PlatingMeshData();
                asynchPlatingDataList.Add(newInfo);
                asynchMeshesList.Add(meshes[i]);
                asynchMeshTransforms.Add(Matrix4X4.Identity);

                PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, i);
                if (meshes.Length > 1)
                {
                    backgroundWorker.ReportProgress(50 + i * 50 / (meshes.Length - 1));
                }
            }
        }

        void createDiscreteMeshesBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // remember the transform we have before spliting so we can put it on each of the parts after spliting.
            Matrix4X4 transformBeforeSplit = SelectedMeshTransform;
            // remove the original mesh and replace it with these new meshes
            PullMeshDataFromAsynchLists();

            for (int i = 0; i < Meshes.Count; i++)
            {
                meshViewerWidget.MeshTransforms[i] = transformBeforeSplit;
            }

            UnlockEditControls();

            autoArrangeButton.Visible = true;
            partSelectButton.ClickButton(null);

            Invalidate();
        }

        void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            processingProgressControl.PercentComplete = e.ProgressPercentage;
        }

        void AddViewControls()
        {
            FlowLayoutWidget transformTypeSelector = new FlowLayoutWidget();
            transformTypeSelector.BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            textImageButtonFactory.FixedHeight = 20;
            textImageButtonFactory.FixedWidth = 20;
            string rotateIconPath = Path.Combine("Icons", "ViewTransformControls", "rotate.png");
            rotateViewButton = textImageButtonFactory.GenerateRadioButton("", rotateIconPath);
            rotateViewButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(rotateViewButton);
            rotateViewButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
            };

            string translateIconPath = Path.Combine("Icons", "ViewTransformControls", "translate.png");
            RadioButton translateButton = textImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(translateButton);
            translateButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
            };

            string scaleIconPath = Path.Combine("Icons", "ViewTransformControls", "scale.png");
            RadioButton scaleButton = textImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(scaleButton);
            scaleButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
            };

            viewControlsSeparator = new GuiWidget(2, 32);
            viewControlsSeparator.Visible = false;
            viewControlsSeparator.BackgroundColor = RGBA_Bytes.White;
            viewControlsSeparator.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(viewControlsSeparator);

            string partSelectIconPath = Path.Combine("Icons", "ViewTransformControls", "partSelect.png");
            partSelectButton = textImageButtonFactory.GenerateRadioButton("", partSelectIconPath);
            partSelectButton.Visible = false;
            partSelectButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(partSelectButton);
            partSelectButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
            };

            transformTypeSelector.Margin = new BorderDouble(5);
            transformTypeSelector.HAnchor |= Agg.UI.HAnchor.ParentLeft;
            transformTypeSelector.VAnchor = Agg.UI.VAnchor.ParentTop;
            AddChild(transformTypeSelector);
            rotateViewButton.Checked = true;
        }

        private void CreateOptionsContent()
        {
            AddRotateControls(rotateOptionContainer);
            AddScaleControls(scaleOptionContainer);
        }

        private FlowLayoutWidget CreateRightButtonPannel(double buildHeight)
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

				expandRotateOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Rotate").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandRotateOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandRotateOptions);

                rotateOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rotateOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                rotateOptionContainer.Visible = false;
                buttonRightPanel.AddChild(rotateOptionContainer);

				expandScaleOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Scale").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandScaleOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandScaleOptions);

                scaleOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                scaleOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                scaleOptionContainer.Visible = false;
                buttonRightPanel.AddChild(scaleOptionContainer);

                // put in the mirror options
                {
					CheckBox expandMirrorOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Mirror").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandMirrorOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandMirrorOptions);

                    FlowLayoutWidget mirrorOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    mirrorOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    mirrorOptionContainer.Visible = false;
                    buttonRightPanel.AddChild(mirrorOptionContainer);

                    expandMirrorOptions.CheckedStateChanged += (object sender, EventArgs e) =>
                    {
                        mirrorOptionContainer.Visible = expandMirrorOptions.Checked;
                    };

                    AddMirrorControls(mirrorOptionContainer);
                }

                // put in the part info display
                if(false)
                {
					CheckBox expandPartInfoOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Part Info").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandPartInfoOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandPartInfoOptions);

                    FlowLayoutWidget PartInfoOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    PartInfoOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    PartInfoOptionContainer.Visible = false;
                    buttonRightPanel.AddChild(PartInfoOptionContainer);

                    expandPartInfoOptions.CheckedStateChanged += (object sender, EventArgs e) =>
                    {
                        PartInfoOptionContainer.Visible = expandPartInfoOptions.Checked;
                    };

                    PartInfoOptionContainer.Margin = new BorderDouble(8, 3);
					string sizeInfoLbl = new LocalizedString("Size").Translated;
					string sizeInfoLblFull = string.Format("{0}:", sizeInfoLbl);
					TextWidget sizeInfo = new TextWidget(sizeInfoLblFull, textColor: RGBA_Bytes.White);
                    PartInfoOptionContainer.AddChild(sizeInfo);
                    TextWidget xSizeInfo = new TextWidget("  x 10.1", pointSize: 10, textColor: RGBA_Bytes.White);
                    xSizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(xSizeInfo);

                    TextWidget ySizeInfo = new TextWidget("  y 10.1", pointSize: 10, textColor: RGBA_Bytes.White);
                    ySizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(ySizeInfo);
                    
                    TextWidget zSizeInfo = new TextWidget("  z 100.1", pointSize: 10, textColor: RGBA_Bytes.White);
                    zSizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(zSizeInfo);
                }

                // put in the view options
                {
					expandViewOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Display").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandViewOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandViewOptions);

                    viewOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    viewOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    viewOptionContainer.Visible = false;
                    {
						showBedCheckBox = checkboxButtonFactory.GenerateCheckBoxButton(new LocalizedString("Show Print Bed").Translated);
                        showBedCheckBox.Checked = true;
                        showBedCheckBox.Margin = buttonMargin;
                        viewOptionContainer.AddChild(showBedCheckBox);

                        if (buildHeight > 0)
                        {
							showBuildVolumeCheckBox = checkboxButtonFactory.GenerateCheckBoxButton(new LocalizedString("Show Print Area").Translated);
                            showBuildVolumeCheckBox.Checked = false;
                            showBuildVolumeCheckBox.Margin = buttonMargin;
                            viewOptionContainer.AddChild(showBuildVolumeCheckBox);
                        }

						showWireframeCheckBox = checkboxButtonFactory.GenerateCheckBoxButton(new LocalizedString("Show Wireframe").Translated);
                        showWireframeCheckBox.Margin = buttonMargin;
                        viewOptionContainer.AddChild(showWireframeCheckBox);
                    }
                    buttonRightPanel.AddChild(viewOptionContainer);
                }

				autoArrangeButton = whiteButtonFactory.Generate(new LocalizedString("Auto-Arrange").Translated, centerText: true);
                autoArrangeButton.Visible = false;
                autoArrangeButton.Cursor = Cursors.Hand;
                buttonRightPanel.AddChild(autoArrangeButton);

                GuiWidget verticalSpacer = new GuiWidget();
                verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
                buttonRightPanel.AddChild(verticalSpacer);

				saveButton = whiteButtonFactory.Generate(new LocalizedString("Save").Translated, centerText: true);
                saveButton.Visible = false;
                saveButton.Cursor = Cursors.Hand;
                buttonRightPanel.AddChild(saveButton);
            }

            buttonRightPanel.Padding = new BorderDouble(6, 6);
            buttonRightPanel.Margin = new BorderDouble(0, 1);
            buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

            return buttonRightPanel;
        }

        private void SetMeshViewerDisplayTheme()
        {
            meshViewerWidget.TrackballTumbleWidget.RotationHelperCircleColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            meshViewerWidget.PartColor = RGBA_Bytes.White;
            meshViewerWidget.SelectedPartColor = ActiveTheme.Instance.PrimaryAccentColor;
            meshViewerWidget.BuildVolumeColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryAccentColor.Red0To255, ActiveTheme.Instance.PrimaryAccentColor.Green0To255, ActiveTheme.Instance.PrimaryAccentColor.Blue0To255, 50);
        }

        MHNumberEdit scaleRatioControl;
        private void AddScaleControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> scaleControls = new List<GuiWidget>();
            transformControls.Add("Scale", scaleControls);

            FlowLayoutWidget scaleRatioContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            scaleRatioContainer.HAnchor = HAnchor.ParentLeftRight;
            scaleRatioContainer.Padding = new BorderDouble(5);

			string scaleRatioLblTxt = new LocalizedString("Ratio").Translated;
			string scaleRatioLblTxtFull = string.Format("{0}:", scaleRatioLblTxt);
			TextWidget scaleRatioLabel = new TextWidget(scaleRatioLblTxtFull, textColor: RGBA_Bytes.White);
            scaleRatioLabel.VAnchor = VAnchor.ParentCenter;
            scaleRatioContainer.AddChild(scaleRatioLabel);

            GuiWidget horizontalSpacer = new GuiWidget();
            horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;
            scaleRatioContainer.AddChild(horizontalSpacer);

            scaleRatioControl = new MHNumberEdit(1, pixelWidth: 50, allowDecimals: true, increment: .05);
            scaleRatioContainer.AddChild(scaleRatioControl);
            scaleRatioControl.ActuallNumberEdit.KeyPressed += (sender, e) =>
            {
                double scale = scaleRatioControl.ActuallNumberEdit.Value;
                if (scale != MeshExtraData[SelectedMeshIndex].currentScale)
                {
                    applyScaleButton.Visible = true;
                }
                else
                {
                    applyScaleButton.Visible = false;
                }
            };

            buttonPanel.AddChild(scaleRatioContainer);

            scaleControls.Add(scaleRatioControl);
            scaleRatioControl.ActuallNumberEdit.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
            {
                ApplyScaleFromEditField();
            };

			DropDownMenu presetScaleMenu = new DropDownMenu(new LocalizedString("Conversions").Translated, Direction.Down);
            RectangleDouble presetBounds = presetScaleMenu.LocalBounds;
            presetBounds.Inflate(new BorderDouble(5, 10, 10, 10));
            presetScaleMenu.LocalBounds = presetBounds;
            presetScaleMenu.MenuAsWideAsItems = false;
            presetScaleMenu.HAnchor |= HAnchor.ParentLeftRight;

            presetScaleMenu.AddItem("mm to in (.03937)");
            presetScaleMenu.AddItem("in to mm (25.4)");
            presetScaleMenu.AddItem("mm to cm (.1)");
            presetScaleMenu.AddItem("cm to mm (10)");
			string resetLbl = new LocalizedString ("reset").Translated;
			string resetLblFull = string.Format("{0} (1)",resetLbl);
			presetScaleMenu.AddItem(resetLblFull);


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

            buttonPanel.AddChild(presetScaleMenu);

			applyScaleButton = whiteButtonFactory.Generate(new LocalizedString("Apply Scale").Translated, centerText: true);
            applyScaleButton.Visible = false;
            applyScaleButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(applyScaleButton);

            scaleControls.Add(applyScaleButton);
            applyScaleButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                ApplyScaleFromEditField();
            };

            buttonPanel.AddChild(generateHorizontalRule());
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
            double scale = scaleRatioControl.ActuallNumberEdit.Value;
            if (scale > 0)
            {
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);

                // first we remove any scale we have applied and then scale to the value in the text edit field
                Matrix4X4 newScale = Matrix4X4.CreateScale(1 / MeshExtraData[SelectedMeshIndex].currentScale);
                newScale *= Matrix4X4.CreateScale(scale);
                totalTransfrom *= newScale;

                totalTransfrom *= Matrix4X4.CreateTranslation(center);
                SelectedMeshTransform *= totalTransfrom;


                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButton.Visible = true;
                Invalidate();
                MeshExtraData[SelectedMeshIndex].currentScale = scale;
                applyScaleButton.Visible = false;
            }
        }

        private void AddRotateControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> rotateControls = new List<GuiWidget>();
			transformControls.Add(new LocalizedString("Rotate").Translated, rotateControls);

            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            degreesContainer.HAnchor = HAnchor.ParentLeftRight;
            degreesContainer.Padding = new BorderDouble(5);

            GuiWidget horizontalSpacer = new GuiWidget();
            horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

			string degreesLabelTxt = new LocalizedString("Degrees").Translated;
			string degreesLabelTxtFull = string.Format("{0}:", degreesLabelTxt);
			TextWidget degreesLabel = new TextWidget(degreesLabelTxt, textColor: RGBA_Bytes.White);
            degreesContainer.AddChild(degreesLabel);
            degreesContainer.AddChild(horizontalSpacer);

            MHNumberEdit degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, increment: 5, minValue: -360, maxValue: 360);
            degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
            degreesContainer.AddChild(degreesControl);
            rotateControls.Add(degreesControl);

            buttonPanel.AddChild(degreesContainer);

            FlowLayoutWidget rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            rotateButtonContainer.HAnchor = HAnchor.ParentLeftRight;

            Button rotateXButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredX = new TextWidget("X", pointSize: 10, textColor: RGBA_Bytes.White); centeredX.Margin = new BorderDouble(3, 0, 0, 0); centeredX.AnchorCenter(); rotateXButton.AddChild(centeredX);
            rotateButtonContainer.AddChild(rotateXButton);
            rotateControls.Add(rotateXButton);
            rotateXButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationX(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButton.Visible = true;
                Invalidate();
            };

            Button rotateYButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredY = new TextWidget("Y", pointSize: 10, textColor: RGBA_Bytes.White); centeredY.Margin = new BorderDouble(3, 0, 0, 0); centeredY.AnchorCenter(); rotateYButton.AddChild(centeredY);
            rotateButtonContainer.AddChild(rotateYButton);
            rotateControls.Add(rotateYButton);
            rotateYButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationY(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButton.Visible = true;
                Invalidate();
            };

            Button rotateZButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredZ = new TextWidget("Z", pointSize: 10, textColor: RGBA_Bytes.White); centeredZ.Margin = new BorderDouble(3, 0, 0, 0); centeredZ.AnchorCenter(); rotateZButton.AddChild(centeredZ);
            rotateButtonContainer.AddChild(rotateZButton);
            rotateControls.Add(rotateZButton);
            rotateZButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationZ(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                saveButton.Visible = true;
                Invalidate();
            };

            buttonPanel.AddChild(rotateButtonContainer);

			Button layFlatButton = whiteButtonFactory.Generate(new LocalizedString("Align to Bed").Translated, centerText: true);
            layFlatButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(layFlatButton);

            layFlatButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                MakeLowestFaceFlat(SelectedMeshIndex);

                saveButton.Visible = true;
                Invalidate();
            };

            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddMirrorControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> mirrorControls = new List<GuiWidget>();
            transformControls.Add("Mirror", mirrorControls);

            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeftRight;

            Button mirrorXButton = textImageButtonFactory.Generate("X", centerText: true);
            buttonContainer.AddChild(mirrorXButton);
            mirrorControls.Add(mirrorXButton);
            mirrorXButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(-1, 1, 1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                saveButton.Visible = true;
                Invalidate();
            };

            Button mirrorYButton = textImageButtonFactory.Generate("Y", centerText: true);
            buttonContainer.AddChild(mirrorYButton);
            mirrorControls.Add(mirrorYButton);
            mirrorYButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(1, -1, 1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                saveButton.Visible = true;
                Invalidate();
            };

            Button mirrorZButton = textImageButtonFactory.Generate("Z", centerText: true);
            buttonContainer.AddChild(mirrorZButton);
            mirrorControls.Add(mirrorZButton);
            mirrorZButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(1, 1, -1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);

                saveButton.Visible = true;
                Invalidate();
            };
            buttonPanel.AddChild(buttonContainer);
            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            closeButton.Click += new ButtonBase.ButtonEventHandler(onCloseButton_Click);
            showBedCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(bedCheckBox_CheckedStateChanged);
            if (showBuildVolumeCheckBox != null)
            {
                showBuildVolumeCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(buildVolumeCheckBox_CheckedStateChanged);
            }
            showWireframeCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(wireframeCheckBox_CheckedStateChanged);

            expandViewOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandViewOptions_CheckedStateChanged);
            expandRotateOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandRotateOptions_CheckedStateChanged);
            expandScaleOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandScaleOptions_CheckedStateChanged);

            saveButton.Click += (sender, e) =>
            {
                MergeAndSavePartsToStl();
            };

            ActiveTheme.Instance.ThemeChanged.RegisterEvent(Instance_ThemeChanged, ref unregisterEvents);
        }

        bool partSelectButtonWasClicked = false;
        private void MergeAndSavePartsToStl()
        {
            if (Meshes.Count > 0)
            {
                partSelectButtonWasClicked = partSelectButton.Checked;

				string progressSavingPartslbl = new LocalizedString ("Saving").Translated;
				string progressSavingPartsLblFull = string.Format("{0}:",progressSavingPartslbl);
				processingProgressControl.textWidget.Text = progressSavingPartsLblFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker mergeAndSavePartsBackgroundWorker = new BackgroundWorker();
                mergeAndSavePartsBackgroundWorker.WorkerReportsProgress = true;

                mergeAndSavePartsBackgroundWorker.DoWork += new DoWorkEventHandler(mergeAndSavePartsBackgroundWorker_DoWork);
                mergeAndSavePartsBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                mergeAndSavePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mergeAndSavePartsBackgroundWorker_RunWorkerCompleted);

                mergeAndSavePartsBackgroundWorker.RunWorkerAsync();
            }
        }

        void mergeAndSavePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // we sent the data to the asynch lists but we will not pull it back out (only use it as a temp holder).
            PushMeshDataToAsynchLists(true);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            try
            {
                // push all the transforms into the meshes
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    asynchMeshesList[i].Transform(MeshTransforms[i]);

                    int nextPercent = (i + 1) * 40 / asynchMeshesList.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                Mesh mergedMesh = PlatingHelper.DoMerge(asynchMeshesList, backgroundWorker, 40, 80);
                StlProcessing.Save(mergedMesh, printItemWrapper.FileLocation);
                printItemWrapper.OnFileHasChanged();
            }
            catch (System.UnauthorizedAccessException)
            {
                //Do something special when unauthorized?
                StyledMessageBox.ShowMessageBox("Oops! Unable to save changes.", "Unable to save");
            }
            catch
            {
                StyledMessageBox.ShowMessageBox("Oops! Unable to save changes.", "Unable to save");
            }
        }

        void mergeAndSavePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            // NOTE: we do not pull the data back out of the asynch lists.
            saveButton.Visible = false;

            if (partSelectButtonWasClicked)
            {
                partSelectButton.ClickButton(null);
            }
        }

        void expandViewOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            viewOptionContainer.Visible = expandViewOptions.Checked;
        }

        void expandRotateOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            rotateOptionContainer.Visible = expandRotateOptions.Checked;
        }

        void expandScaleOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            scaleOptionContainer.Visible = expandScaleOptions.Checked;
        }

        void buildVolumeCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            meshViewerWidget.RenderBuildVolume = showBuildVolumeCheckBox.Checked;
        }

        bool scaleQueueMenu_Click()
        {
            return true;
        }

        bool rotateQueueMenu_Click()
        {
            return true;
        }

        void wireframeCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            meshViewerWidget.ShowWireFrame = showWireframeCheckBox.Checked;
        }

        void bedCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            meshViewerWidget.RenderBed = showBedCheckBox.Checked;
        }

        private void onCloseButton_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(CloseOnIdle);
        }

        void CloseOnIdle(object state)
        {
            Close();
        }

        private void MakeLowestFaceFlat(int indexToLayFlat)
        {
            Mesh meshToLayFlat = Meshes[indexToLayFlat];
            Vertex lowestVertex = meshToLayFlat.Vertices[0];
            Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, MeshTransforms[indexToLayFlat]);
            // find the lowest point on the model
            for (int testIndex = 1; testIndex < meshToLayFlat.Vertices.Count; testIndex++)
            {
                Vertex vertex = meshToLayFlat.Vertices[testIndex];
                Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshTransforms[indexToLayFlat]);
                if (vertexPosition.z < lowestVertexPosition.z)
                {
                    lowestVertex = meshToLayFlat.Vertices[testIndex];
                    lowestVertexPosition = vertexPosition;
                }
            }

            Face faceToLayFlat = null;
            double lowestAngleOfAnyFace = double.MaxValue;
            // Check all the faces that are connected to the lowest point to find out which one to lay flat.
            foreach (Face face in lowestVertex.ConnectedFacesIterator())
            {
                double biggestAngleToFaceVertex = double.MinValue;
                foreach (Vertex faceVertex in face.VertexIterator())
                {
                    if (faceVertex != lowestVertex)
                    {
                        Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, MeshTransforms[indexToLayFlat]);
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

            double maxDistFrom0 = 0;
            List<Vector3> faceVertexes = new List<Vector3>();
            foreach (Vertex vertex in faceToLayFlat.VertexIterator())
            {
                Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshTransforms[indexToLayFlat]);
                faceVertexes.Add(vertexPosition);
                maxDistFrom0 = Math.Max(maxDistFrom0, vertexPosition.z);
            }

            if (maxDistFrom0 > .001)
            {
                Vector3 xPositive = (faceVertexes[1] - faceVertexes[0]).GetNormal();
                Vector3 yPositive = (faceVertexes[2] - faceVertexes[0]).GetNormal();
                Vector3 planeNormal = Vector3.Cross(xPositive, yPositive).GetNormal();

#if true
                // this code takes the minimum rotation required and looks much better.
                Quaternion rotation = new Quaternion(planeNormal, new Vector3(0, 0, -1));
                Matrix4X4 partLevelMatrix = Matrix4X4.CreateRotation(rotation);
#else
                double planeOffset = Vector3.Dot(planeNormal, faceVertexes[1]);

                Matrix4X4 partLevelMatrix = Matrix4X4.LookAt(Vector3.Zero, planeNormal, yPositive);
#endif

                AxisAlignedBoundingBox bounds = meshToLayFlat.GetAxisAlignedBoundingBox(MeshTransforms[indexToLayFlat]);

                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= partLevelMatrix;
                MeshTransforms[indexToLayFlat] *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(MeshTransforms[indexToLayFlat]);
                // and shift it back so the new center is where the old center was
                MeshTransforms[indexToLayFlat] *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButton.Visible = true;
                Invalidate();
            }
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void Instance_ThemeChanged(object sender, EventArgs e)
        {
            SetMeshViewerDisplayTheme();
            Invalidate();
        }
    }
}
