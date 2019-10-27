/*
Involute Spur Gear Builder (c) 2014 Dr. Rainer Hessmer
ported to C# 2018 by Lars Brubaker

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
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	// using Polygons = List<List<IntPoint>>;
	// using Polygon = List<IntPoint>;

	public class Gear2D : VertexSourceLegacySupport
	{
		private double _circularPitch = 8;

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

		private double diametralPitch;
		private double addendum;
		private double clearance = .05;

		// Most common stock gears have a 20° pressure angle, with 14½° and 25° pressure angle gears being much less
		// common. Increasing the pressure angle increases the width of the base of the gear tooth, leading to greater strength and load carrying capacity. Decreasing
		// the pressure angle provides lower backlash, smoother operation and less sensitivity to manufacturing errors. (reference: http://en.wikipedia.org/wiki/Involute_gear)
		private double pressureAngle = 20;

		private double backlash = .05;
		private double profileShift = 0;
		private double shiftedAddendum;
		private double outerRadius;
		private double angleToothToTooth;
		private int toothCount = 30;
		private GearType gearType = GearType.External;
		private int stepsPerToothAngle = 3;
		private double pitchDiameter;
		private double pitchRadius;
		private Vector2 center = Vector2.Zero;
		private Gear2D connectedGear;
		private int centerHoleDiameter = 4;

		public enum GearType
		{
			External,
			Internal,
			Rack
		}

		public Gear2D()
		{
			CalculateDependants();
		}

		private void CalculateDependants()
		{
			// convert circular pitch to diametral pitch
			this.diametralPitch = Math.PI / this.CircularPitch; // Ratio of the number of teeth to the pitch diameter
			// this.circularPitch = Math.PI / this.diametralPitch;

			this.center = Vector2.Zero; // center of the gear
			// this.angle = 0; // angle in degrees of the complete gear (changes during rotation animation)

			// Pitch diameter: Diameter of pitch circle.
			this.pitchDiameter = this.toothCount / this.diametralPitch;
			this.pitchRadius = this.pitchDiameter / 2;

			// Addendum: Radial distance from pitch circle to outside circle.
			this.addendum = 1 / this.diametralPitch;

			// Typically no profile shift is used meaning that this.shiftedAddendum = this.addendum
			this.shiftedAddendum = this.addendum * (1 + this.profileShift);

			// Outer Circle
			this.outerRadius = this.pitchRadius + this.shiftedAddendum;
			this.angleToothToTooth = 360 / this.toothCount;
		}

		public override IEnumerable<VertexData> Vertices()
		{
			// return CreateRackShape().Vertices();
			// return CreateRegularGearShape().Vertices();
			return CreateSingleTooth().tooth.Vertices();
		}

		private IVertexSource CreateRackShape()
		{
			IVertexSource rack = new VertexStorage();

			// we draw one tooth in the middle and then five on either side
			for (var i = 0; i < toothCount; i++)
			{
				var tooth = this.CreateRackTooth();
				tooth = tooth.Translate(0, (0.5 + -toothCount / 2 + i) * this.CircularPitch);
				rack = rack.Union(tooth);
			}

			// creating the bar backing the teeth
			var rightX = -(this.addendum + this.clearance);
			var width = 4 * this.addendum;
			var halfHeight = toothCount * this.CircularPitch / 2;
			var bar = new RoundedRect(rightX - width, -halfHeight, rightX, halfHeight, 0);

			var rackFinal = rack.Union(bar) as VertexStorage;
			rackFinal.Translate(this.addendum * this.profileShift, 0);
			return rackFinal;
		}

		private IVertexSource CreateRegularGearShape()
		{
			var tooth = this.CreateSingleTooth();

			// we could now take the tooth cutout, rotate it tooth count times and union the various slices together into a complete gear.
			// However, the union operations become more and more complex as the complete gear is built up.
			// So instead we capture the outer path of the tooth and concatenate rotated versions of this path into a complete outer gear path.
			// Concatenating paths is inexpensive resulting in significantly faster execution.
			var outlinePaths = tooth.tooth;

			// first we need to find the corner that sits at the center
			for (var i = 1; i < this.toothCount; i++)
			{
				var angle = i * this.angleToothToTooth;
				var roatationMatrix = Affine.NewRotation(MathHelper.DegreesToRadians(angle));
				var rotatedCorner = new VertexSourceApplyTransform(tooth.tooth, roatationMatrix);
				outlinePaths = new CombinePaths(outlinePaths, rotatedCorner);
			}

			var gearShape = tooth.wheel.Subtract(outlinePaths);

			if (this.centerHoleDiameter > 0)
			{
				var radius = this.centerHoleDiameter / 2;
				var centerhole = new Ellipse(0, 0, radius, radius);
				gearShape = gearShape.Subtract(centerhole) as VertexStorage;
			}

			return gearShape;//.RotateZDegrees(-90);
		}

		private IVertexSource CreateRackTooth()
		{
			var toothWidth = this.CircularPitch / 2;
			var toothDepth = this.addendum + this.clearance;

			var sinPressureAngle = Math.Sin(this.pressureAngle * Math.PI / 180);
			var cosPressureAngle = Math.Cos(this.pressureAngle * Math.PI / 180);

			// if a positive backlash is defined then we widen the trapezoid accordingly.
			// Each side of the tooth needs to widened by a fourth of the backlash (vertical to cutter faces).
			var dx = this.backlash / 4 / cosPressureAngle;

			var leftDepth = this.addendum + this.clearance;

			var upperLeftCorner = new Vector2(-leftDepth, toothWidth / 2 - dx + (this.addendum + this.clearance) * sinPressureAngle);
			var upperRightCorner = new Vector2(this.addendum, toothWidth / 2 - dx - this.addendum * sinPressureAngle);
			var lowerRightCorner = new Vector2(upperRightCorner[0], -upperRightCorner[1]);
			var lowerLeftCorner = new Vector2(upperLeftCorner[0], -upperLeftCorner[1]);

			var tooth = new VertexStorage();
			tooth.MoveTo(upperLeftCorner);
			tooth.LineTo(upperRightCorner);
			tooth.LineTo(lowerRightCorner);
			tooth.LineTo(lowerLeftCorner);

			return tooth;
		}

		private (IVertexSource tooth, IVertexSource wheel) CreateSingleTooth()
		{
			// create outer circle sector covering one tooth
			var toothSectorPath = new Arc(Vector2.Zero, new Vector2(this.outerRadius, this.outerRadius), MathHelper.DegreesToRadians(90), MathHelper.DegreesToRadians(90 - this.angleToothToTooth));

			var toothCutOut = CreateToothCutout();

			return (toothCutOut, toothSectorPath);
		}

		private IVertexSource CreateToothCutout()
		{
			var angleToothToTooth = 360 / this.toothCount;
			var angleStepSize = this.angleToothToTooth / this.stepsPerToothAngle;

			IVertexSource toothCutout = new VertexStorage();

			var toothCutterShape = this.CreateToothCutter();
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

				var movedLowerLeftCorner = lowerLeftCorner + xTranslation;
				movedLowerLeftCorner = Vector2.Rotate(movedLowerLeftCorner, MathHelper.DegreesToRadians(angle));

				if (movedLowerLeftCorner.Length > this.outerRadius)
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
					movedToothCutterShape = toothCutterShape.Translate(new Vector2(-xTranslation[0], xTranslation[1]));
					movedToothCutterShape = movedToothCutterShape.RotateZDegrees(-angle);
					toothCutout = toothCutout.Union(movedToothCutterShape);
				}

				stepCounter++;
			}

			toothCutout = this._smoothConcaveCorners(toothCutout);

			return toothCutout.RotateZDegrees(-this.angleToothToTooth / 2);
		}

		private IVertexSource CreateToothCutter()
		{
			// we create a trapezoidal cutter as described at http://lcamtuf.coredump.cx/gcnc/ch6/ under the section 'Putting it all together'
			var toothWidth = this.CircularPitch / 2;

			var cutterDepth = this.addendum + this.clearance;
			var cutterOutsideLength = 3 * this.addendum;

			var sinPressureAngle = Math.Sin(this.pressureAngle * Math.PI / 180.0);
			var cosPressureAngle = Math.Cos(this.pressureAngle * Math.PI / 180.0);

			// if a positive backlash is defined then we widen the trapezoid accordingly.
			// Each side of the tooth needs to widened by a fourth of the backlash (vertical to cutter faces).
			var dx = this.backlash / 2 / cosPressureAngle;

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

		private IVertexSource _smoothConvexCorners(IVertexSource corners)
		{
			// removes single convex corners located between concave corners
			return this._smoothCorners(corners, true); // removeSingleConvex
		}

		private IVertexSource _createInternalToothCutter()
		{
			// To cut the internal gear teeth, the actual pinion comes close but we need to enlarge it so properly caters for clearance and backlash
			var pinion = this.connectedGear;

			throw new NotImplementedException();
			var enlargedPinion = new Gear2D()
			{
				CircularPitch = pinion.CircularPitch,
				// pressureAngle: pinion.pressureAngle,
				// clearance: -pinion.clearance,
				// backlash: -pinion.backlash,
				// toothCount: pinion.toothCount,
				// centerHoleDiameter: 0,
				// profileShift: pinion.profileShift,
				// stepsPerToothAngle: pinion.stepsPerToothAngle
			};

			var tooth = enlargedPinion.CreateSingleTooth();
			//return tooth.RotateZDegrees(90 + 180 / enlargedPinion.toothCount); // we need a tooth pointing to the left
		}

		private IVertexSource _createInternalToothProfile()
		{
			var radius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.clearance;
			var angleToothToTooth = 360 / this.toothCount;
			var sin = Math.Sin(angleToothToTooth / 2 * Math.PI / 180);
			var cos = Math.Cos(angleToothToTooth / 2 * Math.PI / 180);

			var fullSector = new VertexStorage();

			fullSector.MoveTo(0, 0);
			fullSector.LineTo(-(radius * cos), radius * sin);
			fullSector.LineTo(-radius, 0);
			fullSector.LineTo(-(radius * cos), -radius * sin);

			var innerRadius = radius - (2 * this.addendum + this.clearance);
			var innerCircle = new Ellipse(this.center, innerRadius);
			var sector = fullSector.Subtract(innerCircle);

			var cutterTemplate = this._createInternalToothCutter();

			var pinion = this.connectedGear;
			var stepsPerTooth = this.stepsPerToothAngle;
			var angleStepSize = angleToothToTooth / stepsPerTooth;
			var toothShape = sector;
			var cutter = cutterTemplate.Translate(-this.pitchRadius + this.connectedGear.pitchRadius, 0);
			toothShape = toothShape.Subtract(cutter);

			for (var i = 1; i < stepsPerTooth; i++)
			{
				var pinionRotationAngle = i * angleStepSize;
				var pinionCenterRayAngle = -pinionRotationAngle * pinion.toothCount / this.toothCount;

				// var cutter = cutterTemplate;
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


		private IVertexSource _smoothConcaveCorners(IVertexSource corners)
		{
			// removes single concave corners located between convex corners
			return this._smoothCorners(corners, false); // removeSingleConvex
		}

		private IVertexSource _smoothCorners(IVertexSource corners_in, bool removeSingleConvex)
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
			var previousIndex = corners.Count - 1;
			var currentIndex = 0;
			for (var i = 0; i < corners.Count; i++)
			{
				var corner = corners[currentIndex];
				var nextIndex = (i + 1) % corners.Count;

				var isSingleConcave = !isConvex[currentIndex] && isConvex[previousIndex] && isConvex[nextIndex];
				var isSingleConvex = isConvex[currentIndex] && !isConvex[previousIndex] && !isConvex[nextIndex];

				previousIndex = currentIndex;
				currentIndex = nextIndex;

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
	}
}

public static class Extensions
{
	public static IVertexSource Subtract(this IVertexSource a, IVertexSource b)
	{
		return a.Minus(b);
	}

	public static IVertexSource Union(this IVertexSource a, IVertexSource b)
	{
		return a.Plus(b);
	}

	public static IVertexSource RotateZDegrees(this IVertexSource a, double angle)
	{
		return new VertexSourceApplyTransform(a, Affine.NewRotation(MathHelper.DegreesToRadians(angle)));
	}

	public static IVertexSource Translate(this IVertexSource a, Vector2 delta)
	{
		return new VertexSourceApplyTransform(a, Affine.NewTranslation(delta));
	}

	public static IVertexSource Minus(this IVertexSource a, IVertexSource b)
	{
		return CombinePaths(a, b, ClipType.ctDifference);
	}

	public static IVertexSource Plus(this IVertexSource a, IVertexSource b)
	{
		return CombinePaths(a, b, ClipType.ctUnion);
	}

	private static VertexStorage CombinePaths(IVertexSource a, IVertexSource b, ClipType clipType)
	{
		List<List<IntPoint>> aPolys = a.CreatePolygons();
		List<List<IntPoint>> bPolys = b.CreatePolygons();

		Clipper clipper = new Clipper();

		clipper.AddPaths(aPolys, PolyType.ptSubject, true);
		clipper.AddPaths(bPolys, PolyType.ptClip, true);

		List<List<IntPoint>> outputPolys = new List<List<IntPoint>>();
		clipper.Execute(clipType, outputPolys);

		Clipper.CleanPolygons(outputPolys);

		VertexStorage output = outputPolys.CreateVertexStorage();

		output.Add(0, 0, ShapePath.FlagsAndCommand.Stop);

		return output;
	}
}

/*
 	<h1>Involute Spur Gear Builder <span style="font-size:10px">(C) 2014 Dr. Rainer Hessmer</span></h1>
	<p>An open source, browser based utility for calculating and drawing involute spur gears. As an improvement over the majority of other freely available scripts and utilities it fully accounts for undercuts. For additional information please head over to my blog posts <a href="http://www.hessmer.org/blog/2014/01/01/online-involute-spur-gear-builder">part 1</a> and <a href="http://www.hessmer.org/blog/2015/07/13/online-involute-spur-gear-builder-part-2/">part 2</a>. If you prefer a standalone utility and you use Windows 64, see <a href="http://dougrogers.blogspot.com/2016/08/gear-bakery-10-port-of-dr-rainer.html">Doug Roger's port to C++</a>.</p>
	<p>The implementation is inspired by the subtractive process that Michal Zalewski's describes in <a href="http://lcamtuf.coredump.cx/gcnc/ch6/#6.2">part six</a> of his excellent <a href="http://lcamtuf.coredump.cx/gcnc/">Guerrilla guide to CNC machining, mold making, and resin casting</a>.</p>
    <h2>Instructions</h2>
    <p>Specify desired values in the parameters box and then click on the 'Update' button. The tooth count n1 of gear one defines various configurations:
    </p><ul>
        <li>n1 &gt; 0: A regular external gear <br><img src="./Involute Spur Gear Builder_files/RegularSpurGear_Small.png" alt="Regular Spur Gear"></li>
		<li>n1 = 0: Rack and pinion <br><img src="./Involute Spur Gear Builder_files/RackAndPinion_Small.png" alt="Rack and Pinion"></li>
        <li>n1 &lt; 0: An internal gear as used in planetary gears <br><img src="./Involute Spur Gear Builder_files/InternalGear_Small.png" alt="Internal Gear"></li>
    </ul>
	<p></p>
	<p>The tool also supports profile shift to reduce the amount of undercut in gears with low tooth counts.


			var g_ExpandToCAGParams = {pathradius: 0.01, resolution: 2};

			function main(params)
			{
				// Main entry point; here we construct our solid:
				var qualitySettings = {resolution: params.resolution, stepsPerToothAngle: params.stepsPerToothAngle};

				var gear1 = new Gear({
					circularPitch: params.CircularPitch,
					pressureAngle: params.pressureAngle,
					clearance: params.clearance,
					backlash: params.backlash,
					toothCount: params.wheel1ToothCount,
					centerHoleDiameter: params.wheel1CenterHoleDiamater,
					profileShift: -params.profileShift,
					qualitySettings: qualitySettings
				});
				var gear2 = new Gear({
					circularPitch: params.CircularPitch,
					pressureAngle: params.pressureAngle,
					clearance: params.clearance,
					backlash: params.backlash,
					toothCount: params.wheel2ToothCount,
					centerHoleDiameter: params.wheel2CenterHoleDiamater,
					profileShift: params.profileShift,
					qualitySettings: qualitySettings
				});

				var gearSet = new GearSet(
					gear1,
					gear2,
					params.showOption);

				var shape = gearSet.createShape();
				return shape;
			}

			function getParameterDefinitions() {
				return [
					{ name: 'circularPitch', caption: 'Circular pitch (the circumference of the pitch circle divided by the number of teeth):', type: 'float', initial: 8 },
					{ name: 'pressureAngle', caption: 'Pressure Angle (common values are 14.5, 20 and 25 degrees):', type: 'float', initial: 20 },
					{ name: 'clearance', caption: 'Clearance (minimal distance between the apex of a tooth and the trough of the other gear; in length units):', type: 'float', initial: 0.05 },
					{ name: 'backlash', caption: 'Backlash (minimal distance between meshing gears; in length units):', type: 'float', initial: 0.05 },
					{ name: 'profileShift', caption: 'Profile Shift (indicates what portion of gear one\'s addendum height should be shifted to gear two. E.g., a value of 0.1 means the adddendum of gear two is increased by a factor of 1.1 while the height of the addendum of gear one is reduced to 0.9 of its normal height.):', type: 'float', initial: 0.0 },
					{ name: 'wheel1ToothCount', caption: 'Wheel 1 Tooth Count (n1 > 0: external gear; n1 = 0: rack; n1 < 0: internal gear):', type: 'int', initial: 30 },
					{ name: 'wheel1CenterHoleDiamater', caption: 'Wheel 1 Center Hole Diameter (0 for no hole):', type: 'float', initial: 4 },
					{ name: 'wheel2ToothCount', caption: 'Wheel 2 Tooth Count:', type: 'int', initial: 8 },
					{ name: 'wheel2CenterHoleDiamater', caption: 'Wheel 2 Center Hole Diameter (0 for no hole):', type: 'float', initial: 4 },
					{ name: 'showOption', caption: 'Show:', type: 'choice', values: [3, 1, 2], initial: 3, captions: ["Wheel 1 and Wheel 2", "Wheel 1 Only", "Wheel 2 Only"]},
					{ name: 'stepsPerToothAngle', caption: 'Rotation steps per tooth angle when assembling the tooth profile (3 = draft, 10 = good quality). Increasing the value will result in smoother profiles at the cost of significantly higher calcucation time. Incease in small increments and check the result by zooming in.', type: 'int', initial: 3 },
					{ name: 'resolution', caption: 'Number of segments per 360 degree of rotation (only used for circles and arcs); 90 is plenty:', type: 'int', initial: 30 },
				];
			}

			// Start base class Gear
			var Gear = (function () {
				Gear.prototype.getZeroedShape = function() {
					// return the gear shape center on the origin and rotation angle 0.
					if (this.zeroedShape == null) {
						this.zeroedShape = this._createZeroedShape();
					}
					return this.zeroedShape;
				}
				Gear.prototype._createZeroedShape = function() {
					if (this.gearType == GearType.Regular) {
						return this._createRegularGearShape();
					}
					else if (this.gearType == GearType.Internal) {
						return this._createInternalGearShape();
					}
					else if (this.gearType == GearType.Rack) {
						return this.CreateRackShape();
					}
				}
				Gear.prototype._createInternalGearShape = function() {
					var singleTooth = this._createInternalToothProfile();
					//return singleTooth;

					var outlinePaths = singleTooth.getOutlinePaths();
					var corners = outlinePaths[0].points;

					// first we need to find the corner that sits at the center
					var centerCornerIndex;
					var radius = this.pitchRadius + ( 1 + this.profileShift) * this.addendum + this.clearance;

					var delta = 0.0000001;
					for(var i = 0; i < corners.length; i++) {
						var corner = corners[i];
						if (corner.y < delta && (corner.x + radius) < delta) {
							centerCornerIndex = i;
							break;
						}
					}
					var outerCorners = [];
					for(var i = 2; i < corners.length - 2; i++) {
						var corner = corners[(i + centerCornerIndex) % corners.length];
						outerCorners.push(corner);
					}

					outerCorners.reverse();
					var cornersCount = outerCorners.length;

					for(var i = 1; i < this.toothCount; i++) {
						var angle = i * this.angleToothToTooth;
						var roatationMatrix = CSG.Matrix4x4.rotationZ(angle)
						for (var j = 0; j < cornersCount; j++) {
							var rotatedCorner = outerCorners[j].transform(roatationMatrix);
							outerCorners.push(rotatedCorner);
						}
					}

					var outerCorners = this._smoothConcaveCorners(outerCorners);
					var outerPoints = [];
					outerCorners.map(function(corner) { outerPoints.push([corner.x, corner.y]); });

					var innerRadius = this.pitchRadius + (1 - this.profileShift) * this.addendum + this.clearance;
					var outerRadius = innerRadius + 4 * this.addendum;
					var outerCircle = CAG.circle({center: this.center, radius: outerRadius, resolution: this.qualitySettings.resolution});
					//return outerCircle;

					var gearCutout = CAG.fromPointsNoCheck(outerPoints);
					//return gearCutout;
					return outerCircle.subtract(gearCutout);
				}
				Gear.prototype.pointsToString = function(points) {
					var result = "[";
					points.map(function(point) {
						result += "[" + point.x + "," + point.y + "],";
					});
					return result + "]";
				}
				return Gear;
			})();
*/
