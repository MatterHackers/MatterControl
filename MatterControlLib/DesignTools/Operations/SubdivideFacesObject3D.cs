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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SubdivideFacesObject3D : OperationSourceContainerObject3D
	{
		public SubdivideFacesObject3D()
		{
			Name = "Subdivide".Localize();
		}

		[Description("The maximum allowed edge length")]
		public double MaxEdgeLength { get; set; } = 20;

		[Description("No faces will be subdivided after this is reached")]
		public int MaxAllowedFaces { get; set; } = 100000;

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool valuesChanged = false;

			if (MaxEdgeLength < .01)
			{
				MaxEdgeLength = Math.Max(.01, MaxEdgeLength);
				valuesChanged = true;
			}

			if (MaxAllowedFaces < 100)
			{
				MaxAllowedFaces = Math.Max(100, MaxAllowedFaces);
				valuesChanged = true;
			}

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Subdivide".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var subdividedChildren = new List<IObject3D>();

					var status = new ProgressStatus();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						status.Status = "Copy Mesh".Localize();
						reporter.Report(status);
						var transformedMesh = originalMesh.Copy(CancellationToken.None);
						var itemMatrix = sourceItem.WorldMatrix(SourceContainer);

						// transform into this space
						transformedMesh.Transform(itemMatrix);

						status.Status = "Split Mesh".Localize();
						reporter.Report(status);

						// split faces until they are small enough
						var newVertices = new List<Vector3Float>();
						var newFaces = new List<Face>();

						for (int i = 0; i < transformedMesh.Faces.Count; i++)
						{
							var face = transformedMesh.Faces[i];

							SplitRecursive(new Vector3Float[]
								{
									transformedMesh.Vertices[face.v0],
									transformedMesh.Vertices[face.v1],
									transformedMesh.Vertices[face.v2]
								},
								face.normal,
								newVertices,
								newFaces);
						}

						transformedMesh.Vertices = newVertices;
						transformedMesh.Faces = new FaceList(newFaces);

						// transform back into item local space
						transformedMesh.Transform(itemMatrix.Inverted);

						var subdividedChild = new Object3D()
						{
							Mesh = transformedMesh
						};
						subdividedChild.CopyWorldProperties(sourceItem, SourceContainer, Object3DPropertyFlags.All);
						subdividedChild.Visible = true;

						subdividedChildren.Add(subdividedChild);
					}

					RemoveAllButSource();
					this.SourceContainer.Visible = false;

					this.Children.Modify((list) =>
					{
						list.AddRange(subdividedChildren);
					});

					rebuildLocks.Dispose();

					if (valuesChanged)
					{
						Invalidate(InvalidateType.DisplayValues);
					}

					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));

					return Task.CompletedTask;
				});
		}

		private void SplitRecursive(Vector3Float[] verts,
			Vector3Float normal,
			List<Vector3Float> newVertices,
			List<Face> newFaces)
		{
			var length = new float[3];

			var v0 = -1;
			var longestEdge = this.MaxEdgeLength;

			for (int i = 0; i < 3; i++)
			{
				var next = (i + 1) % 3;
				length[i] = (verts[next] - verts[i]).Length;

				// check if this edge should be split
				if (length[i] > longestEdge)
				{
					// record the edge to split
					v0 = i;
					longestEdge = length[i];
				}
			}

			// if we did not find a face
			if (v0 == -1)
			{
				var vertCount = newVertices.Count;
				newVertices.AddRange(verts);
				// found a polygon small enough so add it
				newFaces.Add(new Face(vertCount, vertCount + 1, vertCount + 2, normal));
				// no more splitting so return
				return;
			}

			// split the longest face in two and recurse on both of them
			var v1 = (v0 + 1) % 3;
			var v2 = (v0 + 2) % 3;

			var midPoint = (verts[v0] + verts[v1]) / 2;

			SplitRecursive(new Vector3Float[] { verts[v0], midPoint, verts[v2] },
				normal,
				newVertices,
				newFaces);

			// recurse on second split
			SplitRecursive(new Vector3Float[] { verts[v1], verts[v2], midPoint },
				normal,
				newVertices,
				newFaces);
		}
	}
}