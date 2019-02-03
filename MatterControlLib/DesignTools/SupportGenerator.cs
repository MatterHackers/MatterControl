﻿/*
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public static class FaceListExtensions
	{
		public static IPrimitive CreateTraceData(this FaceList faceList, List<Vector3> vertexList, int maxRecursion = int.MaxValue)
		{
			var allPolys = new List<IPrimitive>();

			foreach (var face in faceList)
			{
				allPolys.Add(new TriangleShape(vertexList[face.v0], vertexList[face.v1], vertexList[face.v2], null));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, maxRecursion);
		}

		public static IPrimitive CreateTraceData(this FaceList faceList, List<Vector3Float> vertexList, int maxRecursion = int.MaxValue)
		{
			var allPolys = new List<IPrimitive>();

			foreach (var face in faceList)
			{
				allPolys.Add(new TriangleShape(vertexList[face.v0], vertexList[face.v1], vertexList[face.v2], null));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, maxRecursion);
		}
	}

	[HideFromTreeViewAttribute, Immutable]
	public class GeneratedSupportObject3D : Object3D
	{
		public GeneratedSupportObject3D()
		{
			OutputType = PrintOutputTypes.Support;
		}
	}

	public class SupportGenerator
	{
		private InteractiveScene scene;

		public SupportGenerator(InteractiveScene scene)
		{
			this.scene = scene;
		}

		public enum SupportGenerationType { Normal, From_Bed }

		public double MaxOverHangAngle
		{
			get
			{
				if (UserSettings.Instance.get(UserSettingsKey.SupportMaxOverHangAngle) == null)
				{
					return 45;
				}
				var value = UserSettings.Instance.GetValue<double>(UserSettingsKey.SupportMaxOverHangAngle);
				if (value < 0)
				{
					return 0;
				}
				if (value > 90)
				{
					value = 90;
				}

				return value;
			}

			set
			{
				UserSettings.Instance.set(UserSettingsKey.SupportMaxOverHangAngle, value.ToString());
			}
		}

		public double PillarSize
		{
			get
			{
				var value = UserSettings.Instance.GetValue<double>(UserSettingsKey.SupportPillarSize);
				if (value < 1.5)
				{
					return 1.5;
				}

				return value;
			}
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SupportPillarSize, value.ToString());
			}
		}

		public SupportGenerationType SupportType
		{
			get
			{
				var supportString = UserSettings.Instance.get(UserSettingsKey.SupportGenerationType);
				if (Enum.TryParse(supportString, out SupportGenerationType supportType))
				{
					return supportType;
				}

				return SupportGenerationType.Normal;
			}

			set
			{
				UserSettings.Instance.set(UserSettingsKey.SupportGenerationType, value.ToString());
			}
		}

		/// <summary>
		/// The amount to reduce the pillars so they are separated in the 3D view
		/// </summary>
		private double reduceAmount => .99;

		public Task Create(IProgress<ProgressStatus> progress, CancellationToken cancelationToken)
		{
			ProgressStatus status = new ProgressStatus();
			status.Status = "Enter";
			progress.Report(status);

			// Get visible meshes for each of them
			var supportCandidates = scene.Children.SelectMany(i => i.VisibleMeshes());

			AxisAlignedBoundingBox allBounds = AxisAlignedBoundingBox.Empty();
			foreach (var candidate in supportCandidates)
			{
				allBounds += candidate.GetAxisAlignedBoundingBox(candidate.Matrix.Inverted * candidate.WorldMatrix());
			}

			// create the gird of possible support
			var gridBounds = new RectangleDouble(Math.Floor((double)(allBounds.MinXYZ.X / PillarSize)),
				Math.Floor((double)(allBounds.MinXYZ.Y / PillarSize)),
				Math.Ceiling(allBounds.MaxXYZ.X / PillarSize),
				Math.Ceiling(allBounds.MaxXYZ.Y / PillarSize));
			var partBounds = new RectangleDouble(gridBounds.Left * PillarSize,
				gridBounds.Bottom * PillarSize,
				gridBounds.Right * PillarSize,
				gridBounds.Top * PillarSize);

			int gridWidth = (int)gridBounds.Width;
			int gridHeight = (int)gridBounds.Height;
			var supportGrid = new List<List<List<(bool isBottom, double z)>>>();
			for (int x = 0; x < gridWidth; x++)
			{
				supportGrid.Add(new List<List<(bool, double)>>());
				for (int y = 0; y < gridHeight; y++)
				{
					supportGrid[x].Add(new List<(bool, double)>());
				}
			}

			// get all the support plane intersections
			status.Status = "Trace";
			progress.Report(status);
			var detectedPlanes = DetectRequiredSupportByTracing(gridBounds, supportCandidates);

			status.Status = "Columns";
			progress.Report(status);

			AddSupportColumns(gridBounds, detectedPlanes);

			// this is the theory for regions rather than pillars
			// separate the faces into face patch groups (these are the new support tops)
			// project all the vertices of each patch group down until they hit an up face in the scene (or 0)
			// make a new patch group at the z of the hit (these will be the bottoms)
			// find the outline of the patch groups (these will be the walls of the top and bottom patches
			// make a new mesh object with the top, bottom and walls, add it to the scene and mark it as support

			return Task.CompletedTask;
		}

		public void RemoveExisting()
		{
			scene.SelectedItem = null;
			var existingSupports = scene.Children.Where(i => i.GetType() == typeof(GeneratedSupportObject3D));

			scene.UndoBuffer.AddAndDo(new DeleteCommand(scene, existingSupports.ToList()));
		}

		public bool RequiresSupport()
		{
			bool supportInScene = scene.VisibleMeshes().Any(i => i.WorldOutputType() == PrintOutputTypes.Support);
			if (!supportInScene)
			{
				// there is no support in the scene check if there are faces that require support
				var supportCandidates = scene.VisibleMeshes().Where(i => i.OutputType != PrintOutputTypes.Support);

				// find all the faces that are candidates for support
				foreach (var item in supportCandidates)
				{
					var matrix = item.WorldMatrix(scene);
					for (int faceIndex = 0; faceIndex < item.Mesh.Faces.Count; faceIndex++)
					{
						bool aboveBed = false;
						var face = item.Mesh.Faces[faceIndex];
						var verts = new int[] { face.v0, face.v1, face.v2 };
						foreach (var vertex in verts)
						{
							if (item.Mesh.Vertices[vertex].Transform(matrix).Z > .01)
							{
								aboveBed = true;
								break;
							}
						}
						if (aboveBed)
						{
							var face0Normal = item.Mesh.Faces[faceIndex].normal.TransformNormal(matrix).GetNormal();
							var angle = MathHelper.RadiansToDegrees(Math.Acos(face0Normal.Dot(-Vector3Float.UnitZ)));

							if (angle < MaxOverHangAngle)
							{
								// TODO: consider how much area all supported polygons represent
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		private void AddSupportColumn(IObject3D holder, double gridX, double gridY, double bottomZ, double topZ)
		{
			if (topZ - bottomZ < .01)
			{
				// less than 10 micros high, don't ad it
				return;
			}
			var support = new GeneratedSupportObject3D()
			{
				Mesh = PlatonicSolids.CreateCube(1, 1, 1)
			};
			support.Matrix = Matrix4X4.CreateScale(PillarSize - reduceAmount, PillarSize - reduceAmount, topZ - bottomZ)
				* Matrix4X4.CreateTranslation(gridX, gridY, bottomZ + (topZ - bottomZ) / 2);

			holder.Children.Add(support);
		}

		private void AddSupportColumns(RectangleDouble gridBounds, Dictionary<(int x, int y), List<(double z, bool bottom)>> detectedPlanes)
		{
			IObject3D supportColumnsToAdd = new Object3D();
			bool fromBed = SupportType == SupportGenerationType.From_Bed;
			var halfPillar = PillarSize / 2;
			foreach (var kvp in detectedPlanes)
			{
				if (kvp.Value.Count == 0)
				{
					continue;
				}

				int i = 0;

				kvp.Value.Sort((a, b) =>
				{
					return a.z.CompareTo(b.z);
				});

				var yPos = (gridBounds.Bottom + kvp.Key.y) * PillarSize + halfPillar;
				var xPos = (gridBounds.Left + kvp.Key.x) * PillarSize + halfPillar;

				if (fromBed)
				{
					i = GetNextBottom(i, kvp.Value);
					if (kvp.Value[i].bottom)
					{
						AddSupportColumn(supportColumnsToAdd, xPos, yPos, 0, kvp.Value[i].z + .01);
					}
				}
				else
				{
					double lastTopZ = 0;
					int lastBottom = i;
					do
					{
						// if the first plane is a top, move to the last top before we find a bottom
						if (i == 0
							&& !kvp.Value[i].bottom)
						{
							i = GetNextTop(i + 1, kvp.Value);
							if (i < kvp.Value.Count)
							{
								lastTopZ = kvp.Value[i].z;
							}
						}
						lastBottom = i;
						// find all open arreas in the list and add support
						i = GetNextBottom(i, kvp.Value);
						if (i < kvp.Value.Count
							&& kvp.Value[i].bottom)
						{
							AddSupportColumn(supportColumnsToAdd, xPos, yPos, lastTopZ, kvp.Value[i].z);
						}
						i = GetNextTop(i + 1, kvp.Value);
						if (i < kvp.Value.Count)
						{
							lastTopZ = kvp.Value[i].z;
						}
					} while (i != lastBottom && i < kvp.Value.Count);
				}
			}

			scene.UndoBuffer.AddAndDo(new InsertCommand(scene, supportColumnsToAdd.Children, false));
		}

		private Dictionary<(int x, int y), List<(double z, bool bottom)>> DetectRequiredSupportByTracing(RectangleDouble gridBounds, IEnumerable<IObject3D> supportCandidates)
		{
			var traceData = GetTraceData(supportCandidates);

			// keep a list of all the detected planes in each support column
			var detectedPlanes = new Dictionary<(int x, int y), List<(double z, bool bottom)>>();

			int gridWidth = (int)gridBounds.Width;
			int gridHeight = (int)gridBounds.Height;

			// at the center of every grid item add in a list of all the top faces to look down from
			for (int y = 0; y < gridHeight; y++)
			{
				for (int x = 0; x < gridWidth; x++)
				{
					IntersectInfo upHit = null;

					for (double yOffset = -1; yOffset <= 1; yOffset++)
					{
						for (double xOffset = -1; xOffset <= 1; xOffset++)
						{
							var halfPillar = PillarSize / 2;
							var yPos = (gridBounds.Bottom + y) * PillarSize + halfPillar + (yOffset * halfPillar);
							var xPos = (gridBounds.Left + x) * PillarSize + halfPillar + (xOffset * halfPillar);

							// detect all the bottom plans (surfaces that might need support
							var upRay = new Ray(new Vector3(xPos + .000013, yPos - .00027, 0), Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									if (!detectedPlanes.ContainsKey((x, y)))
									{
										detectedPlanes.Add((x, y), new List<(double z, bool bottom)>());
										// add a single plane at the bed so we always know the bed is a top
										//detectedPlanes[(x, y)].Add((0, false));
									}

									detectedPlanes[(x, y)].Add((upHit.HitPosition.Z, true));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001), Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
								}
							} while (upHit != null);

							// detect all the up plans (surfaces that will have support on top of them)
							upRay = new Ray(new Vector3(xPos + .000013, yPos - .00027, 0), Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									if (!detectedPlanes.ContainsKey((x, y)))
									{
										detectedPlanes.Add((x, y), new List<(double z, bool bottom)>());
										// add a single plane at the bed so we always know the bed is a top
										//detectedPlanes[(x, y)].Add((0, false));
									}

									detectedPlanes[(x, y)].Add((upHit.HitPosition.Z, false));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001), Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
								}
							} while (upHit != null);
						}
					}
				}
			}

			return detectedPlanes;
		}

		public static int GetNextBottom(int i, List<(double z, bool bottom)> planes)
		{
			// first skip all the tops
			while (i < planes.Count
				&& !planes[i].bottom)
			{
				i++;
			}

			// then look for the last bottom before next top
			while (i < planes.Count
				&& planes[i].bottom
				&& planes.Count > i + 1
				&& planes[i + 1].bottom)
			{
				i++;
			}

			return i;
		}

		public static int GetNextTop(int i, List<(double z, bool bottom)> planes)
		{
			while (i < planes.Count
				&& planes[i].bottom)
			{
				i++;
			}

			return i;
		}

		// function to get all the columns that need support generation
		private IEnumerable<(int x, int y)> GetSupportCorrodinates(ImageBuffer supportNeededImage)
		{
			var buffer = supportNeededImage.GetBuffer();
			// check if the image has any alpha set to something other than 255
			for (int y = 0; y < supportNeededImage.Height; y++)
			{
				var yOffset = supportNeededImage.GetBufferOffsetY(y);
				for (int x = 0; x < supportNeededImage.Width; x++)
				{
					// get the alpha at this pixel
					//if (buffer[yOffset + x] > 0)
					{
						yield return (x, y);
					}
				}
			}
		}

		private IPrimitive GetTraceData(IEnumerable<IObject3D> supportCandidates)
		{
			List<Vector3Float> supportVerts;
			FaceList supportFaces;

			// find all the faces that are candidates for support
			supportVerts = new List<Vector3Float>();
			supportFaces = new FaceList();
			foreach (var item in supportCandidates)
			{
				// add all the down faces to supportNeededImage
				var matrix = item.WorldMatrix(scene);
				for (int faceIndex = 0; faceIndex < item.Mesh.Faces.Count; faceIndex++)
				{
					var face0Normal = item.Mesh.Faces[faceIndex].normal.TransformNormal(matrix).GetNormal();
					var angle = MathHelper.RadiansToDegrees(Math.Acos(face0Normal.Dot(-Vector3Float.UnitZ)));

					// check if the face is pointing in the up direction at all
					bool isUpFace = angle >= 90;

					// check if the face is pointing down

					if (angle <= MaxOverHangAngle
						|| isUpFace)
					{
						var face = item.Mesh.Faces[faceIndex];
						var verts = new int[] { face.v0, face.v1, face.v2 };
						var p0 = item.Mesh.Vertices[face.v0].Transform(matrix);
						var p1 = item.Mesh.Vertices[face.v1].Transform(matrix);
						var p2 = item.Mesh.Vertices[face.v2].Transform(matrix);
						var vc = supportVerts.Count;
						supportVerts.Add(p0);
						supportVerts.Add(p1);
						supportVerts.Add(p2);

						supportFaces.Add(vc, vc + 1, vc + 2, face0Normal);
					}
				}
			}

			return supportFaces.CreateTraceData(supportVerts);
		}
	}
}