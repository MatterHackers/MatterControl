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
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    public class RingObject3D : PrimitiveObject3D, IPropertyGridModifier, IObject3DControlsProvider
	{
		public enum RoundDirection
		{
			Inner,
			Outter,
		}

		public RingObject3D()
		{
			Name = "Ring".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Ring"];
		}

		public override string ThumbnailName => "Ring";
	
		public RingObject3D(double outerDiameter, double innerDiameter, double height, int sides)
			: this()
		{
			this.OuterDiameter = outerDiameter;
			this.InnerDiameter = innerDiameter;
			this.Height = height;
			this.Sides = sides;

			Rebuild();
		}

		public static async Task<RingObject3D> Create()
		{
			var item = new RingObject3D();

			await item.Rebuild();
			return item;
		}

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression OuterDiameter { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression InnerDiameter { get; set; } = 15;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Height { get; set; } = 5;

		[Slider(3, 360, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression Sides { get; set; } = 40;

		public bool Advanced { get; set; } = false;

		[JsonIgnore]
		[ReadOnly(true)]
		[DisplayName("")] // clear the display name so this text will be the full width of the editor
		public string EasyModeMessage { get; set; } = "You can switch to Advanced mode to get more ring options.";

		[MaxDecimalPlaces(2)]
		[Slider(0, 359, snapDistance: 1)]
		public DoubleOrExpression StartingAngle { get; set; } = 0;

		[MaxDecimalPlaces(2)]
		[Slider(1, 360, snapDistance: 1)]
		public DoubleOrExpression EndingAngle { get; set; } = 360;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public RoundTypes Round { get; set; } = RoundTypes.None;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public RoundDirection Direction { get; set; } = RoundDirection.Outter;

		[Slider(2, 90, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression RoundSegments { get; set; } = 15;

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
				var outerDiameter = OuterDiameter.Value(this);
				var innerDiameter = InnerDiameter.ClampIfNotCalculated(this, 0, outerDiameter - .1, ref valuesChanged);
				var sides = Sides.ClampIfNotCalculated(this, 3, 360, ref valuesChanged);
				var startingAngle = StartingAngle.ClampIfNotCalculated(this, 0, 360 - .01, ref valuesChanged);
				var endingAngle = EndingAngle.ClampIfNotCalculated(this, startingAngle + .01, 360, ref valuesChanged);
				var height = Height.Value(this);
				var roundSegments = RoundSegments.ClampIfNotCalculated(this, 2, 90, ref valuesChanged);

				using (new CenterAndHeightMaintainer(this, MaintainFlags.Origin | MaintainFlags.Bottom))
				{
					if (!Advanced)
					{
						startingAngle = 0;
						endingAngle = 360;
					}

					innerDiameter = Math.Min(outerDiameter - .1, innerDiameter);

					var path = new VertexStorage();
					var width = (outerDiameter - innerDiameter) / 2;
					var r = innerDiameter / 2;
					path.MoveTo(r, 0);
					path.LineTo(r + width, 0);
					var range = 360 / 4.0;

					if (!Advanced)
					{
						path.LineTo(r + width, height);
						path.LineTo(r, height);
					}
					else
					{
						switch (Round)
						{
							case RoundTypes.None:
								path.LineTo(r + width, height);
								path.LineTo(r, height);
								break;

							case RoundTypes.Down:
								if (Direction == RoundDirection.Inner)
								{
									path.LineTo(r + width, height);
									for (int i = 1; i < roundSegments - 1; i++)
									{
										var angle = range / (roundSegments - 1) * i;
										var rad = MathHelper.DegreesToRadians(angle);
										path.LineTo(r + Math.Cos(rad) * width, height - Math.Sin(rad) * height);
									}
								}
								else
								{
									for (int i = 1; i < roundSegments - 1; i++)
									{
										var angle = range / (roundSegments - 1) * i;
										var rad = MathHelper.DegreesToRadians(angle);
										path.LineTo(r + width - Math.Sin(rad) * width, height - Math.Cos(rad) * height);
									}
									path.LineTo(r, height);
								}
								break;

							case RoundTypes.Up:
								if (Direction == RoundDirection.Inner)
								{
									path.LineTo(r + width, height);
									for (int i = 1; i < roundSegments - 1; i++)
									{
										var angle = range / (roundSegments - 1) * i;
										var rad = MathHelper.DegreesToRadians(angle);
										path.LineTo(r + width - Math.Sin(rad) * width, Math.Cos(rad) * height);
									}
								}
								else
								{
									for (int i = 1; i < roundSegments - 1; i++)
									{
										var angle = range / (roundSegments - 1) * i;
										var rad = MathHelper.DegreesToRadians(angle);
										path.LineTo(r + Math.Cos(rad) * width, Math.Sin(rad) * height);
									}
									path.LineTo(r, height);
								}
								break;
						}
					}

					var startAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(startingAngle));
					var endAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(endingAngle));
					Mesh = VertexSourceToMesh.Revolve(path, sides, startAngle, endAngle);
				}
			}

			Invalidate(InvalidateType.DisplayValues);

			this.DoRebuildComplete();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(Round), () => Advanced);
			change.SetRowVisible(nameof(RoundSegments), () => Advanced || Round != RoundTypes.None);
			change.SetRowVisible(nameof(StartingAngle), () => Advanced);
			change.SetRowVisible(nameof(EndingAngle), () => Advanced);
			change.SetRowVisible(nameof(EasyModeMessage), () => !Advanced);
			change.SetRowVisible(nameof(RoundDirection), () => Advanced && Round != RoundTypes.None);
			change.SetRowVisible(nameof(RoundSegments), () => Advanced && Round != RoundTypes.None);
			change.SetRowVisible(nameof(Direction), () => Advanced && Round != RoundTypes.None);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			double getHeight() => Height.Value(this);
			void setHeight(double height) => Height = height;
			var getDiameters = new List<Func<double>>() { () => OuterDiameter.Value(this), () => InnerDiameter.Value(this) };
			var setDiameters = new List<Action<double>>() { (diameter) => OuterDiameter = diameter, (diameter) => InnerDiameter = diameter };
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				getHeight,
				setHeight,
				getDiameters,
				setDiameters,
				0));
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				getHeight,
				setHeight,
				getDiameters,
				setDiameters,
				1,
				angleOffset: -MathHelper.Tau / 32));
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