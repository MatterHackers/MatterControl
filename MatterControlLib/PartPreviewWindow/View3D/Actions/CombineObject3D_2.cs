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

		public static void CheckManifoldData(CombineObject3D_2 item, IObject3D result)
		{
			bool IsManifold(Mesh mesh)
			{
				var meshEdgeList = mesh.NewMeshEdges();

				foreach (var meshEdge in meshEdgeList)
				{
					if (meshEdge.Faces.Count() != 2)
					{
						return false;
					}
				}

				return true;
			}

			if (!IsManifold(result.Mesh))
			{
				// create a new combine of a and b and add it to the root
				var combine = new CombineObject3D_2();

				var participants = item.SourceContainer.VisibleMeshes().Where(m => m.WorldOutputType(item.SourceContainer) != PrintOutputTypes.Hole);
				// all participants are manifold
				foreach (var participant in participants)
				{
					combine.SourceContainer.Children.Add(new Object3D() 
					{ 
						Mesh = participant.Mesh.Copy(new CancellationToken()),
						Matrix = participant.Matrix
					});
				}

				var scene = result.Parents().Last();
				scene.Children.Add(combine);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
				"Combine".Localize(),
				null,
				(reporter, cancellationTokenSource) =>
				{
					this.cancellationToken = cancellationTokenSource;
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Combine(cancellationTokenSource.Token, reporter);

						if (cancellationToken.IsCancellationRequested)
                        {
							// the combine was canceled set our children to the source object children
							SourceContainer.Visible = true;
							RemoveAllButSource();
							Children.Modify((list) =>
							{
								foreach (var child in SourceContainer.Children)
								{
									list.Add(child);
								}
							});

							SourceContainer.Visible = false;
						}
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

            var holes = SourceContainer.VisibleMeshes().Where(m => m.WorldOutputType(SourceContainer) == PrintOutputTypes.Hole);

            Mesh resultsMesh = null;
            var participants = SourceContainer.VisibleMeshes().Where(m => m.WorldOutputType(SourceContainer) != PrintOutputTypes.Hole);
            if (participants.Count() == 0)
            {
                if (holes.Count() == 0)
                {
                    return;
                }
            }
            else
            {
                resultsMesh = Object3D.CombineParticipants(SourceContainer, participants, cancellationToken, reporter, Processing, InputResolution, OutputResolution);
            }

            var resultsItem = new Object3D()
            {
                Mesh = resultsMesh
            };

            if (holes != null)
            {
                var holesMesh = CombineParticipants(SourceContainer, holes, cancellationToken, null);
                if (holesMesh != null)
                {
                    var holesItem = new Object3D()
                    {
                        Mesh = holesMesh,
                        OutputType = PrintOutputTypes.Hole
                    };

                    if (resultsMesh != null)
                    {
                        var resultItems = SubtractObject3D_2.DoSubtract(this,
                            new List<IObject3D>() { resultsItem },
                            new List<IObject3D>() { holesItem },
                            null,
                            cancellationToken);

                        resultsItem.Mesh = resultItems.First().Mesh;
                    }
                    else
                    {
                        holesItem.CopyProperties(holes.First(), Object3DPropertyFlags.All & (~Object3DPropertyFlags.Matrix));
                        this.Children.Add(holesItem);
                        SourceContainer.Visible = false;
                        return;
                    }
                }
            }

            resultsItem.CopyProperties(participants.First(), Object3DPropertyFlags.All & (~Object3DPropertyFlags.Matrix));
            this.Children.Add(resultsItem);
#if DEBUG
			//resultsItem.Mesh.MergeVertices(.01);
			//resultsItem.Mesh.CleanAndMerge();
			//CheckManifoldData(this, resultsItem);
#endif
			SourceContainer.Visible = false;
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
