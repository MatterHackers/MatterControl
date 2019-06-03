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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ScaleObject3D : TransformWrapperObject3D, IEditorDraw, IPropertyGridModifier
	{
		public enum ScaleType { Specify, Inches_to_mm, mm_to_Inches, mm_to_cm, cm_to_mm };
		public ScaleObject3D()
		{
			Name = "Scale".Localize();
		}

		public ScaleObject3D(IObject3D item, double x = 1, double y = 1, double z = 1)
			: this(item, new Vector3(x, y, z))
		{
		}

		public ScaleObject3D(IObject3D itemToScale, Vector3 scale)
			: this()
		{
			WrapItems(new IObject3D[] { itemToScale });

			ScaleRatio = scale;
			Rebuild();
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

		#region // editable properties
		public ScaleType Operation { get; set; } = ScaleType.Specify;

		[JsonIgnore]
		[DisplayName("Width")]
		public double SizeX
		{
			get
			{
				if(UsePercentage)
				{
					return ScaleRatio.X * 100;
				}

				return ScaleRatio.X * UntransformedChildren.GetAxisAlignedBoundingBox().XSize;
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.X = value / 100;
				}
				else
				{
					ScaleRatio.X = value / UntransformedChildren.GetAxisAlignedBoundingBox().XSize;
				}
			}
		}

		[JsonIgnore]
		[DisplayName("Depth")]
		public double SizeY
		{
			get
			{
				if (UsePercentage)
				{
					return ScaleRatio.Y * 100;
				}

				return ScaleRatio.Y * UntransformedChildren.GetAxisAlignedBoundingBox().YSize;
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.Y = value / 100;
				}
				else
				{
					ScaleRatio.Y = value / UntransformedChildren.GetAxisAlignedBoundingBox().YSize;
				}
			}
		}

		[JsonIgnore]
		[DisplayName("Height")]
		public double SizeZ
		{
			get
			{
				if (UsePercentage)
				{
					return ScaleRatio.Z * 100;
				}

				return ScaleRatio.Z * UntransformedChildren.GetAxisAlignedBoundingBox().ZSize;
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.Z = value / 100;
				}
				else
				{
					ScaleRatio.Z = value / UntransformedChildren.GetAxisAlignedBoundingBox().ZSize;
				}
			}
		}

		[Description("Ensure that the part maintains its proportions.")]
		[DisplayName("Maintain Proportions")]
		public bool MaitainProportions { get; set; } = true;

		[Description("Toggle between specifying the size or the percentage to scale.")]
		public bool UsePercentage { get; set; }

		[Description("This is the position to perform the scale about.")]
		public Vector3 ScaleAbout { get; set; }

		#endregion // editable properties

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				layer.World.RenderAxis(ScaleAbout, this.WorldMatrix(), 30, 1);
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
				// set the matrix for the transform object
				ItemWithTransform.Matrix = Matrix4X4.Identity;
				ItemWithTransform.Matrix *= Matrix4X4.CreateTranslation(-ScaleAbout);
				ItemWithTransform.Matrix *= Matrix4X4.CreateScale(ScaleRatio);
				ItemWithTransform.Matrix *= Matrix4X4.CreateTranslation(ScaleAbout);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));

			return Task.CompletedTask;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(SizeX), () => Operation == ScaleType.Specify);
			change.SetRowVisible(nameof(SizeY), () => Operation == ScaleType.Specify);
			change.SetRowVisible(nameof(SizeZ), () => Operation == ScaleType.Specify);
			change.SetRowVisible(nameof(MaitainProportions), () => Operation == ScaleType.Specify);
			change.SetRowVisible(nameof(UsePercentage), () => Operation == ScaleType.Specify);
			change.SetRowVisible(nameof(ScaleAbout), () => Operation == ScaleType.Specify);

			if(change.Changed == nameof(Operation))
			{
				// recalculate the scaling
				double scale = 1;
				switch (Operation)
				{
					case ScaleType.Inches_to_mm:
						scale = 25.4;
						break;
					case ScaleType.mm_to_Inches:
						scale = .0393;
						break;
					case ScaleType.mm_to_cm:
						scale = .1;
						break;
					case ScaleType.cm_to_mm:
						scale = 10;
						break;
				}
				ScaleRatio = new Vector3(scale, scale, scale);
				Rebuild();
			}
			else if(change.Changed == nameof(UsePercentage))
			{
				// make sure we update the controls on screen to reflect the different data type
				Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
			}
			else if (change.Changed == nameof(MaitainProportions))
			{
				if (MaitainProportions)
				{
					var maxScale = Math.Max(ScaleRatio.X, Math.Max(ScaleRatio.Y, ScaleRatio.Z));
					ScaleRatio = new Vector3(maxScale, maxScale, maxScale);
					Rebuild();
					// make sure we update the controls on screen to reflect the different data type
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
			}
			else if (change.Changed == nameof(SizeX))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[1] = ScaleRatio[0];
					ScaleRatio[2] = ScaleRatio[0];
					Rebuild();
					// and invalidate the other properties
					Invalidate(new InvalidateArgs(this, InvalidateType.Properties));
					// then update the display values
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
			}
			else if (change.Changed == nameof(SizeY))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[0] = ScaleRatio[1];
					ScaleRatio[2] = ScaleRatio[1];
					Rebuild();
					// and invalidate the other properties
					Invalidate(new InvalidateArgs(this, InvalidateType.Properties));
					// then update the display values
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
			}
			else if (change.Changed == nameof(SizeZ))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[0] = ScaleRatio[2];
					ScaleRatio[1] = ScaleRatio[2];
					Rebuild();
					// and invalidate the other properties
					Invalidate(new InvalidateArgs(this, InvalidateType.Properties));
					// then update the display values
					Invalidate(new InvalidateArgs(null, InvalidateType.DisplayValues));
				}
			}
		}
	}
}