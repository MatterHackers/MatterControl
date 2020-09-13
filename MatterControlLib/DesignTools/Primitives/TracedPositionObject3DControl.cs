/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TracedPositionObject3DControl : IObject3DControl
	{
		private readonly double blockSize = 7 * GuiWidget.DeviceScale;

		private ITraceable collisionVolume;
		private ThemeConfig theme;
		private IObject3DControlContext context;

		private Func<Vector3> getPosition;

		private IObject3D owner;

		private Action<Vector3> setPosition;

		private Mesh shape;
		private bool mouseOver;

		public TracedPositionObject3DControl(IObject3DControlContext context, IObject3D owner, Func<Vector3> getPosition, Action<Vector3> setPosition)
		{
			this.theme = ApplicationController.Instance.Theme;
			this.context = context;
			this.getPosition = getPosition;
			this.setPosition = setPosition;
			this.shape = PlatonicSolids.CreateCube();
			this.owner = owner;
			collisionVolume = shape.CreateBVHData();
		}

		public bool DrawOnTop => false;

		public string Name => "Traced Position";

		public bool Visible { get; set; }

		public void CancelOperation()
		{
		}

		public void Dispose()
		{
		}

		public void Draw(DrawGlContentEventArgs e)
		{
			var color = Color.Black;
			if (mouseOver)
			{
				color = theme.PrimaryAccentColor;
			}

			GLHelper.Render(shape, color, ShapeMatrix(), RenderTypes.Shaded);
		}

		private Matrix4X4 ShapeMatrix()
		{
			var worldPosition = getPosition().Transform(owner.Matrix);
			double distBetweenPixelsWorldSpace = context.World.GetWorldUnitsPerScreenPixelAtPosition(worldPosition);
			var scale = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace * blockSize);
			var offset = Matrix4X4.CreateTranslation(getPosition());

			var cubeMatrix = scale * owner.Matrix * offset;
			return cubeMatrix;
		}

		public ITraceable GetTraceable()
		{
			return new Transform(collisionVolume, ShapeMatrix());
		}

		public void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
		}

		public void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			if (mouseOver != mouseIsOver)
			{
				mouseOver = mouseIsOver;
				context.GuiSurface.Invalidate();
			}
		}

		public void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
		}

		public void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
		}
	}
}