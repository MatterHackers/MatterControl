/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using System.Threading;
	using MatterHackers.Agg.Platform;
	using MatterHackers.DataConverters3D;
	using MatterHackers.PolygonMesh;
	using MatterHackers.PolygonMesh.Processors;
	using MatterHackers.RenderOpenGl;
	using MatterHackers.VectorMath;

	public class LogoSpinner
	{
		public Color MeshColor { get; set; } = Color.White;

		public bool SpinLogo { get; set; } = true;

		public LogoSpinner(GuiWidget widget, double scale = 1.6, double spinSpeed = 0.6, double yOffset = 0.5, double rotateX = -0.1)
		{
			// loading animation stuff
			LightingData lighting = new LightingData();

			Mesh logoMesh;

			using (var logoStream = AggContext.StaticData.OpenStream(Path.Combine("Stls", "MH Logo.stl")))
			{
				logoMesh = StlProcessing.Load(logoStream, CancellationToken.None);
			}

			// Position
			var aabb = logoMesh.GetAxisAlignedBoundingBox();
			logoMesh.Transform(Matrix4X4.CreateTranslation(-aabb.Center));

			logoMesh.Transform(Matrix4X4.CreateScale(scale / aabb.XSize));

			var anglePerDraw = 1 / MathHelper.Tau * spinSpeed;
			var angle = 0.0;

			widget.BeforeDraw += (s, e) =>
			{
				var screenSpaceBounds = widget.TransformToScreenSpace(widget.LocalBounds);
				WorldView world = new WorldView(screenSpaceBounds.Width, screenSpaceBounds.Height);
				world.Translate(new Vector3(0, yOffset, 0));
				world.Rotate(Quaternion.FromEulerAngles(new Vector3(rotateX, 0, 0)));

				GLHelper.SetGlContext(world, screenSpaceBounds, lighting);
				GLHelper.Render(logoMesh, this.MeshColor, Matrix4X4.CreateRotationY(angle), RenderTypes.Shaded);
				GLHelper.UnsetGlContext();
			};

			Animation spinAnimation = new Animation()
			{
				DrawTarget = widget,
				FramesPerSecond = 20
			};
			spinAnimation.Update += (s, time) =>
			{
				if (this.SpinLogo)
				{
					angle += anglePerDraw;
				}
			};
			spinAnimation.Start();
		}
	}
}