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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DCreatorWidget : PartPreview3DWidget
	{
		protected ProgressControl processingProgressControl;
		private FlowLayoutWidget editPlateButtonsContainer;

		protected Button saveButton;
		protected Button saveAndExitButton;
		private Button closeButton;

		protected string printItemName;

		private string prependToFileName;

		private bool partSelectButtonWasClicked = false;

		public View3DCreatorWidget(Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape, string prependToFileName, bool partSelectVisible)
		{
			this.prependToFileName = prependToFileName;

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
				AddToBottomToolbar(editPlateButtonsContainer);

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
			closeButton.Click += (s, e) => UiThread.RunOnIdle(Close);
			buttonBottomPanel.AddChild(closeButton);

			mainContainerTopToBottom.AddChild(buttonBottomPanel);

			this.AddChild(mainContainerTopToBottom);
			this.AnchorAll();

			meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;

			AddChild(viewControls3D);

			// set the view to be a good angle and distance
			meshViewerWidget.ResetView();

			saveButton.Click += (s, e) => MergeAndSavePartsToStl();

			saveAndExitButton.Click += (s, e) => MergeAndSavePartsToStl();

			UnlockEditControls();

			// but make sure we can't use the right panel yet
			buttonRightPanelDisabledCover.Visible = true;

			viewControls3D.PartSelectVisible = partSelectVisible;
		}

		protected void LockEditControls()
		{
			editPlateButtonsContainer.Visible = false;
			buttonRightPanelDisabledCover.Visible = true;

			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
			{
				viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
			}
		}

		protected virtual void UnlockEditControls()
		{
			buttonRightPanelDisabledCover.Visible = false;
			processingProgressControl.Visible = false;

			editPlateButtonsContainer.Visible = true;
		}

		protected virtual void AddToBottomToolbar(GuiWidget parentContainer)
		{
		}

		protected virtual void AddToWordEditMenu(GuiWidget wordOptionContainer)
		{
		}

		protected virtual FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
		{
			FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRightPanel.Width = 200;
			{
				BorderDouble buttonMargin = new BorderDouble(top: 3);

				// put in the word editing menu
				{
					CheckBox expandWordOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Word Edit".Localize(), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
					expandWordOptions.Margin = new BorderDouble(bottom: 2);
					buttonRightPanel.AddChild(expandWordOptions);

					FlowLayoutWidget wordOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						HAnchor = HAnchor.ParentLeftRight,
						Visible = false
					};

					buttonRightPanel.AddChild(wordOptionContainer);

					// 
					AddToWordEditMenu(wordOptionContainer);

					expandWordOptions.CheckedStateChanged += (sender, e) =>
					{
						wordOptionContainer.Visible = expandWordOptions.Checked;
					};

					expandWordOptions.Checked = true;
				}

				GuiWidget verticalSpacer = new GuiWidget(vAnchor: VAnchor.ParentBottomTop);
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

		private async void MergeAndSavePartsToStl()
		{
			if (Scene.HasItems)
			{
				partSelectButtonWasClicked = viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect;

				processingProgressControl.ProcessType = "Saving Parts:".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				System.Diagnostics.Debugger.Launch();

				// TODO: jlewin - reuse save mechanism in View3DWidget once written
				/*
				// we sent the data to the async lists but we will not pull it back out (only use it as a temp holder).
				PushMeshGroupDataToAsynchLists(true);
				*/

				string fileName = prependToFileName + Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				string filePath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				processingProgressControl.RatioComplete = 0;
				await Task.Run(() => MergeAndSavePartsDoWork(filePath));

				PrintItem printItem = new PrintItem();

				printItem.Name = printItemName;
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
				System.Diagnostics.Debugger.Launch();
				// TODO: jlewin - reuse save mechanism in View3DWidget once written
				/*
				// push all the transforms into the meshes
				for (int i = 0; i < asyncMeshGroups.Count; i++)
				{
					asyncMeshGroups[i].Transform(MeshGroupTransforms[i]);

					processingProgressControl.RatioComplete = (double)i / asyncMeshGroups.Count * .1;
				}

				MeshFileIo.Save(asyncMeshGroups, filePath);
				*/

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
	}


}