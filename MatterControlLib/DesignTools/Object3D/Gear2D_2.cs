/*
Involute Spur Gear Builder (c) 2020 Dr. Rainer Hessmer
ported to C# 2021 by Lars Brubaker

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

using MatterHackers.VectorMath;
using System;

#if false

namespace Gear2D_2
{
    public class GearBase
    {
        public Vector2 ORIGIN = default(Vector2);

        public double CLIPPER_SCALE = 100000;
        public double CLIPPER_LIGHTEN_FACTOR = 0.0005;

        /*
        function getParameterDefinitions() {
            return [
            { name: 'circularPitch', caption: 'Circular pitch (the circumference of the pitch circle divided by the number of teeth):', type: 'float', initial: 8 },
            { name: 'pressureAngle', caption: 'Pressure Angle (common values are 14.5, 20 and 25 degrees):', type: 'float', initial: 20 },
            { name: 'clearance', caption: 'Clearance (minimal distance between the apex of a tooth and the trough of the other gear; in length units):', type: 'float', initial: 0.05 },
            { name: 'backlash', caption: 'Backlash (minimal distance between meshing gears; in length units):', type: 'float', initial: 0.05 },
            { name: 'profileShift', caption: 'Profile Shift (indicates what portion of gear one\'s addendum height should be shifted to gear two. E.g., a value of 0.1 means the adddendum of gear two is increased by a factor of 1.1 while the height of the addendum of gear one is reduced to 0.9 of its normal height):', type: 'float', initial: 0.0 },
            { name: 'gear1ToothCount', caption: 'Gear 1 Tooth Count (n1 > 0: external gear; n1 = 0: rack; n1 < 0: internal gear):', type: 'int', initial: 30 },
            { name: 'gear1PitchDiameter', caption: 'Gear 1 Pitch Circle Diameter', type: 'float', isCalculated: true },
            { name: 'gear1OuterDiameter', caption: 'Gear 1 Outer Circle Diameter', type: 'float', isCalculated: true },
            { name: 'gear1CenterHoleDiamater', caption: 'Gear 1 Center Hole Diameter (0 for no hole):', type: 'float', initial: 4 },
            { name: 'gear2ToothCount', caption: 'Gear 2 Tooth Count:', type: 'int', initial: 8 },
            { name: 'gear2PitchDiameter', caption: 'Gear 2 Pitch Circle Diameter', type: 'float', isCalculated: true },
            { name: 'gear2OuterDiameter', caption: 'Gear 2 Outer Circle Diameter', type: 'float', isCalculated: true },
            { name: 'gear2CenterHoleDiamater', caption: 'Gear 2 Center Hole Diameter (0 for no hole):', type: 'float', initial: 4 },
            { name: 'gearCentersDistance', caption: 'Gear Centers Distance', type: 'float', isCalculated: true },
            { name: 'showOption', caption: 'Show:', type: 'choice', values:[3, 1, 2], initial: 3, captions:["Gear 1 and 2", "Gear 1 Only", "Gear 2 Only"]},
          ];
        }
        */

        public void update() {
            var gearSet = new GearSet();
            parameters.injectFromUI(gearSet);
            var status = gearSet.update();
            if (!status.ok()) {
                alert(status.message);
                return;
            }
            parameters.writeToUI(gearSet);
            parameters.setQueryParameters();

            display(gearSet);
            exportSvg(gearSet);
        }

        public void display(GearSet gearSet) {
            drawing.clear();
            var topGroup = drawing.group();
            topGroup.panZoom();

            // Scale to fit with some border around
            var borderRatio = 0.02;
            var border = borderRatio * Math.max(gearSet.width, gearSet.height);
            var totalWidth = gearSet.width + 2 * border;
            var totalHeight = gearSet.height + 2 * border;

            var scalingFactor = Math.min(drawingWidth / totalWidth, drawingHeight / totalHeight);
            mainGroup = topGroup.group().scale(scalingFactor, scalingFactor).x(-gearSet.center.X).y(-gearSet.center.Y);
            mainGroup.dx(drawingWidth / scalingFactor / 2);
            mainGroup.dy(drawingHeight / scalingFactor / 2);

            //console.log(drawingWidth / scalingFactor / 2);
            //console.log(gearSet.width / 2);

            mainGroup.stroke(regularLinesStyle).fill('none');

            gearSet.createGraphics(mainGroup);
        }

        public void exportSvg(gearSet) {
            // For export we use a separate svg instance that is properly sized in mm.
            // The viewbox is always in px. 1 mm = 3.543307 px (see https://mpetroff.net/2013/08/analysis-of-svg-units/)
            var pxPerMillimeter = 3.543307;
            // We leave some space around the escapement drawing.
            var borderRatio = 0.05;
            var border = borderRatio * Math.max(gearSet.width, gearSet.height);
            var totalWidth = gearSet.width + 2 * border;
            var totalHeight = gearSet.height + 2 * border;
            var drawingForExport = SVG('drawingForExport')
                .size(totalWidth + 'mm', totalHeight + 'mm')
                .viewbox(gearSet.left - border, -gearSet.top - border, totalWidth, totalHeight);
            var topGroup = drawingForExport.group();

            gearSet.createGraphics(topGroup);
            var exportedSVG = drawingForExport.exportSvg({ whitespace: true });
            document.getElementById("exportedSVG").value = exportedSVG;

            // based on code from Andreas Köberle (http://stackoverflow.com/questions/10120975/how-to-save-an-svg-generated-by-raphael)
            var anchor = document.getElementById('downloadSVG');
            anchor.innerHTML = 'Download SVG';
            anchor.download =`involute_gear_${ gearSet.gear1.toothCount? gearSet.gear1.toothCount : 0} _to_${ gearSet.gear2.toothCount}.svg`;
            anchor.type = 'image/svg+xml';

            // see Eric Bidelman: http://updates.html5rocks.com/2012/06/Don-t-Build-Blobs-Construct-Them
            var blob = new Blob([exportedSVG], { type: 'image/svg+xml'});
            anchor.href = (window.URL || webkitURL).createObjectURL(blob);
        }
    }

    public class GearSet
    {
        public double diametralPitch;
        public double circularPitch;
        public int gear2ToothCount;
        public double gear2CenterHoleDiamater;
        public double gear2PitchDiameter;
        public double gear2OuterDiameter;
        public RegularGear gear2;
        public double clearance;
        public double backlash;
        public double profileShift;
        public double gear1PitchDiameter;
		private double gear1OuterDiameter;
		private double gearCentersDistance;
		private int gear1ToothCount;
		private RegularGear gear1;
		private double gear1CenterHoleDiamater;

        public void update() {
            // convert circular pitch to diametral pitch
            this.diametralPitch = Math.PI / this.circularPitch;

            // Gear 2 must always be a regular gear.
            if (this.gear2ToothCount < 3) {
                return Status.createError('Gear 2 must have at least three teeth.');
            }
            this.gear2 = new RegularGear(
              this,
              this.gear2ToothCount,
              this.gear2CenterHoleDiamater,
              this.clearance,
              this.backlash,
              this.profileShift
            );
            this.gear2PitchDiameter = this.gear2.pitchDiameter;
            this.gear2OuterDiameter = 2 * this.gear2.outerRadius;

            if (this.gear1ToothCount == 0) {
                // Create rack
                this.gear1 = new Rack(
                  this,
                  this.clearance,
                  this.backlash,
                  -this.profileShift
                );
                this.gear1PitchDiameter = 0;
                this.gear1OuterDiameter = 0;
                this.gearCentersDistance = 0;

                this.gear1.isLeft = true;
                this.gear2.isLeft = false;
            }

            if (this.gear1ToothCount > 0) {
                // Regular gear
                if (this.gear1ToothCount < 3) {
                    return Status.createError('External gear 1 must have at least three teeth.');
                }

                this.gear1 = new RegularGear(
                  this,
                  this.gear1ToothCount,
                  this.gear1CenterHoleDiamater,
                  this.clearance,
                  this.backlash,
                  -this.profileShift
                );
                this.gear1PitchDiameter = this.gear1.pitchDiameter;
                this.gear1OuterDiameter = 2 * this.gear1.outerRadius;
                this.gearCentersDistance = this.gear1.pitchRadius + this.gear2.pitchRadius;

                this.gear1.isLeft = true;
                this.gear2.isLeft = false;
            }

            if (this.gear1ToothCount < 0) {
                // Internal gear
                if (-this.gear1ToothCount - this.gear2ToothCount < 1) {
                    return Status.createError('Internal gear 1 must have at least one more tooth than gear 2.');
                }

                this.gear1 = new InternalGear(
                  this,
                  -this.gear1ToothCount,
                  this.clearance,
                  this.backlash,
                  -this.profileShift
                );
                this.gear1PitchDiameter = this.gear1.pitchDiameter;
                this.gear1OuterDiameter = 2 * this.gear1.outerRadius;
                this.gearCentersDistance = this.gear1.pitchRadius - this.gear2.pitchRadius;

                this.gear1.isLeft = false;
                this.gear2.isLeft = true;
            }

            this.gear1.connectedGear = this.gear2;
            this.gear2.connectedGear = this.gear1;

            this.gear1.center = ORIGIN;
            this.gear2.center = this.calcGear2Center();

            this.gear1.update();
            this.gear2.update();

            this.topLeft = createPoint(Math.min(this.gear1.topLeft.X, this.gear2.topLeft.X), Math.max(this.gear1.topLeft.Y, this.gear2.topLeft.Y));
            this.lowerRight = createPoint(Math.max(this.gear1.lowerRight.X, this.gear2.lowerRight.X), Math.min(this.gear1.lowerRight.Y, this.gear2.lowerRight.Y));

            this.width = this.lowerRight.X - this.topLeft.X;
            this.height = this.topLeft.Y - this.lowerRight.Y;
            this.center = multiplyVector(0.5, addVectors(this.topLeft, this.lowerRight));

            this.left = this.topLeft.X;
            this.top = this.topLeft.Y;

            return Status.OK;
        }

        public void calcGear2Center() {
            if (this.gear1ToothCount == 0) {
                // Rack
                return addVectors(this.gear1.center, createPoint(this.gear2.pitchRadius, 0));
            }
            if (this.gear1ToothCount > 0) {
                // Regular gear
                return addVectors(this.gear1.center, createPoint(this.gear1.pitchRadius + this.gear2.pitchRadius, 0));
            }
            if (this.gear1ToothCount < 0) {
                // Inner gear
                return addVectors(this.gear1.center, createPoint(this.gear1.pitchRadius - this.gear2.pitchRadius, 0));
            }
        }

        public void createGraphics(parent) {
            var crossMarkerLength = Math.min(this.circularPitch / 2, this.width / 50);
            if ((this.showOption & 2) > 0) {
                // show gear 2
                this.gear2.createGraphics(parent, crossMarkerLength);
            }
            if ((this.showOption & 1) > 0) {
                // show gear 1
                this.gear1.createGraphics(parent, crossMarkerLength);
            }
        }
    }

    public class RegularGear {
        public GearSet gearSet;
		public int toothCount;
		public double centerHoleDiameter;
        public double clearance;
        public double backlash;
        public double profileShift;
        public double addendumExtension;
        public double angle;
        public double pitchDiameter;
        public double pitchRadius;
        public double addendum;
        public double shiftedAddendum;
        public double outerRadius;
        public double angleToothToTooth;

		public RegularGear(GearSet gearSet, int toothCount, double centerHoleDiameter, double clearance, double backlash, double profileShift, double addendumExtension = 0) {
        this.gearSet = gearSet;
        this.toothCount = toothCount;

        this.centerHoleDiameter = centerHoleDiameter;
        this.clearance = clearance;
        this.backlash = backlash;
        this.profileShift = profileShift;

        // addendumExtension is only set for a pinion that is used as a cutter of an inner gear. In this case the 
        // addendum extension creates the clearance between the inner gear and its pinion.
        this.addendumExtension = addendumExtension;

        this.angle = 0; // angle in rad of the complete gear (changes during rotation animation)

        // Pitch diameter: Diameter of pitch circle.
        this.pitchDiameter = this.toothCount / this.gearSet.diametralPitch;
        this.pitchRadius = this.pitchDiameter / 2;

        // Addendum: Radial distance from pitch circle to outside circle.
        this.addendum = 1 / this.gearSet.diametralPitch;
        
        // Typically no profile shift is used meaning that this.shiftedAddendum = this.addendum 
        this.shiftedAddendum = this.addendum * (1 + this.profileShift);

        //Outer Circle
        this.outerRadius = this.pitchRadius + this.shiftedAddendum + this.addendumExtension;
        this.angleToothToTooth = degreeToRad(360 / this.toothCount);
      }

        double degreeToRad(double degrees)
		{
            return MathHelper.DegreesToRadians(degrees);
		}

      public void update() {
        this.topLeft = addVectors(createPoint(-this.outerRadius, this.outerRadius), this.center);
        this.lowerRight = addVectors(createPoint(this.outerRadius, -this.outerRadius), this.center);

        this.toothPointsTemplate = this.createToothPath();
      }

      createGraphics(parent, crossMarkerLength) {
        var gearGroup = parent.group();
        var helperGroup = gearGroup.group();
        helperGroup.stroke(helperLinesStyle).fill('none');
        // Pitch circle
        drawCircle(helperGroup, ORIGIN, this.pitchRadius);
        // Outer circle
        drawCircle(helperGroup, ORIGIN, this.outerRadius);

        drawCross(helperGroup, ORIGIN, crossMarkerLength);

        var regularGroup = gearGroup.group();
        regularGroup.stroke(regularLinesStyle).fill('none');

        if (this.centerHoleDiameter > 0) {
          drawCircle(regularGroup, ORIGIN, this.centerHoleDiameter / 2);
        }

        //var {cutterPath, lowerLeftCorner} = this.createToothCutter();
        //insertSvgPath(regularGroup, cutterPath, /* isClosed=*/true);

        //var toothSectorPath = this.createToothSectorPath();
        //insertSvgPath(regularGroup, toothSectorPath, /* isClosed=*/true);

        //var {cutterPaths, lowerLeftCornerIndex} = this.createToothCutterPaths();
        //cutterPaths.forEach(toothCutterPath => insertSvgPath(regularGroup, toothCutterPath, /* isClosed=*/true));

        //var cornersPath = [];
        //cutterPaths.forEach(toothCutterPath => cornersPath.push(clonePoint(toothCutterPath[lowerLeftCornerIndex])));

        //var helperGroup2 = gearGroup.group();
        //helperGroup2.stroke(markerLinesStyle).fill('none');
        //drawCircles(helperGroup2, cornersPath, 0.0001);
        //insertSvgPath(helperGroup2, cornersPath, /* isClosed=*/true)


        //var halfToothPath = this.createHalfToothPath();
        //insertSvgPath(regularGroup, halfToothPath, /* isClosed=*/false);
        //drawCircles(helperGroup, halfToothPath, 0.0002);
        //drawCircle(helperGroup, toothCutoutPath[dedendumStartIndex], 0.1);

        //var toothPath = this.createToothPath();
        //insertSvgPath(regularGroup, toothPath, /* isClosed=*/false);
        //drawCircles(helperGroup, toothPath, 0.01);

        //var nextIndex = (dedendumStartIndex - 1) % toothCutoutPath.length;
        //drawCircle(helperGroup, toothCutoutPath[nextIndex], 0.1);

        //var corners = [];
        //cutterPaths.forEach(toothCutterPath => corners.push(toothCutterPath[lowerLeftCornerIndex]));
        //drawCircles(helperGroup, corners, 0.1);

        this.insertGearSvgPath(regularGroup);

        gearGroup.move(this.center.X, this.center.Y);
      }

      public void createToothSectorPath() {
        // create outer circle sector covering one tooth
        return [
          ORIGIN,
          createPoint(0, this.outerRadius),
          rotatePointAroundCenter(createPoint(0, this.outerRadius), ORIGIN, this.angleToothToTooth)
        ];
      }

        public void insertGearSvgPath(group) {
        var svgPath = group.path();
        var firstSvgPoint;
        // Next create N (tooth count) rotated tooth paths and connect them via arcs.
        var angleOffset;
        if (this.isLeft) {
          // rotate counter clockwise so that the starter tooth points east.
          angleOffset = -Math.PI / 2 - this.angleToothToTooth / 2;
        } else {
          // rotate clock wise so that the starter tooth meshes with the left gear.
          angleOffset = Math.PI / 2 - this.angleToothToTooth;
        }

        for (var i = 0; i < this.toothCount; i++) {
            var angle = i * this.angleToothToTooth + angleOffset;
            var rotatedToothPoints = rotatePointsAroundCenter(this.toothPointsTemplate, ORIGIN, angle);

          if (i == 0) {
            // Start with the second point since the closing arc of the last tooth will add the first point.
            addLineSegmentsToPath(svgPath, rotatedToothPoints.slice(1), /*moveToFirst=*/ true);
            firstSvgPoint = createSvgPoint(rotatedToothPoints[0]);
          } else {
            // connect the previous last point with an arc to the new, rotated tooth points.
            svgPath.A(this.outerRadius, this.outerRadius, 0, 0, 1, createSvgPoint(rotatedToothPoints[0]));
            addLineSegmentsToPath(svgPath, rotatedToothPoints.slice(1));
          }
        }

        // Close the path by connecting the final arc.
        svgPath.A(this.outerRadius, this.outerRadius, 0, 0, 1, firstSvgPoint);
        // Close the path
        svgPath.Z();

        return svgPath;
      }

        public void createToothPath() {
        var halfToothPath = this.createHalfToothPath();

        var toothPath = [];
        // Add mirrored half tooth
        for (var i = halfToothPath.length - 1; i > 0; i--) {
            var point = halfToothPath[i];
          toothPath.push(createPoint(-point.X, point.Y));
        }
        // Add the unmirrored original half.
        halfToothPath.forEach(point => toothPath.push(point));

        return toothPath;
      }

        public void createHalfToothPath() {
        var toothCutoutPath = this.createToothCutoutPath();

        // Intersect with a slice that is half the pitch angle.
        var angle = this.angleToothToTooth / 2;
        var cosAngle = Math.cos(angle);
        var sinAngle = Math.sin(angle);

        var halfPointOnCircle = {X: -this.outerRadius * sinAngle, Y: this.outerRadius * cosAngle};
    var tangentIntercept = {
          X: 0,
          Y: this.outerRadius * (cosAngle + sinAngle * sinAngle / cosAngle)
        };

    var intersectPath = [
          ORIGIN,
          halfPointOnCircle,
          tangentIntercept
        ];
        ClipperLib.JS.ScaleUpPath(intersectPath,  CLIPPER_SCALE);

        var clipper = new ClipperLib.Clipper();
        clipper.AddPath(toothCutoutPath, ClipperLib.PolyType.ptSubject, true);  // true means closed path;
        clipper.AddPath(intersectPath, ClipperLib.PolyType.ptClip, true);  // true means closed path;

        var solutionPaths = new ClipperLib.Paths();
        var succeeded = clipper.Execute(ClipperLib.ClipType.ctIntersection, solutionPaths, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);

        var lightenedPaths = ClipperLib.JS.Lighten(solutionPaths[0], this.gearSet.circularPitch * CLIPPER_LIGHTEN_FACTOR * CLIPPER_SCALE);
        var clippedToothCutoutPath = lightenedPaths[0];

        ClipperLib.JS.ScaleDownPath(clippedToothCutoutPath, CLIPPER_SCALE);

        // Find dedendum start point at x == 0;
        var dedendumStartIndex = clippedToothCutoutPath.findIndex((point) => Math.abs(point.X) < 0.01 * this.addendum && point.Y < this.pitchRadius);
    //console.log("dedendumStartIndex: ", dedendumStartIndex);

    // Start from the dedendumStartIndex and iterate over all points until the next point is outside of the outer radius.
    var halfToothPath = [clippedToothCutoutPath[dedendumStartIndex]];
        var currentIndex = dedendumStartIndex;
    var squaredOuterRadius = this.outerRadius * this.outerRadius;
    var getNextIndex = (index) => (index - 1 + clippedToothCutoutPath.length) % clippedToothCutoutPath.length
        while (true) {
          var nextIndex = getNextIndex(currentIndex);
          if (squaredLenth(clippedToothCutoutPath[nextIndex]) >= squaredOuterRadius) {
            break;
          }
          currentIndex = nextIndex;
          halfToothPath.push(clippedToothCutoutPath[currentIndex]);
        }

// Interpolate between the last point in the trimmed path and the next point
// to find the point that intersects with the outer radius.
var lastInsidePoint = clippedToothCutoutPath[currentIndex];
var lastInsideLength = length(lastInsidePoint);

var firstOnOrOutsidePoint = clippedToothCutoutPath[getNextIndex(currentIndex)];
var firstOnOrOutsideLength = length(firstOnOrOutsidePoint);

var ratio = (this.outerRadius - lastInsideLength) / (firstOnOrOutsideLength - lastInsideLength);

var vectorBetweenPoints = subtractVectors(firstOnOrOutsidePoint, lastInsidePoint);
var pointOnOuterRadius = addVectors(lastInsidePoint, multiplyVector(ratio, vectorBetweenPoints));

        halfToothPath.push(pointOnOuterRadius);

        return halfToothPath;
      }

      createToothCutoutPath() {
    var { cutterPaths, lowerLeftCornerIndex} = this.createToothCutterPaths();

    // Also create a path from one of the addendum corners to get smooth undercut curves.
    var cornersPath = [];
        cutterPaths.forEach(toothCutterPath => cornersPath.push(clonePoint(toothCutterPath[lowerLeftCornerIndex])));
        cornersPath.reverse();

        const combinedPaths = [...cutterPaths];
        combinedPaths.push(cornersPath);

        var clipper = new ClipperLib.Clipper();
        combinedPaths.forEach(path => {
          ClipperLib.JS.ScaleUpPath(path,  CLIPPER_SCALE);
          clipper.AddPath(path, ClipperLib.PolyType.ptSubject, true);  // true means closed path;
        });

        // Union the shapes of all the tooth cutter paths.
        var solutionPaths = new ClipperLib.Paths();
        var succeeded = clipper.Execute(ClipperLib.ClipType.ctUnion, solutionPaths, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);

        return solutionPaths[0];
      }

      createToothCutterPaths() {
    var angleStepSize = Math.PI / 600;

    var { cutterPath, lowerLeftCornerIndex} = this.createToothCutter();
    var cutterPaths = [cutterPath];
        
        // To create the tooth profile we move the (virtual) infinite gear and then turn the resulting cutter position back. 
        // For illustration see http://lcamtuf.coredump.cx/gcnc/ch6/, section 'Putting it all together'.
        // We continue until the moved tooth cutter's lower left corner is outside of the outer circle of the gear.
        // Going any further will no longer influence the shape of the tooth.
        var stepCounter = 0;
        while (true) {
        var angle = stepCounter * angleStepSize;
        var xTranslation = angle * this.pitchRadius;

          // we move in both directions
          var transformedCutterPath = createTranslatedPath(cutterPath, xTranslation, 0);
          transformedCutterPath = rotatePointsAroundCenter(transformedCutterPath, ORIGIN, angle);

          cutterPaths.push(transformedCutterPath)

          // Rotate in opposite direction. This is required to get the undercuts.
          //console.log("xTranslation: " + xTranslation);
          transformedCutterPath = createTranslatedPath(cutterPath, -xTranslation, 0);
          transformedCutterPath = rotatePointsAroundCenter(transformedCutterPath, ORIGIN, -angle);

          cutterPaths.unshift(transformedCutterPath);

          //var movedLowerLeftCorner = createTranslatedPoint(lowerLeftCorner, xTranslation, 0);
          //movedLowerLeftCorner = rotatePointAroundCenter(movedLowerLeftCorner, ORIGIN, angle);
          
          if (length(transformedCutterPath[lowerLeftCornerIndex]) > this.outerRadius) {
            // The cutter is now completely outside the gear and additional steps will no longer influences the shape of the gear tooth.
            break;
          }

          stepCounter++;
       }

       return {
          cutterPaths: cutterPaths,
          lowerLeftCornerIndex: lowerLeftCornerIndex 
        }
      }

public void createToothCutter() {
    // we create a trapezoidal cutter as described at http://lcamtuf.coredump.cx/gcnc/ch6/ under the section 'Putting it all together'
    var toothWidth = this.gearSet.circularPitch / 2;
    //console.log("toothWidth: " + toothWidth);
    //console.log("addendum: " + this.addendum);
    //console.log("shiftedAddendum: " + this.shiftedAddendum);
    //console.log("clearance: " + this.clearance);

    var cutterDepth = this.addendum + this.clearance;
    var cutterOutsideLength = 3 * this.addendum;
    //console.log("cutterDepth: " + cutterDepth);
    //console.log("cutterOutsideLength: " + cutterOutsideLength);

    var cosPressureAngle = Math.cos(this.gearSet.pressureAngle * Math.PI / 180);
    var tanPressureAngle = Math.tan(this.gearSet.pressureAngle * Math.PI / 180);

    // If a positive backlash is defined then we widen the trapezoid accordingly.
    // Each side of the tooth needs to widened by a fourth of the backlash (perpendiculr to cutter faces).
    var dx = this.backlash / 4 / cosPressureAngle;

    // Create the cutout at 6 o'clock position pointing upwards
    var yBottom = this.pitchRadius + this.profileShift * this.addendum - cutterDepth;
    var yTop = this.pitchRadius + this.profileShift * this.addendum + cutterOutsideLength;

    var lowerRightCorner = createPoint(toothWidth / 2 + dx - tanPressureAngle * cutterDepth, yBottom);
    var upperRightCorner = createPoint(toothWidth / 2 + dx  + tanPressureAngle * cutterOutsideLength, yTop);
    var upperLeftCorner = createPoint(-upperRightCorner.X, upperRightCorner.Y);
    var lowerLeftCorner = createPoint(-lowerRightCorner.X, lowerRightCorner.Y);

    var cutterPath = [lowerLeftCorner, upperLeftCorner, upperRightCorner, lowerRightCorner];

        return {
          cutterPath: cutterPath,
          lowerLeftCornerIndex: 0 
        }
      }
    }

    public class InternalGear {
      public InternalGear(GearSet gearSet, int toothCount, double clearance, double backlash, double profileShift) {
        this.gearSet = gearSet;
        this.toothCount = toothCount;
        this.clearance = clearance;
        this.backlash = backlash;
        this.profileShift = profileShift;

        this.angle = 0; // angle in rad of the complete gear (changes during rotation animation)

        // Pitch diameter: Diameter of pitch circle.
        this.pitchDiameter = this.toothCount / this.gearSet.diametralPitch;
        this.pitchRadius = this.pitchDiameter / 2;

        // Addendum: Radial distance from pitch circle to inside circle.
        this.addendum = 1 / this.gearSet.diametralPitch;
        
        // Typically no profile shift is used meaning that this.shiftedAddendum = this.addendum 
        this.shiftedAddendum = this.addendum * (1 + this.profileShift);

        // Inner Circle (addendum)
        this.innerRadius = this.pitchRadius - this.shiftedAddendum;

        // Dedendum Circle
        this.dedendumRadius = this.pitchRadius + this.shiftedAddendum;

        this.angleToothToTooth = degreeToRad(360 / this.toothCount);

        // Outer circle; just a circle that is greater than the dedendum circle.
        this.outerRadius = this.pitchRadius + 2.5 * this.addendum;
      }

    public void update() {
        this.topLeft = addVectors(createPoint(-this.outerRadius, this.outerRadius), this.center);
        this.lowerRight = addVectors(createPoint(this.outerRadius, -this.outerRadius), this.center);

        this.pinion = this.connectedGear;
        this.toothPointsTemplate = this.createToothPath();
      }

    public void createGraphics(parent, crossMarkerLength) {
        var gearGroup = parent.group();
        var helperGroup = gearGroup.group();
        helperGroup.stroke(helperLinesStyle).fill('none');
        // Inner circle (addendum)
        drawCircle(helperGroup, ORIGIN, this.innerRadius);
        // Pitch circle
        drawCircle(helperGroup, ORIGIN, this.pitchRadius);
        // Dedendum circle
        drawCircle(helperGroup, ORIGIN, this.dedendumRadius);

        drawCross(helperGroup, ORIGIN, crossMarkerLength);

        //var zeroedHalfCutterPath = this.createZeroedHalfToothCutterPath();
        //insertSvgPath(helperGroup, zeroedHalfCutterPath, true);

        //var halfToothSectorPath = this.createHalfToothSectorPath();
        //insertSvgPath(helperGroup, halfToothSectorPath, /* isClosed=*/true);

        var regularGroup = gearGroup.group();
        regularGroup.stroke(regularLinesStyle).fill('none');

        //var halfToothPath = this.createEnlargedPinionHalfToothPath();
        //insertSvgPath(regularGroup, halfToothPath, /* isClosed=*/false);

        //var zeroedCutterPath = this.createZeroedCutterPath();
        //insertSvgPath(regularGroup, zeroedCutterPath, false);

        var helperGroup2 = gearGroup.group();
        helperGroup2.stroke(helperLinesStyle2).fill('none');

        //var zeroedHalfCutterPath = this.createZeroedHalfToothCutterPath();
        //insertSvgPath(helperGroup2, zeroedHalfCutterPath, true);

        //var halfToothSectorPath = this.createHalfToothSectorPath();
        //insertSvgPath(helperGroup2, halfToothSectorPath, true);

        //var cutterPaths = this.createHalfToothCutterPaths(zeroedHalfCutterPath);
        //cutterPaths.forEach(path => insertSvgPath(helperGroup2, path, true));

        //var cornersPaths = this.createCornersPaths(cutterPaths);
        //cornersPaths.forEach(path => insertSvgPath(helperGroup, path, true));

        var markerGroup = gearGroup.group();
        markerGroup.stroke(markerLinesStyle).fill('none');

        /*
        cornersPaths.forEach(path => {
          drawCircle(markerGroup, path[0], 0.01);
          drawCircle(markerGroup, path.slice(-1)[0], 0.01);
        });
        cornersPaths[0].slice(0,5).forEach(point => {
          drawCircle(markerGroup, point, 0.01);
        });
        */

        //var edgesPath = this.createEdgesPath(cutterPaths);
        //insertSvgPath(markerGroup, edgesPath, true);
        //drawCircles(markerGroup, edgesPath, 0.02);

        //var halfToothPath = this.createHalfToothPath();
        //insertSvgPath(markerGroup, halfToothPath, false);
        //drawCircle(markerGroup, halfToothPath[0], 0.01);
        //drawCircle(markerGroup, halfToothPath.slice(-1)[0], 0.01);

        //var toothPath = this.createToothPath();
        //insertSvgPath(markerGroup, this.toothPointsTemplate, false);

        this.insertGearSvgPath(regularGroup);

        // Outer circle
        drawCircle(regularGroup, ORIGIN, this.outerRadius);

        gearGroup.move(this.center.X, this.center.Y);
      }

    public void insertGearSvgPath(group) {
        var svgPath = group.path();

        var firstSvgPoint;
        var angleOffset;
        if (this.isLeft) {
          // rotate counter clockwise so that the starter tooth points east.
          angleOffset = Math.PI - this.angleToothToTooth / 2;
        } else {
          // rotate clock wise so that the starter tooth meshes with the left gear.
          angleOffset = -this.angleToothToTooth / 2;
        }
        for (var i = 0; i < this.toothCount; i++) {
            var angle = -i * this.angleToothToTooth + angleOffset;
            var rotatedToothPoints = rotatePointsAroundCenter(this.toothPointsTemplate, ORIGIN, angle);

          if (i == 0) {
            // Start with the second point since the closing arc of the last tooth will add the first point.
            addLineSegmentsToPath(svgPath, rotatedToothPoints.slice(1), /*moveToFirst=*/ true);
            firstSvgPoint = createSvgPoint(rotatedToothPoints[0]);
          } else {
            // connect the previous last point with an arc to the new, rotated tooth points.
            svgPath.A(this.outerRadius, this.outerRadius, 0, 0, 1, createSvgPoint(rotatedToothPoints[0]));
            addLineSegmentsToPath(svgPath, rotatedToothPoints.slice(1));
          }
        }

        // Close the path by connecting the final arc.
        svgPath.A(this.outerRadius, this.outerRadius, 0, 0, 1, firstSvgPoint);
        // Close the path
        svgPath.Z();

        return svgPath;
      }

      createHalfToothSectorPath() {
        // create outer circle sector covering half a tooth
        return [
          ORIGIN,
          createPoint(this.outerRadius, 0),
          rotatePointAroundCenter(createPoint(this.outerRadius, 0), ORIGIN, -this.angleToothToTooth / 2)
        ];
      }

      createCornersPaths(cutterPaths) {
        // Create a paths from each of the corners to avoid ragged edges.
        var cornersPaths = [];
        // Ignore the last point which is always the origin.
        for (var i = 0; i < cutterPaths[0].length - 1; i++) {
            var cornersPath = [];
          cutterPaths.forEach(cutterPath => cornersPath.push(clonePoint(cutterPath[i])));
          cornersPaths.push(cornersPath);
        }

        return cornersPaths;
      }

      createEdgesPath(cutterPaths) {
        // This step is described in the accompanying pdf doc.
        // Conceptually, first we create shapes by connecting correponding points from the various cutter paths.
        // Then we connect the top most endpoints of these shapes.
        // Finally we add the top half of the rightmost shape.

        // Create a paths from each of the corners to avoid ragged edges.
        var edgesPath = [ORIGIN];
        // Ignore the last point which is always the origin.
        for (var i = cutterPaths[0].length - 1; i > 0; i--) {
            var endPoint1 = cutterPaths[0][i];
            var endPoint2 = cutterPaths.slice(-1)[0][i];

          if (endPoint1.Y < endPoint2.Y) {
            edgesPath.push(clonePoint(endPoint1));
          } else {
            edgesPath.push(clonePoint(endPoint2));
          }
        }

        // Add the top half of the points of the rightmost shape.
        for (var i = 0; i < (cutterPaths.length + 1) / 2; i++) {
          edgesPath.push(clonePoint(cutterPaths[i][0]));
        }
        
        return edgesPath;
      }

      createToothPath() {
        var halfToothPath = this.createHalfToothPath();
        var rotatedHalfToothPath = rotatePointsAroundCenter(halfToothPath, ORIGIN, this.angleToothToTooth / 2);

        var toothPath = [...rotatedHalfToothPath];
        // Add mirrored half tooth
        for (var i = rotatedHalfToothPath.length - 1; i > 0; i--) {
            var point = rotatedHalfToothPath[i];
          toothPath.push(createPoint(point.X, -point.Y));
        }

        return toothPath;      }

      createHalfToothPath() {
        var zeroedHalfCutterPath = this.createZeroedHalfToothCutterPath();
        var cutterPaths = this.createHalfToothCutterPaths(zeroedHalfCutterPath);
        // Also create a paths from each of the corners to avoid ragged edges.
        var cornersPaths = this.createCornersPaths(cutterPaths);
        var edgesPath = this.createEdgesPath(cutterPaths);
        var halfToothSectorPath = this.createHalfToothSectorPath();

        // Scale them all up to prepare for union and interesction operations.
        cutterPaths.forEach(path => {
          ClipperLib.JS.ScaleUpPath(path, CLIPPER_SCALE);
        });
        cornersPaths.forEach(path => {
          ClipperLib.JS.ScaleUpPath(path, CLIPPER_SCALE);
        });
        ClipperLib.JS.ScaleUpPath(edgesPath, CLIPPER_SCALE);
        // The edges path often self intersects. If needed split into non-intersecting paths.
        var nonIntersectingEdgesPaths = ClipperLib.Clipper.SimplifyPolygon(edgesPath, ClipperLib.PolyFillType.pftNonZero);
        ClipperLib.JS.ScaleUpPath(halfToothSectorPath, CLIPPER_SCALE);

        // Union all these shapes then clip with the half tooth sector path.
        var cutoutFromAllPaths = this.unionAndClipPaths([...cutterPaths, ...cornersPaths, ...nonIntersectingEdgesPaths], halfToothSectorPath);

        // Remove the origin and order the points so that the point intersecting with the addendum is the first point.
        var indexOfOrigin;
        for (var i = 0; i < cutoutFromAllPaths.length; i++) {
            var point = cutoutFromAllPaths[i];
          if (point.X == 0 && point.Y == 0) {
            indexOfOrigin = i;
            break;
          }
        }
        console.log(`Index of origin: ${indexOfOrigin}`);
        var maxSquaredRadius = 0;
        var indexMaxSquaredRadius = 0;
        var halfToothPath = [];
        for (var i = 1; i < cutoutFromAllPaths.length; i++) {
            var point = cutoutFromAllPaths[(indexOfOrigin + i) % cutoutFromAllPaths.length];
            var squaredRadius = squaredLenth(point);
          if (squaredRadius > maxSquaredRadius) {
            maxSquaredRadius = squaredRadius;
            indexMaxSquaredRadius = i - 1;
          }
          halfToothPath.push(point);
        }
        console.log(`Index of max radius point: ${indexMaxSquaredRadius}`);

        // Shave off the part beyond the max radius point.
        halfToothPath = halfToothPath.slice(0, indexMaxSquaredRadius + 1);

        var lightenedPaths = ClipperLib.JS.Lighten(halfToothPath, this.gearSet.circularPitch * CLIPPER_LIGHTEN_FACTOR * CLIPPER_SCALE);
        var lightenedHalfToothPath = lightenedPaths[0];
        console.log(`Length of lightened half tooth path: ${lightenedHalfToothPath.length}`);

        ClipperLib.JS.ScaleDownPath(lightenedHalfToothPath, CLIPPER_SCALE);
        return lightenedHalfToothPath.reverse();
      }

      unionAndClipPaths(scaledUpPaths, scaledUpHalfToothSectorPath) {
        var orientation = ClipperLib.Clipper.Orientation(scaledUpHalfToothSectorPath);

        var clipper = new ClipperLib.Clipper();
        scaledUpPaths.forEach(path => {
          // Make sure the orientation matches.
          if (ClipperLib.Clipper.Orientation(path) != orientation) {
            path.reverse();
          }
          clipper.AddPath(path, ClipperLib.PolyType.ptSubject, true);  // true means closed path;
        });

        // Union the shapes of all paths.
        var solutionPaths = new ClipperLib.Paths();
        var succeeded = clipper.Execute(ClipperLib.ClipType.ctUnion, solutionPaths, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);

        clipper = new ClipperLib.Clipper();
        clipper.AddPath(scaledUpHalfToothSectorPath, ClipperLib.PolyType.ptSubject, true);  // true means closed path;
        clipper.AddPath(solutionPaths[0], ClipperLib.PolyType.ptClip, true);  // true means closed path;

        solutionPaths = new ClipperLib.Paths();
        succeeded = clipper.Execute(ClipperLib.ClipType.ctIntersection, solutionPaths, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);
        return solutionPaths[0];
      }

      createHalfToothCutterPaths(zeroedHalfCutterPath) {
        var gearRatio = this.toothCount / this.pinion.toothCount;

        // To create the tooth profile we move the (virtual) infinite gear and then turn the resulting cutter position back. 
        // For illustration see http://lcamtuf.coredump.cx/gcnc/ch6/, section 'Putting it all together'.
        // We continue until the moved tooth cutter's lower left corner is outside of the outer circle of the gear.
        // Going any further will no longer influence the shape of the tooth.
        var angleStepSize = this.angleToothToTooth / 10;
        //var angleStepSize = Math.PI / Math.sqrt(this.toothCount - this.pinion.toothCount) / halfCount;
        var cutterPaths = [];

        var pinionRotationAngle = 0;
        var rotatedCutter = this.createRotatedCutter(zeroedHalfCutterPath, pinionRotationAngle, gearRatio);

        cutterPaths.push(rotatedCutter);

        var desiredSquaredDistance = (this.gearSet.circularPitch / 25) ** 2;

        // Empirical value for when a half tooth is covered.
        var maxPinionRotationAngle =  Math.PI / Math.sqrt(this.toothCount - this.pinion.toothCount);

        var cutterEndpointIndex = zeroedHalfCutterPath.length - 1;
        var outerCounter = 0;
        while(outerCounter < 200 && pinionRotationAngle < maxPinionRotationAngle) {
            var previousRotatedCutter = rotatedCutter;
            var previousPinionRotationAngle = pinionRotationAngle;
          //console.log(`Outer counter ${outerCounter}; pinionRotationAngle: ${pinionRotationAngle}; maxPinionRotationAngle: ${maxPinionRotationAngle}`);
          outerCounter++;

            var searchingUp = true;
            var innerCounter = 0;
          while(innerCounter < 20) {
            //console.log(`  inner counter ${innerCounter}`);
            innerCounter++;
            pinionRotationAngle = previousPinionRotationAngle + angleStepSize;
            rotatedCutter = this.createRotatedCutter(zeroedHalfCutterPath, pinionRotationAngle, gearRatio);

                // We look at the start and end point of each cutter path. We dtermine rotation angles so that the squared distance between
                // consecutive start and end points is below an accuracy threshold. 
                var squaredDistanceStartPoint = squaredDistance(previousRotatedCutter[0], rotatedCutter[0]);
                var squaredDistanceEndPoint = squaredDistance(previousRotatedCutter[cutterEndpointIndex], rotatedCutter[cutterEndpointIndex]);
                var actualSquaredDistance = Math.max(squaredDistanceStartPoint, squaredDistanceEndPoint);
            if (actualSquaredDistance < desiredSquaredDistance / 10) {
              if (!searchingUp) {
                // We crossed from too big an angle to one that is small enough.
                break;
              } else {
                angleStepSize = 2 * angleStepSize;
              }
            } else if (squaredDistance < desiredSquaredDistance) {
              // new angle is okay
              break;
            } else {
              // squared distance is too large
              searchingUp = false;
              angleStepSize = 0.5 * angleStepSize;
            }
          }
          cutterPaths.push(rotatedCutter);
          cutterPaths.unshift(this.createRotatedCutter(zeroedHalfCutterPath, -pinionRotationAngle, gearRatio));
        }

        console.log(`Cutter paths count: ${cutterPaths.length}`);
        return cutterPaths;
      }

      createRotatedCutter(zeroedHalfCutterPath, pinionRotationAngle, gearRatio) {
        // Rotating the pinion by an angle will also roll it along the inner side of the outer ring gear.
        // First rotate the pinion.
        // This results in the center of the pinion to be rotated around the center of the ring gear.
        var pinionCenterRayAngle = -pinionRotationAngle / gearRatio;

        var transformedCutterPath = rotatePointsAroundCenter(zeroedHalfCutterPath, ORIGIN, pinionCenterRayAngle);
        var rotatedPinionCenter = rotatePointAroundCenter(this.pinion.center, ORIGIN, pinionCenterRayAngle);

        // The pinion turns into the opposite direction.
        var rotatedCutter = rotatePointsAroundCenter(transformedCutterPath, rotatedPinionCenter, pinionRotationAngle);
        // Close the half tooth profile.
        rotatedCutter.push(ORIGIN);
        return rotatedCutter;
      }

      createZeroedHalfToothCutterPath() {
        var enlargedPinionHalfToothPath = this.createEnlargedPinionHalfToothPath();

        // Rotate so that the half tooth points to 3 o'clock. Then shift so that it mashes with the internal gear.
        var rotatedHalfToothPath = rotatePointsAroundCenter(enlargedPinionHalfToothPath, ORIGIN, -Math.PI / 2 - this.pinion.angleToothToTooth / 2);
        return createTranslatedPath(rotatedHalfToothPath, this.pinion.center.X, 0).reverse();
      }

      createEnlargedPinionHalfToothPath() {
        // To cut the internal gear teeth, the actual pinion comes close but we need to enlarge it to properly cater for clearance and backlash
        var enlargedPinion = new RegularGear(
          this.gearSet,
          this.pinion.toothCount,
          /* centerHoleDiameter = */ 0,
          /* clearance = */ 0,
          -this.pinion.backlash,
          this.pinion.profileShift,
          /* addendumExtension = */ this.pinion.clearance
        );

        return enlargedPinion.createHalfToothPath();
        //return enlargedPinion.createToothPath();
      }
    }

    class Rack {
      constructor(gearSet, clearance, backlash, profileShift) {
        this.gearSet = gearSet;
        this.clearance = clearance;
        this.backlash = backlash;
        this.profileShift = profileShift;

        this.addendum = 1 / this.gearSet.diametralPitch;
      }

      update() {
        this.rackShape = this.createRackShape();

        var minX = this.rackShape[this.rackShape.length - 1].X;
        var maxX = this.rackShape[1].X;
        this.topLeft = addVectors(createPoint(minX, this.gearSet.diametralPitch / 2), this.center);
        this.lowerRight = addVectors(createPoint(maxX, -this.gearSet.diametralPitch / 2), this.center);
      }

      createRackShape() {
        var rackToothTemplate = this.createRackTooth();

        // The template is a tooth pointing right and centered on the x-axis.
        // We build the rack by starting with the template and then create an equal number
        // of teeth above and below.

        var rackShape = [];
        var halfCount = Math.floor(this.connectedGear.outerRadius / this.gearSet.circularPitch);
        for (var i = -halfCount; i < halfCount + 1; i++) {
          rackToothTemplate.forEach(point => {
              var deltaY = -i * this.gearSet.circularPitch;
            rackShape.push(createTranslatedPoint(point, 0, deltaY));
          });
        }

        // create a bar backing the teeth.
        var width = 0.5 * this.gearSet.circularPitch;
        var lowerLeftBacking = createTranslatedPoint(rackShape[rackShape.length - 1], -width, 0);
        var upperLeftBacking = createTranslatedPoint(rackShape[0], -width, 0);
        rackShape.push(lowerLeftBacking);
        rackShape.push(upperLeftBacking);

        return rackShape;
        //return rackToothTemplate;
      }

      createRackTooth() {
        // we create a trapezoidal cutter as described at http://lcamtuf.coredump.cx/gcnc/ch6/ under the section 'Putting it all together'
        var toothWidth = this.gearSet.circularPitch / 2;

        //console.log("toothWidth: " + toothWidth);
        //console.log("addendum: " + this.addendum);
        //console.log("shiftedAddendum: " + this.shiftedAddendum);
        //console.log("clearance: " + this.clearance);

        var toothDepth = this.addendum + this.clearance;
        //console.log("toothDepth: " + toothDepth);

        var cosPressureAngle = Math.cos(this.gearSet.pressureAngle * Math.PI / 180);
        var tanPressureAngle = Math.tan(this.gearSet.pressureAngle * Math.PI / 180);

        // If a positive backlash is defined then we narrow the trapezoid accordingly.
        // Each side of the tooth needs to narrowed by a fourth of the backlash (perpendiculr tooth faces).
        var dx = this.backlash / 4 / cosPressureAngle;
        console.log("backlash: " + this.backlash);
        console.log("dx: " + dx);
        console.log("profileShift: " + this.profileShift);

        // Create the tooth pointing right.
        var profileShiftOffset = this.profileShift * this.addendum;

        var upperLeftCorner = createPoint(-toothDepth + profileShiftOffset, toothWidth / 2 - dx + tanPressureAngle * toothDepth);
        var upperRightCorner = createPoint(this.addendum + profileShiftOffset, toothWidth / 2 - dx - tanPressureAngle * this.addendum);
        var lowerRightCorner = createPoint(upperRightCorner.X, -upperRightCorner.Y);
        var lowerLeftCorner = createPoint(upperLeftCorner.X, -upperLeftCorner.Y);

        return [upperLeftCorner, upperRightCorner, lowerRightCorner, lowerLeftCorner];
      }
         
      createGraphics(parent, crossMarkerLength) {
        var gearGroup = parent.group();
        var helperGroup = gearGroup.group();
        helperGroup.stroke(helperLinesStyle).fill('none');

        // Draw pitch 'line'.
        helperGroup.line(this.center.X, 2 * this.gearSet.circularPitch, this.center.X, -2 * this.gearSet.circularPitch);

        var regularGroup = gearGroup.group();
        regularGroup.stroke(regularLinesStyle).fill('none');

        insertSvgPath(regularGroup, this.rackShape, /*isClosed=*/ true);

        gearGroup.move(this.center.X, this.center.Y);
      }
    }

    function createPoint(x, y) {
      return { X: x, Y: y};
    }

    function clonePoint(point) {
      return { X: point.X, Y: point.Y};
    }

    function createTranslatedPoint(point, dx, dy) {
      return { X: point.X + dx, Y: point.Y + dy};
    }

    function createTranslatedPath(path, dx, dy) {
      return path.map(point => createTranslatedPoint(point, dx, dy));
    }

    // Translate point p by vector v
    function translatePoint(p, v) {
      return { X: p.X + v.X, Y: p.Y + v.Y};
    }

    function translatePoints(points, v) {
      return points.map(point => translatePoint(point, v));
    }

    // Squared distance between two 2d points.
    function squaredDistance(a, b) {
      return square(a.X - b.X) + square(a.Y - b.Y);
    }

    // Distance between two 2d points.
    function distance(a, b) {
      return Math.sqrt(squaredDistance(a, b));
    }

    function squaredLenth(vector) {
      return square(vector.X) + square(vector.Y)
    }

    function length(vector) {
      return Math.sqrt(squaredLenth(vector));
    }

    function square(x) { return x * x }

    function addVectors(v1, v2) { return { X: v1.X + v2.X, Y: v1.Y + v2.Y}; }
    function subtractVectors(v1, v2) { return { X: v1.X - v2.X, Y: v1.Y - v2.Y}; }
    function multiplyVector(a, v) { return { X: a * v.X, Y: a * v.Y}; }

    function radToDegree(angle) {
      return angle / Math.PI * 180;
    }

    function degreeToRad(angle) {
      return angle * Math.PI / 180;
    }

    function rotatePointAroundCenter(point, center, angle) {
      var cosAngle = Math.cos(angle);
      var sinAngle = Math.sin(angle);
      return _rotatePointAroundCenter(point, center, cosAngle, sinAngle);
    }

    function _rotatePointAroundCenter(point, center, cosAngle, sinAngle) {
      // Move so that center ends up at the origin
      var movedPoint = {X: point.X - center.X, Y: point.Y - center.Y};
      var rotated = {
        X: movedPoint.X * cosAngle - movedPoint.Y * sinAngle,
        Y: movedPoint.X * sinAngle + movedPoint.Y * cosAngle,
      };
      // Undo the move
      return {X: rotated.X + center.X, Y: rotated.Y + center.Y};
    }

    function rotatePointsAroundCenter(points, center, angle) {
      var cosAngle = Math.cos(angle);
      var sinAngle = Math.sin(angle);

      return points.map(point => _rotatePointAroundCenter(point, center, cosAngle, sinAngle));
    }

    function drawCircles(parent, points, radius) {
      points.forEach(point => drawCircle(parent, point, radius));
    }

    function drawCircle(parent, point, radius, strokeStyle) {
      parent.circle(2 * radius).cx(point.X).cy(point.Y);
    }

    function drawCrosses(parent, points, length) {
      points.forEach(point => drawCross(parent, point, length));
    }

    function drawCross(parent, point, length) {
    var halfLength = length / 2;
      parent.line(point.X, point.Y - halfLength, point.X, point.Y + halfLength); //.stroke(markerLinesStyle);
      parent.line(point.X - halfLength, point.Y, point.X + halfLength, point.Y); // .stroke(markerLinesStyle);
    }

    function createSvgPoint(point) {
      return {x: point.X, y: point.Y};
    }

    // Converts array of {X:..., Y:...} points into an SVG paths.
    function insertSvgPath(group, points, isClosed = true) {
      var svgPath = group.path();
      for (var i = 0; i < points.length; i++) {
        var svgPoint = createSvgPoint(points[i]);
        if (i == 0) {
          svgPath.M(svgPoint);
        } else {
          svgPath.L(svgPoint);
        }
      }
      if (isClosed) {
        svgPath.Z();
      }
    }

    function addLineSegmentsToPath(svgPath, points, moveToFirst = false) {
      for (var i = 0; i < points.length; i++) {
        var svgPoint = createSvgPoint(points[i]);
        if (i == 0 && moveToFirst) {
          svgPath.M(svgPoint);
        } else {
          svgPath.L(svgPoint);
        }
      }
    }
}
#endif