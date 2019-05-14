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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class ConnectedFaces
	{
		internal int left;
		internal int right;
		internal int top;
		internal int bottom;

		internal int axis;
		internal double direction;

		internal ConnectedFaces(int axis, double offset, int left, int bottom, int right, int top)
		{
			this.axis = axis;
			this.direction = offset;
			this.left = left;
			this.bottom = bottom;
			this.right = right;
			this.top = top;
		}

		/// <summary>
		/// Find the tile that is connected to the face on an edge
		/// </summary>
		/// <param name="faceSharingEdge"></param>
		/// <returns></returns>
		internal int Tile(int faceSharingEdge)
		{
			if (faceSharingEdge == left)
			{
				return 3;
			}
			else if (faceSharingEdge == bottom)
			{
				return 1;
			}
			else if (faceSharingEdge == right)
			{
				return 5;
			}
			else if (faceSharingEdge == top)
			{
				return 7;
			}

			return 4;
		}

		/// <summary>
		/// Find the tile that is connected to the face on a corner
		/// </summary>
		/// <param name="faceCornerA"></param>
		/// <param name="faceCornerB"></param>
		/// <returns></returns>
		internal int Tile(int faceCornerA, int faceCornerB)
		{
			if (faceCornerA == left)
			{
				if (faceCornerB == top)
				{
					return 6;
				}
				else
				{
					return 0;
				}
			}
			else if (faceCornerA == bottom)
			{
				if (faceCornerB == left)
				{
					return 0;
				}
				else
				{
					return 2;
				}
			}
			else if (faceCornerA == right)
			{
				if (faceCornerB == top)
				{
					return 8;
				}
				else
				{
					return 2;
				}
			}
			else if (faceCornerA == top)
			{
				if (faceCornerB == left)
				{
					return 6;
				}
				else
				{
					return 8;
				}
			}

			return 4;
		}
	}

	public class TumbleCubeControl : GuiWidget
	{
		private Mesh cube = PlatonicSolids.CreateCube(4, 4, 4);
		private IPrimitive cubeTraceData;
		private InteractionLayer interactionLayer;
		private Vector2 lastMovePosition;
		private LightingData lighting = new LightingData();
		private Vector2 mouseDownPosition;
		private bool mouseOver = false;
		private List<TextureData> textureDatas = new List<TextureData>();
		private WorldView world;
		private ThemeConfig theme;
		private List<ConnectedFaces> connections = new List<ConnectedFaces>();
		private HitData lastHitData = new HitData();

		public TumbleCubeControl(InteractionLayer interactionLayer, ThemeConfig theme)
			: base(100 * GuiWidget.DeviceScale, 100 * GuiWidget.DeviceScale)
		{
			this.theme = theme;
			this.interactionLayer = interactionLayer;

			// this data needs to be made on the ui thread
			UiThread.RunOnIdle(() =>
			{
				TextureFace(0, "Top");
				TextureFace(2, "Left", Matrix4X4.CreateRotationZ(MathHelper.Tau / 4));
				TextureFace(4, "Right", Matrix4X4.CreateRotationZ(-MathHelper.Tau / 4));
				TextureFace(6, "Bottom", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
				TextureFace(8, "Back", Matrix4X4.CreateRotationZ(MathHelper.Tau / 2));
				TextureFace(10, "Front");
				cube.MarkAsChanged();

				connections.Add(new ConnectedFaces(2, 1, 1, 5, 2, 4));
				connections.Add(new ConnectedFaces(0, -1, 4, 3, 5, 0));
				connections.Add(new ConnectedFaces(0, 1, 5, 3, 4, 0));
				connections.Add(new ConnectedFaces(2, -1, 1, 4, 2, 5));
				connections.Add(new ConnectedFaces(1, 1, 2, 3, 1, 0));
				connections.Add(new ConnectedFaces(1, -1, 1, 3, 2, 0));

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
			var directionForward = Vector3Ex.TransformNormal(forward, interactionLayer.World.InverseModelviewMatrix);

			var up = Vector3.UnitY;
			var directionUp = Vector3Ex.TransformNormal(up, interactionLayer.World.InverseModelviewMatrix);
			world.RotationMatrix = Matrix4X4.LookAt(Vector3.Zero, directionForward, directionUp) * Matrix4X4.CreateScale(.8);

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

					DrawMouseHover(GetHitData(info.HitPosition));
				}
				else
				{
					ResetTextures();
				}
			}
		}

		private void DrawMouseHover(HitData hitData)
		{
			if (!lastHitData.Equals(hitData))
			{
				ResetTextures();
				lastHitData = hitData;
				for (int i = 0; i < 3; i++)
				{
					var faceIndex = hitData.FaceIndex[i];
					var tileIndex = hitData.TileIndex[i];
					if (faceIndex == -1)
					{
						// done rendering faces
						break;
					}

					var hitTexture = textureDatas[faceIndex];
					var hitGraphics = hitTexture.active.NewGraphics2D();
					switch (tileIndex)
					{
						case 0: // top
							hitGraphics.FillRectangle(0,
								0,
								hitTexture.source.Width / 4,
								hitTexture.source.Height / 4,
								theme.AccentMimimalOverlay);
							break;

						case 1:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4 * 1,
								hitTexture.source.Height / 4 * 0,
								hitTexture.source.Width / 4 * 3,
								hitTexture.source.Height / 4 * 1,
								theme.AccentMimimalOverlay);
							break;

						case 2:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4 * 3,
								hitTexture.source.Height / 4 * 0,
								hitTexture.source.Width / 4 * 4,
								hitTexture.source.Height / 4 * 1,
								theme.AccentMimimalOverlay);
							break;

						case 3:
							hitGraphics.FillRectangle(0,
								hitTexture.source.Height / 4,
								hitTexture.source.Width / 4,
								hitTexture.source.Height / 4 * 3,
								theme.AccentMimimalOverlay);
							break;

						case 4:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4,
									hitTexture.source.Height / 4,
									hitTexture.source.Width / 4 * 3,
									hitTexture.source.Height / 4 * 3,
									theme.AccentMimimalOverlay);
							break;

						case 5:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4 * 3,
								hitTexture.source.Height / 4 * 1,
								hitTexture.source.Width / 4 * 4,
								hitTexture.source.Height / 4 * 3,
								theme.AccentMimimalOverlay);
							break;

						case 6:
							hitGraphics.FillRectangle(0,
								hitTexture.source.Height / 4 * 3,
								hitTexture.source.Width / 4,
								hitTexture.source.Height,
								theme.AccentMimimalOverlay);
							break;

						case 7:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4 * 1,
								hitTexture.source.Height / 4 * 3,
								hitTexture.source.Width / 4 * 3,
								hitTexture.source.Height / 4 * 4,
								theme.AccentMimimalOverlay);
							break;

						case 8:
							hitGraphics.FillRectangle(hitTexture.source.Width / 4 * 3,
								hitTexture.source.Height / 4 * 3,
								hitTexture.source.Width / 4 * 4,
								hitTexture.source.Height / 4 * 4,
								theme.AccentMimimalOverlay);
							break;
					}

					hitTexture.textureChanged = true;
				}
				Invalidate();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);

			if (mouseEvent.Button != MouseButtons.Left)
			{
				return;
			}

			// Make sure we don't use the trace data before it is ready
			if (mouseDownPosition == mouseEvent.Position
				&& cubeTraceData != null)
			{
				Ray ray = world.GetRayForLocalBounds(mouseEvent.Position);
				IntersectInfo info = cubeTraceData.GetClosestIntersection(ray);

				if (info != null)
				{
					var hitData = GetHitData(info.HitPosition);
					var normalAndUp = GetDirectionForFace(hitData);

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

		private (Vector3 normal, Vector3 up) GetDirectionForFace(HitData hitData)
		{
			var up = Vector3.Zero;
			var normal = Vector3.Zero;
			var count = 0;

			for (int i = 0; i < 3; i++)
			{
				count++;
				int faceIndex = hitData.FaceIndex[i];
				switch (faceIndex)
				{
					case -1:
						count--;
						break;

					case 0:
						// top
						normal += -Vector3.UnitZ;
						if (count == 1)
						{
							up = (hitData.TileIndex[0] == 4) ? Vector3.UnitY : Vector3.UnitZ;
						}

						break;

					case 1:
						// Left
						normal += Vector3.UnitX;
						if (count == 1)
						{
							up = Vector3.UnitZ;
						}

						break;

					case 2:
						// Right
						normal += -Vector3.UnitX;
						if (count == 1)
						{
							up = Vector3.UnitZ;
						}

						break;

					case 3:
						// Bottom
						normal += Vector3.UnitZ;
						if (count == 1)
						{
							up = -Vector3.UnitY;
						}

						break;

					case 4:
						// Back
						normal += -Vector3.UnitY;
						if (count == 1)
						{
							up = Vector3.UnitZ;
						}

						break;

					case 5:
						// Front
						normal += Vector3.UnitY;
						if (count == 1)
						{
							up = Vector3.UnitZ;
						}

						break;
				}
			}

			return (normal / count, up);
		}

		private HitData GetHitData(Vector3 hitPosition)
		{
			for (int i = 0; i < 6; i++)
			{
				var faceData = connections[i];
				if (Math.Abs(hitPosition[faceData.axis] - faceData.direction * 2) < .0001)
				{
					// hit to the left
					if (hitPosition[connections[faceData.left].axis]
						* connections[faceData.left].direction
						> 1)
					{
						// hit to the bottom
						if (hitPosition[connections[faceData.bottom].axis]
							* connections[faceData.bottom].direction
							> 1)
						{
							return new HitData(i, 0,
								faceData.left, connections[faceData.left].Tile(i, faceData.bottom),
								faceData.bottom, connections[faceData.bottom].Tile(i, faceData.left));
						}
						// hit to the top
						else if (hitPosition[connections[faceData.top].axis]
							* connections[faceData.top].direction
							> 1)
						{
							return new HitData(i, 6,
								faceData.left, connections[faceData.left].Tile(i, faceData.top),
								faceData.top, connections[faceData.top].Tile(i, faceData.left));
						}

						return new HitData(i, 3, faceData.left, connections[faceData.left].Tile(i));
					}
					// hit to the right
					else if (hitPosition[connections[faceData.right].axis]
						* connections[faceData.right].direction
						> 1)
					{
						// hit to the bottom
						if (hitPosition[connections[faceData.bottom].axis]
							* connections[faceData.bottom].direction
							> 1)
						{
							return new HitData(i, 2,
								faceData.right, connections[faceData.right].Tile(i, faceData.bottom),
								faceData.bottom, connections[faceData.bottom].Tile(i, faceData.right));
						}
						// hit to the top
						else if (hitPosition[connections[faceData.top].axis]
							* connections[faceData.top].direction
							> 1)
						{
							return new HitData(i, 8,
								faceData.right, connections[faceData.right].Tile(i, faceData.top),
								faceData.top, connections[faceData.top].Tile(i, faceData.right));
						}

						return new HitData(i, 5, faceData.right, connections[faceData.right].Tile(i));
					}
					// hit to the bottom
					if (hitPosition[connections[faceData.bottom].axis]
						* connections[faceData.bottom].direction
						> 1)
					{
						return new HitData(i, 1, faceData.bottom, connections[faceData.bottom].Tile(i));
					}
					// hit to the top
					else if (hitPosition[connections[faceData.top].axis]
						* connections[faceData.top].direction
						> 1)
					{
						return new HitData(i, 7, faceData.top, connections[faceData.top].Tile(i));
					}

					// we have found the face we are hitting
					return new HitData(i, 4);
				}
			}

			return new HitData(0, 4);
		}

		private void ResetTextures()
		{
			bool hadReset = false;
			for (int i = 0; i < textureDatas.Count; i++)
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

			lastHitData = new HitData();
		}

		private void TextureFace(int face, string name, Matrix4X4? initialRotation = null)
		{
			var sourceTexture = new ImageBuffer(256, 256);

			var graphics = sourceTexture.NewGraphics2D();
			graphics.Clear(theme.BedColor);

			graphics.DrawString(name,
				sourceTexture.Width / 2,
				sourceTexture.Height / 2,
				60,
				justification: Agg.Font.Justification.Center,
				baseline: Agg.Font.Baseline.BoundsCenter,
				color: theme.TextColor);

			graphics.Render(new Stroke(new RoundedRect(.5, .5, 254.5, 254.4, 0), 6), theme.BedGridColors.Line);

			var activeTexture = new ImageBuffer(sourceTexture);
			ImageGlPlugin.GetImageGlPlugin(activeTexture, true);

			var faces = cube.GetCoplanerFaces(face);
			cube.PlaceTextureOnFaces(faces, activeTexture, cube.GetMaxPlaneProjection(faces, activeTexture, initialRotation));

			textureDatas.Add(new TextureData()
			{
				source = sourceTexture,
				active = activeTexture
			});
		}
	}

	internal class HitData
	{
		internal int[] FaceIndex = new int[] { -1, -1, -1 };
		internal int[] TileIndex = new int[] { -1, -1, -1 };

		public HitData()
		{
		}

		public HitData(int faceIndex0, int tileIndex0,
			int faceIndex1 = -1, int tileIndex1 = -1,
			int faceIndex2 = -1, int tileIndex2 = -1)
		{
			FaceIndex[0] = faceIndex0;
			TileIndex[0] = tileIndex0;
			FaceIndex[1] = faceIndex1;
			TileIndex[1] = tileIndex1;
			FaceIndex[2] = faceIndex2;
			TileIndex[2] = tileIndex2;
		}

		public override bool Equals(object obj)
		{
			if (obj is HitData hitData)
			{
				for (int i = 0; i < 3; i++)
				{
					if (FaceIndex[i] != hitData.FaceIndex[i]
						|| TileIndex != hitData.TileIndex)
					{
						return false;
					}
				}

				return true;
			}

			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			var hashCode = 1739626167;
			hashCode = hashCode * -1521134295 + EqualityComparer<int[]>.Default.GetHashCode(FaceIndex);
			hashCode = hashCode * -1521134295 + TileIndex.GetHashCode();
			return hashCode;
		}
	}

	internal class TextureData
	{
		internal ImageBuffer active;
		internal ImageBuffer source;
		internal bool textureChanged;
	}
}