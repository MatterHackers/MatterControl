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

using System.ComponentModel;
using System.Linq;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ArrayRadialObject3D : Object3D, IRebuildable
	{
		public ArrayRadialObject3D()
		{
		}

		public override string ActiveEditor => "PublicPropertyEditor";
		public double Angle { get; set; } = 360;
		public DirectionAxis Axis { get; set; } = new DirectionAxis() { Origin = Vector3.NegativeInfinity, Normal = Vector3.UnitZ };
		public int Count { get; set; } = 3;

		[DisplayName("Keep Within Angle")]
		[Description("Keep the entire extents of the part within the angle described.")]
		public bool KeepInAngle { get; set; } = false;

		[DisplayName("Rotate Part")]
		[Description("Rotate the part to the same angle as the array.")]
		public bool RotatePart { get; set; } = true;

		public void Rebuild()
		{
			if (Axis.Origin.X == double.NegativeInfinity)
			{
				// make it something reasonable (just to the left of the aabb of the object)
				var aabb = this.GetAxisAlignedBoundingBox();
				Axis.Origin = new Vector3(aabb.minXYZ.X - aabb.XSize / 2, aabb.Center.Y, 0);
			}
			this.Children.Modify(list =>
			{
				IObject3D first = list.First();

				list.Clear();
				list.Add(first);
				var offset = Vector3.Zero;
				for (int i = 1; i < Count; i++)
				{
					var next = first.Clone();

					var normal = Axis.Normal.GetNormal();
					var angleRadians = MathHelper.DegreesToRadians(Angle) / Count * i;
					next.Rotate(Axis.Origin, normal, angleRadians);

					if (!RotatePart)
					{
						next.Rotate(next.GetAxisAlignedBoundingBox().Center, normal, -angleRadians);
					}

					list.Add(next);
				}
			});
			this.Invalidate();
		}
	}
}