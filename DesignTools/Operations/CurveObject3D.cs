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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CurveObject3D : MeshWrapperObject3D, IPublicPropertyObject
	{
		public double Diameter { get; set; } = 0;

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		public CurveObject3D()
		{
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			Rebuilding = true;
			ResetMeshWrappers();

			// remember the current matrix then clear it so the parts will rotate at the original wrapped position
			var currentMatrix = Matrix;
			Matrix = Matrix4X4.Identity;

			var meshWrapper = this.Descendants()
				.Where((obj) => obj.OwnerID == this.ID).ToList();

			// reset the positions before we take the aabb
			foreach (var items in meshWrapper.Select((mw) => (Original: mw.Children.First(),
				 Transformed: mw)))
			{
				var transformedMesh = items.Transformed.Mesh;
				var originalMesh = items.Original.Mesh;

				for (int i = 0; i < transformedMesh.Vertices.Count; i++)
				{
					transformedMesh.Vertices[i].Position = originalMesh.Vertices[i].Position;
				}

				transformedMesh.MarkAsChanged();
			}

			var aabb = this.GetAxisAlignedBoundingBox();

			if (Diameter > 0)
			{
				var radius = Diameter / 2;
				var circumference = MathHelper.Tau * radius;
				var rotationCenter = new Vector2(aabb.minXYZ.X, aabb.maxXYZ.Y + radius);
				foreach (var object3Ds in meshWrapper.Select((mw) => (Original: mw.Children.First(), Curved: mw)))
				{
					// split edges to make it curve better
					/*if(false)
					{
						var maxXLength = aabb.XSize / AngleDegrees;
						// chop any segment that is too short in x
						for (int i = transformedMesh.MeshEdges.Count - 1; i >= 0; i--)
						{
							var edgeToSplit = transformedMesh.MeshEdges[i];
							var start = edgeToSplit.VertexOnEnd[0].Position;
							var end = edgeToSplit.VertexOnEnd[1].Position;
							var edgeXLength = Math.Abs(end.X - start.X);
							int numberOfDivides = (int)(edgeXLength / maxXLength);
							if (numberOfDivides > 1)
							{
								for (int j = 1; j < numberOfDivides - 1; j++)
								{
									IVertex newVertex;
									MeshEdge newMeshEdge;
									transformedMesh.SplitMeshEdge(edgeToSplit, out newVertex, out newMeshEdge);
									var otherIndex = newMeshEdge.GetVertexEndIndex(newVertex);
									var ratio = (numberOfDivides - j) / (double)numberOfDivides;
									newVertex.Position = start + (end - start) * ratio;
									edgeToSplit = newMeshEdge;
									start = edgeToSplit.VertexOnEnd[0].Position;
									end = edgeToSplit.VertexOnEnd[1].Position;
								}
							}
						}
					}*/

					var originalMatrix = object3Ds.Original.WorldMatrix(this);
					var cuvedMesh = object3Ds.Curved.Mesh;
					var originalMesh = object3Ds.Original.Mesh;
					// make sure we are working with a copy
					if (cuvedMesh == originalMesh)
					{
						// Make sure the mesh we are going to copy is in a good state to be copied (so we maintain vertex count)
						originalMesh.CleanAndMergeMesh(CancellationToken.None);
						cuvedMesh = Mesh.Copy(originalMesh, CancellationToken.None);
						object3Ds.Curved.Mesh = cuvedMesh;
					}

					for (int i = 0; i < originalMesh.Vertices.Count; i++)
					{
						var matrix = originalMatrix;
						if (!BendCcw)
						{
							// rotate around so it wil bend correctly
							matrix *= Matrix4X4.CreateTranslation(0, -aabb.maxXYZ.Y, 0);
							matrix *= Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
							matrix *= Matrix4X4.CreateTranslation(0, aabb.maxXYZ.Y - aabb.YSize, 0);
						}
						var worldPosition = Vector3.Transform(originalMesh.Vertices[i].Position, matrix);

						var angleToRotate = ((worldPosition.X - aabb.minXYZ.X) / circumference) * MathHelper.Tau - MathHelper.Tau / 4;
						var distanceFromCenter = rotationCenter.Y - worldPosition.Y;

						var rotatePosition = new Vector3(Math.Cos(angleToRotate), Math.Sin(angleToRotate), 0) * distanceFromCenter;
						rotatePosition.Z = worldPosition.Z;
						matrix.Invert();
						var worldWithBend = rotatePosition + new Vector3(aabb.minXYZ.X, radius + aabb.maxXYZ.Y, 0);
						cuvedMesh.Vertices[i].Position = Vector3.Transform(worldWithBend, matrix);
					}

					cuvedMesh.MarkAsChanged();
					cuvedMesh.CalculateNormals();
				}
			}

			// set the matrix back
			Matrix = currentMatrix;

			Rebuilding = false;
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix)
				&& invalidateType.Source != this
				&& !Rebuilding)
			{
				Rebuild(null);
			}
			base.OnInvalidate(invalidateType);
		}
	}
}