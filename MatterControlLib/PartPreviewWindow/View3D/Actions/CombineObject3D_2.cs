/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[ShowUpdateButton]
	public class CombineObject3D_2 : OperationSourceContainerObject3D, IPropertyGridModifier, IBuildsOnThread
	{
        private CancellationTokenSource cancellationToken;

        public CombineObject3D_2()
		{
			Name = "Combine";
		}

#if DEBUG
		public ProcessingModes Processing { get; set; } = ProcessingModes.Polygons;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ProcessingResolution OutputResolution { get; set; } = ProcessingResolution._64;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public IplicitSurfaceMethod MeshAnalysis { get; set; }

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ProcessingResolution InputResolution { get; set; } = ProcessingResolution._64;
#else
		private ProcessingModes Processing { get; set; } = ProcessingModes.Polygons;
		private ProcessingResolution OutputResolution { get; set; } = ProcessingResolution._64;
		private IplicitSurfaceMethod MeshAnalysis { get; set; }
		private ProcessingResolution InputResolution { get; set; } = ProcessingResolution._64;
#endif
		public bool IsBuilding => this.cancellationToken != null;
		
		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
				"Combine".Localize(),
				null,
				(reporter, cancellationTokenSource) =>
				{
					this.cancellationToken = cancellationTokenSource as CancellationTokenSource;
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Combine(cancellationTokenSource.Token, reporter);
					}
					catch
					{
					}

					if (!NameOverriden)
					{
						Name = NameFromChildren();
						NameOverriden = false;
					}

					this.cancellationToken = null;
					UiThread.RunOnIdle(() =>
					{
                        rebuildLocks.Dispose();
                        this.CancelAllParentBuilding();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}

		public void Combine()
		{
			Combine(CancellationToken.None, null);
		}

        public override string NameFromChildren()
        {
			return CalculateName(SourceContainer.Children, " + ");
		}

		private void Combine(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
        {
            SourceContainer.Visible = true;
            RemoveAllButSource();

            Mesh resultsMesh = null;
            var participants = SourceContainer.VisibleMeshes().Where(m => m.WorldOutputType(this) != PrintOutputTypes.Hole);
            if (participants.Count() == 0)
            {
                return;
            }
            else
            {
                resultsMesh = CombineParticipanets(reporter, participants, cancellationToken);
            }

            var resultsItem = new Object3D()
            {
                Mesh = resultsMesh
            };

            if (resultsMesh != null)
            {
                var holes = SourceContainer.VisibleMeshes().Where(m => m.WorldOutputType(this) == PrintOutputTypes.Hole);
                if (holes != null)
                {
                    var holesMesh = CombineParticipanets(null, holes, cancellationToken);
                    var holesItem = new Object3D()
                    {
                        Mesh = holesMesh
                    };
                    var resultItems = SubtractObject3D_2.DoSubtract(null,
                        new List<IObject3D>() { resultsItem },
                        new List<IObject3D>() { holesItem },
                        null,
                        cancellationToken);

                    resultsItem.Mesh = resultItems.First().Mesh;
                }
            }

            resultsItem.CopyProperties(participants.First(), Object3DPropertyFlags.All & (~Object3DPropertyFlags.Matrix));
            this.Children.Add(resultsItem);
            SourceContainer.Visible = false;
        }

        private Mesh CombineParticipanets(IProgress<ProgressStatus> reporter, IEnumerable<IObject3D> participants, CancellationToken cancellationToken)
        {
            List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>> touchingSets = GetTouchingMeshes(participants);

            var totalOperations = touchingSets.Sum(t => t.Count);

            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var progressStatus = new ProgressStatus();

            var setMeshes = new List<Mesh>();
            foreach (var set in touchingSets)
            {
                var setMesh = set.First().Item1;
                var keepWorldMatrix = set.First().matrix;

                if (set.Count > 1)
                {
#if false
                    setMesh = BooleanProcessing.DoArray(set.Select(i => (i.mesh, i.matrix)),
                        CsgModes.Union,
                        Processing,
                        InputResolution,
                        OutputResolution,
                        reporter,
                        cancellationToken);
#else

                    bool first = true;
                    foreach (var next in set)
                    {
                        if (first)
                        {
                            first = false;
                            continue;
                        }

                        setMesh = BooleanProcessing.Do(setMesh,
                            keepWorldMatrix,
                            // other mesh
                            next.mesh,
                            next.matrix,
                            // operation type
                            CsgModes.Union,
                            Processing,
                            InputResolution,
                            OutputResolution,
                            // reporting
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            progressStatus,
                            cancellationToken);

                        // after the first time we get a result the results mesh is in the right coordinate space
                        keepWorldMatrix = Matrix4X4.Identity;

                        // report our progress
                        ratioCompleted += amountPerOperation;
                        progressStatus.Progress0To1 = ratioCompleted;
                        reporter?.Report(progressStatus);
                    }
#endif

                    setMeshes.Add(setMesh);
                }
                else
                {
                    setMesh.Transform(keepWorldMatrix);
                    // report our progress
                    ratioCompleted += amountPerOperation;
                    progressStatus.Progress0To1 = ratioCompleted;
                    reporter?.Report(progressStatus);
                    setMeshes.Add(setMesh);
                }
            }

            Mesh resultsMesh = null;
            foreach (var setMesh in setMeshes)
            {
                if (resultsMesh == null)
                {
                    resultsMesh = setMesh;
                }
                else
                {
                    resultsMesh.CopyAllFaces(setMesh, Matrix4X4.Identity);
                }
            }

            return resultsMesh;
        }

        private List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>> GetTouchingMeshes(IEnumerable<IObject3D> participants)
        {
            void AddAllTouching(List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)> touching,
                List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)> available)
            {
                // add the frirst item
                touching.Add(available[available.Count - 1]);
                available.RemoveAt(available.Count - 1);

                var indexBeingChecked = 0;

                // keep adding items until we have checked evry item in the the touching list
                while (indexBeingChecked < touching.Count
                    && available.Count > 0)
                {
                    // look for a aabb that intersects any aabb in the set
                    for (int i = available.Count - 1; i >= 0; i--)
                    {
                        if (touching[indexBeingChecked].aabb.Intersects(available[i].aabb))
                        {
                            touching.Add(available[i]);
                            available.RemoveAt(i);
                        }
                    }

                    indexBeingChecked++;
                }
            }

            var allItems = participants.Select(i =>
            {
                var mesh = i.Mesh.Copy(CancellationToken.None);
                var matrix = i.WorldMatrix(SourceContainer);
                var aabb = mesh.GetAxisAlignedBoundingBox(matrix);
                return (mesh, matrix, aabb);
            }).ToList();

            var touchingSets = new List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>>();

            while (allItems.Count > 0)
            {
                var touchingSet = new List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>();
                touchingSets.Add(touchingSet);
                AddAllTouching(touchingSet, allItems);
            }

            return touchingSets;
        }

        public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(OutputResolution), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(MeshAnalysis), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons && MeshAnalysis == IplicitSurfaceMethod.Grid);
		}

        public void CancelBuild()
        {
			var threadSafe = this.cancellationToken;
			if(threadSafe != null)
            {
				threadSafe.Cancel();
            }
        }
    }
}
