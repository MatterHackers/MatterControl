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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class View3DTextCreator : PartPreview3DWidget
	{
		MHTextEditWidget textToAddWidget;
		private SolidSlider spacingScrollBar;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;

		private CheckBox createUnderline;

		private double lastHeightValue = 1;
		private double lastSizeValue = 1;

		private ProgressControl processingProgressControl;
		private FlowLayoutWidget editPlateButtonsContainer;

		private Button saveButton;
		private Button saveAndExitButton;
		private Button closeButton;
		private String word;

		private List<MeshGroup> asyncMeshGroups = new List<MeshGroup>();
		private List<Matrix4X4> asyncMeshGroupTransforms = new List<Matrix4X4>();
		private List<PlatingMeshGroupData> asyncPlatingDatas = new List<PlatingMeshGroupData>();

		private List<PlatingMeshGroupData> MeshGroupExtraData;

		public Matrix4X4 SelectedMeshTransform
		{
			get { return meshViewerWidget.SelectedMeshGroupTransform; }
			set { meshViewerWidget.SelectedMeshGroupTransform = value; }
		}

		public MeshGroup SelectedMeshGroup
		{
			get
			{
				return meshViewerWidget.SelectedMeshGroup;
			}
		}

		public int SelectedMeshGroupIndex
		{
			get
			{
				return meshViewerWidget.SelectedMeshGroupIndex;
			}
			set
			{
				meshViewerWidget.SelectedMeshGroupIndex = value;
			}
		}

		public List<MeshGroup> MeshGroups
		{
			get
			{
				return meshViewerWidget.MeshGroups;
			}
		}

		public List<Matrix4X4> MeshGroupTransforms
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

		private TypeFace boldTypeFace;

		public View3DTextCreator(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape)
		{
			boldTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationSans-Bold.svg")));

			MeshGroupExtraData = new List<PlatingMeshGroupData>();

			FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

			FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
			centerPartPreviewAndControls.AnchorAll();

			GuiWidget viewArea = new GuiWidget();
			viewArea.AnchorAll();
			{
				meshViewerWidget = new MeshViewerWidget(viewerVolume, bedCenter, bedShape);
				meshViewerWidget.AllowBedRenderingWhenEmpty = true;
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

			// add in the plater tools
			{
				FlowLayoutWidget editToolBar = new FlowLayoutWidget();

				processingProgressControl = new ProgressControl("Finding Parts:".Localize(), ActiveTheme.Instance.PrimaryTextColor, ActiveTheme.Instance.PrimaryAccentColor);
				processingProgressControl.VAnchor = Agg.UI.VAnchor.ParentCenter;
				editToolBar.AddChild(processingProgressControl);
				editToolBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;

				editPlateButtonsContainer = new FlowLayoutWidget();

				textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter Text Here".Localize());
				textToAddWidget.VAnchor = VAnchor.ParentCenter;
				textToAddWidget.Margin = new BorderDouble(5);
				editPlateButtonsContainer.AddChild(textToAddWidget);
				textToAddWidget.ActualTextEditWidget.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
				{
					InsertTextNow(textToAddWidget.Text);
				};

				Button insertTextButton = textImageButtonFactory.Generate("Insert".Localize());
				editPlateButtonsContainer.AddChild(insertTextButton);
				insertTextButton.Click += (sender, e) =>
				{
					InsertTextNow(textToAddWidget.Text);
				};

				KeyDown += (sender, e) =>
				{
					KeyEventArgs keyEvent = e as KeyEventArgs;
					if (keyEvent != null && !keyEvent.Handled)
					{
						if (keyEvent.KeyCode == Keys.Escape)
						{
							if (meshSelectInfo.downOnPart)
							{
								meshSelectInfo.downOnPart = false;

								SelectedMeshTransform *= transformOnMouseDown;

								Invalidate();
							}
						}
					}
				};

				editToolBar.AddChild(editPlateButtonsContainer);
				buttonBottomPanel.AddChild(editToolBar);
			}

			GuiWidget buttonRightPanelHolder = new GuiWidget(HAnchor.FitToChildren, VAnchor.ParentBottomTop);
			centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
			buttonRightPanelHolder.AddChild(buttonRightPanel);

			viewControls3D = new ViewControls3D(meshViewerWidget);

			viewControls3D.ResetView += (sender, e) =>
			{
				meshViewerWidget.ResetView();
			};

			buttonRightPanelDisabledCover = new Cover(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
			buttonRightPanelDisabledCover.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 150);
			buttonRightPanelHolder.AddChild(buttonRightPanelDisabledCover);
			LockEditControls();

			GuiWidget leftRightSpacer = new GuiWidget();
			leftRightSpacer.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.AddChild(leftRightSpacer);

			closeButton = textImageButtonFactory.Generate("Close".Localize());
			buttonBottomPanel.AddChild(closeButton);

			mainContainerTopToBottom.AddChild(buttonBottomPanel);

			this.AddChild(mainContainerTopToBottom);
			this.AnchorAll();

			meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;

			AddChild(viewControls3D);

			meshViewerWidget.ResetView();

			AddHandlers();
			UnlockEditControls();
			// but make sure we can't use the right panel yet
			buttonRightPanelDisabledCover.Visible = true;
		}

		private async void InsertTextNow(string text)
		{
			if (text.Length > 0)
			{
				this.word = text;
				ResetWordLayoutSettings();
				processingProgressControl.ProcessType = "Inserting Text".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => insertTextBackgroundWorker_DoWork(text));

				UnlockEditControls();
				PullMeshDataFromAsynchLists();
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
				// now set the selection to the new copy
				SelectedMeshGroupIndex = 0;
			}

			meshViewerWidget.ResetView();
		}

		private void ResetWordLayoutSettings()
		{
			spacingScrollBar.Value = 1;
			sizeScrollBar.Value = 1;
			heightScrollBar.Value = .25;
			lastHeightValue = 1;
			lastSizeValue = 1;
		}

		private bool FindMeshGroupHitPosition(Vector2 screenPosition, out int meshHitIndex)
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
			IPrimitive allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
			IntersectInfo info = allObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				meshSelectInfo.planeDownHitPos = info.hitPosition;
				meshSelectInfo.lastMoveDelta = new Vector3();

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

		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private MeshSelectInfo meshSelectInfo;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
			{
				if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;
					int meshHitIndex;
					if (FindMeshGroupHitPosition(mouseEvent.Position, out meshHitIndex))
					{
						meshSelectInfo.hitPlane = new PlaneShape(Vector3.UnitZ, meshSelectInfo.planeDownHitPos.z, null);
						SelectedMeshGroupIndex = meshHitIndex;
						transformOnMouseDown = SelectedMeshTransform;
						Invalidate();
						meshSelectInfo.downOnPart = true;
					}
				}
			}
		}

		private bool firstDraw = true;

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (firstDraw)
			{
#if !__ANDROID__
				textToAddWidget.Focus();
#endif
				//textToAddWidget.Text = "Test Text";
				firstDraw = false;
			}
			//DoCsgTest();
			base.OnDraw(graphics2D);
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

					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-meshSelectInfo.lastMoveDelta));
					totalTransform *= Matrix4X4.CreateTranslation(new Vector3(delta));
					meshSelectInfo.lastMoveDelta = delta;

					SelectedMeshTransform *= totalTransform;

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
				saveAndExitButton.Visible = true;
			}

			meshSelectInfo.downOnPart = false;

			base.OnMouseUp(mouseEvent);
		}

		private void insertTextBackgroundWorker_DoWork(string currentText)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			asyncMeshGroups.Clear();
			asyncMeshGroupTransforms.Clear();
			asyncPlatingDatas.Clear();

			TypeFacePrinter printer = new TypeFacePrinter(currentText, new StyledTypeFace(boldTypeFace, 12));
			Vector2 size = printer.GetSize(currentText);
			double centerOffset = -size.x / 2;

			double ratioPerMeshGroup = 1.0 / currentText.Length;
			double currentRatioDone = 0;
			for (int i = 0; i < currentText.Length; i++)
			{
				int newIndex = asyncMeshGroups.Count;

				TypeFacePrinter letterPrinter = new TypeFacePrinter(currentText[i].ToString(), new StyledTypeFace(boldTypeFace, 12));
				Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 10 + (i % 2));

				if (textMesh.Faces.Count > 0)
				{
					asyncMeshGroups.Add(new MeshGroup(textMesh));

					PlatingMeshGroupData newMeshInfo = new PlatingMeshGroupData();

					newMeshInfo.spacing.x = printer.GetOffsetLeftOfCharacterIndex(i).x + centerOffset;
					asyncPlatingDatas.Add(newMeshInfo);
					asyncMeshGroupTransforms.Add(Matrix4X4.Identity);

					PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, newIndex, (double progress0To1, string processingState, out bool continueProcessing) =>
					{
						continueProcessing = true;
						int nextPercent = (int)((currentRatioDone + ratioPerMeshGroup * progress0To1) * 100);
						processingProgressControl.PercentComplete = nextPercent;
					});

					currentRatioDone += ratioPerMeshGroup;

					PlatingHelper.PlaceMeshGroupOnBed(asyncMeshGroups, asyncMeshGroupTransforms, newIndex);
				}

				processingProgressControl.PercentComplete = ((i + 1) * 95 / currentText.Length);
			}

			SetWordSpacing(asyncMeshGroups, asyncMeshGroupTransforms, asyncPlatingDatas);
			SetWordSize(asyncMeshGroups, asyncMeshGroupTransforms);
			SetWordHeight(asyncMeshGroups, asyncMeshGroupTransforms);

			if (createUnderline.Checked)
			{
				CreateUnderline(asyncMeshGroups, asyncMeshGroupTransforms, asyncPlatingDatas);
			}

			processingProgressControl.PercentComplete = 95;
		}

		private void CreateUnderline(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms, List<PlatingMeshGroupData> platingDataList)
		{
			if (meshesList.Count > 0)
			{
				AxisAlignedBoundingBox bounds = meshesList[0].GetAxisAlignedBoundingBox(meshTransforms[0]);
				for (int i = 1; i < meshesList.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, meshesList[i].GetAxisAlignedBoundingBox(meshTransforms[i]));
				}

				double xSize = bounds.XSize;
				double ySize = sizeScrollBar.Value * 3;
				double zSize = bounds.ZSize / 3;
				Mesh connectionLine = PlatonicSolids.CreateCube(xSize, ySize, zSize);
				meshesList.Add(new MeshGroup(connectionLine));
				platingDataList.Add(new PlatingMeshGroupData());
				meshTransforms.Add(Matrix4X4.CreateTranslation((bounds.maxXYZ.x + bounds.minXYZ.x) / 2, bounds.minXYZ.y + ySize / 2 - ySize * 1 / 3, zSize / 2));
				PlatingHelper.CreateITraceableForMeshGroup(platingDataList, meshesList, meshesList.Count - 1, null);
			}
		}

		private void PushMeshGroupDataToAsynchLists(bool copyTraceInfo)
		{
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
					asyncMeshGroupTransforms.Add(MeshGroupTransforms[meshGroupIndex]);
				}
				asyncMeshGroups.Add(newMeshGroup);
			}
			asyncPlatingDatas.Clear();

			for (int meshGroupIndex = 0; meshGroupIndex < MeshGroupExtraData.Count; meshGroupIndex++)
			{
				PlatingMeshGroupData meshData = new PlatingMeshGroupData();
				MeshGroup meshGroup = MeshGroups[meshGroupIndex];
				for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
				{
					if (copyTraceInfo)
					{
						meshData.meshTraceableData.AddRange(MeshGroupExtraData[meshGroupIndex].meshTraceableData);
					}
				}
				asyncPlatingDatas.Add(meshData);
			}
		}

		private void arrangePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			UnlockEditControls();
			saveButton.Visible = true;
			saveAndExitButton.Visible = true;
			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

			PullMeshDataFromAsynchLists();
		}

		private void PullMeshDataFromAsynchLists()
		{
			MeshGroups.Clear();
			foreach (MeshGroup mesh in asyncMeshGroups)
			{
				MeshGroups.Add(mesh);
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
		}

		private void meshViewerWidget_LoadDone(object sender, EventArgs e)
		{
			UnlockEditControls();
		}

		private void LockEditControls()
		{
			editPlateButtonsContainer.Visible = false;
			buttonRightPanelDisabledCover.Visible = true;

			viewControls3D.PartSelectVisible = false;
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				viewControls3D.ActiveButton = ViewControls3DButtons.Rotate; 
			}
		}

		private void UnlockEditControls()
		{
			buttonRightPanelDisabledCover.Visible = false;
			processingProgressControl.Visible = false;

			viewControls3D.PartSelectVisible = true;
			editPlateButtonsContainer.Visible = true;
		}

		private void DeleteSelectedMesh()
		{
			// don't ever delete the last mesh
			if (MeshGroups.Count > 1)
			{
				int removeIndex = SelectedMeshGroupIndex;
				MeshGroups.RemoveAt(removeIndex);
				MeshGroupExtraData.RemoveAt(removeIndex);
				MeshGroupTransforms.RemoveAt(removeIndex);
				SelectedMeshGroupIndex = Math.Min(SelectedMeshGroupIndex, MeshGroups.Count - 1);
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
				Invalidate();
			}
		}

		private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			processingProgressControl.PercentComplete = e.ProgressPercentage;
		}

		private FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
		{
			FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRightPanel.Width = 200;
			{
				BorderDouble buttonMargin = new BorderDouble(top: 3);

				// put in the word editing menu
				{
					CheckBox expandWordOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Word Edit".Localize(), StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32, 32).InvertLightness());
					expandWordOptions.Margin = new BorderDouble(bottom: 2);
					buttonRightPanel.AddChild(expandWordOptions);

					FlowLayoutWidget wordOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					wordOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					wordOptionContainer.Visible = false;
					buttonRightPanel.AddChild(wordOptionContainer);

					spacingScrollBar = InsertUiForSlider(wordOptionContainer, "Spacing:".Localize(), .5, 1);
					{
						spacingScrollBar.ValueChanged += (sender, e) =>
						{
							SetWordSpacing(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
							RebuildUnderlineIfRequired();
						};
					}

					sizeScrollBar = InsertUiForSlider(wordOptionContainer, "Size:".Localize(), .3, 2);
					{
						sizeScrollBar.ValueChanged += (sender, e) =>
						{
							SetWordSize(MeshGroups, MeshGroupTransforms);

							//SetWordSpacing(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
							RebuildUnderlineIfRequired();
						};
					}

					heightScrollBar = InsertUiForSlider(wordOptionContainer, "Height:".Localize(), .05, 1);
					{
						heightScrollBar.ValueChanged += (sender, e) =>
						{
							SetWordHeight(MeshGroups, MeshGroupTransforms);
							RebuildUnderlineIfRequired();
						};
					}

					createUnderline = new CheckBox(new CheckBoxViewText("Underline".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
					createUnderline.Checked = true;
					createUnderline.Margin = new BorderDouble(10, 5);
					createUnderline.HAnchor = HAnchor.ParentLeft;
					wordOptionContainer.AddChild(createUnderline);
					createUnderline.CheckedStateChanged += (sender, e) =>
					{
						int oldIndex = SelectedMeshGroupIndex;
						if (!createUnderline.Checked)
						{
							// we need to remove the underline
							if (MeshGroups.Count > 1)
							{
								SelectedMeshGroupIndex = MeshGroups.Count - 1;
								DeleteSelectedMesh();
							}
						}
						else if (MeshGroups.Count > 0)
						{
							// we need to add the underline
							CreateUnderline(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
						}
						SelectedMeshGroupIndex = Math.Min(oldIndex, MeshGroups.Count - 1);
					};

					expandWordOptions.CheckedStateChanged += (sender, e) =>
					{
						wordOptionContainer.Visible = expandWordOptions.Checked;
					};

					expandWordOptions.Checked = true;
				}

				// put in the letter editing menu
				{
					CheckBox expandLetterOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Letter", StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32, 32).InvertLightness());
					expandLetterOptions.Margin = new BorderDouble(bottom: 2);
					//buttonRightPanel.AddChild(expandLetterOptions);

					FlowLayoutWidget letterOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					letterOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					letterOptionContainer.Visible = false;
					buttonRightPanel.AddChild(letterOptionContainer);

					SolidSlider sizeScrollBar = InsertUiForSlider(letterOptionContainer, "Size:".Localize());
					SolidSlider heightScrollBar = InsertUiForSlider(letterOptionContainer, "Height:".Localize());
					SolidSlider rotationScrollBar = InsertUiForSlider(letterOptionContainer, "Rotation:".Localize());

					expandLetterOptions.CheckedStateChanged += (sender, e) =>
					{
						letterOptionContainer.Visible = expandLetterOptions.Checked;
					};
				}

				GuiWidget verticalSpacer = new GuiWidget();
				verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
				buttonRightPanel.AddChild(verticalSpacer);

				saveButton = WhiteButtonFactory.Generate("Save".Localize(), centerText: true);
				saveButton.Visible = false;
				saveButton.Cursor = Cursors.Hand;

				saveAndExitButton = WhiteButtonFactory.Generate("Save & Exit".Localize(), centerText: true);
				saveAndExitButton.Visible = false;
				saveAndExitButton.Cursor = Cursors.Hand;

				//buttonRightPanel.AddChild(saveButton);
				buttonRightPanel.AddChild(saveAndExitButton);
			}

			buttonRightPanel.Padding = new BorderDouble(6, 6);
			buttonRightPanel.Margin = new BorderDouble(0, 1);
			buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

			return buttonRightPanel;
		}

		private void RebuildUnderlineIfRequired()
		{
			if (createUnderline.Checked)
			{
				// we need to remove the underline
				if (MeshGroups.Count > 1)
				{
					int oldIndex = SelectedMeshGroupIndex;
					SelectedMeshGroupIndex = MeshGroups.Count - 1;
					DeleteSelectedMesh();
					// we need to add the underline
					CreateUnderline(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
					SelectedMeshGroupIndex = oldIndex;
				}
			}
		}

		private void SetWordSpacing(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms, List<PlatingMeshGroupData> platingDataList)
		{
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count; meshIndex++)
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, meshTransforms[meshIndex]);

					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(-startPosition);
					double newX = platingDataList[meshIndex].spacing.x * spacingScrollBar.Value * lastSizeValue;
					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(new Vector3(newX, 0, 0) + new Vector3(MeshViewerWidget.BedCenter));
				}
			}
		}

		private void SetWordSize(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms)
		{
			Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count; meshIndex++)
				{
					// take out the last scale
					double oldSize = 1.0 / lastSizeValue;

					double newSize = sizeScrollBar.Value;

					meshTransforms[meshIndex] = PlatingHelper.ApplyAtPosition(meshTransforms[meshIndex], Matrix4X4.CreateScale(new Vector3(oldSize, oldSize, oldSize)), new Vector3(bedCenter));
					meshTransforms[meshIndex] = PlatingHelper.ApplyAtPosition(meshTransforms[meshIndex], Matrix4X4.CreateScale(new Vector3(newSize, newSize, newSize)), new Vector3(bedCenter));
				}

				lastSizeValue = sizeScrollBar.Value;
			}
		}

		private void SetWordHeight(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms)
		{
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count; meshIndex++)
				{
					// take out the last scale
					double oldHeight = lastHeightValue;
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(1, 1, 1 / oldHeight));

					double newHeight = heightScrollBar.Value;
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(1, 1, newHeight));
				}

				lastHeightValue = heightScrollBar.Value;
			}
		}

		private void AddLetterControls(FlowLayoutWidget buttonPanel)
		{
			textImageButtonFactory.FixedWidth = 44 * GuiWidget.DeviceScale;

			FlowLayoutWidget degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			degreesContainer.HAnchor = HAnchor.ParentLeftRight;
			degreesContainer.Padding = new BorderDouble(5);

			GuiWidget horizontalSpacer = new GuiWidget();
			horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

			TextWidget degreesLabel = new TextWidget("Degrees:".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			degreesContainer.AddChild(degreesLabel);
			degreesContainer.AddChild(horizontalSpacer);

			MHNumberEdit degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, increment: 5, minValue: -360, maxValue: 360);
			degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
			degreesContainer.AddChild(degreesControl);

			buttonPanel.AddChild(degreesContainer);

			FlowLayoutWidget rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rotateButtonContainer.HAnchor = HAnchor.ParentLeftRight;

			buttonPanel.AddChild(rotateButtonContainer);

			buttonPanel.AddChild(generateHorizontalRule());
			textImageButtonFactory.FixedWidth = 0;
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

		private void AddHandlers()
		{
			closeButton.Click += new EventHandler(onCloseButton_Click);

			saveButton.Click += (sender, e) =>
			{
				MergeAndSavePartsToStl();
			};

			saveAndExitButton.Click += (sender, e) =>
			{
				MergeAndSavePartsToStl();
			};
		}

		private bool partSelectButtonWasClicked = false;

		private async void MergeAndSavePartsToStl()
		{
			if (MeshGroups.Count > 0)
			{
				partSelectButtonWasClicked = viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect;
				

				processingProgressControl.ProcessType = "Saving Parts:".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				// we sent the data to the async lists but we will not pull it back out (only use it as a temp holder).
				PushMeshGroupDataToAsynchLists(true);

				string fileName = "TextCreator_{0}".FormatWith(Path.ChangeExtension(Path.GetRandomFileName(), ".amf"));
				string filePath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				processingProgressControl.RatioComplete = 0;
				await Task.Run(() => mergeAndSavePartsBackgroundWorker_DoWork(filePath));

				PrintItem printItem = new PrintItem();

				printItem.Name = string.Format("{0}", word);
				printItem.FileLocation = Path.GetFullPath(filePath);

				PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem);

				// and save to the queue
				QueueData.Instance.AddItem(printItemWrapper);

				//Exit after save
				UiThread.RunOnIdle(CloseOnIdle);
			}
		}

		private void mergeAndSavePartsBackgroundWorker_DoWork(string filePath)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			try
			{
				// push all the transforms into the meshes
				for (int i = 0; i < asyncMeshGroups.Count; i++)
				{
					asyncMeshGroups[i].Transform(MeshGroupTransforms[i]);

					processingProgressControl.RatioComplete = (double)i / asyncMeshGroups.Count * .1;
				}

				List<MeshGroup> mergResults = new List<MeshGroup>();
				mergResults.Add(new MeshGroup());
				mergResults[0].Meshes.Add(new Mesh());
				double meshGroupIndex = 0;
				foreach (MeshGroup meshGroup in asyncMeshGroups)
				{
					foreach (Mesh mesh in meshGroup.Meshes)
					{
						processingProgressControl.RatioComplete = .1 + (double)meshGroupIndex / asyncMeshGroups.Count;
						mergResults[0].Meshes[0] = CsgOperations.Union(mergResults[0].Meshes[0], mesh);
					}
					meshGroupIndex++;
				}

				MeshFileIo.Save(mergResults, filePath);
			}
			catch (System.UnauthorizedAccessException)
			{
				//Do something special when unauthorized?
				UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.".Localize(), "Unable to save".Localize()));
			}
			catch
			{
				UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.".Localize(), "Unable to save".Localize()));
			}
		}

		private bool scaleQueueMenu_Click()
		{
			return true;
		}

		private bool rotateQueueMenu_Click()
		{
			return true;
		}

		private void onCloseButton_Click(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(Close);
		}
	}
}