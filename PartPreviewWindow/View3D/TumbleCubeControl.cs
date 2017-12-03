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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class TumbleCubeControl : GuiWidget
	{
		LightingData lighting = new LightingData();
		Mesh cube = PlatonicSolids.CreateCube(3, 3, 3);
		IPrimitive cubeTraceData;
		InteractionLayer interactionLayer;
		WorldView world;
		Vector2 mouseDownPosition;
		Vector2 lastMovePosition;

		public TumbleCubeControl(InteractionLayer interactionLayer)
			: base(100, 100)
		{
			this.interactionLayer = interactionLayer;
		
			TextureFace(cube.Faces[0], "Top");
			TextureFace(cube.Faces[1], "Left", Matrix4X4.CreateRotationZ(MathHelper.Tau / 4));
			TextureFace(cube.Faces[2], "Right", Matrix4X4.CreateRotationZ(-MathHelper.Tau/4));
			TextureFace(cube.Faces[3], "Bottom", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
			TextureFace(cube.Faces[4], "Back", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
			TextureFace(cube.Faces[5], "Front");

			cubeTraceData = cube.CreateTraceData();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var screenSpcaeBounds = this.TransformToScreenSpace(LocalBounds);
			world = new WorldView(screenSpcaeBounds.Width, screenSpcaeBounds.Height);

			var forward = -Vector3.UnitZ;
			var directionForward = Vector3.TransformNormal(forward, interactionLayer.World.InverseModelviewMatrix);

			var up = Vector3.UnitY;
			var directionUp = Vector3.TransformNormal(up, interactionLayer.World.InverseModelviewMatrix);
			world.RotationMatrix = Matrix4X4.LookAt(Vector3.Zero, directionForward, directionUp);

			InteractionLayer.SetGlContext(world, screenSpcaeBounds, lighting);
			GLHelper.Render(cube, Color.White, Matrix4X4.Identity, RenderTypes.Shaded);
			InteractionLayer.UnsetGlContext();

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			mouseDownPosition = mouseEvent.Position;
			lastMovePosition = mouseDownPosition;
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// find the ray for this control
			// check what face it hits
			// mark that face to draw a highlight
			base.OnMouseMove(mouseEvent);

			// rotate the view
			if (MouseDownOnWidget)
			{
				var movePosition = mouseEvent.Position;
				Quaternion activeRotationQuaternion = TrackBallController.GetRotationForMove(world, Width, lastMovePosition, movePosition, false);

				if (activeRotationQuaternion != Quaternion.Identity)
				{
					lastMovePosition = movePosition;
					interactionLayer.World.RotationMatrix = interactionLayer.World.RotationMatrix * Matrix4X4.CreateRotation(activeRotationQuaternion);
					interactionLayer.Invalidate();
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);

			if (mouseDownPosition == mouseEvent.Position)
			{
				Ray ray = world.GetRayForLocalBounds(mouseEvent.Position);
				IntersectInfo info = cubeTraceData.GetClosestIntersection(ray);

				if (info != null)
				{
					var normal = ((TriangleShape)info.closestHitObject).Plane.PlaneNormal;
					var directionForward = -new Vector3(normal);

					var directionUp = Vector3.UnitY;
					if (directionForward.Equals(Vector3.UnitX, .001))
					{
						directionUp = Vector3.UnitZ;
					}
					else if (directionForward.Equals(-Vector3.UnitX, .001))
					{
						directionUp = Vector3.UnitZ;
					}
					else if (directionForward.Equals(Vector3.UnitY, .001))
					{
						directionUp = Vector3.UnitZ;
					}
					else if (directionForward.Equals(-Vector3.UnitY, .001))
					{
						directionUp = Vector3.UnitZ;
					}
					else if (directionForward.Equals(Vector3.UnitZ, .001))
					{
						directionUp = -Vector3.UnitY;
					}

					var look = Matrix4X4.LookAt(Vector3.Zero, directionForward, directionUp);

					var start = new Quaternion(interactionLayer.World.RotationMatrix);
					var end = new Quaternion(look);

					Task.Run(() =>
					{
						// TODO: stop any spinning happening in the view
						double duration = .25;
						var timer = Stopwatch.StartNew();
						var time = timer.Elapsed.TotalSeconds;
						while (time < duration)
						{
							var current = Quaternion.Slerp(start, end, time / duration);
							UiThread.RunOnIdle(() =>
							{
								interactionLayer.World.RotationMatrix = Matrix4X4.CreateRotation(current);
								Invalidate();
							});
							time = timer.Elapsed.TotalSeconds;
							Thread.Sleep(10);
						}
						interactionLayer.World.RotationMatrix = Matrix4X4.CreateRotation(end);
						Invalidate();
					});
				}
			}

			interactionLayer.Focus();
		}

		private static void TextureFace(Face face, string name, Matrix4X4? initialRotation = null)
		{
			ImageBuffer textureToUse = new ImageBuffer(256, 256);
			var frontGraphics = textureToUse.NewGraphics2D();
			frontGraphics.Clear(Color.White);
			frontGraphics.DrawString(name,
				textureToUse.Width / 2,
				textureToUse.Height / 2,
				60,
				justification: Agg.Font.Justification.Center,
				baseline: Agg.Font.Baseline.BoundsCenter);
			frontGraphics.Render(new Stroke(new RoundedRect(.5, .5, 254.5, 254.4, 0), 6), Color.DarkGray);
			//ImageGlPlugin.GetImageGlPlugin(textureToUse, true);
			MeshHelper.PlaceTextureOnFace(face, textureToUse, MeshHelper.GetMaxFaceProjection(face, textureToUse, initialRotation));
		}
	}
}