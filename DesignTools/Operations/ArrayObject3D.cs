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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Aabb = AxisAlignedBoundingBox;

	public class DirectionAxis
	{
		public Vector3 Origin { get; set; }
		public Vector3 Normal { get; set; }
	}

	public class DirectionVector
	{
		public Vector3 Normal { get; set; }
	}

	public class ArangeObject3D : Object3D, IRebuildable
	{
		[JsonIgnoreAttribute]
		Aabb startingBounds = Aabb.Empty;
		[JsonIgnoreAttribute]
		List<Aabb> childrenBounds = new List<Aabb>();

		public enum AlignTo { First, Last, All_Bounds }
		public enum Align { None, Min, Center, Max, Flow }

		public Align AlignmentX { get; set; } = Align.None;
		public AlignTo AlignToX { get; set; } = AlignTo.First;
		public double OffsetX { get; set; } = 0;

		public Align AlignmentY { get; set; } = Align.None;
		public AlignTo AlignToY { get; set; } = AlignTo.First;
		public double OffsetY { get; set; } = 0;

		public Align AlignmentZ { get; set; } = Align.None;
		public AlignTo AlignToZ { get; set; } = AlignTo.First;
		public double OffsetZ { get; set; } = 0;

		public override string ActiveEditor => "PublicPropertyEditor";

		public ArangeObject3D()
		{
		}

		public void Rebuild()
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			if (startingBounds == Aabb.Empty)
			{
				startingBounds = aabb;
				this.Children.Modify(list =>
				{
					foreach (var child in list)
					{
						childrenBounds.Add(child.GetAxisAlignedBoundingBox());
					}
				});
			}

			this.Children.Modify(list =>
			{
				int i = 0;
				foreach (var child in list)
				{
					var originalBounds = childrenBounds[i++];
					AlignAxis(0, AlignmentX, GetCorrectAabb(AlignToX), OffsetX, child, originalBounds);
					AlignAxis(1, AlignmentY, GetCorrectAabb(AlignToY), OffsetY, child, originalBounds);
					AlignAxis(2, AlignmentZ, GetCorrectAabb(AlignToZ), OffsetZ, child, originalBounds);
				}
			});
		}

		private Aabb GetCorrectAabb(AlignTo alignTo)
		{
			switch (alignTo)
			{
				case AlignTo.First:
					return childrenBounds.First();
				case AlignTo.Last:
					return childrenBounds.Last();
				default:
					return startingBounds;
			}
		}

		private void AlignAxis(int axis, Align align, Aabb bounds, double offset, 
			IObject3D item, Aabb originalBounds)
		{
			var aabb = item.GetAxisAlignedBoundingBox();
			var translate = Vector3.Zero;

			switch (align)
			{
				case Align.None:
					translate[axis] = originalBounds.minXYZ[axis] - aabb.minXYZ[axis];
					break;

				case Align.Min:
					translate[axis] = bounds.minXYZ[axis] - aabb.minXYZ[axis] + offset;
					break;

				case Align.Center:
					translate[axis] = bounds.Center[axis] - aabb.Center[axis] + offset;
					break;

				case Align.Max:
					translate[axis] = bounds.maxXYZ[axis] - aabb.maxXYZ[axis] + offset;
					break;

				case Align.Flow:
					break;
			}

			item.Translate(translate);
		}
	}

	public class ArrayLinearObject3D : Object3D, IRebuildable
	{
		public int Count { get; set; } = 3;
		public DirectionVector Direction { get; set; } = new DirectionVector { Normal = new Vector3(1, 0, 0) };
		public double Distance { get; set; } = 30;

		public override string ActiveEditor => "PublicPropertyEditor";

		public ArrayLinearObject3D()
		{
		}

		public void Rebuild()
		{
			this.Children.Modify(list =>
			{
				IObject3D lastChild = list.First();
				list.Clear();
				list.Add(lastChild);
				var offset = Vector3.Zero;
				for (int i = 1; i < Count; i++)
				{
					var next = lastChild.Clone();
					next.Matrix *= Matrix4X4.CreateTranslation(Direction.Normal.GetNormal() * Distance);
					list.Add(next);
					lastChild = next;
				}
			});
		}
	}

	public class ArrayRadialObject3D : Object3D, IRebuildable
	{
		public int Count { get; set; } = 3;

		public DirectionAxis Axis { get; set; } = new DirectionAxis() { Origin = Vector3.NegativeInfinity, Normal = Vector3.UnitZ };
		public double Angle { get; set; } = 360;

		[DisplayName("Keep Within Angle")]
		[Description("Keep the entire extents of the part within the angle described.")]
		public bool KeepInAngle { get; set; } = false;

		[DisplayName("Rotate Part")]
		[Description("Rotate the part to the same angle as the array.")]
		public bool RotatePart { get; set; } = true;

		public override string ActiveEditor => "PublicPropertyEditor";

		public ArrayRadialObject3D()
		{
		}

		public void Rebuild()
		{
			if(Axis.Origin.X == double.NegativeInfinity)
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

	public class ArrayAdvancedObject3D : Object3D, IRebuildable
	{
		public int Count { get; set; } = 3;
		public double XOffset { get; set; } = 30;
		public double YOffset { get; set; } = 0;
		public double Rotate { get; set; } = 0;
		public double Scale { get; set; } = 1;
		public bool RotatePart { get; set; } = false;
		public bool ScaleOffset { get; set; } = false;

		public override string ActiveEditor => "PublicPropertyEditor";

		public ArrayAdvancedObject3D()
		{
		}

		public void Rebuild()
		{
			this.Children.Modify(list =>
			{
				IObject3D lastChild = list.First();
				list.Clear();
				list.Add(lastChild);
				var offset = Vector3.Zero;
				for (int i = 1; i < Count; i++)
				{
					var rotateRadians = MathHelper.DegreesToRadians(Rotate);
					var nextOffset = new Vector2(XOffset, YOffset);
					if (ScaleOffset)
					{
						for (int j = 1; j < i; j++)
						{
							nextOffset *= Scale;
						}
					}

					nextOffset.Rotate(rotateRadians * i);
					var next = lastChild.Clone();
					next.Matrix *= Matrix4X4.CreateTranslation(nextOffset.X, nextOffset.Y, 0);

					if (RotatePart)
					{
						next.ApplyAtBoundsCenter(Matrix4X4.CreateRotationZ(rotateRadians));
					}

					next.ApplyAtBoundsCenter(Matrix4X4.CreateScale(Scale));
					list.Add(next);
					lastChild = next;
				}
			});
		}
	}
}