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

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Aabb = AxisAlignedBoundingBox;

	public class ArangeObject3D : Object3D, IRebuildable
	{
		[JsonIgnoreAttribute]
		private List<Aabb> childrenBounds = new List<Aabb>();

		public ArangeObject3D()
		{
		}

		public enum Align { None, Min, Center, Max }

		public override string ActiveEditor => "PublicPropertyEditor";
		// Attributes - [SameLineAsLast, Icons(new string[] {"LeftX", "CenterX", "RightX"})]
		public Align XAlign { get; set; } = Align.None;
		// Attributes - [EnableIfNot("XAlign", "None"), Icons(new string[] {"NoneX", "LeftX", "CenterX", "RightX"})]
		public Align XAlignTo { get; set; } = Align.None;
		// Attributes - [EnableIfNot("XAlign", "None")]
		public double OffsetX { get; set; } = 0;

		public Align YAlign { get; set; } = Align.None;
		public Align YAlignTo { get; set; } = Align.None;
		public double YOffset { get; set; } = 0;

		public Align ZAlign { get; set; } = Align.None;
		public Align ZAlignTo { get; set; } = Align.None;
		public double ZOffset { get; set; } = 0;


		public void Rebuild()
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			// TODO: check if the has code for the children
			if (childrenBounds.Count == 0)
			{
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
				var firstBounds = childrenBounds[0];
				int i = 0;
				foreach (var child in list)
				{
					if (i > 0)
					{
						if (XAlign == Align.None)
						{
							// make sure it is where it started
							AlignAxis(0, Align.Min, childrenBounds[i].minXYZ.X, 0, child);
						}
						else
						{
							AlignAxis(0, XAlign, GetAlignToOffset(0, XAlignTo == Align.None ? XAlign : XAlignTo), OffsetX, child);
						}
						if (YAlign == Align.None)
						{
							AlignAxis(1, Align.Min, childrenBounds[i].minXYZ.Y, 0, child);
						}
						else
						{
							AlignAxis(1, YAlign, GetAlignToOffset(1, YAlignTo == Align.None ? YAlign : YAlignTo), YOffset, child);
						}
						if (ZAlign == Align.None)
						{
							AlignAxis(2, Align.Min, childrenBounds[i].minXYZ.Z, 0, child);
						}
						else
						{
							AlignAxis(2, ZAlign, GetAlignToOffset(2, ZAlignTo == Align.None ? ZAlign : ZAlignTo), ZOffset, child);
						}
					}
					i++;
				}
			});
		}

		private void AlignAxis(int axis, Align align, double alignTo, double offset,
			IObject3D item)
		{
			var aabb = item.GetAxisAlignedBoundingBox();
			var translate = Vector3.Zero;

			switch (align)
			{
				case Align.Min:
					translate[axis] = alignTo - aabb.minXYZ[axis] + offset;
					break;

				case Align.Center:
					translate[axis] = alignTo - aabb.Center[axis] + offset;
					break;

				case Align.Max:
					translate[axis] = alignTo - aabb.maxXYZ[axis] + offset;
					break;
			}

			item.Translate(translate);
		}

		private double GetAlignToOffset(int axis, Align alignTo)
		{
			switch (alignTo)
			{
				case Align.Min:
					return childrenBounds[0].minXYZ[axis];

				case Align.Center:
					return childrenBounds[0].Center[axis];

				case Align.Max:
					return childrenBounds[0].maxXYZ[axis];

				default:
					throw new NotImplementedException();
			}
		}
	}

	public class ArrayAdvancedObject3D : Object3D, IRebuildable
	{
		public ArrayAdvancedObject3D()
		{
		}

		public override string ActiveEditor => "PublicPropertyEditor";
		public int Count { get; set; } = 3;
		public double Rotate { get; set; } = 0;
		public bool RotatePart { get; set; } = false;
		public double Scale { get; set; } = 1;
		public bool ScaleOffset { get; set; } = false;
		public double XOffset { get; set; } = 30;
		public double YOffset { get; set; } = 0;

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

	public class ArrayLinearObject3D : Object3D, IRebuildable
	{
		public ArrayLinearObject3D()
		{
		}

		public override string ActiveEditor => "PublicPropertyEditor";
		public int Count { get; set; } = 3;
		public DirectionVector Direction { get; set; } = new DirectionVector { Normal = new Vector3(1, 0, 0) };
		public double Distance { get; set; } = 30;

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

	public class DirectionAxis
	{
		public Vector3 Normal { get; set; }
		public Vector3 Origin { get; set; }
	}

	public class DirectionVector
	{
		public Vector3 Normal { get; set; }
	}
}