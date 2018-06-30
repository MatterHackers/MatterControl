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
		[DisplayName("X")]
		[Description("Rotate about the X axis")]
		public double RotationXDegrees { get; set; }
		[DisplayName("Y")]
		[Description("Rotate about the Y axis")]
		public double RotationYDegrees { get; set; }
		[DisplayName("Z")]
		[Description("Rotate about the Z axis")]
		public double RotationZDegrees { get; set; }

		// this needs to serialize
		Matrix4X4 appliedRotation = Matrix4X4.Identity;

		public RotateObject3D()
		{
			Name = "Rotate".Localize();
		}

		public RotateObject3D(IObject3D item, double xRadians = 0, double yRadians = 0, double zRadians = 0, string name = "")
		{
			RotationXDegrees = MathHelper.RadiansToDegrees(xRadians);
			RotationYDegrees = MathHelper.RadiansToDegrees(yRadians);
			RotationZDegrees = MathHelper.RadiansToDegrees(zRadians);
			Children.Add(item.Clone());

			Rebuild(null);
		}

		public RotateObject3D(IObject3D item, Vector3 translation, string name = "")
			: this(item, translation.X, translation.Y, translation.Z, name)
		{
		}

		public override Matrix4X4 Matrix
		{
			get => base.Matrix;
			set
			{
				var final = value;
				using (RebuildLock())
				{
					var a = Matrix;
					var c = value;
					// assuming ab = c and we have a and are recieving a new c, what was b
					// (the matrix that is being applied)? 
					// b = (a^-1)c.
					var b = a.Inverted * c;
					// new we can re-apply the transform being attempted and the rotation after
					final = (appliedRotation.Inverted * a) * b * RotationMatrix;
				}

				base.Matrix = final;
			}
		}

		public Matrix4X4 RotationMatrix
		{
			get
			{
				var a = Matrix4X4.CreateRotation(new Vector3(
					MathHelper.DegreesToRadians(RotationXDegrees),
					MathHelper.DegreesToRadians(RotationYDegrees),
					MathHelper.DegreesToRadians(RotationZDegrees)));

				var b = Matrix4X4.CreateRotationX(MathHelper.DegreesToRadians(RotationXDegrees))
					* Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(RotationYDegrees))
					* Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(RotationZDegrees));

				return b;
			}
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				var startingAabb = this.GetAxisAlignedBoundingBox();

				var rotationMatrix = RotationMatrix;
				// remove whatever rotation has been applied (they go in reverse order)
				base.Matrix = appliedRotation.Inverted * Matrix;

				// add the current rotation
				base.Matrix = this.ApplyAtPosition(startingAabb.Center, rotationMatrix);

				appliedRotation = rotationMatrix;

				if (startingAabb.ZSize > 0)
				{
					// If the part was already created and at a height, maintain the height.
					//PlatingHelper.PlaceMeshAtHeight(this, startingAabb.minXYZ.Z);
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