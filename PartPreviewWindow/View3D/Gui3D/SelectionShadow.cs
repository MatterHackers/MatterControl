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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectionShadow : InteractionVolume
	{
		private View3DWidget view3DWidget;

		public SelectionShadow(View3DWidget view3DWidget)
			: base(null, view3DWidget.meshViewerWidget)
		{
			this.view3DWidget = view3DWidget;
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			Vector3 boundsCenter = selectedBounds.Center;

			TotalTransform = Matrix4X4.CreateTranslation(new Vector3(boundsCenter.x, boundsCenter.y, 0.1));
		}

		public override void DrawGlContent(EventArgs e)
		{
			if (MeshViewerToDrawWith.Scene.HasSelection)
			{
				// draw the bounds on the bed
				AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.Scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				Mesh bottomBounds = PlatonicSolids.CreateCube(selectedBounds.XSize, selectedBounds.YSize, .1);

				bool authorized = true;
				if (authorized)
				{
					GLHelper.Render(bottomBounds, new RGBA_Bytes(22, 80, 220, 30), TotalTransform, RenderTypes.Shaded);
				}
				else
				{
					TypeFacePrinter demoTextPrinter = new TypeFacePrinter("Demo ", 62);
					var bounds = demoTextPrinter.LocalBounds;

					var demoTexture = new ImageBuffer(512, 512);
					var scale = demoTexture.Width / bounds.Width;
					demoTextPrinter.Origin = new Vector2(0, -bounds.Bottom / scale / 2);

					Graphics2D imageGraphics = demoTexture.NewGraphics2D();
					imageGraphics.Clear(new RGBA_Bytes(RGBA_Bytes.White, 30));

					imageGraphics.Render(new VertexSourceApplyTransform(demoTextPrinter, Affine.NewScaling(scale, scale)), new RGBA_Bytes(RGBA_Bytes.White, 100));

					int count = 0;
					ImageBuffer clearImage = new ImageBuffer(2, 2, 32, new BlenderBGRA());
					foreach (Face face in bottomBounds.Faces)
					{
						if (count == 0)
						{
							MeshHelper.PlaceTextureOnFace(face, demoTexture);
						}
						else
						{
							MeshHelper.PlaceTextureOnFace(face, clearImage);
						}
						count++;
					}

					ImageGlPlugin.GetImageGlPlugin(demoTexture, true);
					GLHelper.Render(bottomBounds, new RGBA_Bytes(RGBA_Bytes.Black, 254), TotalTransform, RenderTypes.Shaded);
				}
			}

			base.DrawGlContent(e);
		}
	}
}