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
		private double minimumSupportHeight;
		private readonly InteractiveScene scene;

		public SupportGenerator(InteractiveScene scene, double minimumSupportHeight)
		{
			this.minimumSupportHeight = minimumSupportHeight;
			this.scene = scene;
		}

		public enum SupportGenerationType
		{
			Normal,
			From_Bed
		}

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
		/// Gets the amount to reduce the pillars so they are separated in the 3D view.
		/// </summary>
		public static double ColumnReduceAmount => 1;

		public Task Create(IProgress<ProgressStatus> progress, CancellationToken cancelationToken)
		{
			var selectedItem = scene.SelectedItem;

			using (new SelectionMaintainer(scene))
			{
				var status = new ProgressStatus
				{
					Status = "Enter"
				};
				progress?.Report(status);

				// Get visible meshes for each of them
				var allBedItems = scene.Children.SelectMany(i => i.VisibleMeshes());

				var suppoortBounds = AxisAlignedBoundingBox.Empty();
				if (selectedItem != null)
				{
					foreach (var candidate in selectedItem.VisibleMeshes())
					{
						suppoortBounds += candidate.GetAxisAlignedBoundingBox(candidate.Matrix.Inverted * candidate.WorldMatrix());
					}
				}
				else
				{
					foreach (var candidate in allBedItems)
					{
						suppoortBounds += candidate.GetAxisAlignedBoundingBox(candidate.Matrix.Inverted * candidate.WorldMatrix());
					}
				}

				// create the gird of possible support
				var gridBounds = new RectangleDouble(Math.Floor((double)(suppoortBounds.MinXYZ.X / PillarSize)),
					Math.Floor((double)(suppoortBounds.MinXYZ.Y / PillarSize)),
					Math.Ceiling(suppoortBounds.MaxXYZ.X / PillarSize),
					Math.Ceiling(suppoortBounds.MaxXYZ.Y / PillarSize));
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
				progress?.Report(status);
				var detectedPlanes = DetectRequiredSupportByTracing(gridBounds, allBedItems);

				status.Status = "Columns";
				progress?.Report(status);

				// minimum height requiring support is 1/2 the layer height
				AddSupportColumns(gridBounds, detectedPlanes);

				// this is the theory for regions rather than pillars
				// separate the faces into face patch groups (these are the new support tops)
				// project all the vertices of each patch group down until they hit an up face in the scene (or 0)
				// make a new patch group at the z of the hit (these will be the bottoms)
				// find the outline of the patch groups (these will be the walls of the top and bottom patches
				// make a new mesh object with the top, bottom and walls, add it to the scene and mark it as support

				return Task.CompletedTask;
			}
		}

		public void RemoveExisting()
		{
			var selectedItem = scene.SelectedItem;
			var bedBounds = new RectangleDouble(Vector2.NegativeInfinity, Vector2.PositiveInfinity);
			if (selectedItem != null)
			{
				var aabb = selectedItem.GetAxisAlignedBoundingBox();
				bedBounds = new RectangleDouble(new Vector2(aabb.MinXYZ), new Vector2(aabb.MaxXYZ));
			}

			using (new SelectionMaintainer(scene))
			{
				var existingSupports = scene.Descendants().Where(i =>
				{
					if (i.GetType() == typeof(GeneratedSupportObject3D))
					{
						// we have a support, check if it is within the bounds of the selected object
						var xyCenter = new Vector2(i.GetAxisAlignedBoundingBox().Center);
						if (bedBounds.Contains(xyCenter))
						{
							return true;
						}
					}

					return false;
				});

				scene.UndoBuffer.AddAndDo(new DeleteCommand(scene, existingSupports.ToList()));
			}
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
			// make it just a bit small so they always overlap correctly
			var reduceAmount = ColumnReduceAmount * .99;
			support.Matrix = Matrix4X4.CreateScale(PillarSize - reduceAmount, PillarSize - reduceAmount, topZ - bottomZ)
				* Matrix4X4.CreateTranslation(gridX, gridY, bottomZ + (topZ - bottomZ) / 2);

			holder.Children.Add(support);
		}

		private void AddSupportColumns(RectangleDouble gridBounds, Dictionary<(int x, int y), HitPlanes> detectedPlanes)
		{
			IObject3D supportColumnsToAdd = new Object3D();
			bool fromBed = SupportType == SupportGenerationType.From_Bed;
			var halfPillar = PillarSize / 2;
			foreach (var kvp in detectedPlanes)
			{
				var planes = kvp.Value;

				if (planes.Count == 0)
				{
					continue;
				}

				planes.Sort((a, b) =>
				{
					return a.Z.CompareTo(b.Z);
				});

				var yPos = (gridBounds.Bottom + kvp.Key.y) * PillarSize + halfPillar;
				var xPos = (gridBounds.Left + kvp.Key.x) * PillarSize + halfPillar;

				if (fromBed)
				{
					var nextPlaneIsBottom = planes.Count > 1 && planes[1].Bottom;
					if (!nextPlaneIsBottom // if the next plane is a top, we don't have any space from the bed to the part to put support
						|| planes[1].Z > minimumSupportHeight) // if the next plane is a bottom and is not far enough away, there is no space to put any support
					{
						var firstBottomAboveBed = planes.GetNextBottom(0);

						if (firstBottomAboveBed >= 0)
						{
							AddSupportColumn(supportColumnsToAdd, xPos, yPos, 0, planes[firstBottomAboveBed].Z + .01);
						}
					}
				}
				else
				{
					int i = 0;
					double lastTopZ = 0;
					int lastBottom = -1;
					var nextPlaneIsBottom = planes.Count > 1 && planes[1].Bottom;
					// if the next plane (the one above the bed) is a bottom, we have a part on the bed and will not generate support
					if (nextPlaneIsBottom && planes[1].Z <= minimumSupportHeight)
					{
						// go up to the next top
						i = planes.GetNextTop(i);
						if (i >= 0)
						{
							lastTopZ = planes[i].Z;
						}
					}

					while (i != -1
						&& i != lastBottom
						&& i < planes.Count)
					{
						lastBottom = i;
						// find all open areas in the list and add support
						i = planes.GetNextBottom(i);
						if (i >= 0)
						{
							if (i < planes.Count
								&& planes[i].Bottom)
							{
								AddSupportColumn(supportColumnsToAdd, xPos, yPos, lastTopZ, planes[i].Z);
							}

							i = planes.GetNextTop(i + 1);
							if (i >= 0
								&& i < planes.Count)
							{
								lastTopZ = planes[i].Z;
							}
						}
					}
				}
			}

			scene.UndoBuffer.AddAndDo(new InsertCommand(scene, supportColumnsToAdd.Children, false));
		}

		public struct HitPlane
		{
			public double Z;
			public bool Bottom;

			public HitPlane(double z, bool bottom)
			{
				this.Z = z;
				this.Bottom = bottom;
			}

			public override string ToString()
			{
				return $"Z={Z:0.###} Bottom={Bottom}";
			}
		}

		public class HitPlanes : List<HitPlane>
		{
			private double minimumSupportHeight;

			public HitPlanes(double minimumSupportHeight)
			{
				this.minimumSupportHeight = minimumSupportHeight;
			}

			/// <summary>
			/// Modify the list to have Bottom <-> Top, Bottom <-> Top items exactly.
			/// Remove any internal Planes that are not required. This may reduce the set to no items.
			/// </summary>
			public void Simplify()
			{
				// sort the list on Z
				this.Sort((a, b) =>
				{
					return a.Z.CompareTo(b.Z);
				});

				// remove items until the first item is a bottom
				while (Count > 0 && !this[0].Bottom)
				{
					this.RemoveAt(0);
				}

				// remove any items that are between a bottom and a top
				int currentBottom = 0;
				while (Count > currentBottom)
				{
					var top = GetNextTop(currentBottom);
					if (top != -1)
					{
						// remove everything between the top and the bottom
						for (int i = top - 1; i > currentBottom; i--)
						{
							this.RemoveAt(i);
						}

						// move the bottom up past the current top
						currentBottom += 2;
					}
					else // there is not a top above this bottom
					{
						// remove the bottom
						this.RemoveAt(currentBottom);
						break;
					}
				}
			}

			public void Merge(HitPlanes other)
			{
				other.Simplify();
				this.Simplify();

				// now both lists are only start->end, start->end
				// merge them, considering minimumSupportHeight
				for (int i = 0; i < this.Count; i += 2)
				{
					for (int j = 0; j < other.Count; j += 2)
					{
						// check if they overlap and other is not completely contained in this
						if (this[i].Z <= other[i + 1].Z + minimumSupportHeight
							&& this[i + 1].Z >= other[i].Z - minimumSupportHeight
							&& (this[i].Z > other[i].Z || this[i + 1].Z < other[i + 1].Z))
						{
							// set this range to be the union
							this[i] = new HitPlane(Math.Min(this[i].Z, other[i].Z), true);
							this[i + 1] = new HitPlane(Math.Max(this[i + 1].Z, other[i + 1].Z), false);
							// fix up the planes in this
							this.Simplify();
							// and start at the beginning again
							i -= 2;
							// drop out of the j loop
							break;
						}
						else if (this[i + 1].Z < other[i].Z)
						{
							// we are beyond the end of this
							// add every additional set and return
							for (int k = j; k < other.Count; k++)
							{
								this.Add(other[k]);
							}

							return;
						}
					}
				}
			}

			public int GetNextBottom(int i)
			{
				HitPlanes planes = this;

				while (i < planes.Count)
				{
					// if we are on a bottom
					if (planes[i].Bottom)
					{
						// move up to the next plane and re-evaluate
						i++;
					}
					else // we are on a top
					{
						// if the next plane is a bottom and more than skipDistanc away
						if (i + 1 < planes.Count
							&& planes[i + 1].Bottom
							&& planes[i + 1].Z > planes[i].Z + minimumSupportHeight)
						{
							// this is the next bottom we are looking for
							return i + 1;
						}
						else // move up to the next plane and re-evaluate
						{
							i++;
						}
					}
				}

				return -1;
			}

			public int GetNextTop(int start)
			{
				HitPlanes planes = this;

				var i = start;

				if (!planes[i].Bottom)
				{
					// skip the one we are
					i++;
				}

				while (i < planes.Count)
				{
					// if we are on a bottom
					if (planes[i].Bottom)
					{
						// move up to the next plane and re-evaluate
						i++;
					}
					else // we are on a top
					{
						// if the next plane is a bottom and more than skipDistanc away
						if (i + 1 < planes.Count
							&& planes[i + 1].Bottom
							&& planes[i + 1].Z > planes[i].Z + minimumSupportHeight)
						{
							// this is the next top we are looking for
							return i;
						}
						else // move up to the next plane and re-evaluate
						{
							// if we started on a bottom
							// and we are the last top
							// and we are far enough away from the start bottom
							if (this[start].Bottom
								&& i == this.Count - 1
								&& this[i].Z - this[start].Z > minimumSupportHeight)
							{
								// we are on the last top of the part and have move up from some other part
								return i;
							}

							i++;
						}
					}
				}

				return -1;
			}
		}

		private Dictionary<(int x, int y), HitPlanes> DetectRequiredSupportByTracing(RectangleDouble gridBounds, IEnumerable<IObject3D> supportCandidates)
		{
			var allBounds = supportCandidates.GetAxisAlignedBoundingBox();
			var rayStartZ = allBounds.MinXYZ.Z - 1;

			var traceData = GetTraceData(supportCandidates);

			// keep a list of all the detected planes in each support column
			var detectedPlanes = new Dictionary<(int x, int y), HitPlanes>();

			int gridWidth = (int)gridBounds.Width;
			int gridHeight = (int)gridBounds.Height;

			// at the center of every grid item add in a list of all the top faces to look down from
			for (int y = 0; y < gridHeight; y++)
			{
				for (int x = 0; x < gridWidth; x++)
				{
					// add a single plane at the bed so we always know the bed is a top
					detectedPlanes.Add((x, y), new HitPlanes(minimumSupportHeight));
					detectedPlanes[(x, y)].Add(new HitPlane(0, false));
					for (double yOffset = -1; yOffset <= 1; yOffset++)
					{
						for (double xOffset = -1; xOffset <= 1; xOffset++)
						{
							var halfPillar = PillarSize / 2;
							var yPos = (gridBounds.Bottom + y) * PillarSize + halfPillar + (yOffset * halfPillar);
							var xPos = (gridBounds.Left + x) * PillarSize + halfPillar + (xOffset * halfPillar);

							// detect all the bottom plans (surfaces that might need support
							var upRay = new Ray(new Vector3(xPos + .000013, yPos - .00027, rayStartZ), Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
							IntersectInfo upHit;
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									detectedPlanes[(x, y)].Add(new HitPlane(upHit.HitPosition.Z, true));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001), Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
								}
							}
							while (upHit != null);

							// detect all the up plans (surfaces that will have support on top of them)
							upRay = new Ray(new Vector3(xPos + .000013, yPos - .00027, rayStartZ), Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									detectedPlanes[(x, y)].Add(new HitPlane(upHit.HitPosition.Z, false));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001), Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
								}
							}
							while (upHit != null);
						}
					}
				}
			}

			return detectedPlanes;
		}

		private IPrimitive GetTraceData(IEnumerable<IObject3D> supportCandidates)
		{
			List<Vector3Float> supportVerts;
			FaceList supportFaces;

			// find all the faces that are candidates for support
			supportVerts = new List<Vector3Float>();
			supportFaces = new FaceList();

			// find all the down faces from the support candidates
			AddSupportFaces(supportCandidates,
				supportVerts,
				supportFaces,
				(angle) => angle <= MaxOverHangAngle);

			// find all the up faces from everything on the bed
			AddSupportFaces(scene.Children.SelectMany(i => i.VisibleMeshes()),
				supportVerts,
				supportFaces,
				(angle) => angle >= 90);

			return supportFaces.CreateTraceData(supportVerts);
		}

		private void AddSupportFaces(IEnumerable<IObject3D> supportCandidates, List<Vector3Float> supportVerts, FaceList supportFaces, Func<double, bool> doAdd)
		{
			foreach (var item in supportCandidates)
			{
				// add all the down faces to supportNeededImage
				var matrix = item.WorldMatrix(scene);
				for (int faceIndex = 0; faceIndex < item.Mesh.Faces.Count; faceIndex++)
				{
					var face0Normal = item.Mesh.Faces[faceIndex].normal.TransformNormal(matrix).GetNormal();
					var angle = MathHelper.RadiansToDegrees(Math.Acos(face0Normal.Dot(-Vector3Float.UnitZ)));

					if (doAdd(angle))
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
		}
	}
}