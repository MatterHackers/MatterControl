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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools
{
	public class FindSliceObject3D : OperationSourceContainerObject3D, IPropertyGridModifier
	{
		public FindSliceObject3D()
		{
			Name = "Find Slice".Localize();
		}

		public double SliceHeight { get; set; } = 10;

		
		private double cutMargin = .01;

		public (Mesh mesh, Polygons polygons) Cut(IObject3D item)
		{
			var mesh = new Mesh(item.Mesh.Vertices, item.Mesh.Faces);

			var itemMatrix = item.WorldMatrix(this);
			mesh.Transform(itemMatrix);

			// calculate and add the PWN face from the loops
			var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, SliceHeight));
			var slice = SliceLayer.CreateSlice(mesh, cutPlane);

			// copy every face that is on or below the cut plane
			// cut the faces at the cut plane
			mesh.Split(new Plane(Vector3.UnitZ, SliceHeight), cutMargin, cleanAndMerge: false);

			// remove every face above the cut plane
			RemoveFacesAboveCut(mesh);

			slice.AsVertices().TriangulateFaces(null, mesh, SliceHeight);

			mesh.Transform(itemMatrix.Inverted);

			return (mesh, slice);
		}

		public override void Apply(UndoBuffer undoBuffer)
		{
			var newPathObject = new PathObject3D()
			{
				VertexStorage = new VertexStorage(this.GetVertexSource())
			};

			base.Apply(undoBuffer, new IObject3D[] { newPathObject });
		}

		private void RemoveFacesAboveCut(Mesh mesh)
		{
			var newVertices = new List<Vector3Float>();
			var newFaces = new List<Face>();
			var facesToRemove = new HashSet<int>();

			var cutRemove = SliceHeight - cutMargin;
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];

				if (mesh.Vertices[face.v0].Z >= cutRemove
					&& mesh.Vertices[face.v1].Z >= cutRemove
					&& mesh.Vertices[face.v2].Z >= cutRemove)
				{
					// record the face for removal
					facesToRemove.Add(i);
				}
			}

			// make a new list of all the faces we are keeping
			var keptFaces = new List<Face>();
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				if (!facesToRemove.Contains(i))
				{
					keptFaces.Add(mesh.Faces[i]);
				}
			}

			var vertexCount = mesh.Vertices.Count;

			// add the new vertices
			mesh.Vertices.AddRange(newVertices);

			// add the new faces (have to make the vertex indices to the new vertices
			foreach (var newFace in newFaces)
			{
				Face faceNewIndices = newFace;
				faceNewIndices.v0 += vertexCount;
				faceNewIndices.v1 += vertexCount;
				faceNewIndices.v2 += vertexCount;
				keptFaces.Add(faceNewIndices);
			}

			mesh.Faces = new FaceList(keptFaces);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			var valuesChanged = false;

			return TaskBuilder(
				"Find Slice".Localize(),
				(reporter, cancellationToken) =>
				{
					var polygons = new Polygons();
					VertexStorage = polygons.PolygonToPathStorage();

					var newChildren = new List<Object3D>();
					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var meshPolygons = Cut(sourceItem);
						
						polygons = polygons.CreateUnion(meshPolygons.polygons);

						var newMesh = new Object3D()
						{
							Mesh = meshPolygons.mesh,
							OwnerID = sourceItem.ID
						};
						newMesh.CopyProperties(sourceItem, Object3DPropertyFlags.All);
						newChildren.Add(newMesh);
					}

					VertexStorage = polygons.PolygonToPathStorage();

					RemoveAllButSource();
					SourceContainer.Visible = false;
					foreach (var child in newChildren)
					{
						this.Children.Add(child);
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLocks.Dispose();
						Invalidate(InvalidateType.DisplayValues);
						this.CancelAllParentBuilding();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}

		public void UpdateControls(PublicPropertyChange change)
		{
		}
	}
}