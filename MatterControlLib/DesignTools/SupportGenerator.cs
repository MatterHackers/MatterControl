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
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SupportGenerator
	{
		private readonly InteractiveScene scene;

		private readonly double minimumSupportHeight;

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

		/// <summary>
		/// Gets the amount to reduce the pillars so they are separated in the 3D view.
		/// </summary>
		public static double ColumnReduceAmount => 1;

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

		public static IPrimitive CreateTraceData(FaceList faceList, List<Vector3Float> vertexList, int maxRecursion = int.MaxValue)
		{
			var allPolys = new List<IPrimitive>();

			foreach (var face in faceList)
			{
				allPolys.Add(new TriangleShape(vertexList[face.v0], vertexList[face.v1], vertexList[face.v2], null));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, maxRecursion);
		}

		public Task Create(IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
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

			// make it just a bit small so they always overlap correctly
			var reduceAmount = ColumnReduceAmount * .99;

			var support = new GeneratedSupportObject3D()
			{
				Mesh = PlatonicSolids.CreateCube(1, 1, 1),
				Matrix = Matrix4X4.CreateScale(PillarSize - reduceAmount, PillarSize - reduceAmount, topZ - bottomZ)
					* Matrix4X4.CreateTranslation(gridX, gridY, bottomZ + (topZ - bottomZ) / 2),
			};

			holder.Children.Add(support);
		}

		private void AddSupportColumns(RectangleDouble gridBounds, Dictionary<(int x, int y), SupportColumn> supportGrid)
		{
			IObject3D supportColumnsToAdd = new Object3D();
			using (supportColumnsToAdd.RebuildLock())
			{
				bool fromBed = SupportType == SupportGenerationType.From_Bed;
				var halfPillar = PillarSize / 2;

				foreach (var kvp in supportGrid)
				{
					var supportColumnData = kvp.Value;

					if (supportColumnData.Count == 0)
					{
						continue;
					}

					var yPos = (gridBounds.Bottom + kvp.Key.y) * PillarSize + halfPillar;
					var xPos = (gridBounds.Left + kvp.Key.x) * PillarSize + halfPillar;

					if (fromBed)
					{
						// if the next plane is a bottom and is not far enough away, there is no space to put any support
						if (supportColumnData[0].start < minimumSupportHeight
							&& supportColumnData[0].end > minimumSupportHeight)
						{
							AddSupportColumn(supportColumnsToAdd, xPos, yPos, 0, supportColumnData[0].end + .01);
						}
					}
					else
					{
						foreach (var (start, end) in supportColumnData)
						{
							AddSupportColumn(supportColumnsToAdd, xPos, yPos, start, end);
						}
					}
				}
			}

			scene.UndoBuffer.AddAndDo(new InsertCommand(scene, supportColumnsToAdd.Children, false));
		}

		private void AddSupportFaces(IEnumerable<IObject3D> supportCandidates, List<Vector3Float> supportVerts, FaceList supportFaces)
		{
			foreach (var item in supportCandidates)
			{
				// add all the faces
				var matrix = item.WorldMatrix(scene);

				for (int faceIndex = 0; faceIndex < item.Mesh.Faces.Count; faceIndex++)
				{
					var face0Normal = item.Mesh.Faces[faceIndex].normal.TransformNormal(matrix).GetNormal();

					var face = item.Mesh.Faces[faceIndex];
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

		private Dictionary<(int x, int y), SupportColumn> DetectRequiredSupportByTracing(RectangleDouble gridBounds, IEnumerable<IObject3D> supportCandidates)
		{
			var allBounds = supportCandidates.GetAxisAlignedBoundingBox();
			var rayStartZ = allBounds.MinXYZ.Z - 1;

			var traceData = GetTraceData(supportCandidates);

			// keep a list of all the detected planes in each support column
			var supportColumnData = new Dictionary<(int x, int y), SupportColumn>();

			int gridWidth = (int)gridBounds.Width;
			int gridHeight = (int)gridBounds.Height;

			var offset = new Vector3(.000013, .00027, 0);

			// at the center of every grid item add in a list of all the top faces to look down from
			for (int y = 0; y < gridHeight; y++)
			{
				for (int x = 0; x < gridWidth; x++)
				{
					var supportColumn = new SupportColumn(minimumSupportHeight);
					supportColumnData.Add((x, y), supportColumn);

					// create support plans at this xy
					for (double yOffset = -1; yOffset <= 1; yOffset++)
					{
						for (double xOffset = -1; xOffset <= 1; xOffset++)
						{
							var thisTracePlanes = new HitPlanes(minimumSupportHeight);
							var halfPillar = PillarSize / 2;
							var yPos = (gridBounds.Bottom + y) * PillarSize + halfPillar + (yOffset * halfPillar);
							var xPos = (gridBounds.Left + x) * PillarSize + halfPillar + (xOffset * halfPillar);

							// detect all the bottom plans (surfaces that might need support
							var upRay = new Ray(new Vector3(xPos, yPos, rayStartZ) + offset, Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
							IntersectInfo upHit;
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									var angle = MathHelper.RadiansToDegrees(Math.Acos(upHit.normalAtHit.Dot(-Vector3.UnitZ)));
									thisTracePlanes.Add(new HitPlane(upHit.HitPosition.Z, angle));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001) + offset, Vector3.UnitZ, intersectionType: IntersectionType.FrontFace);
								}
							}
							while (upHit != null);

							// detect all the up plans (surfaces that will have support on top of them)
							upRay = new Ray(new Vector3(xPos, yPos, rayStartZ) + offset, Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
							do
							{
								upHit = traceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									var angle = MathHelper.RadiansToDegrees(Math.Acos(upHit.normalAtHit.Dot(-Vector3.UnitZ)));
									thisTracePlanes.Add(new HitPlane(upHit.HitPosition.Z, angle));

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001) + offset, Vector3.UnitZ, intersectionType: IntersectionType.BackFace);
								}
							}
							while (upHit != null);

							var debugPlanes = new HitPlanes(minimumSupportHeight);
							debugPlanes.AddRange(thisTracePlanes);
							debugPlanes.Sort(MaxOverHangAngle);
							var lineSupport = new SupportColumn(thisTracePlanes, minimumSupportHeight, MaxOverHangAngle);

							if (lineSupport.Count > 0)
							{
								int a = 0;
							}

							supportColumn.Union(lineSupport);
						}
					}
				}
			}

			return supportColumnData;
		}

		private IPrimitive GetTraceData(IEnumerable<IObject3D> supportCandidates)
		{
			List<Vector3Float> supportVerts;
			FaceList supportFaces;

			// find all the faces that are candidates for support
			supportVerts = new List<Vector3Float>();
			supportFaces = new FaceList();

			// find all the faces from the support candidates
			AddSupportFaces(supportCandidates,
				supportVerts,
				supportFaces);

			return CreateTraceData(supportFaces, supportVerts);
		}

		public struct HitPlane
		{
			public double Z;

			public HitPlane(double z, bool bottom)
				: this(z, bottom ? 0 : 180)
			{
			}

			public HitPlane(double z, double angle)
			{
				this.Z = z;

				this.Angle = angle;
			}

			public bool Bottom(double maxOverHangAngle = 45)
			{
				return Angle <= maxOverHangAngle;
			}

			public double Angle { get; set; }

			public bool Top(double maxOverHangAngle = 45)
			{
				return Angle > maxOverHangAngle;
			}

			public override string ToString()
			{
				return $"Z={Z:0.###} {(Bottom(45) ? "Bottom" : "Top")}";
			}
		}

		public class SupportColumn : List<(double start, double end)>
		{
			private readonly double minimumSupportHeight;

			/// <summary>
			/// Initializes a new instance of the <see cref="SupportColumn"/> class.
			/// </summary>
			/// <param name="minimumSupportHeight">The minimum distance between support regions.</param>
			public SupportColumn(double minimumSupportHeight)
			{
				this.minimumSupportHeight = minimumSupportHeight;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="SupportColumn"/> class.
			/// </summary>
			/// <param name="inputPlanes">The planes to consider while creating the support regions.</param>
			/// <param name="minimumSupportHeight">The minimum distance between support regions.</param>
			/// <param name="maxOverHangAngle">The maximum angle that will be treated as a bottom.</param>
			public SupportColumn(HitPlanes inputPlanes, double minimumSupportHeight, double maxOverHangAngle = 45)
				: this(minimumSupportHeight)
			{
				var hitPlanes = new HitPlanes(inputPlanes.MinimumSupportHeight);
				hitPlanes.AddRange(inputPlanes);
				hitPlanes.Simplify(maxOverHangAngle);

				var i = 0;
				var currentTop = 0.0;
				// if the first bottom is more than the min distance
				if (hitPlanes.Count > 1 && hitPlanes[i].Z <= minimumSupportHeight)
				{
					currentTop = hitPlanes[i + 1].Z;
					i += 2;
				}

				for (; i < hitPlanes.Count / 2 * 2; i += 2)
				{
					if (hitPlanes[i].Z > currentTop + minimumSupportHeight)
					{
						this.Add((currentTop, hitPlanes[i].Z));
						currentTop = hitPlanes[i + 1].Z;
					}
				}
			}

			public void Union(SupportColumn other)
			{
				if (this.Count == 0)
				{
					this.AddRange(other);
					return;
				}

				// merge them, considering minimumSupportHeight
				for (int i = 0; i < this.Count; i++)
				{
					for (int j = 0; j < other.Count; j++)
					{
						// check if they overlap and other is not completely contained in this
						if (this[i].start <= other[j].end + minimumSupportHeight
							&& this[i].end >= other[j].start - minimumSupportHeight
							&& (this[i].start > other[j].start || this[i].end < other[j].end))
						{
							// set this range to be the union
							this[i] = (Math.Min(this[i].start, other[j].start),
								Math.Max(this[i].end, other[j].end));
							// fix up the planes in this
							this.RemoveOverLaps();
							// and start at the beginning again
							i--;
							// drop out of the j loop
							break;
						}
						else if (this[i].end < other[j].start
							&& i < this.Count - 1
							&& this[i + 1].start > other[j].end)
						{
							// we are beyond the end of this
							// add every additional set and return
							this.Insert(i + 1, other[j]);
							this.RemoveOverLaps();
							i--;
							// drop out of the j loop
							break;
						}
					}
				}
			}

			private void RemoveOverLaps()
			{
				// merge them, considering minimumSupportHeight
				for (int i = 0; i < this.Count; i++)
				{
					for (int j = i + 1; j < this.Count; j++)
					{
						// check this is an overlap with the next segment
						if (this[i].start <= this[j].end + minimumSupportHeight
							&& this[i].end >= this[j].start - minimumSupportHeight
							&& (this[i].start >= this[j].start || this[i].end <= this[j].end))
						{
							// set this range to be the union
							this[i] = (Math.Min(this[i].start, this[j].start),
								Math.Max(this[i].end, this[j].end));
							// fix up the planes in this
							this.RemoveAt(j);
							// and start at the beginning again
							i--;
							// drop out of the j loop
							break;
						}
					}
				}
			}
		}

		public class HitPlanes : List<HitPlane>
		{
			public double MinimumSupportHeight { get; private set; }

			public HitPlanes(double minimumSupportHeight)
			{
				this.MinimumSupportHeight = minimumSupportHeight;
			}

			public int GetNextBottom(int i, double maxOverHangAngle = 45)
			{
				while (i < this.Count)
				{
					// if we are on a bottom
					if (this[i].Bottom(maxOverHangAngle))
					{
						// move up to the next plane and re-evaluate
						i++;
					}
					else // we are on a top
					{
						// if the next plane is a bottom and more than minimumSupportHeight away
						if (i + 1 < this.Count
							&& this[i + 1].Bottom(maxOverHangAngle)
							&& this[i + 1].Z > this[i].Z + MinimumSupportHeight)
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

			public int GetNextTop(int start, double maxOverHangAngle = 45)
			{
				var i = start;

				if (this.Count > 0
					&& !this[i].Bottom(maxOverHangAngle))
				{
					// skip the one we are
					i++;
				}

				return AdvanceToTop(i, start, maxOverHangAngle);
			}

			public new void Sort()
			{
				throw new NotImplementedException("Call Sort(double maxOverHangAngle) instead");
			}

			public void Sort(double maxOverHangAngle)
			{
				this.Sort((a, b) =>
				{
					// one is a top and the other is a bottom, sort by tops first
					if (((a.Top(maxOverHangAngle) && b.Bottom(maxOverHangAngle)) || (a.Bottom(maxOverHangAngle) && b.Top(maxOverHangAngle)))
						&& a.Z < b.Z + MinimumSupportHeight / 2
						&& a.Z > b.Z - MinimumSupportHeight / 2)
					{
						return a.Top(MinimumSupportHeight) ? 1 : -1;
					}

					return a.Z.CompareTo(b.Z);
				});
			}

			/// <summary>
			/// Modify the list to have Bottom - Top, Bottom - Top items exactly.
			/// Remove any internal Planes that are not required. This may reduce the set to no items.
			/// </summary>
			/// <param name="maxOverHangAngle">The max angle to consider a bottom.</param>
			public void Simplify(double maxOverHangAngle = 45)
			{
				// sort the list on Z
				this.Sort(maxOverHangAngle);

				var highestPlane = double.NegativeInfinity;
				var lastRemoveWasBottom = false;
				// remove anything that is below 0
				while (Count > 0
					&& this[0].Z < 0)
				{
					if (this[0].Z > highestPlane)
					{
						highestPlane = this[0].Z;
						lastRemoveWasBottom = this[0].Bottom(maxOverHangAngle);
					}

					this.RemoveAt(0);
				}

				// if the first item is a top then add a bottom at 0
				if ((Count > 0 && this[0].Top(maxOverHangAngle)
					&& this[0].Z > 0)
					|| lastRemoveWasBottom)
				{
					this.Insert(0, new HitPlane(0, true));
				}

				// if the first item is still a top, remove it
				while (Count > 0
					&& this[0].Top(maxOverHangAngle))
				{
					this.RemoveAt(0);
				}

				// remove any items that are between a bottom and a top
				int currentBottom = 0;

				while (Count > currentBottom
					&& currentBottom != -1)
				{
					var top = GetNextTop(currentBottom, maxOverHangAngle);
					if (top != -1)
					{
						// remove everything between the bottom and the top
						for (int i = top - 1; i > currentBottom; i--)
						{
							this.RemoveAt(i);
						}

						top = currentBottom + 1;
						if (this[top].Z - this[currentBottom].Z < MinimumSupportHeight)
						{
							// also remove the top
							this.RemoveAt(top);
						}
						else
						{
							// move the bottom up past the current top
							currentBottom = GetNextBottom(top, maxOverHangAngle);

							// remove everything between the bottom and the new top
							if (currentBottom != -1)
							{
								for (int i = currentBottom - 1; i > top; i--)
								{
									this.RemoveAt(i);
								}
							}

							currentBottom = top + 1;
						}
					}
					else // not another top
					{
						// if the last plane is a bottom add a top above it at the minimum distance
						if (this.Count > 0
							&& this[this.Count - 1].Bottom(maxOverHangAngle))
						{
							var topHeight = this[this.Count - 1].Z + MinimumSupportHeight;
							// remove all the bottoms from current up to last (but keep the actual last)
							for (int i = this.Count - 1; i > currentBottom; i--)
							{
								this.RemoveAt(i);
							}

							// add a top
							this.Add(new HitPlane(topHeight, false));
						}

						break;
					}
				}
			}

			private int AdvanceToTop(int i, int start, double maxOverHangAngle)
			{
				while (i < this.Count)
				{
					// if we are on a bottom
					if (this[i].Bottom(maxOverHangAngle))
					{
						// move up to the next plane and re-evaluate
						i++;
					}
					else // we are on a top
					{
						// if the next plane is a bottom and more than minimumSupportHeight away
						if (i + 1 < this.Count
							&& this[i + 1].Bottom(maxOverHangAngle)
							&& this[i + 1].Z > this[i].Z + MinimumSupportHeight)
						{
							// this is the next top we are looking for
							return i;
						}
						else // move up to the next plane and re-evaluate
						{
							// if we started on a bottom
							// and we are the last top
							// and we are far enough away from the start bottom
							if (this[start].Bottom(maxOverHangAngle)
								&& i == this.Count - 1
								&& this[i].Z - this[start].Z > MinimumSupportHeight)
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
	}
}