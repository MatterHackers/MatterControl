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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ArrayRadialObject3D : OperationSourceContainerObject3D, IEditorDraw
	{
		public ArrayRadialObject3D()
		{
			Name = "Radial Array".Localize();
		}

		[DisplayName("Rotate About")]
		public DirectionAxis Axis { get; set; } = new DirectionAxis() { Origin = Vector3.NegativeInfinity, Normal = Vector3.UnitZ };

		public override bool CanFlatten => true;

		public int Count { get; set; } = 3;

		[Description("Rotate the part to the same angle as the array.")]
		public bool RotatePart { get; set; } = true;

		// make this public when within angle works
		private double Angle { get; set; } = 360;

		// make this public when it works
		[DisplayName("Keep Within Angle")]
		[Description("Keep the entire extents of the part within the angle described.")]
		private bool KeepInAngle { get; set; } = false;

		public override async Task Rebuild()
		{
			// check if we have initialized the Axis
			if (Axis.Origin.X == double.NegativeInfinity)
			{
				// make it something reasonable (just to the left of the aabb of the object)
				Axis.Origin = this.GetAxisAlignedBoundingBox().Center - new Vector3(-30, 0, 0);
			}

			var rebuildLock = this.RebuildLock();
			SourceContainer.Visible = true;

			await ApplicationController.Instance.Tasks.Execute(
				"Radial Array".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					this.DebugDepth("Rebuild");
					var aabb = this.GetAxisAlignedBoundingBox();

					// make sure our length is in the right axis
					for (int i = 0; i < 3; i++)
					{
						if (Axis.Normal[i] != 0 && Axis.Origin[i] == 0)
						{
							var newOrigin = Vector3.Zero;
							newOrigin[i] = Math.Max(aabb.Center[0] - Axis.Origin[0],
								Math.Max(aabb.Center[1] - Axis.Origin[1],
								aabb.Center[2] - Axis.Origin[2]));
						}
					}

					var sourceContainer = SourceContainer;
					this.Children.Modify(list =>
					{
						list.Clear();
						// add back in the sourceContainer
						list.Add(sourceContainer);
						// get the source item
						var sourceItem = sourceContainer.Children.First();

						var offset = Vector3.Zero;
						for (int i = 0; i < Math.Max(Count, 1); i++)
						{
							var next = sourceItem.Clone();

							var normal = Axis.Normal.GetNormal();
							var angleRadians = MathHelper.DegreesToRadians(Angle) / Count * i;
							next.Rotate(Axis.Origin, normal, angleRadians);

							if (!RotatePart)
							{
								var nextAabb = next.GetAxisAlignedBoundingBox();
								next.Rotate(nextAabb.Center, normal, -angleRadians);
							}

							list.Add(next);
						}
					});
					SourceContainer.Visible = false;
					rebuildLock.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					return Task.CompletedTask;
				});
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			layer.World.RenderDirectionAxis(Axis, this.WorldMatrix(), 30);
		}
	}
}