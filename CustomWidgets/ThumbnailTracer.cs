/*
Copyright (c) 2013, Lars Brubaker
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
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MatterHackers.RayTracer
{
	using MatterControl.PrinterCommunication;
	using MatterHackers.Agg.RasterizerScanline;
	using MatterHackers.Agg.VertexSource;
	using MatterHackers.RayTracer.Light;
	using System.Linq;

	public class ThumbnailTracer
	{
		public ImageBuffer destImage;

		private IPrimitive allObjects;

		private Transform allObjectsHolder;

		private List<MeshGroup> loadedMeshGroups;

		private bool hasOneOrMoreMesh;

		private RayTracer rayTracer = new RayTracer()
		{
			//AntiAliasing = AntiAliasing.None,
			//AntiAliasing = AntiAliasing.Low,
			//AntiAliasing = AntiAliasing.Medium,
			//AntiAliasing = AntiAliasing.High,
			AntiAliasing = AntiAliasing.VeryHigh,
			MultiThreaded = false,
		};

		private List<IPrimitive> renderCollection = new List<IPrimitive>();
		private Scene scene;
		private Point2D size;
		public TrackballTumbleWidget trackballTumbleWidget;

		public ThumbnailTracer(IObject3D item, int width, int height)
		{
			size = new Point2D(width, height);
			trackballTumbleWidget = new TrackballTumbleWidget();
			trackballTumbleWidget.DoOpenGlDrawing = false;
			trackballTumbleWidget.LocalBounds = new RectangleDouble(0, 0, width, height);

			loadedMeshGroups = item.ToMeshGroupList();

			hasOneOrMoreMesh = loadedMeshGroups.SelectMany(mg => mg.Meshes).Where(mesh => mesh != null).Any();
			if (hasOneOrMoreMesh)
			{
				SetRenderPosition(loadedMeshGroups);
				trackballTumbleWidget.AnchorCenter();
			}
		}

		public void DoTrace()
		{
			if (!hasOneOrMoreMesh)
			{
				destImage = null;
				return;
			}

			CreateScene();
			RectangleInt rect = new RectangleInt(0, 0, size.x, size.y);
			if (destImage == null || destImage.Width != rect.Width || destImage.Height != rect.Height)
			{
				destImage = new ImageBuffer(rect.Width, rect.Height);
			}

			rayTracer.MultiThreaded = !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting;

			rayTracer.RayTraceScene(rect, scene);
			rayTracer.CopyColorBufferToImage(destImage, rect);
		}

		public void SetRenderPosition(List<MeshGroup> loadedMeshGroups)
		{
			trackballTumbleWidget.TrackBallController.Reset();
			trackballTumbleWidget.TrackBallController.Scale = .03;

			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));

			ScaleMeshToView(loadedMeshGroups);
		}

		private void AddAFloor()
		{
			ImageBuffer testImage = new ImageBuffer(200, 200);
			Graphics2D graphics = testImage.NewGraphics2D();
			Random rand = new Random(0);
			for (int i = 0; i < 100; i++)
			{
				RGBA_Bytes color = new RGBA_Bytes(rand.NextDouble(), rand.NextDouble(), rand.NextDouble());
				graphics.Circle(new Vector2(rand.NextDouble() * testImage.Width, rand.NextDouble() * testImage.Height), rand.NextDouble() * 40 + 10, color);
			}
			scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new TextureMaterial(testImage, 0, 0, .2, 1)));
			//scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new ChessboardMaterial(new RGBA_Floats(1, 1, 1), new RGBA_Floats(0, 0, 0), 0, 0, 1, 0.7)));
		}

		static Vector3 lightNormal = (new Vector3(-1, 1, 1)).GetNormal();
		static RGBA_Floats lightIllumination = new RGBA_Floats(1, 1, 1);
		static RGBA_Floats ambiantIllumination = new RGBA_Floats(.4, .4, .4);

		internal class RenderPoint
		{
			internal Vector2 position;
			internal double z;
			internal RGBA_Bytes color;
		}

		internal void render_gouraud(IImageByte backBuffer, IScanlineCache sl, IRasterizer ras, RenderPoint[] points)
		{
			ImageBuffer image = new ImageBuffer();
			image.Attach(backBuffer, new BlenderZBuffer());

			ImageClippingProxy ren_base = new ImageClippingProxy(image);

			MatterHackers.Agg.span_allocator span_alloc = new span_allocator();
			span_gouraud_rgba span_gen = new span_gouraud_rgba();

			span_gen.colors(points[0].color, points[1].color, points[2].color);
			span_gen.triangle(points[0].position.x, points[0].position.y, points[1].position.x, points[1].position.y, points[2].position.x, points[2].position.y);
			ras.add_path(span_gen);
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);
		}

		public void DrawTo(Graphics2D graphics2D, Mesh meshToDraw, RGBA_Bytes partColorIn, double minZ, double maxZ)
		{
			RGBA_Floats partColor = partColorIn.GetAsRGBA_Floats();
			graphics2D.Rasterizer.gamma(new gamma_power(.3));
			RenderPoint[] points = new RenderPoint[3] { new RenderPoint(), new RenderPoint(), new RenderPoint() };

			foreach (Face face in meshToDraw.Faces)
			{
				int i = 0;
				Vector3 normal = Vector3.TransformVector(face.normal, trackballTumbleWidget.ModelviewMatrix).GetNormal();
				if (normal.z > 0)
				{
					foreach (FaceEdge faceEdge in face.FaceEdges())
					{
						points[i].position = trackballTumbleWidget.GetScreenPosition(faceEdge.firstVertex.Position);

						Vector3 transformedPosition = Vector3.TransformPosition(faceEdge.firstVertex.Position, trackballTumbleWidget.ModelviewMatrix);
						points[i].z = transformedPosition.z;
						i++;
					}

					RGBA_Floats polyDrawColor = new RGBA_Floats();
					double L = Vector3.Dot(lightNormal, normal);
					if (L > 0.0f)
					{
						polyDrawColor = partColor * lightIllumination * L;
					}

					polyDrawColor = RGBA_Floats.ComponentMax(polyDrawColor, partColor * ambiantIllumination);
					for (i = 0; i < 3; i++)
					{
						double ratio = (points[i].z - minZ) / (maxZ - minZ);
						int ratioInt16 = (int)(ratio * 65536);
						points[i].color = new RGBA_Bytes(polyDrawColor.Red0To255, ratioInt16 >> 8, ratioInt16 & 0xFF);
					}


#if true
					scanline_unpacked_8 sl = new scanline_unpacked_8();
					ScanlineRasterizer ras = new ScanlineRasterizer();
					render_gouraud(graphics2D.DestImage, sl, ras, points);
#else
					IRecieveBlenderByte oldBlender = graphics2D.DestImage.GetRecieveBlender();
					graphics2D.DestImage.SetRecieveBlender(new BlenderZBuffer());
					graphics2D.Render(polygonProjected, renderColor);
					graphics2D.DestImage.SetRecieveBlender(oldBlender);
#endif

					byte[] buffer = graphics2D.DestImage.GetBuffer();
					int pixels = graphics2D.DestImage.Width * graphics2D.DestImage.Height;
					for (int pixelIndex = 0; pixelIndex < pixels; pixelIndex++)
					{
						buffer[pixelIndex * 4 + ImageBuffer.OrderR] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
						buffer[pixelIndex * 4 + ImageBuffer.OrderG] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
						buffer[pixelIndex * 4 + ImageBuffer.OrderB] = buffer[pixelIndex * 4 + ImageBuffer.OrderR];
					}
				}
			}
		}

		public sealed class BlenderZBuffer : BlenderBase8888, IRecieveBlenderByte
		{
			public RGBA_Bytes PixelToColorRGBA_Bytes(byte[] buffer, int bufferOffset)
			{
				return new RGBA_Bytes(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
			}

			public void CopyPixels(byte[] buffer, int bufferOffset, RGBA_Bytes sourceColor, int count)
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

			public void BlendPixel(byte[] buffer, int bufferOffset, RGBA_Bytes sourceColor)
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
				RGBA_Bytes[] sourceColors, int sourceColorsOffset,
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
		
		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(List<MeshGroup> meshGroups)
		{
			AxisAlignedBoundingBox totalMeshBounds = AxisAlignedBoundingBox.Empty;
			bool first = true;
			foreach (MeshGroup meshGroup in meshGroups)
			{
				AxisAlignedBoundingBox meshBounds = meshGroup.GetAxisAlignedBoundingBox();
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

		private void AddTestMesh(List<MeshGroup> meshGroups)
		{
			if (meshGroups != null)
			{
				AxisAlignedBoundingBox totalMeshBounds = GetAxisAlignedBoundingBox(meshGroups);
				loadedMeshGroups = meshGroups;
				Vector3 meshCenter = totalMeshBounds.Center;
				foreach (MeshGroup meshGroup in meshGroups)
				{
					meshGroup.Translate(-meshCenter);
				}

				ScaleMeshToView(loadedMeshGroups);

				RGBA_Bytes partColor = new RGBA_Bytes(0, 130, 153);
				partColor = RGBA_Bytes.White;
				IPrimitive bvhCollection = MeshToBVH.Convert(loadedMeshGroups, new SolidMaterial(partColor.GetAsRGBA_Floats(), .01, 0.0, 2.0));

				renderCollection.Add(bvhCollection);
			}
		}

		private void CreateScene()
		{
			scene = new Scene();
			scene.camera = new TrackBallCamera(trackballTumbleWidget);
			//scene.background = new Background(new RGBA_Floats(0.5, .5, .5), 0.4);
			scene.background = new Background(new RGBA_Floats(1, 1, 1, 0), 0.6);

			AddTestMesh(loadedMeshGroups);

			allObjects = BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
			allObjectsHolder = new Transform(allObjects);
			//allObjects = root;
			scene.shapes.Add(allObjectsHolder);

			//AddAFloor();

			//add two lights for better lighting effects
			//scene.lights.Add(new Light(new Vector3(5000, 5000, 5000), new RGBA_Floats(0.8, 0.8, 0.8)));
			scene.lights.Add(new PointLight(new Vector3(-5000, -5000, 3000), new RGBA_Floats(0.5, 0.5, 0.5)));
		}

		private RectangleDouble GetScreenBounds(AxisAlignedBoundingBox meshBounds)
		{
			RectangleDouble screenBounds = RectangleDouble.ZeroIntersection;

			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.minXYZ.x, meshBounds.minXYZ.y, meshBounds.minXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.maxXYZ.x, meshBounds.minXYZ.y, meshBounds.minXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.maxXYZ.x, meshBounds.maxXYZ.y, meshBounds.minXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.minXYZ.x, meshBounds.maxXYZ.y, meshBounds.minXYZ.z)));

			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.minXYZ.x, meshBounds.minXYZ.y, meshBounds.maxXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.maxXYZ.x, meshBounds.minXYZ.y, meshBounds.maxXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.maxXYZ.x, meshBounds.maxXYZ.y, meshBounds.maxXYZ.z)));
			screenBounds.ExpandToInclude(trackballTumbleWidget.GetScreenPosition(new Vector3(meshBounds.minXYZ.x, meshBounds.maxXYZ.y, meshBounds.maxXYZ.z)));
			return screenBounds;
		}

		public void GetMinMaxZ(Mesh mesh, ref double minZ, ref double maxZ)
		{
			AxisAlignedBoundingBox meshBounds = mesh.GetAxisAlignedBoundingBox(trackballTumbleWidget.ModelviewMatrix);

			minZ = Math.Min(meshBounds.minXYZ.z, minZ);
			maxZ = Math.Max(meshBounds.maxXYZ.z, maxZ);
		}

		private bool NeedsToBeSmaller(RectangleDouble partScreenBounds, RectangleDouble goalBounds)
		{
			if (partScreenBounds.Bottom < goalBounds.Bottom
				|| partScreenBounds.Top > goalBounds.Top
				|| partScreenBounds.Left < goalBounds.Left
				|| partScreenBounds.Right > goalBounds.Right)
			{
				return true;
			}

			return false;
		}

		private void ScaleMeshToView(List<MeshGroup> loadedMeshGroups)
		{
			if (loadedMeshGroups != null)
			{
				AxisAlignedBoundingBox meshBounds = GetAxisAlignedBoundingBox(loadedMeshGroups);

				bool done = false;
				double scaleFraction = .1;
				RectangleDouble goalBounds = new RectangleDouble(0, 0, size.x, size.y);
				goalBounds.Inflate(-10);
				while (!done)
				{
					RectangleDouble partScreenBounds = GetScreenBounds(meshBounds);

					if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						trackballTumbleWidget.TrackBallController.Scale *= (1 + scaleFraction);
						partScreenBounds = GetScreenBounds(meshBounds);

						// If it crossed over the goal reduct the amount we are adjusting by.
						if (NeedsToBeSmaller(partScreenBounds, goalBounds))
						{
							scaleFraction /= 2;
						}
					}
					else
					{
						trackballTumbleWidget.TrackBallController.Scale *= (1 - scaleFraction);
						partScreenBounds = GetScreenBounds(meshBounds);

						// If it crossed over the goal reduct the amount we are adjusting by.
						if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
						{
							scaleFraction /= 2;
							if (scaleFraction < .001)
							{
								done = true;
							}
						}
					}
				}
			}
		}

		private class TrackBallCamera : ICamera
		{
			private TrackballTumbleWidget trackballTumbleWidget;

			public TrackBallCamera(TrackballTumbleWidget trackballTumbleWidget)
			{
				this.trackballTumbleWidget = trackballTumbleWidget;
			}

			public Ray GetRay(double screenX, double screenY)
			{
				return trackballTumbleWidget.GetRayForLocalBounds(new Vector2(screenX, screenY));
			}
		}
	}
}