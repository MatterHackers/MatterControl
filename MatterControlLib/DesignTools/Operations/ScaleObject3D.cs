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
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;

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
			WrapItem(itemToScale);
		}

		public override void WrapItem(IObject3D item, UndoBuffer undoBuffer = null)
		{
			base.WrapItem(item, undoBuffer);

			var aabb = item.GetAxisAlignedBoundingBox();
			var newCenter = new Vector3(aabb.Center.X, aabb.Center.Y, aabb.MinXYZ.Z);
			item.Translate(-newCenter);
			this.Translate(newCenter);
		}

		public override bool CanFlatten => true;

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

				return ScaleRatio.X * SourceItem.XSize();
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.X = value / 100;
				}
				else
				{
					ScaleRatio.X = value / SourceItem.XSize();
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

				return ScaleRatio.Y * SourceItem.YSize();
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.Y = value / 100;
				}
				else
				{
					ScaleRatio.Y = value / SourceItem.YSize();
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

				return ScaleRatio.Z * SourceItem.ZSize();
			}

			set
			{
				if (UsePercentage)
				{
					ScaleRatio.Z = value / 100;
				}
				else
				{
					ScaleRatio.Z = value / SourceItem.ZSize();
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

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				layer.World.RenderAxis(ScaleAbout, this.WorldMatrix(), 30, 1);
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType == InvalidateType.Content
				|| invalidateArgs.InvalidateType == InvalidateType.Matrix
				|| invalidateArgs.InvalidateType == InvalidateType.Mesh)
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateArgs.InvalidateType == InvalidateType.Properties
				&& invalidateArgs.Source == this)
			{
				Rebuild(invalidateArgs.UndoBuffer);
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				// set the matrix for the transform object
				TransformItem.Matrix = Matrix4X4.Identity;
				TransformItem.Matrix *= Matrix4X4.CreateTranslation(-ScaleAbout);
				TransformItem.Matrix *= Matrix4X4.CreateScale(ScaleRatio);
				TransformItem.Matrix *= Matrix4X4.CreateTranslation(ScaleAbout);
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Matrix, null));
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			var editRow = change.Context.GetEditRow(nameof(SizeX));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;
			editRow = change.Context.GetEditRow(nameof(SizeY));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;
			editRow = change.Context.GetEditRow(nameof(SizeZ));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;

			editRow = change.Context.GetEditRow(nameof(MaitainProportions));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;
			editRow = change.Context.GetEditRow(nameof(UsePercentage));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;
			editRow = change.Context.GetEditRow(nameof(ScaleAbout));
			if (editRow != null) editRow.Visible = Operation == ScaleType.Specify;

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
				Rebuild(null);
			}
			else if(change.Changed == nameof(UsePercentage))
			{
				// make sure we update the controls on screen to reflect the different data type
				base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
			}
			else if (change.Changed == nameof(MaitainProportions))
			{
				if (MaitainProportions)
				{
					var maxScale = Math.Max(ScaleRatio.X, Math.Max(ScaleRatio.Y, ScaleRatio.Z));
					ScaleRatio = new Vector3(maxScale, maxScale, maxScale);
					Rebuild(null);
					base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
				}
			}
			else if (change.Changed == nameof(SizeX))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[1] = ScaleRatio[0];
					ScaleRatio[2] = ScaleRatio[0];
					Rebuild(null);
					base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
				}
			}
			else if (change.Changed == nameof(SizeY))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[0] = ScaleRatio[1];
					ScaleRatio[2] = ScaleRatio[1];
					Rebuild(null);
					base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
				}
			}
			else if (change.Changed == nameof(SizeZ))
			{
				if (MaitainProportions)
				{
					// scale y and z to match
					ScaleRatio[0] = ScaleRatio[2];
					ScaleRatio[1] = ScaleRatio[2];
					Rebuild(null);
					base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Properties));
				}
			}
		}
	}
}