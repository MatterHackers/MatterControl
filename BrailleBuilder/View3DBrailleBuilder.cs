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

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
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

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public class View3DBrailleBuilder : PartPreview3DWidget
	{
		private MHTextEditWidget textToAddWidget;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;
		private CheckBox includeText;
		private CheckBox useGrade2;

		private double lastHeightValue = 1;
		private double lastSizeValue = 1;
		private const double unscaledBaseHeight = 7;
		private const double unscaledLetterHeight = 3;

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
			internal Vector3 planeDownHitPos;
			internal Vector3 lastMoveDelta;
		}

		private TypeFace brailTypeFace;
		private TypeFace monoSpacedTypeFace;

		public View3DBrailleBuilder(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape)
		{
			monoSpacedTypeFace = ApplicationController.MonoSpacedTypeFace;

			brailTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "Braille.svg")));

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

				editToolBar.AddChild(editPlateButtonsContainer);
				buttonBottomPanel.AddChild(editToolBar);
			}

			GuiWidget buttonRightPanelHolder = new GuiWidget()
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.ParentBottomTop
			};
			centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
			buttonRightPanelHolder.AddChild(buttonRightPanel);

			viewControls3D = new ViewControls3D(meshViewerWidget);

			viewControls3D.ResetView += (sender, e) =>
			{
				meshViewerWidget.ResetView();
			};

			buttonRightPanelDisabledCover = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop
			};
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

			// set the view to be a good angle and distance
			meshViewerWidget.ResetView();

			AddHandlers();
			UnlockEditControls();
			// but make sure we can't use the right panel yet
			buttonRightPanelDisabledCover.Visible = true;

			//meshViewerWidget.RenderType = RenderTypes.Outlines;
			viewControls3D.PartSelectVisible = false;
			meshViewerWidget.ResetView();
		}

		private async void InsertTextNow(string text)
		{
			if (text.Length > 0)
			{
				this.word = text;
				if (useGrade2.Checked)
				{
					text = BrailleGrade2.ConvertString(text);
				}
				ResetWordLayoutSettings();
				processingProgressControl.ProcessType = "Inserting Text".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run(() => InsertTextDoWork(text,this.word));//replace with this.word when not testing conversions

				PullMeshDataFromAsynchLists();
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
				RebuildBase();
				CenterTextOnScreen(MeshGroups, MeshGroupTransforms);

				UnlockEditControls();
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
			}
		}

		private void SetWordPositions(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms, List<PlatingMeshGroupData> platingDataList)
		{
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count - 1; meshIndex++)
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, meshTransforms[meshIndex]);

					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(-startPosition);
					double newX = platingDataList[meshIndex].spacing.x * lastSizeValue;
					double newY = platingDataList[meshIndex].spacing.y * lastSizeValue;
					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(new Vector3(newX, newY, startPosition.z) + new Vector3(MeshViewerWidget.BedCenter));
				}

				CenterTextOnScreen(meshesList, meshTransforms);
			}
		}

		private void ResetWordLayoutSettings()
		{
			sizeScrollBar.Value = 1;
			heightScrollBar.Value = 1;
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

		private bool firstDraw = true;

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (firstDraw)
			{
				if (!UserSettings.Instance.IsTouchScreen)
				{
					textToAddWidget.Focus();
				}

				//textToAddWidget.Text = "Test Text";
				firstDraw = false;
			}

			base.OnDraw(graphics2D);
		}

		private void InsertTextDoWork(string brailleText, string wordText)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			asyncMeshGroups.Clear();
			asyncMeshGroupTransforms.Clear();
			asyncPlatingDatas.Clear();

			TypeFacePrinter brailPrinter = new TypeFacePrinter(brailleText, new StyledTypeFace(brailTypeFace, 12));

			int firstNewCharacter = 0;
			StyledTypeFace boldStyled = new StyledTypeFace(monoSpacedTypeFace, 12);

			if (includeText.Checked)
			{
				TypeFacePrinter normalPrinter = new TypeFacePrinter(wordText, boldStyled);
				Vector2 normalSize = normalPrinter.GetSize();
				AddCharacterMeshes(wordText, normalPrinter);
				
				firstNewCharacter = asyncPlatingDatas.Count;
			}

			AddCharacterMeshes(brailleText, brailPrinter);
			Vector2 brailSize = brailPrinter.GetSize();

			for (int i = 0; i < firstNewCharacter; i++)
			{
				asyncPlatingDatas[i].spacing = asyncPlatingDatas[i].spacing + new Vector2(0, boldStyled.CapHeightInPixels * 1.5);
			}

			CreateBase(asyncMeshGroups, asyncMeshGroupTransforms, asyncPlatingDatas);

			SetWordPositions(asyncMeshGroups, asyncMeshGroupTransforms, asyncPlatingDatas);
			SetWordSize(asyncMeshGroups, asyncMeshGroupTransforms);
			SetWordHeight(asyncMeshGroups, asyncMeshGroupTransforms);

			CenterTextOnScreen(asyncMeshGroups, asyncMeshGroupTransforms);

			processingProgressControl.PercentComplete = 95;
		}

		private void AddCharacterMeshes(string currentText, TypeFacePrinter printer)
		{
			int newIndex = asyncMeshGroups.Count;
			StyledTypeFace typeFace = printer.TypeFaceStyle;

			for (int i = 0; i < currentText.Length; i++)
			{
				string letter = currentText[i].ToString();
				TypeFacePrinter letterPrinter = new TypeFacePrinter(letter, typeFace);

				if (CharacterHasMesh(letterPrinter, letter))
				{
#if true
					Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, unscaledLetterHeight / 2);
#else
					Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, unscaledLetterHeight / 2);
					// this is the code to make rounded tops
					// convert the letterPrinter to clipper polygons
					List<List<IntPoint>> insetPoly = VertexSourceToPolygon.CreatePolygons(letterPrinter);
					// inset them
					ClipperOffset clipper = new ClipperOffset();
					clipper.AddPaths(insetPoly, JoinType.jtMiter, EndType.etClosedPolygon);
					List<List<IntPoint>> solution = new List<List<IntPoint>>();
					clipper.Execute(solution, 5.0);
					// convert them back into a vertex source
					// merge both the inset and original vertex sources together
					// convert the new vertex source into a mesh (triangulate them)
					// offset the inner loop in z
					// create the polygons from the inner loop to a center point so that there is the rest of an approximation of the bubble
					// make the mesh for the bottom 
					// add the top and bottom together
					// done
#endif

					asyncMeshGroups.Add(new MeshGroup(textMesh));

					PlatingMeshGroupData newMeshInfo = new PlatingMeshGroupData();

					newMeshInfo.spacing = printer.GetOffsetLeftOfCharacterIndex(i);
					asyncPlatingDatas.Add(newMeshInfo);
					asyncMeshGroupTransforms.Add(Matrix4X4.Identity);

					PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, newIndex, null);
					asyncMeshGroupTransforms[newIndex] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, unscaledLetterHeight / 2));

					newIndex++;
				}

				processingProgressControl.PercentComplete = ((i + 1) * 95 / currentText.Length);
			}
		}

		private void CreateBase(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms, List<PlatingMeshGroupData> platingDataList)
		{
			if (meshesList.Count > 0)
			{
				AxisAlignedBoundingBox bounds = meshesList[0].GetAxisAlignedBoundingBox(meshTransforms[0]);
				for (int i = 1; i < meshesList.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, meshesList[i].GetAxisAlignedBoundingBox(meshTransforms[i]));
				}

				double roundingScale = 20;
				RectangleDouble baseRect = new RectangleDouble(bounds.minXYZ.x, bounds.minXYZ.y, bounds.maxXYZ.x, bounds.maxXYZ.y);
				baseRect.Inflate(2);
				baseRect *= roundingScale;
				RoundedRect baseRoundedRect = new RoundedRect(baseRect, 1 * roundingScale);

				Mesh baseMeshResult = VertexSourceToMesh.Extrude(baseRoundedRect, unscaledBaseHeight / 2 * roundingScale * sizeScrollBar.Value * heightScrollBar.Value);

				baseMeshResult.Transform(Matrix4X4.CreateScale(1 / roundingScale));

				meshesList.Add(new MeshGroup(baseMeshResult));
				platingDataList.Add(new PlatingMeshGroupData());
				meshTransforms.Add(Matrix4X4.CreateTranslation(0, 0, 0));
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
					mesh.CleanAndMergMesh();
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

			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
			}
		}

		private void UnlockEditControls()
		{
			buttonRightPanelDisabledCover.Visible = false;
			processingProgressControl.Visible = false;

			editPlateButtonsContainer.Visible = true;
		}

		private void DeleteSelectedMesh()
		{
			// don't ever delete the last mesh
			if (MeshGroups.Count > 1)
			{
				MeshGroups.RemoveAt(SelectedMeshGroupIndex);
				MeshGroupExtraData.RemoveAt(SelectedMeshGroupIndex);
				MeshGroupTransforms.RemoveAt(SelectedMeshGroupIndex);
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
				Invalidate();
			}
		}

		private FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
		{
			FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRightPanel.Width = 200;
			{
				BorderDouble buttonMargin = new BorderDouble(top: 3);

				// put in the word editing menu
				{
					CheckBox expandWordOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Word Edit".Localize(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
					expandWordOptions.Margin = new BorderDouble(bottom: 2);
					buttonRightPanel.AddChild(expandWordOptions);

					FlowLayoutWidget wordOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					wordOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					wordOptionContainer.Visible = false;
					buttonRightPanel.AddChild(wordOptionContainer);

					sizeScrollBar = InsertUiForSlider(wordOptionContainer, "Size:".Localize(), .3, 2);
					{
						sizeScrollBar.ValueChanged += (sender, e) =>
						{
							SetWordSize(MeshGroups, MeshGroupTransforms);

							//SetWordSpacing(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
							RebuildBase();
						};
					}

					heightScrollBar = InsertUiForSlider(wordOptionContainer, "Height:".Localize(), .05, 1);
					{
						heightScrollBar.ValueChanged += (sender, e) =>
						{
							SetWordHeight(MeshGroups, MeshGroupTransforms);
							RebuildBase();
						};
					}

					// put in the user alpha check box
					{
						includeText = new CheckBox(new CheckBoxViewText("Include Text".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
						includeText.ToolTipText = "Show normal text above the braille".Localize();
						includeText.Checked = false;
						includeText.Margin = new BorderDouble(10, 5);
						includeText.HAnchor = HAnchor.ParentLeft;
						wordOptionContainer.AddChild(includeText);
						includeText.CheckedStateChanged += (sender, e) =>
						{
							InsertTextNow(this.word);
						};
					}

					// put in the user alpha check box
					{
						useGrade2 = new CheckBox(new CheckBoxViewText("Use Grade 2".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
						useGrade2.ToolTipText = "Experimental support for Braille grade 2 (contractions)".Localize();
						useGrade2.Checked = false;
						useGrade2.Margin = new BorderDouble(10, 5);
						useGrade2.HAnchor = HAnchor.ParentLeft;
						wordOptionContainer.AddChild(useGrade2);
						useGrade2.CheckedStateChanged += (sender, e) =>
						{
							InsertTextNow(this.word);
						};
					}

					// put in a link to the wikipedia article
					{
						LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
						linkButtonFactory.fontSize = 10;
						linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;

						Button moreAboutBrailleLink = linkButtonFactory.Generate("About Braille".Localize());
						moreAboutBrailleLink.Margin = new BorderDouble(10, 5);
						moreAboutBrailleLink.HAnchor = HAnchor.ParentLeft;
						moreAboutBrailleLink.Click += (sender, e) =>
						{
							UiThread.RunOnIdle(() =>
							{
								MatterControlApplication.Instance.LaunchBrowser("https://en.wikipedia.org/wiki/Braille");
							});
						};

						wordOptionContainer.AddChild(moreAboutBrailleLink);
					}

					expandWordOptions.CheckedStateChanged += (sender, e) =>
					{
						wordOptionContainer.Visible = expandWordOptions.Checked;
					};

					expandWordOptions.Checked = true;
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

		private void RebuildBase()
		{
			// we need to remove the underline
			if (MeshGroups.Count > 1)
			{
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
				DeleteSelectedMesh();
				// we need to add the underline
				CreateBase(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
			}
		}

		private bool CharacterHasMesh(TypeFacePrinter letterPrinter, string letter)
		{
			return letterPrinter.LocalBounds.Width > 0
				&& letter != " "
				&& letter != "\n";
		}

		private void CenterTextOnScreen(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms)
		{			
			// center in y
			if (meshesList.Count > 0)
			{
				AxisAlignedBoundingBox bounds = meshesList[0].GetAxisAlignedBoundingBox(meshTransforms[0]);
				for (int i = 1; i < meshesList.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, meshesList[i].GetAxisAlignedBoundingBox(meshTransforms[i]));
				}

				Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
				Vector3 centerOffset = bounds.Center - bedCenter;

				for (int meshIndex = 0; meshIndex < meshesList.Count; meshIndex++)
				{
					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(new Vector3(-centerOffset.x, -centerOffset.y, 0));
				}
			}			
		}

		private void SetWordSize(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms)
		{
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count; meshIndex++)
				{
					Vector3 startPositionRelCenter = Vector3.Transform(Vector3.Zero, meshTransforms[meshIndex]);

					// take out the last scale
					double oldSize = 1.0 / lastSizeValue;
					Vector3 unscaledStartPositionRelCenter = startPositionRelCenter * oldSize;

					double newSize = sizeScrollBar.Value;
					Vector3 endPositionRelCenter = unscaledStartPositionRelCenter * newSize;

					Vector3 deltaPosition = endPositionRelCenter - startPositionRelCenter;

					// move the part to keep it in the same relative position
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(oldSize, oldSize, oldSize));
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(newSize, newSize, newSize));
					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(deltaPosition);
				}

				lastSizeValue = sizeScrollBar.Value;
			}

			CenterTextOnScreen(meshesList, meshTransforms);
		}

		private void SetWordHeight(List<MeshGroup> meshesList, List<Matrix4X4> meshTransforms)
		{
			if (meshesList.Count > 0)
			{
				for (int meshIndex = 0; meshIndex < meshesList.Count-1; meshIndex++)
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, meshTransforms[meshIndex]);

					// take out the last scale
					double oldHeight = 1.0 / lastHeightValue;
					double newHeight = heightScrollBar.Value;

					// move the part to keep it in the same relative position
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(1, 1, oldHeight));
					meshTransforms[meshIndex] *= Matrix4X4.CreateScale(new Vector3(1, 1, newHeight));

					// if it's not the base
					int baseIndex = meshesList.Count-1;
					AxisAlignedBoundingBox baseBounds = meshesList[baseIndex].GetAxisAlignedBoundingBox(meshTransforms[baseIndex]);

					meshTransforms[meshIndex] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, baseBounds.ZSize - startPosition.z));
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
			closeButton.Click += onCloseButton_Click;

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

				string fileName = "BrailleBuilder_{0}".FormatWith(Path.ChangeExtension(Path.GetRandomFileName(), ".amf"));
				string filePath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				processingProgressControl.RatioComplete = 0;
				await Task.Run(() => MergeAndSavePartsDoWork(filePath));

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

		private void MergeAndSavePartsDoWork(string filePath)
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

				MeshFileIo.Save(asyncMeshGroups, filePath);
			}
			catch (System.UnauthorizedAccessException)
			{
				//Do something special when unauthorized?
				StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.".Localize(), "Unable to save".Localize());
			}
			catch
			{
				StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.".Localize(), "Unable to save".Localize());
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