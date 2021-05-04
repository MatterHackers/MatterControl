/*
Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
Copyright (c) 2018, Lars Brubaker
All rights reserved.
*/

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using g3;
using gs;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using static gs.MeshAutoRepair;

namespace MatterHackers.MatterControl.DesignTools
{
	public class RepairObject3D : OperationSourceContainerObject3D, IPropertyGridModifier
	{
		public RepairObject3D()
		{
			Name = "Repair".Localize();
		}

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		[ReadOnly(true)]
		public int InitialVertices { get; set; }

		[ReadOnly(true)]
		public int InitialFaces { get; set; }

		[Description("Align and merge any vertices that are nearly coincident.")]
		public bool WeldVertices { get; set; } = true;

		[Description("Make all the faces have a consistent orientation.")]
		public bool FaceOrientation { get; set; } = false;

		[Description("Repair any small cracks or bad seams in the model.")]
		public bool WeldEdges { get; set; } = false;

		[Description("Try to fill in any holes that are in the model.")]
		public bool FillHoles { get; set; } = false;


		[Description("Remove interior faces and bodies. This should only be used if the interior bodies are separate from the external faces, otherwise it may remove requried faces.")]
		public RemoveModes RemoveMode { get; set; } = RemoveModes.None;

		[ReadOnly(true)]
		public int FinalVertices { get; set; }

		[ReadOnly(true)]
		public int FinalFaces { get; set; }

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

					var inititialVertices = 0;
					var inititialFaces = 0;
					var finalVertices = 0;
					var finalFaces = 0;
					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						inititialFaces += originalMesh.Faces.Count;
						inititialVertices += originalMesh.Vertices.Count;
						var repairedMesh = Repair(originalMesh, cancellationToken);
						finalFaces += repairedMesh.Faces.Count;
						finalVertices += repairedMesh.Vertices.Count;

						var repairedChild = new Object3D()
						{
							Mesh = repairedMesh
						};
						repairedChild.CopyWorldProperties(sourceItem, this, Object3DPropertyFlags.All, false);
						this.Children.Add(repairedChild);
					}

					this.InitialFaces = inititialFaces;
					this.InitialVertices = inititialVertices;
					this.FinalFaces = finalFaces;
					this.FinalVertices = finalVertices;

					SourceContainer.Visible = false;

