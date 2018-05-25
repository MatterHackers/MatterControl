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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CurveObject3D : MeshWrapperObject3D, IPublicPropertyObject, IEditorDraw
	{
		public double Diameter { get; set; } = double.MinValue;

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		// holds where we rotate the object
		Vector2 rotationCenter;
			
		public CurveObject3D()
		{
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			Rebuilding = true;
			ResetMeshWrappers(Object3DPropertyFlags.All);

			// remember the current matrix then clear it so the parts will rotate at the original wrapped position
			var currentMatrix = Matrix;
			Matrix = Matrix4X4.Identity;

			var meshWrapper = this.Descendants()
				.Where((obj) => obj.OwnerID == this.ID).ToList();

			var meshWrapperEnumerator = meshWrapper.Select((mw) => (Original: mw.Children.First(), Curved: mw));

			bool allMeshesAreValid = true;
			// reset the positions before we take the aabb
			foreach (var items in meshWrapperEnumerator)
			{
				var originalMesh = items.Original.Mesh;
				var curvedMesh = items.Curved.Mesh;

				if(curvedMesh == null || originalMesh == null)
				{
					allMeshesAreValid = false;
					break;
				}

				for (int i = 0; i < curvedMesh.Vertices.Count; i++)
				{
					curvedMesh.Vertices[i].Position = originalMesh.Vertices[i].Position;
				}

				curvedMesh.MarkAsChanged();
			}

			var aabb = this.GetAxisAlignedBoundingBox();

			if (Diameter == double.MinValue)
			{
				// uninitialized set to a reasonable value
				Diameter = (int)aabb.XSize;
			}

			if (Diameter > 0
				&& allMeshesAreValid)
			{
				var radius = Diameter / 2;
				var circumference = MathHelper.Tau * radius;
				rotationCenter = new Vector2(aabb.minXYZ.X, aabb.maxXYZ.Y + radius);
				foreach (var object3Ds in meshWrapperEnumerator)
				{
					// split edges to make it curve better
					/*if(false)
					{
						var maxXLength = aabb.XSize / AngleDegrees;
						// chop any segment that is too short in x
						for (int i = curvedMesh.MeshEdges.Count - 1; i >= 0; i--)
						{
							var edgeToSplit = curvedMesh.MeshEdges[i];
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
									curvedMesh.SplitMeshEdge(edgeToSplit, out newVertex, out newMeshEdge);
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
					var curvedMesh = object3Ds.Curved.Mesh;
					var originalMesh = object3Ds.Original.Mesh;
					// make sure we are working with a copy
					if (curvedMesh == originalMesh)
					{
						// Make sure the mesh we are going to copy is in a good state to be copied (so we maintain vertex count)
						//originalMesh.CleanAndMergeMesh(CancellationToken.None);
						curvedMesh = Mesh.Copy(originalMesh, CancellationToken.None);
						object3Ds.Curved.Mesh = curvedMesh;
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
						curvedMesh.Vertices[i].Position = Vector3.Transform(worldWithBend, matrix);
					}

					curvedMesh.MarkAsChanged();
					curvedMesh.CalculateNormals();
				}

				if (!BendCcw)
				{
					// fix the stored center so we draw correctly
					rotationCenter = new Vector2(aabb.minXYZ.X, aabb.minXYZ.Y - radius);
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

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				// we want to measure the 
				var currentMatrixInv = Matrix.GetInverted();
				var aabb = this.GetAxisAlignedBoundingBox(currentMatrixInv);

				layer.World.RenderCylinderOutline(this.WorldMatrix(), new Vector3(rotationCenter, aabb.Center.Z), Diameter, aabb.ZSize, 30, Color.Red);
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}
	}
}