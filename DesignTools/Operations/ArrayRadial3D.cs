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
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ArrayRadial3D : Object3D, IPublicPropertyObject
	{
		public ArrayRadial3D()
		{
			Name = "Radial Array".Localize();
		}

		[DisplayName("Rotate About")]
		public DirectionAxis Axis { get; set; } = new DirectionAxis() { Origin = Vector3.NegativeInfinity, Normal = Vector3.UnitZ };

		public override bool CanApply => true;
		public override bool CanRemove => true;

		public int Count { get; set; } = 3;

		[Description("Rotate the part to the same angle as the array.")]
		public bool RotatePart { get; set; } = true;

		// make this public when within angle works
		private double Angle { get; set; } = 360;

		// make this public when it works
		[DisplayName("Keep Within Angle")]
		[Description("Keep the entire extents of the part within the angle described.")]
		private bool KeepInAngle { get; set; } = false;

		public override void Apply(UndoBuffer undoBuffer)
		{
			OperationSource.Apply(this);

			base.Apply(undoBuffer);
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildSuspended)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			this.SuspendRebuild();
			this.DebugDepth("Rebuild");

			// check if we have initialized the Axis
			if (Axis.Origin.X == double.NegativeInfinity)
			{
				// make it something reasonable (just to the left of the aabb of the object)
				var aabb = this.GetAxisAlignedBoundingBox();
				Axis.Origin = aabb.Center - new Vector3(30, 0, 0);
			}

			var sourceContainer = OperationSource.GetOrCreateSourceContainer(this);
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
						var aabb = next.GetAxisAlignedBoundingBox();
						next.Rotate(aabb.Center, normal, -angleRadians);
					}

					list.Add(next);
				}
			});

			this.ResumeRebuild();

			this.Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			OperationSource.Remove(this);

			base.Remove(undoBuffer);
		}
	}
}