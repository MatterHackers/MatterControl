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
using System.Threading.Tasks;
using g3;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class DecimateObject3D : OperationSourceContainerObject3D, IPropertyGridModifier
	{
		public DecimateObject3D()
		{
			Name = "Reduce".Localize();
		}

		public enum ReductionMode
		{
			Polygon_Count,
			Polygon_Percent
		}

		public ReductionMode Mode { get; set; } = ReductionMode.Polygon_Percent;

		[ReadOnly(true)]
		[Description("The original number of polygons.")]
		public int SourcePolygonCount
		{
			get
			{
				var total = 0;
				foreach (var sourceItem in SourceContainer.VisibleMeshes())
				{
					total += sourceItem.Mesh.Faces.Count;
				}

				return total;
			}

			set
			{
			}
		}

		[Description("The target number of polygons.")]
		public int TargetCount { get; set; } = -1;

		[Description("The percentage of polygons to keep.")]
		public double TargetPercent { get; set; } = 50;

		[Description("Ensure that each reduced point is on the surface of the original mesh. This is not normally required and slows the computation significantly.")]
		public bool MaintainSurface { get; set; } = false;

		[ReadOnly(true)]
		[Description("The number of polygons determined by the percentage reduction.")]
		[DisplayName("Final Count")]
		public int CountAfterPercentReduction
		{
			get
			{
				return TargetCount;
			}

			set
			{
			}
		}

		public Mesh Reduce(Mesh inMesh, int targetCount)
		{
			var mesh = inMesh.ToDMesh3();
			var reducer = new Reducer(mesh);

			if (MaintainSurface)
			{
				var tree = new DMeshAABBTree3(new DMesh3(mesh));
				tree.Build();
				var target = new MeshProjectionTarget(tree.Mesh, tree);
				reducer.SetProjectionTarget(target);
				reducer.ProjectionMode = Reducer.TargetProjectionMode.Inline;
			}

			reducer.ReduceToTriangleCount(Math.Max(4, targetCount));

			return reducer.Mesh.ToMesh();
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			var valuesChanged = false;

			// check if we have be initialized
			if (Mode == ReductionMode.Polygon_Count)
			{
				TargetCount = agg_basics.Clamp(TargetCount, 4, SourcePolygonCount, ref valuesChanged);
				TargetPercent = TargetCount / (double)SourcePolygonCount * 100;
			}
			else
			{
				TargetPercent = agg_basics.Clamp(TargetPercent, 0, 100, ref valuesChanged);
				TargetCount = (int)(SourcePolygonCount * TargetPercent / 100);
			}

			return ApplicationController.Instance.Tasks.Execute(
				"Reduce".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					SourceContainer.Visible = true;
					RemoveAllButSource();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						var targetCount = (int)(originalMesh.Faces.Count * TargetPercent / 100);
						var reducedMesh = Reduce(originalMesh, targetCount);

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

		public void UpdateControls(PublicPropertyChange change)
		{
			if (change.Context.GetEditRow(nameof(TargetPercent)) is GuiWidget percentWidget)
			{
				percentWidget.Visible = Mode == ReductionMode.Polygon_Percent;
			}

			if (change.Context.GetEditRow(nameof(CountAfterPercentReduction)) is GuiWidget roTargetCountWidget)
			{
				roTargetCountWidget.Visible = Mode == ReductionMode.Polygon_Percent;
			}

			if (change.Context.GetEditRow(nameof(TargetCount)) is GuiWidget countWidget)
			{
				countWidget.Visible = Mode == ReductionMode.Polygon_Count;
			}
		}
	}
}