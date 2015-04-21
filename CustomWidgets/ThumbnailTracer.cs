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
	using MatterHackers.RayTracer.Light;

	public class ThumbnailTracer
	{
		public ImageBuffer destImage;

		private IPrimitive allObjects;

		private Transform allObjectsHolder;

		private MeshGroup loadedMeshGroup;

		//RayTracer raytracer = new RayTracer(AntiAliasing.None, true, true, true, true, true);
		//RayTracer raytracer = new RayTracer(AntiAliasing.Low, true, true, true, true, true);
		//RayTracer raytracer = new RayTracer(AntiAliasing.Medium, true, true, true, true, true);
		//RayTracer raytracer = new RayTracer(AntiAliasing.High, true, true, true, true, true);
		private RayTracer raytracer = new RayTracer(AntiAliasing.VeryHigh, true, true, true, true, true);

		private List<IPrimitive> renderCollection = new List<IPrimitive>();
		private bool SavedTimes = false;
		private Scene scene;
		private Point2D size;
		private TrackballTumbleWidget trackballTumbleWidget;
		public ThumbnailTracer(List<MeshGroup> meshGroups, int width, int height)
		{
			size = new Point2D(width, height);
			trackballTumbleWidget = new TrackballTumbleWidget();
			trackballTumbleWidget.DoOpenGlDrawing = false;
			trackballTumbleWidget.LocalBounds = new RectangleDouble(0, 0, width, height);

			SetRenderPosition(0);

			trackballTumbleWidget.AnchorCenter();

			CreateScene(meshGroups);
		}

		public void DoTrace()
		{
			RectangleInt rect = new RectangleInt(0, 0, size.x, size.y);
			if (destImage == null || destImage.Width != rect.Width || destImage.Height != rect.Height)
			{
				destImage = new ImageBuffer(rect.Width, rect.Height, 32, new BlenderBGRA());
			}

			raytracer.RayTraceScene(rect, scene);
			raytracer.CopyColorBufferToImage(destImage, rect);
		}

		public void SetRenderPosition(int index)
		{
			trackballTumbleWidget.TrackBallController.Reset();
			trackballTumbleWidget.TrackBallController.Scale = .03;

			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));

			ScaleMeshToView();
		}

		private void AddAFloor()
		{
			ImageBuffer testImage = new ImageBuffer(200, 200, 32, new BlenderBGRA());
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

		private void AddTestMesh(List<MeshGroup> meshGroups)
		{
			loadedMeshGroup = meshGroups[0];
			AxisAlignedBoundingBox meshBounds = loadedMeshGroup.GetAxisAlignedBoundingBox();
			Vector3 meshCenter = meshBounds.Center;
			loadedMeshGroup.Translate(-meshCenter);

			ScaleMeshToView();

			RGBA_Bytes partColor = new RGBA_Bytes(0, 130, 153);
			partColor = RGBA_Bytes.White;
			IPrimitive bvhCollection = MeshToBVH.Convert(loadedMeshGroup, new SolidMaterial(partColor.GetAsRGBA_Floats(), .01, 0.0, 2.0));

			renderCollection.Add(bvhCollection);
		}

		private void CreateScene(List<MeshGroup> meshGroups)
		{
			scene = new Scene();
			scene.camera = new TrackBallCamera(trackballTumbleWidget);
			//scene.background = new Background(new RGBA_Floats(0.5, .5, .5), 0.4);
			scene.background = new Background(new RGBA_Floats(0, 0, 0, 0), 0.4);

			AddTestMesh(meshGroups);

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

		private void ScaleMeshToView()
		{
			if (loadedMeshGroup != null)
			{
				AxisAlignedBoundingBox meshBounds = loadedMeshGroup.GetAxisAlignedBoundingBox(); // get it now that we moved it.

				bool done = false;
				double scallFraction = .1;
				RectangleDouble goalBounds = new RectangleDouble(0, 0, size.x, size.y);
				goalBounds.Inflate(-10);
				while (!done)
				{
					RectangleDouble partScreenBounds = GetScreenBounds(meshBounds);

					if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						trackballTumbleWidget.TrackBallController.Scale *= (1 + scallFraction);
						partScreenBounds = GetScreenBounds(meshBounds);

						// If it crossed over the goal reduct the amount we are adjusting by.
						if (NeedsToBeSmaller(partScreenBounds, goalBounds))
						{
							scallFraction /= 2;
						}
					}
					else
					{
						trackballTumbleWidget.TrackBallController.Scale *= (1 - scallFraction);
						partScreenBounds = GetScreenBounds(meshBounds);

						// If it crossed over the goal reduct the amount we are adjusting by.
						if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
						{
							scallFraction /= 2;
							if (scallFraction < .001)
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
				return trackballTumbleWidget.GetRayFromScreen(new Vector2(screenX, screenY));
			}
		}
	}
}