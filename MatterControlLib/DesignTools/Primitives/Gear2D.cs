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
using System.Security.Cryptography;
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

		private Gear2D connectedGear;

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
					//shape = CreateInternalGearShape();
					break;

				case GearTypes.Rack:
					shape = CreateRackShape();
					break;
			}

			if (Debug && debugData.Count > 0)
			{
				var output = debugData[0];
				var offset = 0.0;
				for (int i = 1; i < debugData.Count; i++)
				{
					offset += debugData[i - 1].GetBounds().Height / 2 + 2;
					offset += debugData[i].GetBounds().Height / 2 + 2;
					output = new CombinePaths(output, new VertexSourceApplyTransform(debugData[i], Affine.NewTranslation(0, offset)));
					offset += debugData[i].GetBounds().Height / 2 + 2;
				}

				// return output.Vertices();
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

		private Polygons CreateInternalToothCutter()
		{
			// To cut the internal gear teeth, the actual pinion comes close but we need to enlarge it so properly caters for clearance and backlash
			var pinion = this.connectedGear;

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
			return tooth.tooth.Rotate(90 + 180 / enlargedPinion.ToothCount); // we need a tooth pointing to the left
		}

		private IVertexSource CreateInternalToothProfile()
		{
			var radius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.Clearance;
			var angleToothToTooth = 360 / this.ToothCount;
			var sin = Math.Sin(angleToothToTooth / 2 * Math.PI / 180);
			var cos = Math.Cos(angleToothToTooth / 2 * Math.PI / 180);

			var fullSector = new VertexStorage();

			fullSector.MoveTo(0, 0);
			fullSector.LineTo(-(radius * cos), radius * sin);
			fullSector.LineTo(-radius, 0);
			fullSector.LineTo(-(radius * cos), -radius * sin);

			var innerRadius = radius - (2 * this.addendum + this.Clearance);
			var innerCircle = new Ellipse(this.center, innerRadius)
			{
				ResolutionScale = 10
			};

			var sector = fullSector.Subtract(innerCircle);
			debugData.Add(sector);


			var cutterTemplate = this.CreateInternalToothCutter();
			debugData.Add(cutterTemplate);

			var pinion = this.connectedGear;
			var stepsPerTooth = this.stepsPerToothAngle;
			var angleStepSize = angleToothToTooth / stepsPerTooth;
			var cutter = cutterTemplate.Translate(-this.pitchRadius + this.connectedGear.pitchRadius, 0);
			var toothShape = sector.Subtract(cutter);
			debugData.Add(toothShape);

			for (var i = 1; i < stepsPerTooth; i++)
			{
				var pinionRotationAngle = i * angleStepSize;
				var pinionCenterRayAngle = -pinionRotationAngle * pinion.ToothCount / this.ToothCount;

				cutter = cutterTemplate.RotateZDegrees(pinionRotationAngle);
				cutter = cutter.Translate(-this.pitchRadius + this.connectedGear.pitchRadius, 0);
				cutter = cutter.RotateZDegrees(pinionCenterRayAngle);

				toothShape = toothShape.Subtract(cutter);

				cutter = cutterTemplate.RotateZDegrees(-pinionRotationAngle);
				cutter = cutter.Translate(-this.pitchRadius + this.connectedGear.pitchRadius, 0);
				cutter = cutter.RotateZDegrees(-pinionCenterRayAngle);

				toothShape = toothShape.Subtract(cutter);
			}

			return toothShape;
		}

		private IVertexSource SmoothConcaveCorners(IVertexSource corners)
		{
			// removes single concave corners located between convex corners
			return this.SmoothCorners(corners, false); // removeSingleConvex
		}

		private IVertexSource SmoothConvexCorners(IVertexSource corners)
		{
			// removes single convex corners located between concave corners
			return this.SmoothCorners(corners, true); // removeSingleConvex
		}

		private IVertexSource SmoothCorners(IVertexSource corners_in, bool removeSingleConvex)
		{
			var corners = corners_in as VertexStorage;
			var isConvex = new List<bool>();
			var previousCorner = corners[corners.Count - 1];
			var currentCorner = corners[0];
			for (var i = 0; i < corners.Count; i++)
			{
				var nextCorner = corners[(i + 1) % corners.Count];

				var v1 = previousCorner.position - currentCorner.position;
				var v2 = nextCorner.position - currentCorner.position;
				var crossProduct = v1.Cross(v2);
				isConvex.Add(crossProduct < 0);

				previousCorner = currentCorner;
				currentCorner = nextCorner;
			}

			// we want to remove any concave corners that are located between two convex corners
			var cleanedUpCorners = new VertexStorage();
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

				cleanedUpCorners.Add(corner.X, corner.Y, corner.command);
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

			if (InternalToothCount > 0)
			{
				connectedGear = new Gear2D()
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
		}

		private IVertexSource CreateInternalGearShape()
		{
			var singleTooth = this.CreateInternalToothProfile();
			debugData.Add(singleTooth);

			var corners = singleTooth as VertexStorage;

			// first we need to find the corner that sits at the center
			var centerCornerIndex = 0;
			var radius = this.pitchRadius + (1 + this.profileShift) * this.addendum + this.Clearance;

			var bottomRight = new Vector2(-1000000, -1000000);
			var deltaFromBR = double.MaxValue;
			for (var i = 0; i < corners.Count; i++)
			{
				var corner = corners[i];
				var length = (new Vector2(corner.X, corner.Y) - bottomRight).Length;
				if (length < deltaFromBR)
				{
					centerCornerIndex = i;
					deltaFromBR = length;
				}
			}

			var outerCorner = new VertexStorage();
			var command = ShapePath.FlagsAndCommand.MoveTo;
			for (var i = 0; i < corners.Count - 2; i++)
			{
				var corner = corners[(i + centerCornerIndex) % corners.Count];
				outerCorner.Add(corner.position.X, corner.position.Y, command);
				command = ShapePath.FlagsAndCommand.LineTo;
			}

			//outerCorners.ClosePolygon();

			debugData.Add(outerCorner);

			//var reversedOuterCorners = new VertexStorage();
			//command = ShapePath.FlagsAndCommand.MoveTo;
			//foreach (var vertex in new ReversePath(outerCorners).Vertices())
			//{
			//	reversedOuterCorners.Add(vertex.position.X, vertex.position.Y, command);
			//	command = ShapePath.FlagsAndCommand.LineTo;
			//}

			//// debugData.Add(reversedOuterCorners);

			//outerCorners = reversedOuterCorners;

			var cornerCount = outerCorner.Count;
			var outerCorners = new VertexStorage();
			command = ShapePath.FlagsAndCommand.MoveTo;

			for (var i = 0; i < this.ToothCount; i++)
			{
				var angle = i * this.AngleToothToTooth;
				var roatationMatrix = Affine.NewRotation(MathHelper.DegreesToRadians(angle));
				for (var j = 0; j < cornerCount; j++)
				{
					var rotatedCorner = roatationMatrix.Transform(outerCorner[j].position);
					outerCorners.Add(rotatedCorner.X, rotatedCorner.Y, command);
					command = ShapePath.FlagsAndCommand.LineTo;
				}
			}

			outerCorners = this.SmoothConcaveCorners(outerCorners) as VertexStorage;

			debugData.Add(outerCorners);

			var innerRadius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.Clearance;
			var outerRadius = innerRadius + 4 * this.addendum;
			var outerCircle = new Ellipse(this.center, outerRadius, outerRadius);

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
				rack = rack.CreateUnion(tooth);
			}

			// creating the bar backing the teeth
			var rightX = -(this.addendum + this.Clearance);
			var width = 4 * this.addendum;
			var halfHeight = ToothCount * this.CircularPitch / 2.0;
			var bar = Rectangle(rightX - width, -halfHeight, rightX, halfHeight, 1000);

			var rackFinal = rack.CreateUnion(bar);
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
			var tooth = this.CreateSingleTooth();
			// return tooth.wheel;
			// return tooth.tooth;

			// we could now take the tooth cutout, rotate it tooth count times and union the various slices together into a complete gear.
			// However, the union operations become more and more complex as the complete gear is built up.
			// So instead we capture the outer path of the tooth and concatenate rotated versions of this path into a complete outer gear path.
			// Concatenating paths is inexpensive resulting in significantly faster execution.
			var outlinePaths = tooth.tooth;

			// first we need to find the corner that sits at the center
			for (var i = 1; i < this.ToothCount; i++)
			{
				var angle = i * this.AngleToothToTooth;
				var roatationMatrix = Affine.NewRotation(MathHelper.DegreesToRadians(angle));
				var rotatedCorner = new VertexSourceApplyTransform(tooth.tooth, roatationMatrix);
				outlinePaths = new CombinePaths(outlinePaths, rotatedCorner);
			}

			// return outlinePaths;

			var gearShape = tooth.wheel.CombinePaths(outlinePaths, ClipType.ctDifference);

			// return gearShape;

			if (this.CenterHoleDiameter > 0)
			{
				var radius = this.CenterHoleDiameter / 2;
				var centerhole = new Ellipse(0, 0, radius, radius)
				{
					ResolutionScale = 10
				};
				gearShape = gearShape.Subtract(centerhole) as VertexStorage;
			}

			return gearShape;
		}

		private (Polygons tooth, Polygon wheel) CreateSingleTooth()
		{
			// create outer circle sector covering one tooth
			var toothSectorPath = Circle(0, 0, this.OuterRadius, 1000);

			var toothCutOut = CreateToothCutout();

			return (toothCutOut, toothSectorPath);
		}

		private Polygons CreateToothCutout()
		{
			var angleStepSize = this.AngleToothToTooth / this.stepsPerToothAngle;

			IVertexSource toothCutout = new VertexStorage();

			var toothCutterShape = this.CreateToothCutter();
			// return toothCutterShape;

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
				var xTranslation = new Vector2(angle * Math.PI / 180 * this.pitchRadius, 0);

				if (Vector2.Rotate(lowerLeftCorner + xTranslation, MathHelper.DegreesToRadians(angle)).Length > this.OuterRadius)
				{
					// the cutter is now completely outside the gear and no longer influences the shape of the gear tooth
					break;
				}

				// we move in both directions
				var movedToothCutterShape = toothCutterShape.Translate(xTranslation);
				movedToothCutterShape = movedToothCutterShape.RotateZDegrees(angle);
				toothCutout = toothCutout.Union(movedToothCutterShape);

				if (xTranslation[0] > 0)
				{
					movedToothCutterShape = toothCutterShape.Translate(new Vector2(-xTranslation.X, xTranslation.Y));
					movedToothCutterShape = movedToothCutterShape.RotateZDegrees(-angle);
					toothCutout = toothCutout.Union(movedToothCutterShape);
				}

				stepCounter++;
			}

			toothCutout = this.SmoothConcaveCorners(toothCutout);

			return toothCutout.RotateZDegrees(-this.AngleToothToTooth / 2);
		}

		private IVertexSource CreateToothCutter()
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

			var cutterPath = new VertexStorage();
			cutterPath.MoveTo(lowerLeftCorner);
			cutterPath.LineTo(upperLeftCorner);
			cutterPath.LineTo(upperRightCorner);
			cutterPath.LineTo(lowerRightCorner);

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

	public static Polygons CombinePaths(this Polygons aPolys, Polygons bPolys, ClipType clipType)
	{
		var clipper = new Clipper();

		clipper.AddPaths(aPolys, PolyType.ptSubject, true);
		clipper.AddPaths(bPolys, PolyType.ptClip, true);

		var outputPolys = new List<List<IntPoint>>();
		clipper.Execute(clipType, outputPolys);

		Clipper.CleanPolygons(outputPolys);

		return outputPolys;
	}
}