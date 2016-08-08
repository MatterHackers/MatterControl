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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class MirrorControls : FlowLayoutWidget
	{
		private CheckBox expandMirrorOptions;
		private FlowLayoutWidget mirrorOptionContainer;
		private View3DWidget view3DWidget;

		public MirrorControls(View3DWidget view3DWidget)
			: base(FlowDirection.TopToBottom)
		{
			this.view3DWidget = view3DWidget;

			// put in the mirror options
			{
				expandMirrorOptions = view3DWidget.ExpandMenuOptionFactory.GenerateCheckBoxButton("Mirror".Localize().ToUpper(), StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32,32).InvertLightness());
				expandMirrorOptions.Margin = new BorderDouble(bottom: 2);
				this.AddChild(expandMirrorOptions);

				mirrorOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				mirrorOptionContainer.HAnchor = HAnchor.ParentLeftRight;
				mirrorOptionContainer.Visible = false;
				this.AddChild(mirrorOptionContainer);

				AddMirrorControls(mirrorOptionContainer);
			}

			expandMirrorOptions.CheckedStateChanged += expandMirrorOptions_CheckedStateChanged;
		}

		private void AddMirrorControls(FlowLayoutWidget buttonPanel)
		{
			List<GuiWidget> mirrorControls = new List<GuiWidget>();

			double oldFixedWidth = view3DWidget.textImageButtonFactory.FixedWidth;
			view3DWidget.textImageButtonFactory.FixedWidth = view3DWidget.EditButtonHeight;

			FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonContainer.HAnchor = HAnchor.ParentLeftRight;

			Button mirrorXButton = view3DWidget.textImageButtonFactory.Generate("X", centerText: true);
			buttonContainer.AddChild(mirrorXButton);
			mirrorControls.Add(mirrorXButton);
			mirrorXButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (view3DWidget.SelectedMeshGroupIndex != -1)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(0), () => MirrorOnAxis(0)));
				}
			};

			Button mirrorYButton = view3DWidget.textImageButtonFactory.Generate("Y", centerText: true);
			buttonContainer.AddChild(mirrorYButton);
			mirrorControls.Add(mirrorYButton);
			mirrorYButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (view3DWidget.SelectedMeshGroupIndex != -1)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(1), () => MirrorOnAxis(1)));
				}
			};

			Button mirrorZButton = view3DWidget.textImageButtonFactory.Generate("Z", centerText: true);
			buttonContainer.AddChild(mirrorZButton);
			mirrorControls.Add(mirrorZButton);
			mirrorZButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				if (view3DWidget.SelectedMeshGroupIndex != -1)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(2), () => MirrorOnAxis(2)));
				}
			};
			buttonPanel.AddChild(buttonContainer);
			buttonPanel.AddChild(view3DWidget.GenerateHorizontalRule());
			view3DWidget.textImageButtonFactory.FixedWidth = oldFixedWidth;
		}

		private void MirrorOnAxis(int axisIndex)
		{
			view3DWidget.SelectedMeshGroup.ReverseFaceEdges();
			Vector3 mirorAxis = Vector3.One;
			mirorAxis[axisIndex] = -1;
			view3DWidget.SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(view3DWidget.SelectedMeshGroup, view3DWidget.SelectedMeshGroupTransform, Matrix4X4.CreateScale(mirorAxis));
			view3DWidget.PartHasBeenChanged();
			Invalidate();
		}

		private void expandMirrorOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (mirrorOptionContainer.Visible != expandMirrorOptions.Checked)
			{
				mirrorOptionContainer.Visible = expandMirrorOptions.Checked;
			}
		}
	}
}