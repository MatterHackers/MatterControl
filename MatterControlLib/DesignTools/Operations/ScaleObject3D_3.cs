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
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public enum LockProportions
	{
		[Description("Scale Freely")]
		None,
		[Description("Lock the Width & Depth together")]
		X_Y,
		[Description("Lock the Width, Depth & Height together")]
		X_Y_Z
	}

	public interface IScaleLocker
	{
		LockProportions LockProportion { get; }

		void ScaledProportionally();
	}

	public class ScaleObject3D_3 : TransformWrapperObject3D, IObject3DControlsProvider, IPropertyGridModifier, IScaleLocker
	{
		public enum ScaleTypes
		{
			Custom,
			Inches_to_mm,
			mm_to_Inches,
			mm_to_cm,
			cm_to_mm,
			Ultrafuse_316L,
		}

		public ScaleObject3D_3()
		{
			Name = "Scale".Localize();
		}

		public ScaleObject3D_3(IObject3D item, double x = 1, double y = 1, double z = 1)
			: this(item, new Vector3(x, y, z))
		{
		}

		public ScaleObject3D_3(IObject3D itemToScale, Vector3 scale)
			: this()
		{
			WrapItems(new IObject3D[] { itemToScale });

			ScaleRatio = scale;
			Rebuild();
		}

		public override void WrapSelectedItemAndSelect(InteractiveScene scene)
		{
			base.WrapSelectedItemAndSelect(scene);

			// use source item as it may be a copy of item by the time we have wrapped it
			var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
			var newCenter = new Vector3(aabb.Center.X, aabb.Center.Y, aabb.MinXYZ.Z);
			UntransformedChildren.Translate(-newCenter);
			this.Translate(newCenter);
		}

		public override void WrapItems(IEnumerable<IObject3D> items, UndoBuffer undoBuffer = null)
		{
			base.WrapItems(items, undoBuffer);

			// use source item as it may be a copy of item by the time we have wrapped it
			var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
			var newCenter = new Vector3(aabb.Center.X, aabb.Center.Y, aabb.MinXYZ.Z);
			UntransformedChildren.Translate(-newCenter);
			this.Translate(newCenter);
		}

		// this is the size we actually serialize
		public Vector3 ScaleRatio = Vector3.One;

		public ScaleTypes ScaleType { get; set; } = ScaleTypes.Custom;

		public enum ScaleMethods
		{
			Direct,
			Percentage,
		}

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Tabs)]
		public ScaleMethods ScaleMethod { get; set; } = ScaleMethods.Direct;


		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		[Description("Ensure that the part maintains its proportions.")]
		public LockProportions LockProportion { get; set; } = LockProportions.X_Y_Z;


		[MaxDecimalPlaces(3)]
		[JsonIgnore]
		public double Width
		{
			get
			{
				var children = UntransformedChildren;
				if (children == null)
				{
					return 0;
				}
				return ScaleRatio.X * UntransformedChildren.GetAxisAlignedBoundingBox().XSize;
			}

			set
			{
				var children = UntransformedChildren;
				if (children != null)
				{
					FixIfLockedProportions(0, value / UntransformedChildren.GetAxisAlignedBoundingBox().XSize);
				}
			}
		}

		[MaxDecimalPlaces(3)]
		[JsonIgnore]
		public double Depth
		{
			get
			{
				var children = UntransformedChildren;
				if (children == null)
				{
					return 0;
				}
				return ScaleRatio.Y * children.GetAxisAlignedBoundingBox().YSize;
			}

			set
			{
				var children = UntransformedChildren;
				if (children != null)
				{
					FixIfLockedProportions(1, value / children.GetAxisAlignedBoundingBox().YSize);
				}
			}
		}

		[MaxDecimalPlaces(3)]
		[JsonIgnore]
		public double Height
		{
			get
			{
				var children = UntransformedChildren;
				if (children == null)
				{
					return 0;
				}
				return ScaleRatio.Z * children.GetAxisAlignedBoundingBox().ZSize;
			}

			set
			{
				var children = UntransformedChildren;
				if (children != null)
				{
					FixIfLockedProportions(2, value / children.GetAxisAlignedBoundingBox().ZSize);
				}
			}
		}

		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		public double WidthPercent
		{
			get
			{
				return ScaleRatio.X * 100;
			}

			set
			{
				FixIfLockedProportions(0, value / 100);
			}
		}

		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		public double DepthPercent
		{
			get
			{
				return ScaleRatio.Y * 100;
			}

			set
			{
				FixIfLockedProportions(1, value / 100);
			}
		}

		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		public double HeightPercent
		{
			get
			{
				return ScaleRatio.Z * 100;
			}

			set
			{
				FixIfLockedProportions(2, value / 100);
			}
		}

		[ReadOnly(true)]
		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		[DisplayName("Width Percent")]
		public double WidthPercentDisplay
		{
			get
			{
				return ScaleRatio.X * 100;
			}

			set
			{
				FixIfLockedProportions(0, value / 100);
			}
		}

		[ReadOnly(true)]
		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		[DisplayName("Depth Percent")]
		public double DepthPercentDisplay
		{
			get
			{
				return ScaleRatio.Y * 100;
			}

			set
			{
				FixIfLockedProportions(1, value / 100);
			}
		}

		[ReadOnly(true)]
		[MaxDecimalPlaces(2)]
		[JsonIgnore]
		[DisplayName("Height Percent")]
		public double HeightPercentDisplay
		{
			get
			{
				return ScaleRatio.Z * 100;
			}

			set
			{
				FixIfLockedProportions(2, value / 100);
			}
		}

		private void FixIfLockedProportions(int index, double newScale)
		{
			if (Math.Abs(newScale - ScaleRatio[index]) > .001)
			{
				ScaleRatio[index] = newScale;
				if (ScaleType != ScaleTypes.Custom)
				{
					// WIP: switch back to custom scaling (as we are no longer on a fixed scaling)
					// needs to:
					//   - create an undo point for the switch
					//   - update the properties control to show the right drop down
					//   - show all the settings
				}

				if (LockProportion == LockProportions.X_Y_Z)
				{
					ScaleRatio[(index + 1) % 3] = ScaleRatio[index];
					ScaleRatio[(index + 2) % 3] = ScaleRatio[index];
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
				else if (LockProportion == LockProportions.X_Y)
				{
					if (index == 0)
					{
						ScaleRatio[1] = ScaleRatio[index];
						Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
					}
					else if(index == 1)
					{
						ScaleRatio[0] = ScaleRatio[index];
						Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
					}
				}

				Rebuild();
			}
		}

		public async override void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateArgs.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					// set the matrix for the transform object
					ItemWithTransform.Matrix = Matrix4X4.Identity;
					ItemWithTransform.Matrix *= Matrix4X4.CreateScale(ScaleRatio);
				}
			}


			Invalidate(InvalidateType.DisplayValues);
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));

			return Task.CompletedTask;
		}

		PublicPropertyChange change;

		public void UpdateControls(PublicPropertyChange change)
		{
			this.change = change;
			change.SetRowVisible(nameof(Width), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Direct);
			change.SetRowVisible(nameof(Depth), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Direct);
			change.SetRowVisible(nameof(Height), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Direct);
			change.SetRowVisible(nameof(WidthPercent), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Percentage);
			change.SetRowVisible(nameof(DepthPercent), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Percentage);
			change.SetRowVisible(nameof(HeightPercent), () => ScaleType == ScaleTypes.Custom && ScaleMethod == ScaleMethods.Percentage);
			change.SetRowVisible(nameof(LockProportion), () => ScaleType == ScaleTypes.Custom);
			change.SetRowVisible(nameof(ScaleMethod), () => ScaleType == ScaleTypes.Custom);

			change.SetRowVisible(nameof(WidthPercentDisplay), () => ScaleType != ScaleTypes.Custom);
			change.SetRowVisible(nameof(DepthPercentDisplay), () => ScaleType != ScaleTypes.Custom);
			change.SetRowVisible(nameof(HeightPercentDisplay), () => ScaleType != ScaleTypes.Custom);

			if (change.Changed == nameof(ScaleType))
			{
				// recalculate the scaling
				double scale = 1;
				switch (ScaleType)
				{
					case ScaleTypes.Inches_to_mm:
						scale = 25.4;
						break;
					case ScaleTypes.mm_to_Inches:
						scale = .0393;
						break;
					case ScaleTypes.mm_to_cm:
						scale = .1;
						break;
					case ScaleTypes.cm_to_mm:
						scale = 10;
						break;
					case ScaleTypes.Ultrafuse_316L:
						ScaleRatio = new Vector3(1.1982, 1.1982, 1.261);
						Rebuild();
						Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
						return;
				}

				ScaleRatio = new Vector3(scale, scale, scale);
				Rebuild();
				Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
			}
			else if (change.Changed == nameof(LockProportion))
			{
				if (LockProportion == LockProportions.X_Y_Z)
				{
					var maxScale = Math.Max(ScaleRatio.X, Math.Max(ScaleRatio.Y, ScaleRatio.Z));
					ScaleRatio = new Vector3(maxScale, maxScale, maxScale);
					Rebuild();
					// make sure we update the controls on screen to reflect the different data type
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
				else if (LockProportion == LockProportions.X_Y)
				{
					var maxScale = Math.Max(ScaleRatio.X, ScaleRatio.Y);
					ScaleRatio = new Vector3(maxScale, maxScale, ScaleRatio.Z);
					Rebuild();
					// make sure we update the controls on screen to reflect the different data type
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
			}
		}

		public void ScaledProportionally()
		{
			// this does not work yet
			// it needs to have an undo for the change to custom
			// it needs to not cause extra undos to exist
			return;

			if (ScaleType != ScaleTypes.Custom)
			{
				ScaleType = ScaleTypes.Custom;
				this.UpdateControls(new PublicPropertyChange(change.Context, "Rebuild_On_Scale"));
			}
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddHeightControl(this, Width, Depth, Height);
			object3DControlsLayer.AddWidthDepthControls(this, Width, Depth, Height);

			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}
	}
}