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
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System.ComponentModel;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class RotateObject3D : Object3D
	{
		// we do want this to persist
		Vector3 lastRotation = Vector3.Zero;

		[DisplayName("X")]
		[Description("Rotate about the X axis")]
		public double RotationX { get; set; }
		[DisplayName("Y")]
		[Description("Rotate about the Y axis")]
		public double RotationY { get; set; }
		[DisplayName("Z")]
		[Description("Rotate about the Z axis")]
		public double RotationZ { get; set; }

		public RotateObject3D()
		{
			Name = "Rotate".Localize();
		}

		public RotateObject3D(IObject3D item, double x = 0, double y = 0, double z = 0, string name = "")
		{
			RotationX = x;
			RotationY = y;
			RotationZ = z;
			Children.Add(item.Clone());
		}

		public RotateObject3D(IObject3D item, Vector3 translation, string name = "")
			: this(item, translation.X, translation.Y, translation.Z, name)
		{
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				var startingAabb = this.GetAxisAlignedBoundingBox();

				// remove whatever rotation has been applied (they go in reverse order)
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(-lastRotation.Z)));
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(-lastRotation.Y)));
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationX(MathHelper.DegreesToRadians(-lastRotation.X)));

				// add the current rotation
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationX(MathHelper.DegreesToRadians(RotationX)));
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(RotationY)));
				Matrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(RotationZ)));

				lastRotation = new Vector3(RotationX, RotationY, RotationZ);

				if (startingAabb.ZSize > 0)
				{
					// If the part was already created and at a height, maintain the height.
					PlatingHelper.PlaceMeshAtHeight(this, startingAabb.minXYZ.Z);
				}
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Matrix, null));
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
	}
}