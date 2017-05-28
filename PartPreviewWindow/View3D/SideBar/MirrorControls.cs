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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class MirrorControls : PopupActionPanel
	{
		private View3DWidget view3DWidget;

		public MirrorControls(View3DWidget view3DWidget, TextImageButtonFactory buttonFactory)
		{
			this.view3DWidget = view3DWidget;

			List<GuiWidget> mirrorControls = new List<GuiWidget>();

			FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonContainer.HAnchor = HAnchor.FitToChildren;

			Button mirrorXButton = buttonFactory.Generate("X", centerText: true);
			buttonContainer.AddChild(mirrorXButton);
			mirrorControls.Add(mirrorXButton);
			mirrorXButton.Click += (s, e) =>
			{
				if (view3DWidget.Scene.HasSelection)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(0), () => MirrorOnAxis(0)));

					throw new NotImplementedException();
					
					/* TODO: Revise above for scenebundle with the following...
					var selectedItem = view3DWidget.Scene.SelectedItem;
					selectedItem.Mesh.ReverseFaceEdges();
					selectedItem.Matrix = PlatingHelper.ApplyAtCenter(selectedItem, Matrix4X4.CreateScale(-1, 1, 1));
					view3DWidget.PartHasBeenChanged();
					Invalidate(); */
				}
			};

			Button mirrorYButton = buttonFactory.Generate("Y", centerText: true);
			buttonContainer.AddChild(mirrorYButton);
			mirrorControls.Add(mirrorYButton);
			mirrorYButton.Click += (s, e) =>
			{
				if (view3DWidget.Scene.HasSelection)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(1), () => MirrorOnAxis(1)));

					throw new NotImplementedException();

					/* TODO: Revise above for scenebundle with the following...
					var selectedItem = view3DWidget.Scene.SelectedItem;
					selectedItem.Mesh.ReverseFaceEdges();
					selectedItem.Matrix = PlatingHelper.ApplyAtCenter(selectedItem, Matrix4X4.CreateScale(1, -1, 1));
					view3DWidget.PartHasBeenChanged();
					Invalidate(); */
				}
			};

			Button mirrorZButton = buttonFactory.Generate("Z", centerText: true);
			buttonContainer.AddChild(mirrorZButton);
			mirrorControls.Add(mirrorZButton);
			mirrorZButton.Click += (s, e) =>
			{
				if (view3DWidget.Scene.HasSelection)
				{
					view3DWidget.UndoBuffer.AddAndDo(new UndoRedoActions(() => MirrorOnAxis(2), () => MirrorOnAxis(2)));

					throw new NotImplementedException();

					/* TODO: Revise above for scenebundle with the following...
					var selectedItem = view3DWidget.Scene.SelectedItem;
					selectedItem.Mesh.ReverseFaceEdges();
					selectedItem.Matrix = PlatingHelper.ApplyAtCenter(selectedItem, Matrix4X4.CreateScale(1, 1, -1));
					view3DWidget.PartHasBeenChanged();
					Invalidate(); */
				}
			};

			this.AddChild(buttonContainer);
		}

		private void MirrorOnAxis(int axisIndex)
		{
			/* TODO: Revise for scene_bundle
			view3DWidget.SelectedMeshGroup.ReverseFaceEdges();
			Vector3 mirorAxis = Vector3.One;
			mirorAxis[axisIndex] = -1;
			view3DWidget.SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(view3DWidget.SelectedMeshGroup, view3DWidget.SelectedMeshGroupTransform, Matrix4X4.CreateScale(mirorAxis));
			view3DWidget.PartHasBeenChanged(); */
			Invalidate();
		}
	}
}