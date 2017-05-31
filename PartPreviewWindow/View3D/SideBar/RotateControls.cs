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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RotateControls : PopupActionPanel
	{
		private View3DWidget view3DWidget;

		public RotateControls(View3DWidget view3DWidget, TextImageButtonFactory buttonFactory, TextImageButtonFactory smallMarginButtonFactory)
		{
			this.view3DWidget = view3DWidget;
			this.HAnchor = HAnchor.FitToChildren;

			var degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			degreesContainer.HAnchor = HAnchor.ParentLeftRight;
			degreesContainer.Padding = new BorderDouble(5);

			var degreesLabel = new TextWidget("Degrees".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor);
			degreesContainer.AddChild(degreesLabel);
			degreesContainer.AddChild(new HorizontalSpacer());

			var degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, allowDecimals: true, increment: 5, minValue: -360, maxValue: 360);
			degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
			degreesContainer.AddChild(degreesControl);

			this.AddChild(degreesContainer);

			var rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.FitToChildren
			};

			// Reused on each button below
			var rotateIcon = StaticData.Instance.LoadIcon("icon_rotate_32x32.png", 32, 32);

			var initialMargin = smallMarginButtonFactory.Margin;

			smallMarginButtonFactory.Margin = 0;

			//smallMarginButtonFactory.Margin = 0;

			Button rotateXButton = CreateAxisButton("X", smallMarginButtonFactory.Generate("", rotateIcon));
			rotateXButton.Click += (s, e) =>
			{
				var scene = view3DWidget.Scene;
				if (scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationX(radians);
					Matrix4X4 undoTransform = scene.SelectedItem.Matrix;
					scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(scene.SelectedItem, rotation);
					view3DWidget.UndoBuffer.Add(new TransformUndoCommand(view3DWidget, scene.SelectedItem, undoTransform, scene.SelectedItem.Matrix));
					view3DWidget.PartHasBeenChanged();
					Invalidate();
				}
			};
			rotateButtonContainer.AddChild(rotateXButton);

			Button rotateYButton = CreateAxisButton("Y", smallMarginButtonFactory.Generate("", rotateIcon));
			rotateYButton.Click += (s, e) =>
			{
				var scene = view3DWidget.Scene;
				if (scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationY(radians);
					Matrix4X4 undoTransform = scene.SelectedItem.Matrix;
					scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(scene.SelectedItem, rotation);
					view3DWidget.UndoBuffer.Add(new TransformUndoCommand(view3DWidget, scene.SelectedItem, undoTransform, scene.SelectedItem.Matrix));
					view3DWidget.PartHasBeenChanged();
					Invalidate();
				}
			};
			rotateButtonContainer.AddChild(rotateYButton);

			Button rotateZButton = CreateAxisButton("Z", smallMarginButtonFactory.Generate("", rotateIcon));
			rotateZButton.Click += (s, e) =>
			{
				var scene = view3DWidget.Scene;
				if (scene.HasSelection)
				{
					double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
					Matrix4X4 rotation = Matrix4X4.CreateRotationZ(radians);
					Matrix4X4 undoTransform = scene.SelectedItem.Matrix;
					scene.SelectedItem.Matrix = PlatingHelper.ApplyAtCenter(scene.SelectedItem, rotation);
					view3DWidget.UndoBuffer.Add(new TransformUndoCommand(view3DWidget, scene.SelectedItem, undoTransform, scene.SelectedItem.Matrix));
					view3DWidget.PartHasBeenChanged();
					Invalidate();
				}
			};
			rotateButtonContainer.AddChild(rotateZButton);

			this.AddChild(rotateButtonContainer);

			Button layFlatButton = buttonFactory.Generate("Align to Bed".Localize(), centerText: true);
			layFlatButton.HAnchor = HAnchor.ParentCenter;
			layFlatButton.Margin = new BorderDouble(0, 0, 0, 8);
			layFlatButton.Cursor = Cursors.Hand;
			layFlatButton.Click += (s, e) =>
			{
				var scene = view3DWidget.Scene;

				if (scene.HasSelection)
				{
					Matrix4X4 undoTransform = scene.SelectedItem.Matrix;
					view3DWidget.MakeLowestFaceFlat(scene.SelectedItem);
					view3DWidget.UndoBuffer.Add(new TransformUndoCommand(view3DWidget, scene.SelectedItem, undoTransform, scene.SelectedItem.Matrix));
					view3DWidget.PartHasBeenChanged();
					Invalidate();
				}
			};
			this.AddChild(layFlatButton);

			smallMarginButtonFactory.Margin = initialMargin;
		}

		private static Button CreateAxisButton(string axis, Button button)
		{
			var textWidget = new TextWidget(axis, pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			textWidget.Margin = new BorderDouble(3, 0, 0, 0);
			textWidget.AnchorCenter();

			button.Margin = new BorderDouble(0, 0, 12, 0);
			button.AddChild(textWidget);

			return button;
		}
	}
}