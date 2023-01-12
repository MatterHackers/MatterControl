// This isn't necessary for now as the orthographic camera is always an infinite distance away from the scene.
//#define ENABLE_ORTHOGRAPHIC_CAMERA_POSITIONING_ALONG_Z

//#define ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP

// If not defined, orthographic near/far fitting will use Mesh.Split.
// If defined, use the same method as perspective near/far fitting. Represent the AABB as solid shapes instead of faces.
//#define USE_TETRAHEDRON_CUTTING_FOR_ORTHOGRAPHIC_NEAR_FAR_FITTING

using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers
{
	// For "zoom to selection" and dynamic near/far.
	internal static class CameraFittingUtil
	{
		/// <summary>
		/// This proportion, scaled by the smaller dimension of the viewport, is subtracted from each side of the viewport for fitting.
		/// Exposed for testing.
		/// </summary>
		public const double MarginScale = 0.1;

		public enum EPerspectiveFittingAlgorithm
		{
			/// <summary>
			/// Has a margin, but does not use MarginScale.
			/// </summary>
			TrialAndError,

			/// <summary>
			/// Fit the camera to the AABB's bounding sphere.
			/// Guarantees a centered AABB center, but will not center the screenspace AABB.
			/// </summary>
			Sphere,

			/// <summary>
			/// Place the camera at the center of the AABB, then push the camera back until all points are visible.
			/// Guarantees a centered AABB center, but will not center the screenspace AABB.
			/// </summary>
			CenterOnWorldspaceAABB,

			/// <summary>
			/// Fit the camera to the viewspace AABB, which will tend to be larger than the worldspace AABB.
			/// Guarantees a centered AABB center, but will not center the screenspace AABB.
			/// </summary>
			CenterOnViewspaceAABB,

			/// <summary>
			/// Take the perspective side planes and fit them around the AABB.
			/// There will be two intersection lines and the camera will be placed on the one further back.
			/// Either X or Y will be restrained to one value, and the other will take the value closest to the center of the AABB.
			/// https://stackoverflow.com/questions/2866350/move-camera-to-fit-3d-scene/66113254#66113254
			/// </summary>
			IntersectionOfBoundingPlanesWithApproxCentering,

			/// <summary>
			/// This is the only one that guarantees perfect screenspace centering.
			/// A solver is used to center the screenspace AABB on the non-restrained axis.
			/// But this means that the viewspace center will not always be at the center of the screen, so the object can sometimes look off-centered.
			/// </summary>
			IntersectionOfBoundingPlanesWithPerfectCentering,
		}

		// Exposed for testing.
		// "static readonly" to silence unreachable code warnings.
		public static readonly EPerspectiveFittingAlgorithm PerspectiveFittingAlgorithm = EPerspectiveFittingAlgorithm.CenterOnWorldspaceAABB;

#if ENABLE_ORTHOGRAPHIC_CAMERA_POSITIONING_ALONG_Z
		// Scaled by the box's Z size in viewspace.
		// If camera Z translation is needed and far - near > threshold, the camera will be placed close to the object.
		const double OrthographicLargeZRangeScaledThreshold = 3.0;
		const double OrthographicLargeZRangeScaledDistanceBetweenNearAndObject = 1.0;
#endif

		public struct FitResult
		{
			public Vector3 CameraPosition;
			public double OrthographicViewspaceHeight;
		}

		public static FitResult ComputeOrthographicCameraFit(WorldView world, double centerOffsetX, double zNear, double zFar, AxisAlignedBoundingBox worldspaceAABB)
		{
			Vector3[] worldspacePoints = worldspaceAABB.GetCorners();
			Vector3[] viewspacePoints = worldspacePoints.Select(x => x.TransformPosition(world.ModelviewMatrix)).ToArray();

			Vector3 viewspaceCenter = worldspaceAABB.Center.TransformPosition(world.ModelviewMatrix);
			AxisAlignedBoundingBox viewspaceAABB = new AxisAlignedBoundingBox(viewspaceCenter, viewspaceCenter);
			foreach (Vector3 point in viewspacePoints)
			{
				viewspaceAABB.ExpandToInclude(point);
			}

			// Take the viewport with margins subtracted, then fit the viewspace AABB to it.
			Vector2 viewportSize = new Vector2(world.Width + centerOffsetX, world.Height);
			double baseDim = Math.Min(viewportSize.X, viewportSize.Y);
			double absTotalMargin = baseDim * MarginScale * 2;
			Vector2 reducedViewportSize = viewportSize - new Vector2(absTotalMargin, absTotalMargin);
			double unitsPerPixelX = viewspaceAABB.XSize / reducedViewportSize.X;
			double unitsPerPixelY = viewspaceAABB.YSize / reducedViewportSize.Y;
			double unitsPerPixel = Math.Max(unitsPerPixelX, unitsPerPixelY);
			Vector2 targetViewspaceSize = viewportSize * unitsPerPixel;

			Vector3 viewspaceNearCenter = new Vector3(viewspaceAABB.Center.Xy, viewspaceAABB.MaxXYZ.Z);
			Vector3 viewspaceCameraPosition = viewspaceNearCenter;

#if ENABLE_ORTHOGRAPHIC_CAMERA_POSITIONING_ALONG_Z
			Vector3 viewspaceFarCenter = new Vector3(viewspaceAABB.Center.Xy, viewspaceAABB.MinXYZ.Z);
			if (-viewspaceNearCenter.Z >= zNear && -viewspaceFarCenter.Z <= zFar)
			{
				// The object fits in the Z range without translating along Z.
				viewspaceCameraPosition.Z = 0;
			}
			else if (viewspaceAABB.ZSize * OrthographicLargeZRangeScaledThreshold < zFar - zNear)
			{
				// There's lots of Z range.
				// Place the camera close to the object such that there's a reasonable amount of Z space behind and in front of the object.
				viewspaceCameraPosition.Z += zNear + viewspaceAABB.ZSize * OrthographicLargeZRangeScaledDistanceBetweenNearAndObject;
			}
			else if (viewspaceAABB.ZSize < zFar - zNear)
			{
				// There's not much Z range, but enough to contain the object.
				// Place the camera such that the object is in the middle of the Z range.
				viewspaceCameraPosition.Z = viewspaceAABB.Center.Z + (zFar - zNear) * 0.5;
			}
			else
			{
				// The object is too big to fit in the Z range.
				// Place the camera at the near side of the object.
				viewspaceCameraPosition.Z = viewspaceAABB.MaxXYZ.Z;
			}
#endif

			Vector3 worldspaceCameraPosition = viewspaceCameraPosition.TransformPosition(world.InverseModelviewMatrix);
			return new FitResult { CameraPosition = worldspaceCameraPosition, OrthographicViewspaceHeight = targetViewspaceSize.Y };
		}

		public static FitResult ComputePerspectiveCameraFit(WorldView world, double centerOffsetX, AxisAlignedBoundingBox worldspaceAABB)
		{
			System.Diagnostics.Debug.Assert(!world.IsOrthographic);

			Vector3[] worldspacePoints = worldspaceAABB.GetCorners();
			Vector3[] viewspacePoints = worldspacePoints.Select(p => p.TransformPosition(world.ModelviewMatrix)).ToArray();
			Vector3 viewspaceCenter = viewspacePoints.Aggregate((a, b) => a + b) / viewspacePoints.Length;

			// Construct a temp WorldView with a smaller FOV to give the resulting view a margin.
			Vector2 viewportSize = world.ViewportSize + new Vector2(centerOffsetX, 0);
			double margin = MarginScale * Math.Min(viewportSize.X, viewportSize.Y);
			WorldView reducedWorld = new WorldView(viewportSize.X - margin * 2, viewportSize.Y - margin * 2);
			double reducedVFOVDegrees = WorldView.CalcPerspectiveVFOVDegreesFromDistanceAndHeight(world.NearZ, world.NearPlaneHeightInViewspace * (reducedWorld.Height / world.Height));
			
			reducedWorld.CalculatePerspectiveMatrixOffCenter(
				reducedWorld.Width, reducedWorld.Height,
				0,
				1, 2, // Arbitrary
				reducedVFOVDegrees
				);

			Plane[] viewspacePlanes = Frustum.FrustumFromProjectionMatrix(reducedWorld.ProjectionMatrix).Planes.Take(4).ToArray();

			Vector3 viewspaceCameraPosition;

			switch (PerspectiveFittingAlgorithm)
			{
			case EPerspectiveFittingAlgorithm.TrialAndError:
				return new FitResult { CameraPosition = TryPerspectiveCameraFitByIterativeAdjust(world, centerOffsetX, worldspaceAABB) };
			case EPerspectiveFittingAlgorithm.Sphere:
			default:
				viewspaceCameraPosition = PerspectiveCameraFitToSphere(reducedWorld, viewspaceCenter, viewspacePoints);
				break;
			case EPerspectiveFittingAlgorithm.CenterOnWorldspaceAABB:
				viewspaceCameraPosition = PerspectiveCameraFitAlongAxisThroughCenter(viewspacePlanes, viewspaceCenter, viewspacePoints);
				break;
			case EPerspectiveFittingAlgorithm.CenterOnViewspaceAABB:
				viewspaceCameraPosition = PerspectiveCameraFitToViewspaceAABB(viewspacePlanes, viewspaceCenter, viewspacePoints);
				break;
			case EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithApproxCentering:
				viewspaceCameraPosition = PerspectiveCameraFitByAxisAlignedPlaneIntersections(reducedWorld, viewspacePlanes, viewspaceCenter, viewspacePoints, false);
				break;
			case EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithPerfectCentering:
				viewspaceCameraPosition = PerspectiveCameraFitByAxisAlignedPlaneIntersections(reducedWorld, viewspacePlanes, viewspaceCenter, viewspacePoints, true);
				break;
			}

			return new FitResult { CameraPosition = viewspaceCameraPosition.TransformPosition(world.InverseModelviewMatrix) };
		}

		static bool NeedsToBeSmaller(RectangleDouble partScreenBounds, RectangleDouble goalBounds)
		{
			if (partScreenBounds.Bottom < goalBounds.Bottom
				|| partScreenBounds.Top > goalBounds.Top
				|| partScreenBounds.Left < goalBounds.Left
				|| partScreenBounds.Right > goalBounds.Right)
			{
				return true;
			}

			return false;
		}

		// Original code relocated from https://github.com/MatterHackers/MatterControl/blob/e5967ff858f2844734e4802a6c6c8ac973ad92d1/MatterControlLib/PartPreviewWindow/View3D/View3DWidget.cs
		static Vector3 TryPerspectiveCameraFitByIterativeAdjust(WorldView world, double centerOffsetX, AxisAlignedBoundingBox worldspaceAABB)
		{
			var aabb = worldspaceAABB;
			var center = aabb.Center;
			// pan to the center
			var screenCenter = new Vector2(world.Width / 2 + centerOffsetX / 2, world.Height / 2);
			var centerRay = world.GetRayForLocalBounds(screenCenter);

			// make the target size a portion of the total size
			var goalBounds = new RectangleDouble(0, 0, world.Width, world.Height);
			goalBounds.Inflate(-world.Width * .1);

			int rescaleAttempts = 0;
			var testWorld = new WorldView(world.Width, world.Height);
			testWorld.RotationMatrix = world.RotationMatrix;
			var distance = 80.0;

			void AjustDistance()
			{
				testWorld.TranslationMatrix = world.TranslationMatrix;
				var delta = centerRay.origin + centerRay.directionNormal * distance - center;
				testWorld.Translate(delta);
			}

			AjustDistance();

			while (rescaleAttempts++ < 500)
			{

				var partScreenBounds = testWorld.GetScreenBounds(aabb);

				if (NeedsToBeSmaller(partScreenBounds, goalBounds))
				{
					distance++;
					AjustDistance();
					partScreenBounds = testWorld.GetScreenBounds(aabb);

					// If it crossed over the goal reduct the amount we are adjusting by.
					if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						break;
					}
				}
				else
				{
					distance--;
					AjustDistance();
					partScreenBounds = testWorld.GetScreenBounds(aabb);

					// If it crossed over the goal reduct the amount we are adjusting by.
					if (NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						break;
					}
				}
			}

			//TrackballTumbleWidget.AnimateTranslation(center, centerRay.origin + centerRay.directionNormal * distance);
			// zoom to fill the view
			// viewControls3D.NotifyResetView();

			return world.EyePosition - ((centerRay.origin + centerRay.directionNormal * distance) - center);
		}

		static Vector3 PerspectiveCameraFitToSphere(WorldView world, Vector3 viewspaceCenter, Vector3[] viewspacePoints)
		{
			double radius = viewspacePoints.Select(p => (p - viewspaceCenter).Length).Max();
			double distForBT = radius / Math.Sin(MathHelper.DegreesToRadians(world.VFovDegrees) / 2);
			double distForLR = radius / Math.Sin(MathHelper.DegreesToRadians(world.HFovDegrees) / 2);
			double distForN = radius + WorldView.PerspectiveProjectionMinimumNearZ;
			double dist = Math.Max(Math.Max(distForBT, distForLR), distForN);
			return viewspaceCenter + new Vector3(0, 0, dist);
		}


		static Vector3 PerspectiveCameraFitAlongAxisThroughCenter(Plane[] viewspacePlanes, Vector3 viewspaceCenter, Vector3[] viewspacePoints)
		{
			Vector3 viewspaceCameraPosition = viewspaceCenter;

			viewspacePoints = viewspacePoints.Select(p => p - viewspaceCameraPosition).ToArray();

			double relZ = double.NegativeInfinity;

			foreach (Plane viewspacePlane in viewspacePlanes)
			{
				relZ = Math.Max(relZ, viewspacePoints.Select(
					p => p.Z - (viewspacePlane.DistanceFromOrigin - viewspacePlane.Normal.Dot(new Vector3(p.X, p.Y, 0))) / viewspacePlane.Normal.Z
				).Max());
			}

			return viewspaceCameraPosition + new Vector3(0, 0, relZ);
		}

		static Vector3 PerspectiveCameraFitToViewspaceAABB(Plane[] viewspacePlanes, Vector3 viewspaceCenter, Vector3[] viewspacePoints)
		{
			AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(viewspacePoints);
			return PerspectiveCameraFitAlongAxisThroughCenter(viewspacePlanes, aabb.Center, aabb.GetCorners());
		}

		struct Line
		{
			public double gradient;
			public double refX;

			public double YAt(double x) => gradient * (x - refX);

			public Line NegatedY()
			{
				return new Line { gradient = -gradient, refX = refX };
			}
		}

		static double GetIntersectX(Line a, Line b, double minX, double maxX)
		{
			double x = (a.gradient * a.refX - b.gradient * b.refX) / (a.gradient - b.gradient);
			if (x < minX)
				return minX;
			else if (x < maxX)
				return x;
			else // or, infinity, NaN
				return double.PositiveInfinity;
		}

		struct PiecewiseSegment
		{
			public Vector2 start;
			public Line line;

			public PiecewiseSegment NegatedY()
			{
				return new PiecewiseSegment { start = new Vector2(start.X, -start.Y), line = line.NegatedY() };
			}
		}

		static List<PiecewiseSegment> SweepDescendingMaxY(double minX, double maxX, Line[] lines)
		{
			// Find the piecewise maximum of all the lines.
			// NOTE: Monotonic decreasing Y, monotonic increasing gradient, max Y at minX.

			bool startsBelow(PiecewiseSegment seg, Line line) => seg.start.Y < line.YAt(seg.start.X);

			// Order segments by gradient, steep to shallow.
			// For each line:
			//     Discard segments of the piecewise function that start below the line.
			//     Intersect and append a new segment.

			Array.Sort(lines, (a, b) => a.gradient.CompareTo(b.gradient));

			var output = new List<PiecewiseSegment>();

			foreach (Line line in lines)
			{
				while (output.Count >= 1 && startsBelow(output.Last(), line))
				{
					output.RemoveAt(output.Count - 1);
				}

				if (output.Count == 0)
				{
					output.Add(new PiecewiseSegment { start = new Vector2(minX, line.YAt(minX)), line = line });
				}
				else
				{
					double x = GetIntersectX(output.Last().line, line, output.Last().start.X, maxX);
					if (output.Last().start.X < x && x < maxX)
						output.Add(new PiecewiseSegment { start = new Vector2(x, line.YAt(x)), line = line });
				}
			}

			return output;
		}

		static List<PiecewiseSegment> SweepDescendingMinY(double minX, double maxX, Line[] lines)
		{
			// Negate X and Y.

			// -f(-x) = gradient * (x - refX)
			// -f(x) = gradient * (-x - refX)
			// f(x) = gradient * (x + refX)

			List<PiecewiseSegment> segs = SweepDescendingMaxY(-maxX, -minX, lines.Select(line => new Line
			{
				gradient = line.gradient,
				refX = -line.refX
			}).ToArray()).Select(seg => new PiecewiseSegment
			{
				start = -seg.start,
				line = new Line { gradient = seg.line.gradient, refX = -seg.line.refX }
			}).Reverse().ToList();

			// The segs above have end points instead of start points. Shift them and set the start point.
			for (int i = segs.Count - 1; i > 0; --i)
			{
				segs[i] = new PiecewiseSegment { start = segs[i - 1].start, line = segs[i].line };
			}

			segs[0] = new PiecewiseSegment { start = new Vector2(minX, segs[0].line.YAt(minX)), line = segs[0].line };

			return segs;
		}

		static Vector3 PerspectiveCameraFitByAxisAlignedPlaneIntersections(
			WorldView world, Plane[] viewspacePlanes, Vector3 viewspaceCenter, Vector3[] viewspacePoints,
			bool useSolver)
		{
			Plane[] viewspaceBoundingPlanes = viewspacePlanes.Select(plane => new Plane(
				plane.Normal,
				viewspacePoints.Select(point => plane.Normal.Dot(point)).Min()
				)).ToArray();

			double maxViewspaceZ = viewspacePoints.Select(p => p.Z).Max();

			// Axis-aligned plane intersection as 2D line intersection: [a, b].[x or y, z] + c = 0
			Vector3 viewspaceLPlane2D = new Vector3(viewspaceBoundingPlanes[0].Normal.X, viewspaceBoundingPlanes[0].Normal.Z, -viewspaceBoundingPlanes[0].DistanceFromOrigin);
			Vector3 viewspaceRPlane2D = new Vector3(viewspaceBoundingPlanes[1].Normal.X, viewspaceBoundingPlanes[1].Normal.Z, -viewspaceBoundingPlanes[1].DistanceFromOrigin);
			Vector3 viewspaceBPlane2D = new Vector3(viewspaceBoundingPlanes[2].Normal.Y, viewspaceBoundingPlanes[2].Normal.Z, -viewspaceBoundingPlanes[2].DistanceFromOrigin);
			Vector3 viewspaceTPlane2D = new Vector3(viewspaceBoundingPlanes[3].Normal.Y, viewspaceBoundingPlanes[3].Normal.Z, -viewspaceBoundingPlanes[3].DistanceFromOrigin);

			Vector3 intersectionLRInXZ = viewspaceLPlane2D.Cross(viewspaceRPlane2D);
			Vector3 intersectionBTInYZ = viewspaceBPlane2D.Cross(viewspaceTPlane2D);
			intersectionLRInXZ.Xy /= intersectionLRInXZ.Z;
			intersectionBTInYZ.Xy /= intersectionBTInYZ.Z;

			double maxZByPlaneIntersections = Math.Max(intersectionLRInXZ.Y, intersectionBTInYZ.Y);
			double maxZByNearPlane = maxViewspaceZ + WorldView.PerspectiveProjectionMinimumNearZ;

			// Initial position, before adjustment.
			Vector3 viewspaceCameraPosition = new Vector3(intersectionLRInXZ.X, intersectionBTInYZ.X, Math.Max(maxZByPlaneIntersections, maxZByNearPlane));

			double optimiseAxis(int axis, double min, double max)
			{
				if (!useSolver)
				{
					// Pick a point closest to viewspaceCenter.
					return Math.Min(Math.Max(viewspaceCenter[axis], min), max);
				}

				// [camX, camY, camZ] = viewspaceCameraPosition (the initial guess, with the final Z)
				// ndcX = m[1,1] / (z - camZ) * (x - camX)
				// ndcY = m[2,2] / (z - camZ) * (y - camY)

				Line[] ndcLines = viewspacePoints.Select(viewspacePoint => new Line
				{
					gradient = world.ProjectionMatrix[axis, axis] / (viewspacePoint.Z - viewspaceCameraPosition.Z),
					refX = viewspacePoint[axis]
				}).ToArray();

				List<PiecewiseSegment> piecewiseMax = SweepDescendingMaxY(min, max, ndcLines);
				List<PiecewiseSegment> piecewiseMin = SweepDescendingMinY(min, max, ndcLines);

#if ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP
				using (var file = new StreamWriter("perspective centering.csv"))
				{
					foreach (Line line in ndcLines)
					{
						double ndcAtMin = line.gradient * (min - line.refX);
						double ndcAtMax = line.gradient * (max - line.refX);
						file.WriteLine("{0}, {1}", min, ndcAtMin);
						file.WriteLine("{0}, {1}", max, ndcAtMax);
					}

					file.WriteLine("");

					foreach (PiecewiseSegment seg in piecewiseMax)
						file.WriteLine("{0}, {1}", seg.start.X, seg.start.Y);
					file.WriteLine("{0}, {1}", max, piecewiseMax.Last().line.gradient * (max - piecewiseMax.Last().line.refX));

					file.WriteLine("");

					foreach (PiecewiseSegment seg in piecewiseMin)
						file.WriteLine("{0}, {1}", seg.start.X, seg.start.Y);
					file.WriteLine("{0}, {1}", max, piecewiseMin.Last().line.gradient * (max - piecewiseMin.Last().line.refX));
				}
#endif

				// Now, with the piecewise min and max functions, determine the X at which max == -min.
				// Max is decreasing, -min is increasing. At some point, they should cross over.

				// Cross-over cannot be before minX.
				if (piecewiseMax[0].start.Y <= -piecewiseMin[0].start.Y)
				{
					return min;
				}

				int maxI = 0;
				int minI = 0;

				double? resultX = null;

#if ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP
				using (var file = new StreamWriter("perspective piecewise crossover.csv"))
#endif
				{
					while (maxI < piecewiseMax.Count && minI < piecewiseMin.Count)
					{
						PiecewiseSegment maxSeg = piecewiseMax[maxI];
						PiecewiseSegment minSeg = piecewiseMin[minI].NegatedY();
						double maxSegEndX = maxI + 1 < piecewiseMax.Count ? piecewiseMax[maxI + 1].start.X : max;
						double minSegEndX = minI + 1 < piecewiseMin.Count ? piecewiseMin[minI + 1].start.X : max;
						double sectionMinX = Math.Max(maxSeg.start.X, minSeg.start.X);
						double sectionMaxX = Math.Min(maxSegEndX, minSegEndX);
						double crossoverX = GetIntersectX(maxSeg.line, minSeg.line, sectionMinX, sectionMaxX);

#if ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP
						file.WriteLine("{0}, {1}, {2}, {3}", sectionMinX, maxSeg.line.YAt(sectionMinX), sectionMinX, minSeg.line.YAt(sectionMinX));
#endif

						if (crossoverX < sectionMaxX && !resultX.HasValue)
						{
							resultX = crossoverX;
#if !ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP
							return resultX.Value;
#endif
						}

						if (maxSegEndX < minSegEndX)
						{
							++maxI;
						}
						else
						{
							++minI;
						}
					}

#if ENABLE_PERSPECTIVE_FITTING_DEBUG_DUMP
					file.WriteLine("{0}, {1}, {2}, {3}", max, piecewiseMax.Last().line.YAt(max), max, piecewiseMin.Last().NegatedY().line.YAt(max));
#endif
				}

				return resultX ?? max;
			}

			// Two axes are restrained to a single value. The last has a range of valid values.
			if (intersectionLRInXZ.Y < intersectionBTInYZ.Y)
			{
				// The camera will be on the intersection of the top/bottom planes.
				// The left/right planes in front intersect with the horizontal line and determine the limits of X.
				double minX = (viewspaceRPlane2D.Y * intersectionBTInYZ.Y + viewspaceRPlane2D.Z) / -viewspaceRPlane2D.X;
				double maxX = (viewspaceLPlane2D.Y * intersectionBTInYZ.Y + viewspaceLPlane2D.Z) / -viewspaceLPlane2D.X;
				viewspaceCameraPosition.X = optimiseAxis(0, minX, maxX);
			}
			else
			{
				// The camera will be on the intersection of the left/right planes.
				// The top/bottom planes in front intersect with the vertical line and determine the limits of Y.
				double minY = (viewspaceTPlane2D.Y * intersectionLRInXZ.Y + viewspaceTPlane2D.Z) / -viewspaceTPlane2D.X;
				double maxY = (viewspaceBPlane2D.Y * intersectionLRInXZ.Y + viewspaceBPlane2D.Z) / -viewspaceBPlane2D.X;
				viewspaceCameraPosition.Y = optimiseAxis(1, minY, maxY);
			}

			return viewspaceCameraPosition;
		}

		/// Tetrahedron-based AABB-frustum intersection code for perspective projection dynamic near/far planes.
		/// Temporary, may be replaced with a method that fits to individual triangles if needed.
		/// This tetrahedron clipping is only used if
		///     ENABLE_PERSPECTIVE_PROJECTION_DYNAMIC_NEAR_FAR is defined in View3DWidget.cs, or
		///     USE_TETRAHEDRON_CUTTING_FOR_ORTHOGRAPHIC_NEAR_FAR_FITTING is defined.

		struct Tetrahedron
		{
			public Vector3 a, b, c, d;
		}

		static Tetrahedron[] ClipTetrahedron(Tetrahedron T, Plane plane)
		{
			// true iff inside
			Vector3[] vs = new Vector3[] { T.a, T.b, T.c, T.d };
			bool[] sides = vs.Select(v => plane.GetDistanceFromPlane(v) > 0).ToArray();
			int numInside = sides.Count(b => b);

			Vector3 temp;

			switch (numInside)
			{
			case 0:
			default:
				return new Tetrahedron[] { };

			case 1:
				{
					int i = Array.IndexOf(sides, true);
					(vs[0], vs[i]) = (vs[i], vs[0]);
					temp = vs[0]; plane.ClipLine(ref temp, ref vs[1]);
					temp = vs[0]; plane.ClipLine(ref temp, ref vs[2]);
					temp = vs[0]; plane.ClipLine(ref temp, ref vs[3]);
					// One tetra inside.
					return new Tetrahedron[] {
						new Tetrahedron{ a = vs[0], b = vs[1], c = vs[2], d = vs[3] },
					};
				}

			case 2:
				{
					int i = Array.IndexOf(sides, true);
					(vs[0], vs[i]) = (vs[i], vs[0]);
					(sides[0], sides[i]) = (sides[i], sides[0]);
					int j = Array.IndexOf(sides, true, 1);
					(vs[1], vs[j]) = (vs[j], vs[1]);
					Vector3 v02 = vs[2];
					Vector3 v03 = vs[3];
					Vector3 v12 = vs[2];
					Vector3 v13 = vs[3];
					temp = vs[0]; plane.ClipLine(ref temp, ref v02);
					temp = vs[0]; plane.ClipLine(ref temp, ref v03);
					temp = vs[1]; plane.ClipLine(ref temp, ref v12);
					temp = vs[1]; plane.ClipLine(ref temp, ref v13);
					// Three new tetra sharing the common edge v03-v12.
					return new Tetrahedron[] {
						new Tetrahedron{ a = v12, b = v03, c = vs[0], d = v02 },
						new Tetrahedron{ a = v12, b = v03, c = vs[0], d = vs[1] },
						new Tetrahedron{ a = v12, b = v03, c = vs[1], d = v13 },
					};
				}

			case 3:
				{
					int i = Array.IndexOf(sides, false);
					(vs[3], vs[i]) = (vs[i], vs[3]);
					Vector3 v03 = vs[3];
					Vector3 v13 = vs[3];
					Vector3 v23 = vs[3];
					temp = vs[0]; plane.ClipLine(ref temp, ref v03);
					temp = vs[1]; plane.ClipLine(ref temp, ref v13);
					temp = vs[2]; plane.ClipLine(ref temp, ref v23);
					// Three new tetra.
					return new Tetrahedron[] {
						new Tetrahedron{ a = vs[0], b = v03, c = v13, d = v23 },
						new Tetrahedron{ a = vs[0], b = vs[1], c = v13, d = v23 },
						new Tetrahedron{ a = vs[0], b = vs[1], c = vs[2], d = v23 },
					};
				}

			case 4:
				return new Tetrahedron[] { T };
			}
		}

		static readonly Tetrahedron[] BoxOfTetras = new Func<Tetrahedron[]>(() =>
		{
			Vector3[] corners = new Vector3[] {
				new Vector3(+1, +1, +1), // [0]
				new Vector3(-1, +1, +1), // [1]
				new Vector3(+1, -1, +1), // [2]
				new Vector3(-1, -1, +1), // [3]
				new Vector3(+1, +1, -1), // [4]
				new Vector3(-1, +1, -1), // [5]
				new Vector3(+1, -1, -1), // [6]
				new Vector3(-1, -1, -1), // [7]
			};

			// All the tetras share a common diagonal edge.
			var box = new Tetrahedron[] {
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[5], d = corners[4] },
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[4], d = corners[6] },
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[6], d = corners[2] },
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[2], d = corners[3] },
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[3], d = corners[1] },
				new Tetrahedron{ a = corners[0], b = corners[7], c = corners[1], d = corners[5] },
			};

			// Sanity check.
			double V = box.Select(T => Math.Abs((T.a - T.d).Dot((T.b - T.d).Cross(T.c - T.d)))).Sum();
			System.Diagnostics.Debug.Assert(MathHelper.AlmostEqual(V, 2 * 2 * 2 * 6, 1e-5));

			return box;
		})();

		static Tetrahedron[] MakeAABBTetraArray(AxisAlignedBoundingBox box)
		{
			Vector3 halfsize = box.Size * 0.5;
			Vector3 center = box.Center;
			return BoxOfTetras.Select(T => new Tetrahedron
			{
				a = center + T.a * halfsize,
				b = center + T.b * halfsize,
				c = center + T.c * halfsize,
				d = center + T.d * halfsize,
			}).ToArray();
		}

		static Tetrahedron[] ClipTetras(Tetrahedron[] tetra, Plane plane)
		{
			return tetra.SelectMany(T => ClipTetrahedron(T, plane)).ToArray();
		}

		public static Tuple<double, double> ComputeNearFarOfClippedWorldspaceAABB(bool isOrthographic, Plane[] worldspacePlanes, Matrix4X4 worldToViewspace, AxisAlignedBoundingBox worldspceAABB)
		{
			if (worldspceAABB == null || worldspceAABB.XSize < 0)
			{
				return null;
			}

#if !USE_TETRAHEDRON_CUTTING_FOR_ORTHOGRAPHIC_NEAR_FAR_FITTING
			if (isOrthographic)
			{
				Mesh mesh = PlatonicSolids.CreateCube(worldspceAABB.Size);
				mesh.Translate(worldspceAABB.Center);

				double tolerance = 0.001;
				foreach (Plane plane in worldspacePlanes)
				{
					mesh.Split(plane, onPlaneDistance: tolerance, cleanAndMerge: false, discardFacesOnNegativeSide: true);

					// Remove any faces outside the plane (without using discardFacesOnNegativeSide).
					//for (int i = mesh.Faces.Count - 1; i >= 0; --i)
					//{
					//	Face face = mesh.Faces[i];
					//	double maxDist = new int[] { face.v0, face.v1, face.v2 }.Select(vi => plane.Normal.Dot(new Vector3(mesh.Vertices[vi]))).Max();
					//	if (maxDist < (plane.DistanceFromOrigin < 0 ? plane.DistanceFromOrigin * (1 - 1e-4) : plane.DistanceFromOrigin * (1 + 1e-4)))
					//	{
					//		mesh.Faces[i] = mesh.Faces[mesh.Faces.Count - 1];
					//		mesh.Faces.RemoveAt(mesh.Faces.Count - 1);
					//	}
					//}
				}

				mesh.CleanAndMerge();

				if (mesh.Vertices.Any())
				{
					mesh.Transform(worldToViewspace);
					var depths = mesh.Vertices.Select(v => -v.Z);
					return Tuple.Create<double, double>(depths.Min(), depths.Max());
				}
			}
			else
#endif
			{
				// The above works for orthographic, but won't for perspective as the planes aren't parallel to Z.
				// So, cut some tetrahedra instead.

				Tetrahedron[] tetras = MakeAABBTetraArray(worldspceAABB);
				foreach (Plane plane in worldspacePlanes)
				{
					tetras = ClipTetras(tetras, plane);
				}

				if (tetras.Any())
				{
					var vertices = tetras.SelectMany(T => new Vector3[] { T.a, T.b, T.c, T.d });
					var depths = vertices.Select(v => -v.TransformPosition(worldToViewspace).Z).ToArray();
					return Tuple.Create<double, double>(depths.Min(), depths.Max());
				}
			}

			return null;
		}
	}
}
