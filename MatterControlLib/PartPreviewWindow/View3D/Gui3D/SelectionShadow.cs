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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectionShadow : Object3DControl
	{
		private static Mesh normalShadowMesh;

		private Color shadowColor;

		private readonly ThemeConfig theme;

		public SelectionShadow(IObject3DControlContext context)
			: base(context)
		{
			theme = AppContext.Theme;
			shadowColor = theme.ResolveColor(theme.BackgroundColor, Color.Black.WithAlpha(80)).WithAlpha(110);
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			Vector3 boundsCenter = selectedBounds.Center;

			TotalTransform = Matrix4X4.CreateTranslation(new Vector3(boundsCenter.X, boundsCenter.Y, 0.1));
		}

		private Mesh GetNormalShadowMesh()
		{
			if (normalShadowMesh == null)
			{
				normalShadowMesh = PlatonicSolids.CreateCube(1, 1, .1);
			}

			return normalShadowMesh;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			var selectedItem = RootSelection;
			if (selectedItem != null
				&& Object3DControlContext.Scene.ShowSelectionShadow)
			{
				// draw the bounds on the bed
				AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

				var withScale = Matrix4X4.CreateScale(selectedBounds.XSize, selectedBounds.YSize, 1) * TotalTransform;
				GLHelper.Render(GetNormalShadowMesh(), shadowColor, withScale, RenderTypes.Shaded);
			}

			base.Draw(e);
		}

		public override AxisAlignedBoundingBox GetWorldspaceAABB()
		{
			var selectedItem = RootSelection;
			if (selectedItem != null
				&& Object3DControlContext.Scene.ShowSelectionShadow)
			{
				AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				var withScale = Matrix4X4.CreateScale(selectedBounds.XSize, selectedBounds.YSize, 1) * TotalTransform;
				return GetNormalShadowMesh().GetAxisAlignedBoundingBox().NewTransformed(withScale);
			}

			return AxisAlignedBoundingBox.Empty();
		}

		public override void Dispose()
		{
			// no widgets allocated so nothing to close
		}

		public override void CancelOperation()
		{
		}
	}
}