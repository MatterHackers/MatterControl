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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class TumbleCubeControl : GuiWidget
	{
		private Mesh cube = PlatonicSolids.CreateCube(3, 3, 3);
		private IPrimitive cubeTraceData;
		private InteractionLayer interactionLayer;
		private Vector2 lastMovePosition;
		private LightingData lighting = new LightingData();
		private Vector2 mouseDownPosition;
		private bool mouseOver = false;
		private List<TextureData> textureDatas = new List<TextureData>();
		private WorldView world;

		public TumbleCubeControl(InteractionLayer interactionLayer)
			: base(100 * GuiWidget.DeviceScale, 100 * GuiWidget.DeviceScale)
		{
			this.interactionLayer = interactionLayer;

			// this data needs to be made on the ui thread
			UiThread.RunOnIdle(() =>
			{
				cube.CleanAndMergeMesh(CancellationToken.None);
				TextureFace(cube.Faces[0], "Top");
				TextureFace(cube.Faces[1], "Left", Matrix4X4.CreateRotationZ(MathHelper.Tau / 4));
				TextureFace(cube.Faces[2], "Right", Matrix4X4.CreateRotationZ(-MathHelper.Tau / 4));
				TextureFace(cube.Faces[3], "Bottom", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
				TextureFace(cube.Faces[4], "Back", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
				TextureFace(cube.Faces[5], "Front");
				cube.MarkAsChanged();

				cubeTraceData = cube.CreateTraceData();
			});

			MouseLeave += (s, e) =>
			{
				ResetTextures();
			};
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!mouseOver)
			{
				ResetTextures();
			}

			var screenSpaceBounds = this.TransformToScreenSpace(LocalBounds);
			world = new WorldView(screenSpaceBounds.Width, screenSpaceBounds.Height);

			var forward = -Vector3.UnitZ;
			var directionForward = Vector3.TransformNormal(forward, interactionLayer.World.InverseModelviewMatrix);

			var up = Vector3.UnitY;
			var directionUp = Vector3.TransformNormal(up, interactionLayer.World.InverseModelviewMatrix);
			world.RotationMatrix = Matrix4X4.LookAt(Vector3.Zero, directionForward, directionUp);

			GLHelper.SetGlContext(world, screenSpaceBounds, lighting);
			GLHelper.Render(cube, Color.White, Matrix4X4.Identity, RenderTypes.Shaded);
			GLHelper.UnsetGlContext();

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
			mouseOver = false;
			// find the ray for this control
			// check what face it hits
			// mark that face to draw a highlight
			base.OnMouseMove(mouseEvent);

			// rotate the view
			if (MouseDownOnWidget)
			{
				var movePosition = mouseEvent.Position;
				Quaternion activeRotationQuaternion = TrackBallController.GetRotationForMove(new Vector2(Width / 2, Height / 2), world, Width, lastMovePosition, movePosition, false);

				if (activeRotationQuaternion != Quaternion.Identity)
				{
					lastMovePosition = movePosition;
					interactionLayer.World.RotationMatrix = interactionLayer.World.RotationMatrix * Matrix4X4.CreateRotation(activeRotationQuaternion);
					interactionLayer.Invalidate();
				}
			}
			else if (world != null
				&& cubeTraceData != null) // Make sure we don't use the trace data before it is ready
			{
				Ray ray = world.GetRayForLocalBounds(mouseEvent.Position);
				IntersectInfo info = cubeTraceData.GetClosestIntersection(ray);

				if (info != null)
				{
					mouseOver = true;

					int hitFace = GetFaceFromHit(info.HitPosition);
					var textureData = textureDatas[hitFace];
					if (!textureData.textureChanged)
					{
						ResetTextures();

						var graphics = textureData.active.NewGraphics2D();
						graphics.Render(textureData.source, 0, 0);
						graphics.FillRectangle(textureData.source.GetBoundingRect(), new Color(Color.LightBlue, 100));
						textureData.textureChanged = true;

						Invalidate();
					}
				}
				else
				{
					ResetTextures();
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);

			// Make sure we don't use the trace data before it is ready
			if (mouseDownPosition == mouseEvent.Position
				&& cubeTraceData != null)
			{
				Ray ray = world.GetRayForLocalBounds(mouseEvent.Position);
				IntersectInfo info = cubeTraceData.GetClosestIntersection(ray);

				if (info != null)
				{
					var faceIndex = GetFaceFromHit(info.HitPosition);
					var normalAndUp = GetDirectionForFace(faceIndex);

					var look = Matrix4X4.LookAt(Vector3.Zero, normalAndUp.normal, normalAndUp.up);

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

		private (Vector3 normal, Vector3 up) GetDirectionForFace(int faceIndex)
		{
			switch (faceIndex)
			{
				case 0:
					// Top
					return (-Vector3.UnitZ, Vector3.UnitY);

				case 1:
					// Left
					return (Vector3.UnitX, Vector3.UnitZ);

				case 2:
					// Right
					return (-Vector3.UnitX, Vector3.UnitZ);

				case 3:
					// Bottom
					return (Vector3.UnitZ, -Vector3.UnitY);

				case 4:
					// Back
					return (-Vector3.UnitY, Vector3.UnitZ);

				case 5:
					// Front
					return (Vector3.UnitY, Vector3.UnitZ);
			}

			return (Vector3.UnitZ, Vector3.UnitZ);
		}

		private int GetFaceFromHit(Vector3 hitPosition)
		{
			if (Math.Abs(hitPosition.Z - 1.5) < .001)
			{
				// Top
				return 0;
			}
			else if (Math.Abs(hitPosition.X + 1.5) < .001)
			{
				// Left
				return 1;
			}
			else if (Math.Abs(hitPosition.X - 1.5) < .001)
			{
				// Right
				return 2;
			}
			else if (Math.Abs(hitPosition.Z + 1.5) < .001)
			{
				// Bottom
				return 3;
			}
			else if (Math.Abs(hitPosition.Y - 1.5) < .001)
			{
				// Back
				return 4;
			}
			else if (Math.Abs(hitPosition.Y + 1.5) < .001)
			{
				// Front
				return 5;
			}

			return 0;
		}

		private void ResetTextures()
		{
			bool hadReset = false;
			for (int i = 0; i < 6; i++)
			{
				var textureData = textureDatas[i];
				if (textureData.textureChanged)
				{
					var graphics = textureData.active.NewGraphics2D();
					graphics.Render(textureData.source, 0, 0);
					textureData.textureChanged = false;
					hadReset = true;
				}
			}

			if (hadReset)
			{
				Invalidate();
			}
		}

		private void TextureFace(Face face, string name, Matrix4X4? initialRotation = null)
		{
			ImageBuffer sourceTexture = new ImageBuffer(256, 256);
			var frontGraphics = sourceTexture.NewGraphics2D();
			frontGraphics.Clear(Color.White);
			frontGraphics.DrawString(name,
				sourceTexture.Width / 2,
				sourceTexture.Height / 2,
				60,
				justification: Agg.Font.Justification.Center,
				baseline: Agg.Font.Baseline.BoundsCenter);
			frontGraphics.Render(new Stroke(new RoundedRect(.5, .5, 254.5, 254.4, 0), 6), Color.DarkGray);
			var activeTexture = new ImageBuffer(sourceTexture);
			ImageGlPlugin.GetImageGlPlugin(activeTexture, true);
			MeshHelper.PlaceTextureOnFace(face, activeTexture, MeshHelper.GetMaxFaceProjection(face, activeTexture, initialRotation));

			textureDatas.Add(new TextureData()
			{
				source = sourceTexture,
				active = activeTexture
			});
		}
	}

	internal class TextureData
	{
		internal ImageBuffer active;
		internal ImageBuffer source;
		internal bool textureChanged;
	}
}