					UiThread.RunOnIdle(() =>
					{
						rebuildLocks.Dispose();
						if (valuesChanged)
						{
							Invalidate(InvalidateType.DisplayValues);
						}
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}

		public Mesh Repair(Mesh sourceMesh, CancellationToken cancellationToken)
		{
			var inMesh = sourceMesh;

			try
			{
				if (WeldVertices)
				{
					inMesh = sourceMesh.Copy(cancellationToken);
					inMesh.CleanAndMerge();
					if (!FaceOrientation
						&& RemoveMode == RemoveModes.None
						&& !WeldEdges
						&& !FillHoles)
					{
						return inMesh;
					}
				}

				var mesh = inMesh.ToDMesh3();
				int repeatCount = 0;
				int erosionIterations = 5;
				double repairTolerance = MathUtil.ZeroTolerancef;
				double minEdgeLengthTol = 0.0001;

			repeat_all:

				if (FaceOrientation)
				{
					// make sure orientation of connected components is consistent
					// TODO: what about mobius strip problems?
					RepairOrientation(mesh, cancellationToken, true);
				}

				if (RemoveMode != RemoveModes.None)
				{
					// Remove parts of the mesh we don't want before we bother with anything else
					// TODO: maybe we need to repair orientation first? if we want to use MWN (MeshWindingNumber)...
					RemoveInside(mesh);
					cancellationToken.ThrowIfCancellationRequested();
				}

				if (WeldEdges || FillHoles)
				{
					// Do safe close-cracks to handle easy cases
					RepairCracks(mesh, true, repairTolerance);

					if (mesh.IsClosed())
					{
						goto all_done;
					}

					cancellationToken.ThrowIfCancellationRequested();

					// Collapse tiny edges and then try easy cases again, and
					// then allow for handling of ambiguous cases
					CollapseAllDegenerateEdges(mesh, cancellationToken, repairTolerance * 0.5, true);
					cancellationToken.ThrowIfCancellationRequested();

					RepairCracks(mesh, true, 2 * repairTolerance);
					cancellationToken.ThrowIfCancellationRequested();

					RepairCracks(mesh, false, 2 * repairTolerance);
					cancellationToken.ThrowIfCancellationRequested();

					if (mesh.IsClosed())
					{
						goto all_done;
					}

					// Possibly we have joined regions with different orientation (is it?), fix that
					// TODO: mobius strips again
					RepairOrientation(mesh, cancellationToken, true);
					cancellationToken.ThrowIfCancellationRequested();

					// get rid of any remaining single-triangles before we start filling holes
					MeshEditor.RemoveIsolatedTriangles(mesh);
				}

				if (FillHoles)
				{
					// Ok, fill simple holes.
					int nRemainingBowties = 0;
					FillTrivialHoles(mesh, cancellationToken, out int nHoles, out bool bSawSpans);
					cancellationToken.ThrowIfCancellationRequested();

					if (mesh.IsClosed())
					{
						goto all_done;
					}

					// Now fill harder holes. If we saw spans, that means boundary loops could
					// not be resolved in some cases, do we disconnect bowties and try again.
					FillAnyHoles(mesh, cancellationToken, out nHoles, out bSawSpans);
					cancellationToken.ThrowIfCancellationRequested();

					if (bSawSpans)
					{
						DisconnectBowties(mesh, out nRemainingBowties);
						FillAnyHoles(mesh, cancellationToken, out nHoles, out bSawSpans);
					}

					cancellationToken.ThrowIfCancellationRequested();

					if (mesh.IsClosed())
					{
						goto all_done;
					}

					// We may have a closed mesh now but it might still have bowties (eg
					// tetrahedra sharing vtx case). So disconnect those.
					DisconnectBowties(mesh, out nRemainingBowties);
					cancellationToken.ThrowIfCancellationRequested();

					// If the mesh is not closed, we will do one more round to try again.
					if (repeatCount == 0 && mesh.IsClosed() == false)
					{
						repeatCount++;
						goto repeat_all;
					}

					// Ok, we didn't get anywhere on our first repeat. If we are still not
					// closed, we will try deleting boundary triangles and repeating.
					// Repeat this N times.
					if (repeatCount <= erosionIterations && mesh.IsClosed() == false)
					{
						repeatCount++;
						var bdry_faces = new MeshFaceSelection(mesh);
						foreach (int eid in MeshIterators.BoundaryEdges(mesh))
						{
							bdry_faces.SelectEdgeTris(eid);
						}

						MeshEditor.RemoveTriangles(mesh, bdry_faces, true);
						goto repeat_all;
					}
				}

			all_done:

				// and do a final clean up of the model
				if (FillHoles)
				{
					// Remove tiny edges
					if (minEdgeLengthTol > 0)
					{
						CollapseAllDegenerateEdges(mesh, cancellationToken, minEdgeLengthTol, false);
					}

					cancellationToken.ThrowIfCancellationRequested();

					// finally do global orientation
					RepairOrientation(mesh, cancellationToken, true);
					cancellationToken.ThrowIfCancellationRequested();
				}

				return mesh.ToMesh();
			}
			catch (OperationCanceledException)
			{
				return inMesh;
			}
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			// if (change.Context.GetEditRow(nameof(TargetPercent)) is GuiWidget percentWidget)
			// {
			// 	percentWidget.Visible = Mode == ReductionMode.Polygon_Percent;
			// }
		}

		private bool CollapseAllDegenerateEdges(DMesh3 mesh,
			CancellationToken cancellationToken,
			double minLength,
			bool bBoundaryOnly)
		{
			bool repeat = true;
			while (repeat)
			{
				cancellationToken.ThrowIfCancellationRequested();
				CollapseDegenerateEdges(mesh, cancellationToken, minLength, bBoundaryOnly, out int collapseCount);
				if (collapseCount == 0)
				{
					repeat = false;
				}
			}

			return true;
		}

		private bool CollapseDegenerateEdges(DMesh3 mesh,
			CancellationToken cancellationToken,
			double minLength,
			bool bBoundaryOnly,
			out int collapseCount)
		{
			collapseCount = 0;
			// don't iterate sequentially because there may be pathological cases
			foreach (int eid in MathUtil.ModuloIteration(mesh.MaxEdgeID))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (mesh.IsEdge(eid) == false)
				{
					continue;
				}

				bool isBoundaryEdge = mesh.IsBoundaryEdge(eid);
				if (bBoundaryOnly && isBoundaryEdge == false)
				{
					continue;
				}

				Index2i ev = mesh.GetEdgeV(eid);
				Vector3d a = mesh.GetVertex(ev.a), b = mesh.GetVertex(ev.b);
				if (a.Distance(b) < minLength)
				{
					int keep = mesh.IsBoundaryVertex(ev.a) ? ev.a : ev.b;
					int discard = (keep == ev.a) ? ev.b : ev.a;
					MeshResult result = mesh.CollapseEdge(keep, discard, out DMesh3.EdgeCollapseInfo collapseInfo);
					if (result == MeshResult.Ok)
					{
						++collapseCount;
						if (mesh.IsBoundaryVertex(keep) == false || isBoundaryEdge)
						{
							mesh.SetVertex(keep, (a + b) * 0.5);
						}
					}
				}
			}

			return true;
		}

