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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Aabb = MatterHackers.VectorMath.AxisAlignedBoundingBox;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class AlignObject3D_2 : Object3D, IPropertyGridModifier
	{
		// We need to serialize this so we can remove the arrange and get back to the objects before arranging
		public Dictionary<string, Vector3> StartingMin = new Dictionary<string, Vector3>();

		public AlignObject3D_2()
		{
			Name = "Align";
		}

		public AlignObject3D_2(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, IObject3D objectToAlignTo, FaceAlign boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0)
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)))
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object to itself.");
			}
		}

		public AlignObject3D_2(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, double positionToAlignToX = 0, double positionToAlignToY = 0, double positionToAlignToZ = 0)
			: this(objectToAlign, boundingFacesToAlign, new Vector3(positionToAlignToX, positionToAlignToY, positionToAlignToZ))
		{
		}

		public AlignObject3D_2(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ)
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ))
		{
		}

		public AlignObject3D_2(IObject3D item, FaceAlign boundingFacesToAlign, Vector3 positionToAlignTo)
		{
			AxisAlignedBoundingBox bounds = item.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, FaceAlign.Left, FaceAlign.Right))
			{
				positionToAlignTo.X -= bounds.MinXYZ.X;
			}

			if (IsSet(boundingFacesToAlign, FaceAlign.Right, FaceAlign.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.MinXYZ.X - (bounds.MaxXYZ.X - bounds.MinXYZ.X);
			}

			if (IsSet(boundingFacesToAlign, FaceAlign.Front, FaceAlign.Back))
			{
				positionToAlignTo.Y -= bounds.MinXYZ.Y;
			}

			if (IsSet(boundingFacesToAlign, FaceAlign.Back, FaceAlign.Front))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.MinXYZ.Y - (bounds.MaxXYZ.Y - bounds.MinXYZ.Y);
			}

			if (IsSet(boundingFacesToAlign, FaceAlign.Bottom, FaceAlign.Top))
			{
				positionToAlignTo.Z -= bounds.MinXYZ.Z;
			}

			if (IsSet(boundingFacesToAlign, FaceAlign.Top, FaceAlign.Bottom))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.MinXYZ.Z - (bounds.MaxXYZ.Z - bounds.MinXYZ.Z);
			}

			Matrix *= Matrix4X4.CreateTranslation(positionToAlignTo);
			Children.Add(item.Clone());
		}

		[ShowAsList]
		[DisplayName("Anchor")]
		public SelectedChildren SelectedChild { get; set; } = new SelectedChildren();

		[SectionStart("X Axis"), DisplayName("Align")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png", "align_origin.png" }, InvertIcons = true)]
		public Align XAlign { get; set; } = Align.None;

		public bool XOptions { get; set; } = false;

		[DisplayName("SubAlign")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_to_left.png", "align_to_center_x.png", "align_to_right.png", "align_origin.png" }, InvertIcons = true)]
		public Align XSubAlign { get; set; } = Align.None;

		[DisplayName("Offset")]
		[Slider(-20, 20, useSnappingGrid: true)]
		public DoubleOrExpression XOffset { get; set; } = 0;

		[SectionStart("Y Axis"), DisplayName("Align")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_Top.png", "align_origin.png" }, InvertIcons = true)]
		public Align YAlign { get; set; } = Align.None;

		public bool YOptions { get; set; } = false;

		[DisplayName("SubAlign")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "align_origin.png" }, InvertIcons = true)]
		public Align YSubAlign { get; set; } = Align.None;

		[DisplayName("Offset")]
		[Slider(-20, 20, useSnappingGrid: true)]
		public DoubleOrExpression YOffset { get; set; } = 0;

		[SectionStart("Z Axis"), DisplayName("Align")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_Top.png", "align_origin.png" }, InvertIcons = true)]
		public Align ZAlign { get; set; } = Align.None;

		public bool ZOptions { get; set; } = false;

		[DisplayName("SubAlign")]
		[EnumDisplay(IconPaths = new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "align_origin.png" }, InvertIcons = true)]
		public Align ZSubAlign { get; set; } = Align.None;

		[DisplayName("Offset")]
		[Slider(-20, 20, useSnappingGrid: true)]
		public DoubleOrExpression ZOffset { get; set; } = 0;

		public override bool CanApply => true;

		[JsonIgnore]
		private Aabb AnchorBounds
		{
			get
			{
				var aabb = this.GetAxisAlignedBoundingBox();

				if (SelectedChild.Count > 0)
				{
					if (Children.Where(c => SelectedChild.FirstOrDefault() == c.ID).FirstOrDefault() == null)
					{
						// none of our children have the selected id so clear the list
						SelectedChild.Clear();
					}
				}

				if (SelectedChild.Count == 0)
				{
					SelectedChild.Add(this.Children.FirstOrDefault().ID);
				}

				var sourceChild = this.Children.Where(c => c.ID == SelectedChild.FirstOrDefault()).FirstOrDefault();

				if (sourceChild != null)
				{
					aabb = sourceChild.GetAxisAlignedBoundingBox();
				}

				return aabb;
			}
		}

		[JsonIgnore]
		private IObject3D SelectedObject
		{
			get
			{
				if (SelectedChild.Count > 0)
				{
					return this.Children.Where(c => c.ID == SelectedChild.FirstOrDefault()).FirstOrDefault();
				}

				return this.Children.FirstOrDefault();
			}
		}

		public static Vector3 GetPositionToAlignTo(IObject3D objectToAlignTo, FaceAlign boundingFacesToAlignTo, Vector3 extraOffset)
		{
			var positionToAlignTo = default(Vector3);
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Left, FaceAlign.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().MinXYZ.X;
			}

			if (IsSet(boundingFacesToAlignTo, FaceAlign.Right, FaceAlign.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().MaxXYZ.X;
			}

			if (IsSet(boundingFacesToAlignTo, FaceAlign.Front, FaceAlign.Back))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().MinXYZ.Y;
			}

			if (IsSet(boundingFacesToAlignTo, FaceAlign.Back, FaceAlign.Front))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().MaxXYZ.Y;
			}

			if (IsSet(boundingFacesToAlignTo, FaceAlign.Bottom, FaceAlign.Top))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().MinXYZ.Z;
			}

			if (IsSet(boundingFacesToAlignTo, FaceAlign.Top, FaceAlign.Bottom))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().MaxXYZ.Z;
			}

			return positionToAlignTo + extraOffset;
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Name)
				&& !NameOverriden)
			{
				Name = NameFromChildren();
				NameOverriden = false;
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				// and also always pass back the actual type
				base.OnInvalidate(invalidateArgs);
			}
		}

		public void ResetPositions()
		{
			// move all the children that we know of back to where they started
			foreach (var child in Children.Where(c => StartingMin.ContainsKey(c.ID)))
			{
				child.Translate(StartingMin[child.ID] - child.GetAxisAlignedBoundingBox().MinXYZ);
			}

			// Now that all the known children are back in their original positions, remember all positions (including previously unknown)
			StartingMin.Clear();
			foreach (var child in Children)
			{
				StartingMin.Add(child.ID, child.GetAxisAlignedBoundingBox().MinXYZ);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				ResetPositions();

				this.Children.Modify(list =>
				{
					if (list.Count == 0)
					{
						return;
					}

					var anchorBounds = AnchorBounds;
					var children = list.Where(c => c.GetType() != typeof(OperationSourceObject3D)
						&& c.ID != SelectedChild.FirstOrDefault());

					// align all the objects to the anchor
					foreach (var child in children)
					{
						AlignAxis(0,
							(XOptions && XSubAlign != Align.None) ? XSubAlign : XAlign,
							GetSubAlignOffset(anchorBounds, 0, XAlign),
							XOffset.Value(this),
							child);
						AlignAxis(1,
							(YOptions && YSubAlign != Align.None) ? YSubAlign : YAlign,
							GetSubAlignOffset(anchorBounds, 1, YAlign),
							YOffset.Value(this),
							child);
						AlignAxis(2,
							(ZOptions && ZSubAlign != Align.None) ? ZSubAlign : ZAlign,
							GetSubAlignOffset(anchorBounds, 2, ZAlign),
							ZOffset.Value(this),
							child);
					}
				});

				if (!NameOverriden)
				{
					Name = NameFromChildren();
					NameOverriden = false;
				}
			}

			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
			return Task.CompletedTask;
		}

		public override void Cancel(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// put everything back to where it was before the arrange started
				ResetPositions();

				base.Cancel(undoBuffer);
			}

			Invalidate(InvalidateType.Children);
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(XSubAlign), () => XOptions);
			change.SetRowVisible(nameof(XOffset), () => XOptions);
			change.SetRowVisible(nameof(YSubAlign), () => YOptions);
			change.SetRowVisible(nameof(YOffset), () => YOptions);
			change.SetRowVisible(nameof(ZSubAlign), () => ZOptions);
			change.SetRowVisible(nameof(ZOffset), () => ZOptions);
		}

		private static bool IsSet(FaceAlign variableToCheck, FaceAlign faceToCheckFor, FaceAlign faceToAssertNot)
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
					translate[axis] = alignTo - aabb.MinXYZ[axis] + offset;
					break;

				case Align.Center:
					translate[axis] = alignTo - aabb.Center[axis] + offset;
					break;

				case Align.Max:
					translate[axis] = alignTo - aabb.MaxXYZ[axis] + offset;
					break;

				case Align.Origin:
					// find the origin in world space of the item
					var itemOrigin = Vector3Ex.Transform(Vector3.Zero, item.WorldMatrix(this));
					translate[axis] = alignTo - itemOrigin[axis] + offset;
					break;
			}

			item.Translate(translate);
		}

		private double GetSubAlignOffset(Aabb anchorBounds, int axis, Align alignTo)
		{
			switch (alignTo)
			{
				case Align.None:
					return 0;

				case Align.Min:
					return anchorBounds.MinXYZ[axis];

				case Align.Center:
					return anchorBounds.Center[axis];

				case Align.Max:
					return anchorBounds.MaxXYZ[axis];

				case Align.Origin:
					return Vector3Ex.Transform(Vector3.Zero, SelectedObject.WorldMatrix(this))[axis];

				default:
					throw new NotImplementedException();
			}
		}

		public string NameFromChildren()
		{
			return CalculateName(this.Children, ", ");
		}
	}
}