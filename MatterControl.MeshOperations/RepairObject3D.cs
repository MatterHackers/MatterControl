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

using g3;
using gs;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	public class RepairObject3D : OperationSourceContainerObject3D, IPropertyGridModifier
	{
		public RepairObject3D()
		{
			Name = "Repair".Localize();
		}

		[Description("Ensure that each reduced point is on the surface of the original mesh. This is not normally required and slows the computation significantly.")]
		public bool FaceOrientation { get; set; } = true;

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			var valuesChanged = false;

			// check if we have be initialized

			return TaskBuilder(
				"Repair".Localize(),
				(reporter, cancellationToken) =>
				{
					SourceContainer.Visible = true;
					RemoveAllButSource();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						var reducedMesh = Repair(originalMesh, cancellationToken);

						var newMesh = new Object3D()
						{
							Mesh = reducedMesh
						};
						newMesh.CopyProperties(sourceItem, Object3DPropertyFlags.All);
						this.Children.Add(newMesh);
					}

					SourceContainer.Visible = false;
					rebuildLocks.Dispose();

					if (valuesChanged)
					{
						Invalidate(InvalidateType.DisplayValues);
					}

					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));

					return Task.CompletedTask;
				});
		}

		public Mesh Repair(Mesh inMesh, CancellationToken cancellationToken)
		{
			var mesh = inMesh.ToDMesh3();
			if (FaceOrientation)
			{
				var repaired = new MeshRepairOrientation(mesh);
				repaired.OrientComponents();
				mesh = repaired.Mesh;
			}

			{
				int repeat_count = 0;

				repeat_all:
				// Remove parts of the mesh we don't want before we bother with anything else
				// TODO: maybe we need to repair orientation first? if we want to use MWN...
				do_remove_inside(mesh);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				// make sure orientation of connected components is consistent
				// TODO: what about mobius strip problems?
				repair_orientation(mesh, cancellationToken, false);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				// Do safe close-cracks to handle easy cases
				repair_cracks(true, RepairTolerance);

				if (Mesh.IsClosed()) goto all_done;
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				// Collapse tiny edges and then try easy cases again, and
				// then allow for handling of ambiguous cases
				collapse_all_degenerate_edges(RepairTolerance * 0.5, true);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				repair_cracks(true, 2 * RepairTolerance);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				repair_cracks(false, 2 * RepairTolerance);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				if (Mesh.IsClosed()) goto all_done;

				// Possibly we have joined regions with different orientation (is it?), fix that
				// TODO: mobius strips again
				repair_orientation(false);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}


				// get rid of any remaining single-triangles before we start filling holes
				remove_loners();
				
				// Ok, fill simple holes.
				int nRemainingBowties = 0;
				int nHoles; bool bSawSpans;
				fill_trivial_holes(out nHoles, out bSawSpans);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				if (Mesh.IsClosed()) goto all_done;
				
				// Now fill harder holes. If we saw spans, that means boundary loops could
				// not be resolved in some cases, do we disconnect bowties and try again.
				fill_any_holes(out nHoles, out bSawSpans);
				if (Cancelled()) return false;
				if (bSawSpans)
				{
					disconnect_bowties(out nRemainingBowties);
					fill_any_holes(out nHoles, out bSawSpans);
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				if (mesh.IsClosed())
				{
					goto all_done;
				}

				// We may have a closed mesh now but it might still have bowties (eg
				// tetrahedra sharing vtx case). So disconnect those.
				disconnect_bowties(out nRemainingBowties);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				// If the mesh is not closed, we will do one more round to try again.
				if (repeat_count == 0 && mesh.IsClosed() == false)
				{
					repeat_count++;
					goto repeat_all;
				}

				// Ok, we didn't get anywhere on our first repeat. If we are still not
				// closed, we will try deleting boundary triangles and repeating.
				//* Repeat this N times.
				if (repeat_count <= ErosionIterations && Mesh.IsClosed() == false)
				{
					repeat_count++;
					MeshFaceSelection bdry_faces = new MeshFaceSelection(Mesh);
					foreach (int eid in MeshIterators.BoundaryEdges(Mesh))
					{
						bdry_faces.SelectEdgeTris(eid);
					}

					MeshEditor.RemoveTriangles(Mesh, bdry_faces, true);
					goto repeat_all;
				}

				all_done:
				// Remove tiny edges
				if (MinEdgeLengthTol > 0)
				{
					collapse_all_degenerate_edges(MinEdgeLengthTol, false);
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}

				// finally do global orientation
				repair_orientation(mesh, cancellationToken, true);
				if (cancellationToken.IsCancellationRequested)
				{
					return inMesh;
				}
			}

			return mesh.ToMesh();
		}

		void repair_orientation(DMesh3 Mesh, CancellationToken cancellationToken, bool bGlobal)
		{
			MeshRepairOrientation orient = new MeshRepairOrientation(Mesh);
			orient.OrientComponents();
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			if (bGlobal)
			{
				orient.SolveGlobalOrientation();
			}
		}

		bool remove_interior(DMesh3 Mesh, out int nRemoved)
		{
			RemoveOccludedTriangles remove = new RemoveOccludedTriangles(Mesh);
			remove.PerVertex = true;
			remove.InsideMode = RemoveOccludedTriangles.CalculationMode.FastWindingNumber;
			remove.Apply();
			nRemoved = remove.RemovedT.Count();
			return true;
		}

		bool remove_occluded(DMesh3 Mesh, out int nRemoved)
		{
			RemoveOccludedTriangles remove = new RemoveOccludedTriangles(Mesh);
			remove.PerVertex = true;
			remove.InsideMode = RemoveOccludedTriangles.CalculationMode.SimpleOcclusionTest;
			remove.Apply();
			nRemoved = remove.RemovedT.Count();
			return true;
		}

		bool do_remove_inside(DMesh3 Mesh)
		{
			int nRemoved = 0;
			if (RemoveMode == RemoveModes.Interior)
			{
				return remove_interior(Mesh, out nRemoved);
			}
			else if (RemoveMode == RemoveModes.Occluded)
			{
				return remove_occluded(Mesh, out nRemoved);
			}

			return true;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			//if (change.Context.GetEditRow(nameof(TargetPercent)) is GuiWidget percentWidget)
			//{
			//	percentWidget.Visible = Mode == ReductionMode.Polygon_Percent;
			//}
		}
	}
}