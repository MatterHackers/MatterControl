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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    using Matter_CAD_Lib.DesignTools.Interfaces;
    using Matter_CAD_Lib.DesignTools.Objects3D;
    using MatterHackers.Agg.RasterizerScanline;
    using MatterHackers.Agg.VertexSource;
    using MatterHackers.RayTracer.Light;
    using MatterHackers.RayTracer.Traceable;

    public class ThumbnailTracer
	{
		private static Vector3 lightNormal = (new Vector3(-1, 1, 1)).GetNormal();
		private static ColorF lightIllumination = new ColorF(1, 1, 1);
		private static ColorF ambiantIllumination = new ColorF(.4, .4, .4);

		public ImageBuffer destImage;

		private ITraceable allObjects;

		private Transform allObjectsHolder;

		private IObject3D sceneToRender;

		private Scene scene;
		private Point2D size;

		private WorldView world;

		private RayTracer rayTracer = new RayTracer()
		{
			AntiAliasing = AntiAliasing.VeryHigh,
			MultiThreaded = false,
		};

		public ThumbnailTracer(IObject3D item, int width, int height)
		{
			size = new Point2D(width, height);

			world = new WorldView(width, height);

			sceneToRender = item;

			SetRenderPosition();
		}

		public bool MultiThreaded
		{
			get => rayTracer.MultiThreaded;
			set => rayTracer.MultiThreaded = value;
		}

		public void TraceScene()
		{
			using (new QuickTimer("Create Trace Data"))
			{
				CreateScene();
			}

			var rect = new RectangleInt(0, 0, size.x, size.y);
			if (destImage == null || destImage.Width != rect.Width || destImage.Height != rect.Height)
			{
				destImage = new ImageBuffer(rect.Width, rect.Height);
			}

			using (new QuickTimer("Do Ray Trace"))
			{
				rayTracer.RayTraceScene(rect, scene);
				rayTracer.CopyColorBufferToImage(destImage, rect);
			}
		}

		public void SetRenderPosition()
		{
			world.Reset();
			world.Scale = .03;

			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, -MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(MathHelper.Tau * .19, 0, 0)));

			if (sceneToRender != null)
			{
				world.Fit(sceneToRender, new RectangleDouble(0, 0, size.x, size.y), Matrix4X4.Identity);
			}
		}

		private void AddAFloor()
		{
			var testImage = new ImageBuffer(200, 200);
			Graphics2D graphics = testImage.NewGraphics2D();
			var rand = new Random(0);
			for (int i = 0; i < 100; i++)
			{
				var color = new Color(rand.NextDouble(), rand.NextDouble(), rand.NextDouble());
				graphics.Circle(new Vector2(rand.NextDouble() * testImage.Width, rand.NextDouble() * testImage.Height), rand.NextDouble() * 40 + 10, color);
			}
			scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new TextureMaterial(testImage, 0, 0, .2, 1)));
			//scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new ChessboardMaterial(new RGBA_Floats(1, 1, 1), new RGBA_Floats(0, 0, 0), 0, 0, 1, 0.7)));
		}

		internal class RenderPoint
		{
			internal Vector2 position;
			internal double z;
			internal Color color;
		}

		internal void render_gouraud(IImageByte backBuffer, IScanlineCache sl, IRasterizer ras, RenderPoint[] points)
		{
			var image = new ImageBuffer();
			image.Attach(backBuffer, new BlenderZBuffer());

			var ren_base = new ImageClippingProxy(image);

			var span_gen = new span_gouraud_rgba();
			span_gen.colors(points[0].color, points[1].color, points[2].color);
			span_gen.triangle(points[0].position.X, points[0].position.Y, points[1].position.X, points[1].position.Y, points[2].position.X, points[2].position.Y);

			ras.add_path(span_gen);

			var scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.GenerateAndRender(ras, sl, ren_base, new span_allocator(), span_gen);
		}

		public void RenderPerspective(Graphics2D graphics2D, Mesh meshToDraw, Color partColorIn, double minZ, double maxZ)
		{
			ColorF partColor = partColorIn.ToColorF();
			graphics2D.Rasterizer.gamma(new gamma_power(.3));
			RenderPoint[] points = new RenderPoint[3] { new RenderPoint(), new RenderPoint(), new RenderPoint() };

			throw new NotImplementedException();
//			foreach (Face face in meshToDraw.Faces)
//			{
//				int i = 0;
//				Vector3 normal = Vector3Ex.TransformVector(face.Normal, world.ModelviewMatrix).GetNormal();
//				if (normal.Z > 0)
//				{
//					foreach (FaceEdge faceEdge in face.FaceEdges())
//					{
//						points[i].position = world.GetScreenPosition(faceEdge.FirstVertex.Position);

//						Vector3 transformedPosition = Vector3Ex.TransformPosition(faceEdge.FirstVertex.Position, world.ModelviewMatrix);
//						points[i].z = transformedPosition.Z;
//						i++;
//					}

//					ColorF polyDrawColor = new ColorF();
//					double L = Vector3Ex.Dot(lightNormal, normal);
//					if (L > 0.0f)
//					{
//						polyDrawColor = partColor * lightIllumination * L;
//					}

//					polyDrawColor = ColorF.ComponentMax(polyDrawColor, partColor * ambiantIllumination);
//					for (i = 0; i < 3; i++)
//					{
//						double ratio = (points[i].z - minZ) / (maxZ - minZ);
//						int ratioInt16 = (int)(ratio * 65536);
//						points[i].color = new Color(polyDrawColor.Red0To255, ratioInt16 >> 8, ratioInt16 & 0xFF);
//					}

//#if true
//					render_gouraud(
//						graphics2D.DestImage,
//						new scanline_unpacked_8(),
//						new ScanlineRasterizer(),
//						points);
//#else
//					IRecieveBlenderByte oldBlender = graphics2D.DestImage.GetRecieveBlender();
//					graphics2D.DestImage.SetRecieveBlender(new BlenderZBuffer());
//					graphics2D.Render(polygonProjected, renderColor);
//					graphics2D.DestImage.SetRecieveBlender(oldBlender);
//#endif

//					byte[] buffer = graphics2D.DestImage.GetBuffer();
//					int pixels = graphics2D.DestImage.Width * graphics2D.DestImage.Height;
//					for (int pixelIndex = 0; pixelIndex < pixels; pixelIndex++)
//					{
//						buffer[pixelIndex * 4 + ImageBuffer.OrderR] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
//						buffer[pixelIndex * 4 + ImageBuffer.OrderG] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
//						buffer[pixelIndex * 4 + ImageBuffer.OrderB] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
//					}
//				}
//			}
		}

		public sealed class BlenderZBuffer : BlenderBase8888, IRecieveBlenderByte
		{
			public Color PixelToColor(byte[] buffer, int bufferOffset)
			{
				return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
			}

			public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
			{
				do
				{
					if (sourceColor.green > buffer[bufferOffset + ImageBuffer.OrderG])
					{
						buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
						buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
						buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
						buffer[bufferOffset + ImageBuffer.OrderA] = 255;
					}
					else if (sourceColor.green == buffer[bufferOffset + ImageBuffer.OrderG]
						&& sourceColor.blue > buffer[bufferOffset + ImageBuffer.OrderB])
					{
						buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
						buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
						buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
						buffer[bufferOffset + ImageBuffer.OrderA] = 255;
					}
					bufferOffset += 4;
				}
				while (--count != 0);
			}

			public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
			{
				//unsafe
				{
					unchecked
					{
						if (sourceColor.green > buffer[bufferOffset + ImageBuffer.OrderG])
						{
							buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
							buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
							buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
							buffer[bufferOffset + ImageBuffer.OrderA] = 255;
						}
						else if (sourceColor.green == buffer[bufferOffset + ImageBuffer.OrderG]
							&& sourceColor.blue > buffer[bufferOffset + ImageBuffer.OrderB])
						{
							buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
							buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
							buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
							buffer[bufferOffset + ImageBuffer.OrderA] = 255;
						}
					}
				}
			}

			public void BlendPixels(byte[] destBuffer, int bufferOffset,
				Color[] sourceColors, int sourceColorsOffset,
				byte[] covers, int coversIndex, bool firstCoverForAll, int count)
			{
				do
				{
					BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
					bufferOffset += 4;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}

		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(List<IObject3D> renderDatas)
		{
			AxisAlignedBoundingBox totalMeshBounds = AxisAlignedBoundingBox.Empty();
			bool first = true;
			foreach (var renderData in renderDatas)
			{
				AxisAlignedBoundingBox meshBounds = renderData.Mesh.GetAxisAlignedBoundingBox(renderData.WorldMatrix());
				if (first)
				{
					totalMeshBounds = meshBounds;
					first = false;
				}
				else
				{
					totalMeshBounds = AxisAlignedBoundingBox.Union(totalMeshBounds, meshBounds);
				}
			}

			return totalMeshBounds;
		}

		private List<ITraceable> GetRenderCollection()
		{
			if (sceneToRender != null)
			{
				AxisAlignedBoundingBox totalMeshBounds = sceneToRender.GetAxisAlignedBoundingBox();

				var centeredTranslation = Matrix4X4.CreateTranslation(-totalMeshBounds.Center);

				world.Fit(sceneToRender, new RectangleDouble(0, 0, size.x, size.y), centeredTranslation);

				ITraceable bvhCollection = MeshToBVH.Convert(sceneToRender);

				return new List<ITraceable> { new Transform(bvhCollection, centeredTranslation) };
			}

			return null;
		}

		private void CreateScene()
		{
			scene = new Scene();
			scene.camera = new WorldCamera(world);
			//scene.background = new Background(new RGBA_Floats(0.5, .5, .5), 0.4);
			scene.background = new Background(new ColorF(1, 1, 1, 0), 0.6);

			allObjects = BoundingVolumeHierarchy.CreateNewHierachy(GetRenderCollection());
			allObjectsHolder = new Transform(allObjects);
			//allObjects = root;
			scene.shapes.Add(allObjectsHolder);

			//AddAFloor();

			//add two lights for better lighting effects
			//scene.lights.Add(new Light(new Vector3(5000, 5000, 5000), new RGBA_Floats(0.8, 0.8, 0.8)));
			scene.lights.Add(new PointLight(new Vector3(-5000, -5000, 3000), new ColorF(0.5, 0.5, 0.5)));
		}

		public void GetMinMaxZ(Mesh mesh, ref double minZ, ref double maxZ)
		{
			AxisAlignedBoundingBox meshBounds = mesh.GetAxisAlignedBoundingBox(world.ModelviewMatrix);

			minZ = Math.Min(meshBounds.MinXYZ.Z, minZ);
			maxZ = Math.Max(meshBounds.MaxXYZ.Z, maxZ);
		}

		private class WorldCamera : ICamera
		{
			private WorldView world;

			public WorldCamera(WorldView world)
			{
				this.world = world;
			}

			public Ray GetRay(double screenX, double screenY)
			{
				return world.GetRayForLocalBounds(new Vector2(screenX, screenY));
			}
		}
	}
}