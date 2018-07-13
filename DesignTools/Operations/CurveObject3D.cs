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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CurveObject3D : MeshWrapperObject3D, IEditorDraw
	{
		public double Diameter { get; set; } = double.MinValue;

		[Range(0, 100, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Where to start the bend as a percent of the width of the part")]
		public double StartPercent { get; set; } = 50;

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		// holds where we rotate the object
		Vector2 rotationCenter;
			
		public CurveObject3D()
		{
			Name = "Curve".Localize();
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");
			bool propertyUpdated = Diameter == double.MinValue;
			if (StartPercent < 0
				|| StartPercent > 100)
			{
				StartPercent = Math.Min(100, Math.Max(0, StartPercent));
				propertyUpdated = true;
			}

			using (RebuildLock())
			{
				ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);

				// remember the current matrix then clear it so the parts will rotate at the original wrapped position
				var currentMatrix = Matrix;
				Matrix = Matrix4X4.Identity;

				var meshWrapperEnumerator = WrappedObjects();

				var aabb = this.GetAxisAlignedBoundingBox();

				if (Diameter == double.MinValue)
				{
					// uninitialized set to a reasonable value
					Diameter = (int)aabb.XSize;
					// TODO: ensure that the editor display value is updated
				}

				if (Diameter > 0)
				{
					var radius = Diameter / 2;
					var circumference = MathHelper.Tau * radius;
					rotationCenter = new Vector2(aabb.minXYZ.X + (aabb.maxXYZ.X - aabb.minXYZ.X) * (StartPercent / 100), aabb.maxXYZ.Y + radius);
					foreach (var object3Ds in meshWrapperEnumerator)
					{
						var originalMatrix = object3Ds.original.WorldMatrix(this);
						var curvedMesh = object3Ds.meshCopy.Mesh;
						var originalMesh = object3Ds.original.Mesh;

						if (false)
						{
							int sidesPerRotation = 30;
							double numRotations = aabb.XSize / circumference;
							double numberOfCuts = numRotations * sidesPerRotation;
							var maxXLength = aabb.XSize / numberOfCuts;
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

										foreach (var face in edgeToSplit.FacesSharingMeshEdge())
										{
											Face newFace;
											curvedMesh.SplitFace(face,
												edgeToSplit.VertexOnEnd[0],
												edgeToSplit.VertexOnEnd[1],
												out newMeshEdge,
												out newFace);
										}
									}
								}
							}
						}

						for (int i = 0; i < originalMesh.Vertices.Count; i++)
						{
							var matrix = originalMatrix;
							if (!BendCcw)
							{
								// rotate around so it will bend correctly
								matrix *= Matrix4X4.CreateTranslation(0, -aabb.maxXYZ.Y, 0);
								matrix *= Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
								matrix *= Matrix4X4.CreateTranslation(0, aabb.maxXYZ.Y - aabb.YSize, 0);
							}
							var worldPosition = Vector3.Transform(originalMesh.Vertices[i].Position, matrix);

							var angleToRotate = ((worldPosition.X - rotationCenter.X) / circumference) * MathHelper.Tau - MathHelper.Tau / 4;
							var distanceFromCenter = rotationCenter.Y - worldPosition.Y;

							var rotatePosition = new Vector3(Math.Cos(angleToRotate), Math.Sin(angleToRotate), 0) * distanceFromCenter;
							rotatePosition.Z = worldPosition.Z;
							var worldWithBend = rotatePosition + new Vector3(rotationCenter.X, radius + aabb.maxXYZ.Y, 0);
							curvedMesh.Vertices[i].Position = Vector3.Transform(worldWithBend, matrix.Inverted);
						}

						// the vertices need to be resorted as they have moved relative to each other
						curvedMesh.Vertices.Sort();

						curvedMesh.MarkAsChanged();
						curvedMesh.CalculateNormals();
					}

					if (!BendCcw)
					{
						// fix the stored center so we draw correctly
						rotationCenter = new Vector2(rotationCenter.X, aabb.minXYZ.Y - radius);
					}
				}

				// set the matrix back
				Matrix = currentMatrix;
			}

			base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			if(propertyUpdated)
			{
				base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateType.InvalidateType == InvalidateType.Properties
				&& invalidateType.Source == this)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				// we want to measure the 
				var currentMatrixInv = Matrix.Inverted;
				var aabb = this.GetAxisAlignedBoundingBox(currentMatrixInv);

				layer.World.RenderCylinderOutline(this.WorldMatrix(), new Vector3(rotationCenter, aabb.Center.Z), Diameter, aabb.ZSize, 30, Color.Red);
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}
	}
}