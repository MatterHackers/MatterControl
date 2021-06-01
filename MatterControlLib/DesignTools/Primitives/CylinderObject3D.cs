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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CylinderObject3D : PrimitiveObject3D, IPropertyGridModifier, IObject3DControlsProvider
	{
		public CylinderObject3D()
		{
			Name = "Cylinder".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cylinder"];
		}

		public override string ThumbnailName => "Cylinder";

		public CylinderObject3D(double diameter, double height, int sides)
			: this()
		{
			Diameter = diameter;
			Height = height;
			Sides = sides;

			Rebuild();
		}

		public static async Task<CylinderObject3D> Create(double diameter, double height, int sides, Alignment alignment = Alignment.Z)
		{
			if (alignment == Alignment.Z)
			{
				return new CylinderObject3D(diameter, height, sides);
			}

			return await Create(diameter, diameter, height, sides, alignment);
		}

		public static async Task<CylinderObject3D> Create(double diameterBottom, double diameterTop, double height, int sides, Alignment alignment = Alignment.Z)
		{
			var item = new CylinderObject3D()
			{
				Advanced = true,
				Diameter = diameterBottom,
				DiameterTop = diameterTop,
				Height = height,
				Sides = sides,
			};

			await item.Rebuild();
			switch (alignment)
			{
				case Alignment.X:
					item.Matrix = Matrix4X4.CreateRotationY(MathHelper.Tau / 4);
					break;
				case Alignment.Y:
					item.Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 4);
					break;
				case Alignment.Z:
					// This is the natural case (how it was modeled)
					break;
				case Alignment.negX:
					item.Matrix = Matrix4X4.CreateRotationY(-MathHelper.Tau / 4);
					break;
				case Alignment.negY:
					item.Matrix = Matrix4X4.CreateRotationX(-MathHelper.Tau / 4);
					break;
				case Alignment.negZ:
					item.Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
					break;
			}

			return item;
		}

		public static async Task<CylinderObject3D> Create()
		{
			var item = new CylinderObject3D();

			await item.Rebuild();
			return item;
		}

		[MaxDecimalPlaces(2)]
		[Description("The width from one side to the opposite side.")]
		public DoubleOrExpression Diameter { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		public DoubleOrExpression Height { get; set; } = 20;

		[Description("The number of segments around the perimeter.")]
		public int Sides { get; set; } = 40;

		public bool Advanced { get; set; } = false;

		[ReadOnly(true)]
		[DisplayName("")] // clear the display name so this text will be the full width of the editor
		public string EasyModeMessage { get; set; } = "You can switch to Advanced mode to get more cylinder options.";

		[MaxDecimalPlaces(2)]
		public DoubleOrExpression StartingAngle { get; set; } = 0;

		[MaxDecimalPlaces(2)]
		public DoubleOrExpression EndingAngle { get; set; } = 360;

		[MaxDecimalPlaces(2)]
		public DoubleOrExpression DiameterTop { get; set; } = 20;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateType.Source == this)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.SheetUpdated))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

			var height = Height.Value(this);
			var diameter = Diameter.Value(this);
			var diameterTop = DiameterTop.Value(this);
			Sides = agg_basics.Clamp(Sides, 3, 360, ref valuesChanged);
			height = agg_basics.Clamp(height, .01, 1000000, ref valuesChanged);
			diameter = agg_basics.Clamp(diameter, .01, 1000000, ref valuesChanged);
			var startingAngle = agg_basics.Clamp(StartingAngle.Value(this), 0, 360 - .01, ref valuesChanged);
			var endingAngle = agg_basics.Clamp(EndingAngle.Value(this), StartingAngle.Value(this) + .01, 360, ref valuesChanged);

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					if (!Advanced)
					{
						var path = new VertexStorage();
						path.MoveTo(0, -height / 2);
						path.LineTo(diameter / 2, -height / 2);
						path.LineTo(diameter / 2, height / 2);
						path.LineTo(0, height / 2);

						Mesh = VertexSourceToMesh.Revolve(path, Sides);
					}
					else
					{
						var path = new VertexStorage();
						path.MoveTo(0, -height / 2);
						path.LineTo(diameter / 2, -height / 2);
						path.LineTo(diameterTop / 2, height / 2);
						path.LineTo(0, height / 2);

						Mesh = VertexSourceToMesh.Revolve(path, Sides, MathHelper.DegreesToRadians(startingAngle), MathHelper.DegreesToRadians(endingAngle));
					}
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			if (change.Context.GetEditRow(nameof(DiameterTop)) is GuiWidget diameterWidget)
			{
				diameterWidget.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(StartingAngle)) is GuiWidget startingAngleWidget)
			{
				startingAngleWidget.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(EndingAngle)) is GuiWidget endingAngleWidget)
			{
				endingAngleWidget.Visible = Advanced;
			}

			change.SetRowVisible(nameof(EasyModeMessage), () => !Advanced);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			double getHeight() => Height.Value(this);
			void setHeight(double height) => Height = height;
			var getDiameters = new List<Func<double>>() { () => Diameter.Value(this), () => DiameterTop.Value(this) };
			var setDiameters = new List<Action<double>>() { (diameter) => Diameter = diameter, (diameter) => DiameterTop = diameter };
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				getHeight,
				setHeight,
				getDiameters,
				setDiameters,
				0,
				controlVisible: () => true));
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				getHeight,
				setHeight,
				getDiameters,
				setDiameters,
				1,
				ObjectSpace.Placement.Top,
				controlVisible: () => Advanced));
			object3DControlsLayer.Object3DControls.Add(new ScaleHeightControl(object3DControlsLayer,
				null,
				null,
				null,
				null,
				getHeight,
				setHeight,
				getDiameters,
				setDiameters));
			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}
	}
}