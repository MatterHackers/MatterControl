/*
Involute Spur Gear Builder (c) 2014 Dr. Rainer Hessmer
ported to C# 2019 by Lars Brubaker

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
using System.Linq;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools
{
	// Involute Spur Gear Builder
	// For calculating and drawing involute spur gears.
	// As an improvement over the majority of other freely available scripts and utilities it fully accounts for undercuts.
	// For additional information please head over to:
	// http://www.hessmer.org/blog/2014/01/01/online-involute-spur-gear-builder part 1
	// http://www.hessmer.org/blog/2015/07/13/online-involute-spur-gear-builder-part-2/ part 2
	// The implementation is inspired by the subtractive process that Michal Zalewski's describes in
	// http://lcamtuf.coredump.cx/gcnc/ch6/#6.2 part six of his excellent
	// http://lcamtuf.coredump.cx/gcnc/ Guerrilla guide to CNC machining, mold making, and resin casting
	public class Gear2D : VertexSourceLegacySupport
	{
		private double _backlash = .05;
		private double _centerHoleDiameter = 4;
		private double _circularPitch = 8;

		private double _clearance = .05;

		// Most common stock gears have a 20° pressure angle, with 14½° and 25° pressure angle gears being much less
		// common. Increasing the pressure angle increases the width of the base of the gear tooth, leading to greater strength and load carrying capacity. Decreasing
		// the pressure angle provides lower backlash, smoother operation and less sensitivity to manufacturing errors. (reference: http://en.wikipedia.org/wiki/Involute_gear)
		private double _pressureAngle = 20;

		private int _toothCount = 30;

		private double addendum;

		private double AngleToothToTooth => 360.0 / this.ToothCount;

		private Vector2 center = Vector2.Zero;

		private double diametralPitch;

		public double OuterRadius { get; private set; }

		private double pitchDiameter;

		private double pitchRadius;

		private double profileShift = 0;

		private double shiftedAddendum;

		private int stepsPerToothAngle = 10;

		public Gear2D()
		{
			CalculateDependants();
		}

		public enum GearTypes
		{
			External,
			Internal,
			Rack
		}

		private IntPoint ScalledPoint(double x, double y, double scale)
		{
			return new IntPoint(x * scale, y * scale);
		}

		private IntPoint ScalledPoint(Vector2 position, double scale)
		{
			return new IntPoint(position.X * scale, position.Y * scale);
		}

		private Polygon Rectangle(double left, double bottom, double right, double top, double scale)
		{
			var output = new Polygon(4);
			output.Add(ScalledPoint(left, bottom, scale));
			output.Add(ScalledPoint(right, bottom, scale));
			output.Add(ScalledPoint(right, top, scale));
			output.Add(ScalledPoint(left, top, scale));
			return output;
		}

		private Polygon Circle(double x, double y, double radius, double scale = 1, int steps = 100)
		{
			var output = new Polygon(100);
			for (int i = 0; i < steps; i++)
			{
				var angle = 2 * Math.PI * i / steps;
				output.Add(new IntPoint(Math.Cos(angle) * radius * scale, Math.Sin(angle) * radius * scale));
			}

			return output;
		}

		public double Backlash
		{
			get => _backlash;

			set
			{
				_backlash = value;
				CalculateDependants();
			}
		}

		public double CenterHoleDiameter
		{
			get => _centerHoleDiameter;

			set
			{
				_centerHoleDiameter = value;
				CalculateDependants();
			}
		}

		/// <summary>
		/// Gets or sets distance from one face of a tooth to the corresponding face of an adjacent tooth on the same gear, measured along the pitch circle.
		/// </summary>
		public double CircularPitch
		{
			get => _circularPitch;
			set
			{
				_circularPitch = value;
				CalculateDependants();
			}
		}

		public double Clearance
		{
			get => _clearance;

			set
			{
				_clearance = value;
				CalculateDependants();
			}
		}

		private GearTypes _gearType = GearTypes.External;

		public GearTypes GearType
		{
			get => _gearType;

			set
			{
				_gearType = value;
				CalculateDependants();
			}
		}

		public double PressureAngle
		{
			get => _pressureAngle;

			set
			{
				_pressureAngle = value;
				CalculateDependants();
			}
		}

		public int ToothCount
		{
			get => _toothCount;

			set
			{
				_toothCount = value;
				CalculateDependants();
			}
		}

		private int _internalToothCount;

		public int InternalToothCount
		{
			get => _internalToothCount;

			set
			{
				_internalToothCount = value;
				CalculateDependants();
			}
		}

		public bool Debug { get; set; } = false;

		private List<Polygons> debugData = new List<Polygons>();

		public override IEnumerable<VertexData> Vertices()
		{
			Polygons shape = null;

			switch (GearType)
			{
				case GearTypes.External:
					shape = CreateExternalGearShape();
					break;

				case GearTypes.Internal:
					shape = CreateInternalGearShape();
					break;

				case GearTypes.Rack:
					shape = CreateRackShape();
					break;
			}

			if (Debug && debugData.Count > 0)
			{
				var output = new Polygons();
				output.AddRange(debugData[0]);
				var top = debugData[0].GetBounds().Top;
				for (int i = 1; i < debugData.Count; i++)
				{
					var offset = top - debugData[i].GetBounds().Bottom + 2000;
					var offsetPolys = debugData[i].Translate(0, offset);
					output.AddRange(offsetPolys);
					top = offsetPolys.GetBounds().Top;
				}

				shape = output;
			}

			if (shape == null)
			{
				yield return new VertexData(ShapePath.FlagsAndCommand.MoveTo, 0, 0);
				yield return new VertexData(ShapePath.FlagsAndCommand.LineTo, 20, 0);
				yield return new VertexData(ShapePath.FlagsAndCommand.LineTo, 0, 20);
			}
			else
			{
				foreach (var poly in shape)
				{
					var command = ShapePath.FlagsAndCommand.MoveTo;
					foreach (var point in poly)
					{
						yield return new VertexData(command, point.X / 1000.0, point.Y / 1000.0);
						command = ShapePath.FlagsAndCommand.LineTo;
					}
				}
			}
		}

		private Polygon CreateInternalToothCutter(Gear2D pinion)
		{
			// To cut the internal gear teeth, the actual pinion comes close but we need to enlarge it so properly caters for clearance and backlash
			var enlargedPinion = new Gear2D()
			{
				CircularPitch = pinion.CircularPitch,
				PressureAngle = pinion.PressureAngle,
				Clearance = -pinion.Clearance,
				Backlash = -pinion.Backlash,
				ToothCount = pinion.ToothCount,
				CenterHoleDiameter = 0,
				profileShift = pinion.profileShift,
				stepsPerToothAngle = pinion.stepsPerToothAngle
			};

			enlargedPinion.CalculateDependants();

			var tooth = enlargedPinion.CreateSingleTooth();
			return tooth.tooth.Rotate(MathHelper.DegreesToRadians(90 + 180) / enlargedPinion.ToothCount); // we need a tooth pointing to the left
		}

		private Polygon CreateInternalToothProfile()
		{
			var radius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.Clearance;
			var toothToToothRadians = MathHelper.Tau / this.ToothCount;
			var sin = Math.Sin(toothToToothRadians);
			var cos = Math.Cos(toothToToothRadians);

			var fullSector = new Polygon();

			fullSector.Add(ScalledPoint(0, 0, 1000));
			fullSector.Add(ScalledPoint(-(radius * cos), radius * sin, 1000));
			fullSector.Add(ScalledPoint(-radius, 0, 1000));
			fullSector.Add(ScalledPoint(-(radius * cos), -radius * sin, 1000));

			var innerRadius = radius - (2 * this.addendum + this.Clearance);
			var innerCircle = Circle(this.center.X, center.Y, innerRadius, 1000);
			var sector = fullSector.Subtract(innerCircle);
			debugData.Add(sector);

			var pinion = CreateInternalPinion();
			var cutterTemplate = this.CreateInternalToothCutter(pinion);
			debugData.Add(new Polygons() { cutterTemplate });

			var stepsPerTooth = this.stepsPerToothAngle;
			var stepSizeRadians = toothToToothRadians / stepsPerTooth;

			var toothShape = sector;

			for (var i = 0; i < stepsPerTooth; i++)
			{
				var pinionRadians = i * stepSizeRadians;
				var pinionCenterRayRadians = -pinionRadians * pinion.ToothCount / this.ToothCount;

				var cutter = cutterTemplate.Rotate(pinionRadians);
				cutter = cutter.Translate(-this.pitchRadius + pinion.pitchRadius, 0, 1000);
				cutter = cutter.Rotate(pinionCenterRayRadians);

				toothShape = toothShape.Subtract(cutter);

				cutter = cutterTemplate.Rotate(-pinionRadians);
				cutter = cutter.Translate(-this.pitchRadius + pinion.pitchRadius, 0, 1000);
				cutter = cutter.Rotate(-pinionCenterRayRadians);

				toothShape = toothShape.Subtract(cutter);
			}

			debugData.Add(toothShape);

			return toothShape[toothShape.Count - 1];
		}

		private Polygon SmoothConcaveCorners(Polygon corners)
		{
			// removes single concave corners located between convex corners
			return this.SmoothCorners(corners, false); // removeSingleConvex
		}

		private Polygon SmoothConvexCorners(Polygon corners)
		{
			// removes single convex corners located between concave corners
			return this.SmoothCorners(corners, true); // removeSingleConvex
		}

		private Polygon SmoothCorners(Polygon corners, bool removeSingleConvex)
		{
			var isConvex = new List<bool>();
			var previousCorner = corners[corners.Count - 1];
			var currentCorner = corners[0];
			for (var i = 0; i < corners.Count; i++)
			{
				var nextCorner = corners[(i + 1) % corners.Count];

				var v1 = previousCorner - currentCorner;
				var v2 = nextCorner - currentCorner;
				var crossProduct = v1.Cross(v2);
				isConvex.Add(crossProduct < 0);

				previousCorner = currentCorner;
				currentCorner = nextCorner;
			}

			// we want to remove any concave corners that are located between two convex corners
			var cleanedUpCorners = new Polygon();
			for (var currentIndex = 0; currentIndex < corners.Count; currentIndex++)
			{
				var corner = corners[currentIndex];
				var nextIndex = (currentIndex + 1) % corners.Count;
				var previousIndex = (currentIndex + corners.Count - 1) % corners.Count;

				var isSingleConcave = !isConvex[currentIndex] && isConvex[previousIndex] && isConvex[nextIndex];
				var isSingleConvex = isConvex[currentIndex] && !isConvex[previousIndex] && !isConvex[nextIndex];

				if (removeSingleConvex && isSingleConvex)
				{
					continue;
				}

				if (!removeSingleConvex && isSingleConcave)
				{
					continue;
				}

				cleanedUpCorners.Add(corner);
			}

			return cleanedUpCorners;
		}

		private void CalculateDependants()
		{
			// convert circular pitch to diametral pitch
			this.diametralPitch = Math.PI / this.CircularPitch; // Ratio of the number of teeth to the pitch diameter
																// this.circularPitch = Math.PI / this.diametralPitch;

			this.center = Vector2.Zero; // center of the gear
										// this.angle = 0; // angle in degrees of the complete gear (changes during rotation animation)

			// Pitch diameter: Diameter of pitch circle.
			this.pitchDiameter = this.ToothCount / this.diametralPitch;
			this.pitchRadius = this.pitchDiameter / 2;

			// Addendum: Radial distance from pitch circle to outside circle.
			this.addendum = 1 / this.diametralPitch;

			// Typically no profile shift is used meaning that this.shiftedAddendum = this.addendum
			this.shiftedAddendum = this.addendum * (1 + this.profileShift);

			// Outer Circle
			this.OuterRadius = this.pitchRadius + this.shiftedAddendum;
		}

		private Gear2D CreateInternalPinion()
		{
			return new Gear2D()
			{
				ToothCount = this.InternalToothCount,
				CircularPitch = this.CircularPitch,
				CenterHoleDiameter = this.CenterHoleDiameter,
				PressureAngle = this.PressureAngle,
				Backlash = this.Backlash,
				Clearance = this.Clearance,
				GearType = this.GearType,
			};
		}

		private Polygons CreateInternalGearShape()
		{
			var singleTooth = this.CreateInternalToothProfile();
			debugData.Add(new Polygons() { singleTooth });

			var outerCorners = new Polygons();

			for (var i = 0; i < this.ToothCount; i++)
			{
				var angle = i * this.AngleToothToTooth;
				var radians = MathHelper.DegreesToRadians(angle);
				outerCorners.Add(singleTooth.Rotate(radians));
			}

			outerCorners = outerCorners.Union(outerCorners, PolyFillType.pftNonZero);

			debugData.Add(outerCorners);

			var innerRadius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.Clearance;
			var outerRadius = innerRadius + 4 * this.addendum;
			var outerCircle = Circle(this.center.X, center.Y, outerRadius, 1000);

			// return outerCorners;
			var finalShape = outerCircle.Subtract(outerCorners);
			//debugData.Add(finalShape);

			return finalShape;
		}

		private Polygons CreateRackShape()
		{
			var rack = new Polygons();

			for (var i = 0; i < ToothCount; i++)
			{
				var tooth = this.CreateRackTooth();
				tooth = tooth.Translate(0, (0.5 + -ToothCount / 2.0 + i) * this.CircularPitch, 1000);
				rack = rack.Union(tooth);
			}

			// creating the bar backing the teeth
			var rightX = -(this.addendum + this.Clearance);
			var width = 4 * this.addendum;
			var halfHeight = ToothCount * this.CircularPitch / 2.0;
			var bar = Rectangle(rightX - width, -halfHeight, rightX, halfHeight, 1000);

			var rackFinal = rack.Union(bar);
			return rackFinal.Translate(this.addendum * this.profileShift, 0, 1000);
		}

		private Polygon CreateRackTooth()
		{
			var toothWidth = this.CircularPitch / 2;

			var sinPressureAngle = Math.Sin(this.PressureAngle * Math.PI / 180);
			var cosPressureAngle = Math.Cos(this.PressureAngle * Math.PI / 180);

			// if a positive backlash is defined then we widen the trapezoid accordingly.
			// Each side of the tooth needs to widened by a fourth of the backlash (vertical to cutter faces).
			var dx = this.Backlash / 4 / cosPressureAngle;

			var leftDepth = this.addendum + this.Clearance;

			var upperLeftCorner = ScalledPoint(-leftDepth, toothWidth / 2 - dx + (this.addendum + this.Clearance) * sinPressureAngle, 1000);
			var upperRightCorner = ScalledPoint(this.addendum, toothWidth / 2 - dx - this.addendum * sinPressureAngle, 1000);
			var lowerRightCorner = ScalledPoint(upperRightCorner.X, -upperRightCorner.Y, 1);
			var lowerLeftCorner = ScalledPoint(upperLeftCorner.X, -upperLeftCorner.Y, 1);

			var tooth = new Polygon();
			tooth.Add(upperLeftCorner);
			tooth.Add(upperRightCorner);
			tooth.Add(lowerRightCorner);
			tooth.Add(lowerLeftCorner);

			return tooth;
		}

		private Polygons CreateExternalGearShape()
		{
			var toothParts = this.CreateSingleTooth();

			// we could now take the tooth cutout, rotate it tooth count times and union the various slices together into a complete gear.
			// However, the union operations become more and more complex as the complete gear is built up.
			// So instead we capture the outer path of the tooth and concatenate rotated versions of this path into a complete outer gear path.
			// Concatenating paths is inexpensive resulting in significantly faster execution.

			var tooth = toothParts.tooth;
			debugData.Add(new Polygons() { tooth });

			var gearShape = new Polygons();
			for (var i = 0; i < this.ToothCount; i++)
			{
				var angle = i * this.AngleToothToTooth;
				var radians = MathHelper.DegreesToRadians(angle);
				var rotatedCorner = tooth.Rotate(radians);
				gearShape.Add(rotatedCorner);
			}

			gearShape = gearShape.Union(gearShape, PolyFillType.pftNonZero);

			debugData.Add(gearShape);

			gearShape = toothParts.wheel.Subtract(gearShape);

			debugData.Add(gearShape);

			if (this.CenterHoleDiameter > 0)
			{
				var radius = this.CenterHoleDiameter / 2;
				var centerhole = Circle(0, 0, radius, 1000);
				gearShape = gearShape.Subtract(centerhole);
				debugData.Add(gearShape);
			}

			return gearShape;
		}

		private (Polygon tooth, Polygon wheel) CreateSingleTooth()
		{
			// create outer circle sector covering one tooth
			var toothSectorPath = Circle(0, 0, this.OuterRadius, 1000);

			var toothCutOut = CreateToothCutout();

			return (toothCutOut, toothSectorPath);
		}

		private Polygon CreateToothCutout()
		{
			var angleStepSize = this.AngleToothToTooth / this.stepsPerToothAngle;

			var toothCutout = new Polygons();

			var toothCutterShape = this.CreateToothCutter();
			debugData.Add(new Polygons() { toothCutterShape });

			var bounds = toothCutterShape.GetBounds();
			var lowerLeftCorner = new Vector2(bounds.Left, bounds.Bottom);

			// To create the tooth profile we move the (virtual) infinite gear and then turn the resulting cutter position back.
			// For illustration see http://lcamtuf.coredump.cx/gcnc/ch6/, section 'Putting it all together'
			// We continue until the moved tooth cutter's lower left corner is outside of the outer circle of the gear.
			// Going any further will no longer influence the shape of the tooth
			var stepCounter = 0;
			while (true)
			{
				var angle = stepCounter * angleStepSize;
				var radians = MathHelper.DegreesToRadians(angle);
				var xTranslation = new Vector2(radians * this.pitchRadius, 0) * 1000;

				if (Vector2.Rotate(lowerLeftCorner + xTranslation, radians).Length > this.OuterRadius * 1000)
				{
					// the cutter is now completely outside the gear and no longer influences the shape of the gear tooth
					break;
				}

				// we move in both directions
				var movedToothCutterShape = toothCutterShape.Translate(xTranslation.X, xTranslation.Y);
				movedToothCutterShape = movedToothCutterShape.Rotate(radians);
				toothCutout = toothCutout.Union(movedToothCutterShape);

				if (xTranslation[0] > 0)
				{
					movedToothCutterShape = toothCutterShape.Translate(-xTranslation.X, xTranslation.Y);
					movedToothCutterShape = movedToothCutterShape.Rotate(-radians);
					toothCutout = toothCutout.Union(movedToothCutterShape);
				}

				stepCounter++;
			}

			var toothCutout1 = this.SmoothConcaveCorners(toothCutout[0]);

			return toothCutout1.Rotate(MathHelper.DegreesToRadians(-this.AngleToothToTooth / 2));
		}

		private Polygon CreateToothCutter()
		{
			// we create a trapezoidal cutter as described at http://lcamtuf.coredump.cx/gcnc/ch6/ under the section 'Putting it all together'
			var toothWidth = this.CircularPitch / 2;

			var cutterDepth = this.addendum + this.Clearance;
			var cutterOutsideLength = 3 * this.addendum;

			var sinPressureAngle = Math.Sin(this.PressureAngle * Math.PI / 180.0);
			var cosPressureAngle = Math.Cos(this.PressureAngle * Math.PI / 180.0);

			// if a positive backlash is defined then we widen the trapezoid accordingly.
			// Each side of the tooth needs to widened by a fourth of the backlash (vertical to cutter faces).
			var dx = this.Backlash / 2 / cosPressureAngle;

			var lowerRightCorner = new Vector2(toothWidth / 2 + dx - cutterDepth * sinPressureAngle, this.pitchRadius + this.profileShift * this.addendum - cutterDepth);
			var upperRightCorner = new Vector2(toothWidth / 2 + dx + cutterOutsideLength * sinPressureAngle, this.pitchRadius + this.profileShift * this.addendum + cutterOutsideLength);
			var upperLeftCorner = new Vector2(-upperRightCorner[0], upperRightCorner[1]);
			var lowerLeftCorner = new Vector2(-lowerRightCorner[0], lowerRightCorner[1]);

			var cutterPath = new Polygon();
			cutterPath.Add(ScalledPoint(lowerLeftCorner, 1000));
			cutterPath.Add(ScalledPoint(upperLeftCorner, 1000));
			cutterPath.Add(ScalledPoint(upperRightCorner, 1000));
			cutterPath.Add(ScalledPoint(lowerRightCorner, 1000));

			return cutterPath;
		}
	}
}

public static class Extensions
{
	public static IVertexSource Minus(this IVertexSource a, IVertexSource b)
	{
		return CombinePaths(a, b, ClipType.ctDifference);
	}

	public static IVertexSource Plus(this IVertexSource a, IVertexSource b)
	{
		return CombinePaths(a, b, ClipType.ctUnion);
	}

	public static IVertexSource RotateZDegrees(this IVertexSource a, double angle)
	{
		return new VertexSourceApplyTransform(a, Affine.NewRotation(MathHelper.DegreesToRadians(angle)));
	}

	public static IVertexSource Subtract(this IVertexSource a, IVertexSource b)
	{
		return a.Minus(b);
	}

	public static IVertexSource Translate(this IVertexSource a, Vector2 delta)
	{
		return new VertexSourceApplyTransform(a, Affine.NewTranslation(delta));
	}

	public static IVertexSource Union(this IVertexSource a, IVertexSource b)
	{
		return a.Plus(b);
	}

	private static VertexStorage CombinePaths(IVertexSource a, IVertexSource b, ClipType clipType)
	{
		List<List<IntPoint>> aPolys = a.CreatePolygons();
		List<List<IntPoint>> bPolys = b.CreatePolygons();

		var clipper = new Clipper();

		clipper.AddPaths(aPolys, PolyType.ptSubject, true);
		clipper.AddPaths(bPolys, PolyType.ptClip, true);

		var outputPolys = new List<List<IntPoint>>();
		clipper.Execute(clipType, outputPolys);

		Clipper.CleanPolygons(outputPolys);

		VertexStorage output = outputPolys.CreateVertexStorage();

		output.Add(0, 0, ShapePath.FlagsAndCommand.Stop);

		return output;
	}
}