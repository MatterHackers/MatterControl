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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    public class SphereObject3D : PrimitiveObject3D, IPropertyGridModifier, IObject3DControlsProvider
	{
		private int lastSides;
		private int lastLatitudeSides;
		private double lastStartingAngle;
		private double lastEndingAngle;
		private double lastDiameter;

		public SphereObject3D()
		{
			Name = "Sphere".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Sphere"];
		}

		public override string ThumbnailName => "Sphere";

		public SphereObject3D(double diameter, int sides)
			: this()
		{
			Diameter = diameter;
			Sides = sides;

			Rebuild();
		}

		public static async Task<SphereObject3D> Create()
		{
			var item = new SphereObject3D();

			await item.Rebuild();
			return item;
		}

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Diameter { get; set; } = 20;

		[Slider(3, 360, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression Sides { get; set; } = 40;

		public bool Advanced { get; set; } = false;

        [JsonIgnore]
        [ReadOnly(true)]
		[DisplayName("")] // clear the display name so this text will be the full width of the editor
		public string EasyModeMessage { get; set; } = "You can switch to Advanced mode to get more sphere options.";

		[MaxDecimalPlaces(2)]
		[Slider(0, 359, snapDistance: 1)]
		public DoubleOrExpression StartingAngle { get; set; } = 0;

		[MaxDecimalPlaces(2)]
		[Slider(1, 360, snapDistance: 1)]
		public DoubleOrExpression EndingAngle { get; set; } = 360;

		[Slider(3, 180, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression LatitudeSides { get; set; } = 30;

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
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
			bool valuesChanged = false;
			using (RebuildLock())
			{
				var sides = Sides.ClampIfNotCalculated(this, 3, 360, ref valuesChanged);
				var latitudeSides = LatitudeSides.ClampIfNotCalculated(this, 3, 180, ref valuesChanged);
				var startingAngle = StartingAngle.ClampIfNotCalculated(this, 0, 360 - .01, ref valuesChanged);
				var endingAngle = EndingAngle.ClampIfNotCalculated(this, startingAngle + .01, 360, ref valuesChanged);
				var diameter = Diameter.Value(this);

				using (new CenterAndHeightMaintainer(this, MaintainFlags.Origin | MaintainFlags.Bottom))
				{
					if (sides != lastSides
						|| latitudeSides != lastLatitudeSides
						|| startingAngle != lastStartingAngle
						|| endingAngle != lastEndingAngle
						|| diameter != lastDiameter)
					{
						if (!Advanced)
						{
							startingAngle = 0;
							endingAngle = 360;
							latitudeSides = sides;
						}

						Mesh = CreateSphere(diameter, sides, latitudeSides, startingAngle, endingAngle);
					}

					lastDiameter = diameter;
					lastEndingAngle = endingAngle;
					lastStartingAngle = startingAngle;
					lastSides = sides;
					lastLatitudeSides = latitudeSides;
				}
			}

			Invalidate(InvalidateType.DisplayValues);

			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public static Mesh CreateSphere(double diameter = 1, int sides = 30, int latitudeSides = 30, double startingAngleDeg = 0, double endingAngleDeg = 360)
		{
			var path = new VertexStorage();
			var angleDelta = MathHelper.Tau / 2 / latitudeSides;
			var angle = -MathHelper.Tau / 4;
			var radius = diameter / 2;
			path.MoveTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
			for (int i = 0; i < latitudeSides; i++)
			{
				angle += angleDelta;
				path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
			}

			var startAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(startingAngleDeg));
			var endAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(endingAngleDeg));
			var steps = Math.Max(1, (int)(sides * MathHelper.Tau / Math.Abs(MathHelper.GetDeltaAngle(startAngle, endAngle)) + .5));
			return VertexSourceToMesh.Revolve(path,
				steps,
				startAngle,
				endAngle);
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(StartingAngle), () => Advanced);
			change.SetRowVisible(nameof(EndingAngle), () => Advanced);
			change.SetRowVisible(nameof(LatitudeSides), () => Advanced);
			change.SetRowVisible(nameof(EasyModeMessage), () => !Advanced);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				null,
				null,
				new List<Func<double>>() { () => Diameter.Value(this) },
				new List<Action<double>>() { (diameter) => Diameter = diameter },
				0,
				ObjectSpace.Placement.Center));
			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}
	}
}