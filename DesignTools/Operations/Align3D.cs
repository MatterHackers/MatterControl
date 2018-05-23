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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Aabb = AxisAlignedBoundingBox;

	[JsonConverter(typeof(StringEnumConverter))]
	public enum Align { None, Min, Center, Max, Origin }

	[JsonConverter(typeof(StringEnumConverter))]
	public enum Alignment { X, Y, Z, negX, negY, negZ };

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum Edge
	{
		LeftFront = Face.Left | Face.Front,
		LeftBack = Face.Left | Face.Back,
		LeftBottom = Face.Left | Face.Bottom,
		LeftTop = Face.Left | Face.Top,
		RightFront = Face.Right | Face.Front,
		RightBack = Face.Right | Face.Back,
		RightBottom = Face.Right | Face.Bottom,
		RightTop = Face.Right | Face.Top,
		FrontBottom = Face.Front | Face.Bottom,
		FrontTop = Face.Front | Face.Top,
		BackBottom = Face.Back | Face.Bottom,
		BackTop = Face.Back | Face.Top
	}

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum Face
	{
		Left = 0x01,
		Right = 0x02,
		Front = 0x04,
		Back = 0x08,
		Bottom = 0x10,
		Top = 0x20,
	};

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum Side2D
	{
		Left = 0x01,
		Right = 0x02,
		Bottom = 0x10,
		Top = 0x20,
	};

	public class Align2D : VertexSourceApplyTransform
	{
		public Align2D()
		{
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, IVertexSource objectToAlignTo, Side2D boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector2(offsetX, offsetY)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object to itself.");
			}
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, double positionToAlignToX = 0, double positionToAlignToY = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector2(positionToAlignToX, positionToAlignToY), name)
		{
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, Vector2 positionToAlignTo, double offsetX, double offsetY, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector2(offsetX, offsetY), name)
		{
		}

		public Align2D(IVertexSource item, Side2D boundingFacesToAlign, Vector2 positionToAlignTo, string name = "")
		{
			var bounds = item.GetBounds();

			if (IsSet(boundingFacesToAlign, Side2D.Left, Side2D.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.Left;
			}
			if (IsSet(boundingFacesToAlign, Side2D.Right, Side2D.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.Left - (bounds.Right - bounds.Left);
			}
			if (IsSet(boundingFacesToAlign, Side2D.Bottom, Side2D.Top))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.Bottom;
			}
			if (IsSet(boundingFacesToAlign, Side2D.Top, Side2D.Bottom))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.Bottom - (bounds.Top - bounds.Bottom);
			}

			Transform = Affine.NewTranslation(positionToAlignTo);
			VertexSource = item;
		}

		public static Vector2 GetPositionToAlignTo(IVertexSource objectToAlignTo, Side2D boundingFacesToAlignTo, Vector2 extraOffset)
		{
			Vector2 positionToAlignTo = new Vector2();
			if (IsSet(boundingFacesToAlignTo, Side2D.Left, Side2D.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetBounds().Left;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Right, Side2D.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetBounds().Right;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Bottom, Side2D.Top))
			{
				positionToAlignTo.Y = objectToAlignTo.GetBounds().Bottom;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Top, Side2D.Bottom))
			{
				positionToAlignTo.Y = objectToAlignTo.GetBounds().Top;
			}

			return positionToAlignTo + extraOffset;
		}

		private static bool IsSet(Side2D variableToCheck, Side2D faceToCheckFor, Side2D faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}
	}

	[HideUpdateButtonAttribute]
	public class Align3D : Object3D, IPublicPropertyObject, IPropertyGridModifier
	{
		// We need to serialize this so we can remove the arrange and get back to the objects before arranging
		public List<Aabb> OriginalChildrenBounds = new List<Aabb>();

		public Align3D()
		{
			Name = "Align";
		}

		public Align3D(IObject3D objectToAlign, Face boundingFacesToAlign, IObject3D objectToAlignTo, Face boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object to itself.");
			}
		}

		public Align3D(IObject3D objectToAlign, Face boundingFacesToAlign, double positionToAlignToX = 0, double positionToAlignToY = 0, double positionToAlignToZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector3(positionToAlignToX, positionToAlignToY, positionToAlignToZ), name)
		{
		}

		public Align3D(IObject3D objectToAlign, Face boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align3D(IObject3D item, Face boundingFacesToAlign, Vector3 positionToAlignTo, string name = "")
		{
			AxisAlignedBoundingBox bounds = item.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, Face.Left, Face.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X;
			}
			if (IsSet(boundingFacesToAlign, Face.Right, Face.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X - (bounds.maxXYZ.X - bounds.minXYZ.X);
			}
			if (IsSet(boundingFacesToAlign, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlign, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y - (bounds.maxXYZ.Y - bounds.minXYZ.Y);
			}
			if (IsSet(boundingFacesToAlign, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlign, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z - (bounds.maxXYZ.Z - bounds.minXYZ.Z);
			}

			Matrix *= Matrix4X4.CreateTranslation(positionToAlignTo);
			Children.Add(item.Clone());
		}

		public bool Advanced { get; set; } = false;

		[DisplayName("X")]
		[Icons(new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png", "align_origin.png" })]
		public Align XAlign { get; set; } = Align.None;

		[DisplayName("Start X")]
		[Icons(new string[] { "424.png", "align_to_left.png", "align_to_center_x.png", "align_to_right.png", "" })]
		public Align XAlignTo { get; set; } = Align.None;

		[DisplayName("Offset X")]
		public double XOffset { get; set; } = 0;

		[DisplayName("Y")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png", "align_origin.png" })]
		public Align YAlign { get; set; } = Align.None;

		[DisplayName("Start Y")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "" })]
		public Align YAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Y")]
		public double YOffset { get; set; } = 0;

		[DisplayName("Z")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png", "align_origin.png" })]
		public Align ZAlign { get; set; } = Align.None;

		[DisplayName("Start Z")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "" })]
		public Align ZAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Z")]
		public double ZOffset { get; set; } = 0;

		public override bool CanApply => true;

		public override bool CanRemove => true;

		private List<Aabb> CurrentChildrenBounds
		{
			get
			{
				List<Aabb> currentChildrenBounds = new List<Aabb>();
				this.Children.Modify(list =>
				{
					foreach (var child in list)
					{
						currentChildrenBounds.Add(child.GetAxisAlignedBoundingBox());
					}
				});

				return currentChildrenBounds;
			}
		}

		public static Vector3 GetPositionToAlignTo(IObject3D objectToAlignTo, Face boundingFacesToAlignTo, Vector3 extraOffset)
		{
			Vector3 positionToAlignTo = new Vector3();
			if (IsSet(boundingFacesToAlignTo, Face.Left, Face.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Right, Face.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Z;
			}
			return positionToAlignTo + extraOffset;
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix)
				&& invalidateType.Source != this
				&& !Rebuilding)
			{
				Rebuild(null);
			}
			base.OnInvalidate(invalidateType);
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			Rebuilding = true;
			var aabb = this.GetAxisAlignedBoundingBox();

			// TODO: check if the has code for the children
			if (OriginalChildrenBounds.Count == 0)
			{
				this.Children.Modify(list =>
				{
					foreach (var child in list)
					{
						OriginalChildrenBounds.Add(child.GetAxisAlignedBoundingBox());
					}
				});
			}

			var currentChildrenBounds = CurrentChildrenBounds;
			this.Children.Modify(list =>
			{
				if(list.Count == 0)
				{
					return;
				}
				var firstBounds = currentChildrenBounds[0];
				int i = 0;
				foreach (var child in list)
				{
					if (i > 0)
					{
						if (XAlign == Align.None)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								// make sure it is where it started
								AlignAxis(0, Align.Min, OriginalChildrenBounds[i].minXYZ.X, 0, child);
							}
						}
						else
						{
							if (XAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, this.Children.First().WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(-(childOrigin - firstOrigin).X + (Advanced ? XOffset : 0), 0, 0));
							}
							else
							{
								AlignAxis(0, XAlign, GetAlignToOffset(currentChildrenBounds, 0, (!Advanced || XAlignTo == Align.None) ? XAlign : XAlignTo), XOffset, child);
							}
						}
						if (YAlign == Align.None)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								AlignAxis(1, Align.Min, OriginalChildrenBounds[i].minXYZ.Y, 0, child);
							}
						}
						else
						{
							if (YAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, this.Children.First().WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(0, -(childOrigin - firstOrigin).Y + (Advanced ? YOffset : 0), 0));
							}
							else
							{
								AlignAxis(1, YAlign, GetAlignToOffset(currentChildrenBounds, 1, (!Advanced || YAlignTo == Align.None) ? YAlign : YAlignTo), YOffset, child);
							}
						}
						if (ZAlign == Align.None)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								AlignAxis(2, Align.Min, OriginalChildrenBounds[i].minXYZ.Z, 0, child);
							}
						}
						else
						{
							if (ZAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, this.Children.First().WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(0, 0, -(childOrigin - firstOrigin).Z + (Advanced ? ZOffset : 0) ));
							}
							else
							{
								AlignAxis(2, ZAlign, GetAlignToOffset(currentChildrenBounds, 2, (!Advanced || ZAlignTo == Align.None) ? ZAlign : ZAlignTo), ZOffset, child);
							}
						}
					}
					i++;
				}
			});

			Rebuilding = false;
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			// put everything back to where it was before the arrange started
			if (OriginalChildrenBounds.Count == Children.Count)
			{
				int i = 0;
				foreach (var child in Children)
				{
					// Where you are minus where you started to get back to where you started
					child.Translate(-(child.GetAxisAlignedBoundingBox().minXYZ - OriginalChildrenBounds[i].minXYZ));
					i++;
				}
			}

			base.Remove(undoBuffer);
		}

		public void UpdateControls(PPEContext context)
		{
			context.GetEditRow(nameof(XAlignTo)).Visible = Advanced && XAlign != Align.Origin;
			context.GetEditRow(nameof(XOffset)).Visible = Advanced;
			context.GetEditRow(nameof(YAlignTo)).Visible = Advanced && YAlign != Align.Origin;
			context.GetEditRow(nameof(YOffset)).Visible = Advanced;
			context.GetEditRow(nameof(ZAlignTo)).Visible = Advanced && ZAlign != Align.Origin;
			context.GetEditRow(nameof(ZOffset)).Visible = Advanced;
		}

		private static bool IsSet(Face variableToCheck, Face faceToCheckFor, Face faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}

		private void AlignAxis(int axis, Align align, double alignTo, double offset, IObject3D item)
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

		private double GetAlignToOffset(List<Aabb> currentChildrenBounds, int axis, Align alignTo)
		{
			switch (alignTo)
			{
				case Align.Min:
					return currentChildrenBounds[0].minXYZ[axis];

				case Align.Center:
					return currentChildrenBounds[0].Center[axis];

				case Align.Max:
					return currentChildrenBounds[0].maxXYZ[axis];

				default:
					throw new NotImplementedException();
			}
		}
	}
}