		private bool DisconnectBowties(DMesh3 mesh, out int nRemaining)
		{
			var editor = new MeshEditor(mesh);
			nRemaining = editor.DisconnectAllBowties();
			return true;
		}

		private bool RemoveInside(DMesh3 mesh)
		{
			if (RemoveMode == RemoveModes.Interior)
			{
				return RemoveInterior(mesh, out _);
			}
			else if (RemoveMode == RemoveModes.Occluded)
			{
				return RemoveOccluded(mesh, out _);
			}

			return true;
		}

		private void FillAnyHoles(DMesh3 mesh,
			CancellationToken cancellationToken,
			out int nRemaining,
			out bool sawSpans)
		{
			var loops = new MeshBoundaryLoops(mesh);
			nRemaining = 0;
			sawSpans = loops.SawOpenSpans;

			foreach (var loop in loops)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var filler = new MinimalHoleFill(mesh, loop);
				bool filled = filler.Apply();
				if (filled == false)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var fallback = new SimpleHoleFiller(mesh, loop);
					fallback.Fill();
				}
			}
		}

		private void FillTrivialHoles(DMesh3 mesh,
			CancellationToken cancellationToken,
			out int nRemaining,
			out bool sawSpans)
		{
			var loops = new MeshBoundaryLoops(mesh);
			nRemaining = 0;
			sawSpans = loops.SawOpenSpans;

			foreach (var loop in loops)
			{
				cancellationToken.ThrowIfCancellationRequested();
				bool filled = false;
				if (loop.VertexCount == 3)
				{
					var filler = new SimpleHoleFiller(mesh, loop);
					filled = filler.Fill();
				}
				else if (loop.VertexCount == 4)
				{
					var filler = new MinimalHoleFill(mesh, loop);
					filled = filler.Apply();
					if (filled == false)
					{
						var fallback = new SimpleHoleFiller(mesh, loop);
						filled = fallback.Fill();
					}
				}

				if (filled == false)
				{
					++nRemaining;
				}
			}
		}

		private bool RemoveInterior(DMesh3 mesh, out int nRemoved)
		{
			var remove = new RemoveOccludedTriangles(mesh)
			{
				PerVertex = true,
				InsideMode = RemoveOccludedTriangles.CalculationMode.FastWindingNumber
			};
			remove.Apply();
			nRemoved = remove.RemovedT.Count;
			return true;
		}

		private bool RemoveOccluded(DMesh3 mesh, out int nRemoved)
		{
			var remove = new RemoveOccludedTriangles(mesh)
			{
				PerVertex = true,
				InsideMode = RemoveOccludedTriangles.CalculationMode.SimpleOcclusionTest
			};
			remove.Apply();
			nRemoved = remove.RemovedT.Count;
			return true;
		}

		private bool RepairCracks(DMesh3 mesh, bool bUniqueOnly, double mergeDist)
		{
			try
			{
				var merge = new MergeCoincidentEdges(mesh)
				{
					OnlyUniquePairs = bUniqueOnly,
					MergeDistance = mergeDist
				};
				return merge.Apply();
			}
			catch (Exception /*e*/)
			{
				// ??
				return false;
			}
		}

		private void RepairOrientation(DMesh3 mesh,
			CancellationToken cancellationToken,
			bool bGlobal)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var orient = new MeshRepairOrientation(mesh);
			orient.OrientComponents();

			if (bGlobal)
			{
				cancellationToken.ThrowIfCancellationRequested();
				orient.SolveGlobalOrientation();
			}
		}
	}
}