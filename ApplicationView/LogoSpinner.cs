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

using System;
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using System.Threading;
	using MatterHackers.Agg.Platform;
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.RenderOpenGl;
	using MatterHackers.VectorMath;

	public class LogoSpinner
	{
		public Color MeshColor { get; set; } = Color.White;

		public float[] AmbientColor { get; set; } = new float[] { 0, 0, 0, 0 };

		public LogoSpinner(GuiWidget widget, double scale = 1.6, double spinSpeed = 0.6, double yOffset = 0.5, double rotateX = -0.1)
		{
			// loading animation stuff
			LightingData lighting = new LightingData();

			var logoPath = AggContext.StaticData.MapPath(Path.Combine("Stls", "MH Logo.stl"));
			var logoMesh = MeshFileIo.Load(logoPath, CancellationToken.None).Mesh;

			// Position
			var aabb = logoMesh.GetAxisAlignedBoundingBox();
			logoMesh.Transform(Matrix4X4.CreateTranslation(-aabb.Center));

			logoMesh.Transform(Matrix4X4.CreateScale(scale / aabb.XSize));

			var loadTime = Stopwatch.StartNew();
			var anglePerDraw = 1 / MathHelper.Tau * spinSpeed;
			var angle = 0.0;

			widget.BeforeDraw += (s, e) =>
			{
				var thisAngle = Math.Min(anglePerDraw, loadTime.Elapsed.TotalSeconds * MathHelper.Tau);
				angle += thisAngle;
				loadTime.Restart();

				var screenSpaceBounds = widget.TransformToScreenSpace(widget.LocalBounds);
				WorldView world = new WorldView(screenSpaceBounds.Width, screenSpaceBounds.Height);
				world.Translate(new Vector3(0, yOffset, 0));
				world.Rotate(Quaternion.FromEulerAngles(new Vector3(rotateX, 0, 0)));

				InteractionLayer.SetGlContext(world, screenSpaceBounds, lighting, this.AmbientColor);
				GLHelper.Render(logoMesh, this.MeshColor, Matrix4X4.CreateRotationY(angle), RenderTypes.Shaded);
				InteractionLayer.UnsetGlContext();
			};

			UiThread.SetInterval(widget.Invalidate, .05, () => !widget.HasBeenClosed);
		}
	}
}