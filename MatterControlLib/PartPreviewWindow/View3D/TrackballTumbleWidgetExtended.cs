/*
Copyright (c) 2021, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RayTracerNS;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class TrackballTumbleWidgetExtended : GuiWidget
	{
		private const double PerspectiveMinZoomDist = 3;
		private const double PerspectiveMaxZoomDist = 2300;

		// Currently 2.7614237491539679
		private double OrthographicMinZoomWorldspaceHeight
		{
			get
			{
				// Get the Z plane height at the perspective limit.
				// By coincidence, these are currently about the same, with byPerspectiveZoomLimit being slightly less at 2.4852813742385704.
				double byPerspectiveZoomLimit = WorldView.CalcPerspectiveHeight(PerspectiveMinZoomDist, WorldView.DefaultPerspectiveVFOVDegrees);
				double byWorldViewLimit = WorldView.OrthographicProjectionMinimumHeight * Vector3.UnitY.TransformVector(this.world.InverseModelviewMatrix).Length;
				return Math.Max(byPerspectiveZoomLimit, byWorldViewLimit);
			}
		}

		// Currently 1905.3823869162372
		private double OrthographicMaxZoomWorldspaceHeight => WorldView.CalcPerspectiveHeight(PerspectiveMaxZoomDist, WorldView.DefaultPerspectiveVFOVDegrees);

		// When switching the projection from perspective to orthographic, ensure this minimum height.
		// Will tend to be used when fully zoomed into the hit plane, and then the ref position indicator will appear to drift during the animation.
		// The resulting projection might be undesired, but at least it would be non-zero.
		private double PerspectiveToOrthographicMinViewspaceHeight => WorldView.OrthographicProjectionMinimumHeight;

		public NearFarAction GetNearFar;
		private readonly MotionQueue motionQueue = new MotionQueue();
		private readonly GuiWidget sourceWidget;
		private readonly int updatesPerSecond = 30;
		private double _centerOffsetX = 0;
		private Vector2 currentVelocityPerMs = new Vector2();
		private PlaneShape hitPlane;
		private bool isRotating = false;
		private Vector2 lastScaleMousePosition = Vector2.Zero;
		private Vector2 rotationStartPosition = Vector2.Zero;
		private Vector3 mouseDownWorldPosition;
		private readonly Object3DControlsLayer Object3DControlLayer;
		private RunningInterval runningInterval;
		private readonly ThemeConfig theme;
		private readonly WorldView world;
		private Vector2 mouseDown;

		public TrackballTumbleWidgetExtended(WorldView world, GuiWidget sourceWidget, Object3DControlsLayer Object3DControlLayer, ThemeConfig theme)
		{
			AnchorAll();
			TrackBallController = new TrackBallController(world);
			this.theme = theme;
			this.world = world;
			this.sourceWidget = sourceWidget;
			this.Object3DControlLayer = Object3DControlLayer;
			this.PerspectiveMode = !this.world.IsOrthographic;
		}

		public delegate void NearFarAction(WorldView world, out double zNear, out double zFar);

		public double CenterOffsetX
		{
			get
			{
				return _centerOffsetX;
			}
			set
			{
				_centerOffsetX = value;
				RecalculateProjection();
			}
		}

		public TrackBallTransformType CurrentTrackingType { get; set; } = TrackBallTransformType.None;
		public TrackBallController TrackBallController { get; }
		public TrackBallTransformType TransformState { get; set; }
		public double ZoomDelta { get; set; } = 0.2f;
		public double OrthographicZoomScalingFactor { get; set; } = 1.2f;
		public bool TurntableEnabled { get; set; }
		public bool PerspectiveMode { get; private set; }
		// Projection mode switch animations will capture this value. When this is changed, those animations will cease to have an effect.
		UInt64 _perspectiveModeSwitchAnimationSerialNumber = 0;
		Action _perspectiveModeSwitchFinishAnimation = null;

		public void ChangeProjectionMode(bool perspective, bool animate)
		{
			FinishProjectionSwitch();

			if (PerspectiveMode == perspective)
				return;

			PerspectiveMode = perspective;

			if (!PerspectiveMode)
			{
				System.Diagnostics.Debug.Assert(!this.world.IsOrthographic);
				if (!this.world.IsOrthographic)
				{
					// Perspective -> Orthographic
					DoSwitchToProjectionMode(true, GetWorldRefPositionForProjectionSwitch(), animate);
				}
			}
			else
			{
				System.Diagnostics.Debug.Assert(this.world.IsOrthographic);
				if (this.world.IsOrthographic)
				{
					// Orthographic -> Perspective
					DoSwitchToProjectionMode(false, GetWorldRefPositionForProjectionSwitch(), animate);
				}
			}
		}

		private void FinishProjectionSwitch()
		{
			++_perspectiveModeSwitchAnimationSerialNumber;
			_perspectiveModeSwitchFinishAnimation?.Invoke();
			_perspectiveModeSwitchFinishAnimation = null;
		}

		public void DoRotateAroundOrigin(Vector2 mousePosition)
		{
			if (isRotating)
			{
				FinishProjectionSwitch();

				Quaternion activeRotationQuaternion;
				if (TurntableEnabled)
				{
					var delta = mousePosition - rotationStartPosition;
					// scale it to device units
					delta /= TrackBallController.TrackBallRadius / 2;
					var zRotation = Matrix4X4.CreateFromAxisAngle(Vector3.UnitZ.Transform(world.RotationMatrix), delta.X);
					var screenXRotation = Matrix4X4.CreateFromAxisAngle(Vector3.UnitX, -delta.Y);
					activeRotationQuaternion = new Quaternion(zRotation * screenXRotation);
				}
				else
				{
					activeRotationQuaternion = TrackBallController.GetRotationForMove(TrackBallController.ScreenCenter,
						TrackBallController.TrackBallRadius,
						rotationStartPosition,
						mousePosition,
						false);
				}

				rotationStartPosition = mousePosition;

				world.RotateAroundPosition(mouseDownWorldPosition, activeRotationQuaternion);

				Invalidate();
			}
		}

		public void EndRotateAroundOrigin()
		{
			if (isRotating)
			{
				isRotating = false;
				Invalidate();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			void drawCircle(Vector3 worldspacePosition)
			{
				var circle = new Ellipse(world.GetScreenPosition(worldspacePosition), 8 * DeviceScale);
				graphics2D.Render(new Stroke(circle, 2 * DeviceScale), theme.PrimaryAccentColor);
				graphics2D.Render(new Stroke(new Stroke(circle, 4 * DeviceScale), DeviceScale), theme.TextColor.WithAlpha(128));
			}

			if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
			{
				switch (TransformState)
				{
				case TrackBallTransformType.Translation:
				case TrackBallTransformType.Rotation:
					drawCircle(mouseDownWorldPosition);
					break;
				}
			}

			bool isSwitchingProjectionMode = _perspectiveModeSwitchFinishAnimation != null;

			if (isSwitchingProjectionMode)
			{
				drawCircle(GetWorldRefPositionForProjectionSwitch());
			}

			base.OnDraw(graphics2D);
		}

		public void OnDraw3D()
		{
			if (hitPlane != null)
			{
				//world.RenderPlane(hitPlane.Plane, Color.Red, true, 50, 3);
				//world.RenderPlane(mouseDownWorldPosition, hitPlane.Plane.Normal, Color.Red, true, 50, 3);

				//world.RenderAxis(mouseDownWorldPosition, Matrix4X4.Identity, 100, 1);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			mouseDown = mouseEvent.Position;
			
			if (MouseCaptured)
			{
				ZeroVelocity();

				CalculateMouseDownPostionAndPlane(mouseEvent.Position);

				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
					{
						switch (TransformState)
						{
							case TrackBallTransformType.Rotation:
								CurrentTrackingType = TrackBallTransformType.Rotation;
								StartRotateAroundOrigin(mouseEvent.Position);
								break;

							case TrackBallTransformType.Translation:
								CurrentTrackingType = TrackBallTransformType.Translation;
								break;

							case TrackBallTransformType.Scale:
								CurrentTrackingType = TrackBallTransformType.Scale;
								lastScaleMousePosition = mouseEvent.Position;
								break;
						}
					}
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					if (CurrentTrackingType == TrackBallTransformType.None)
					{
						CurrentTrackingType = TrackBallTransformType.Translation;
					}
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					if (CurrentTrackingType == TrackBallTransformType.None)
					{
						CurrentTrackingType = TrackBallTransformType.Rotation;
						StartRotateAroundOrigin(mouseEvent.Position);
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (CurrentTrackingType == TrackBallTransformType.Rotation)
			{
				motionQueue.AddMoveToMotionQueue(mouseEvent.Position, UiThread.CurrentTimerMs);
				DoRotateAroundOrigin(mouseEvent.Position);
			}
			else if (CurrentTrackingType == TrackBallTransformType.Translation)
			{
				Translate(mouseEvent.Position);
			}
			else if (CurrentTrackingType == TrackBallTransformType.Scale)
			{
				Vector2 mouseDelta = (mouseEvent.Position - lastScaleMousePosition) / GuiWidget.DeviceScale;
				double zoomDelta = mouseDelta.Y < 0 ? .01 : -.01;

				for(int i=0; i<Math.Abs(mouseDelta.Y); i++)
				{
					ZoomToWorldPosition(mouseDownWorldPosition, zoomDelta);
				}
				lastScaleMousePosition = mouseEvent.Position;
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (CurrentTrackingType != TrackBallTransformType.None)
			{
				if (CurrentTrackingType == TrackBallTransformType.Rotation)
				{
					EndRotateAroundOrigin();

					// try and preserve some of the velocity
					motionQueue.AddMoveToMotionQueue(mouseEvent.Position, UiThread.CurrentTimerMs);
					if (!Keyboard.IsKeyDown(Keys.ShiftKey))
					{
						currentVelocityPerMs = motionQueue.GetVelocityPixelsPerMs();
						if (currentVelocityPerMs.LengthSquared > 0)
						{
							Vector2 center = LocalBounds.Center;
							StartRotateAroundOrigin(center);
							runningInterval = UiThread.SetInterval(ApplyVelocity, 1.0 / updatesPerSecond);
						}
					}
				}

				CurrentTrackingType = TrackBallTransformType.None;
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (this.ContainsFirstUnderMouseRecursive())
			{
				var mousePosition = mouseEvent.Position;
				var zoomDelta = mouseEvent.WheelDelta > 0 ? -ZoomDelta : ZoomDelta;
				ZoomToMousePosition(mousePosition, zoomDelta);
			}
		}

		private void ZoomToMousePosition(Vector2 mousePosition, double zoomDelta)
		{
			FinishProjectionSwitch();

			var rayAtScreenCenter = world.GetRayForLocalBounds(new Vector2(Width / 2, Height / 2));
			var rayAtMousePosition = world.GetRayForLocalBounds(mousePosition);
			IntersectInfo intersectionInfo = Object3DControlLayer.Scene.GetBVHData().GetClosestIntersection(rayAtMousePosition);
			if (intersectionInfo != null)
			{
				// we hit an object in the scene set the position to that
				hitPlane = new PlaneShape(new Plane(rayAtScreenCenter.directionNormal, mouseDownWorldPosition), null);
				ZoomToWorldPosition(intersectionInfo.HitPosition, zoomDelta);
				mouseDownWorldPosition = intersectionInfo.HitPosition;
			}
			else
			{
				// we did not hit anything
				// find a new 3d mouse position by hitting the screen plane at the distance of the last 3d mouse down position
				hitPlane = new PlaneShape(new Plane(rayAtScreenCenter.directionNormal, mouseDownWorldPosition), null);
				intersectionInfo = hitPlane.GetClosestIntersectionWithinRayDistanceRange(rayAtMousePosition);
				if (intersectionInfo != null)
				{
					ZoomToWorldPosition(intersectionInfo.HitPosition, zoomDelta);
					mouseDownWorldPosition = intersectionInfo.HitPosition;
				}
			}
		}

		public void RecalculateProjection()
		{
			double trackingRadius = Math.Min(Width * .45, Height * .45);

			// TODO: Should probably be `Width / 2`, but currently has no effect?
			TrackBallController.ScreenCenter = new Vector2(Width / 2 - CenterOffsetX, Height / 2);

			TrackBallController.TrackBallRadius = trackingRadius;

			double zNear = WorldView.DefaultNearZ;
			double zFar = WorldView.DefaultFarZ;

			Vector2 newViewportSize = new Vector2(Math.Max(1, sourceWidget.LocalBounds.Width), Math.Max(1, sourceWidget.LocalBounds.Height));

			// Update the projection parameters for GetNearFar.
			// NOTE: PerspectiveMode != this.world.IsOrthographic due to transition animations.
			if (this.world.IsOrthographic)
			{
				this.world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(newViewportSize.X, newViewportSize.Y, CenterOffsetX, this.world.NearPlaneHeightInViewspace, zNear, zFar);
			}
			else
			{
				this.world.CalculatePerspectiveMatrixOffCenter(newViewportSize.X, newViewportSize.Y, CenterOffsetX, zNear, zFar, this.world.VFovDegrees);
			}

			GetNearFar?.Invoke(this.world, out zNear, out zFar);

			// Use the updated near/far planes.
			if (this.world.IsOrthographic)
			{
				this.world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(newViewportSize.X, newViewportSize.Y, CenterOffsetX, this.world.NearPlaneHeightInViewspace, zNear, zFar);
			}
			else
			{
				this.world.CalculatePerspectiveMatrixOffCenter(newViewportSize.X, newViewportSize.Y, CenterOffsetX, zNear, zFar, this.world.VFovDegrees);
			}
		}

		public void SetRotationCenter(Vector3 worldPosition)
		{
			ZeroVelocity();
			mouseDownWorldPosition = worldPosition;
		}

		public void SetRotationWithDisplacement(Quaternion rotationQ)
		{
			FinishProjectionSwitch();

			if (isRotating)
			{
				ZeroVelocity();
			}

			world.SetRotationHoldPosition(mouseDownWorldPosition, rotationQ);
		}

		public void StartRotateAroundOrigin(Vector2 mousePosition)
		{
			FinishProjectionSwitch();

			if (isRotating)
			{
				ZeroVelocity();
			}

			rotationStartPosition = mousePosition;

			isRotating = true;
		}

		public void Translate(Vector2 position)
		{
			FinishProjectionSwitch();

			if (isRotating)
			{
				ZeroVelocity();
			}

			if (hitPlane != null)
			{
				var rayAtPosition = world.GetRayForLocalBounds(position);
				var hitAtPosition = hitPlane.GetClosestIntersectionWithinRayDistanceRange(rayAtPosition);

				if (hitAtPosition != null)
				{
					var offset = hitAtPosition.HitPosition - mouseDownWorldPosition;
					world.Translate(offset);

					Invalidate();
				}
			}
		}

		public void ZeroVelocity()
		{
			motionQueue.Clear();
			currentVelocityPerMs = Vector2.Zero;
			if (runningInterval != null)
			{
				UiThread.ClearInterval(runningInterval);
			}
			EndRotateAroundOrigin();
			FinishProjectionSwitch();
		}

		public void ZoomToWorldPosition(Vector3 worldPosition, double zoomDelta)
		{
			FinishProjectionSwitch();

			if (isRotating)
			{
				ZeroVelocity();
			}

			// calculate the vector between the camera and the intersection position and move the camera along it by ZoomDelta, then set it's direction
			var delta = worldPosition - world.EyePosition;

			if (this.world.IsOrthographic)
			{
				bool isZoomIn = zoomDelta < 0;
				double scaleFactor = isZoomIn ? 1 / OrthographicZoomScalingFactor : OrthographicZoomScalingFactor;
				double newViewspaceHeight = this.world.NearPlaneHeightInViewspace * scaleFactor;
				double newWorldspaceHeight = Vector3.UnitY.TransformVector(this.world.InverseModelviewMatrix).Length * newViewspaceHeight;

				if (isZoomIn
					? newWorldspaceHeight < OrthographicMinZoomWorldspaceHeight
					: newWorldspaceHeight > OrthographicMaxZoomWorldspaceHeight)
				{
					newViewspaceHeight = this.world.NearPlaneHeightInViewspace;
				}

				this.world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(this.world.Width, this.world.Height, CenterOffsetX,
					newViewspaceHeight, this.world.NearZ, this.world.FarZ);

				// Zero out the viewspace Z component.
				delta = delta.TransformVector(this.world.ModelviewMatrix);
				delta.Z = 0;
				delta = delta.TransformVector(this.world.InverseModelviewMatrix);
			}
			else
			{
				var deltaLength = delta.Length;

				if ((deltaLength < PerspectiveMinZoomDist && zoomDelta < 0)
					|| (deltaLength > PerspectiveMaxZoomDist && zoomDelta > 0))
				{
					return;
				}
			}

			var zoomVec = delta * zoomDelta;
			world.Translate(zoomVec);

			Invalidate();
		}

		public void ZoomToAABB(AxisAlignedBoundingBox box)
		{
			FinishProjectionSwitch();

			if (isRotating)
				ZeroVelocity();

			if (world.IsOrthographic)
			{
				// Using fake values for near/far.
				// ComputeOrthographicCameraFit may move the camera to wherever as long as the scene is centered, then
				// GetNearFar will figure out the near/far planes in the next projection update.
				CameraFittingUtil.Result result = CameraFittingUtil.ComputeOrthographicCameraFit(world, CenterOffsetX, 0, 1, box);

				WorldView tempWorld = new WorldView(world.Width, world.Height);
				tempWorld.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(world.Width, world.Height, CenterOffsetX, result.OrthographicViewspaceHeight, 0, 1);
				double endViewspaceHeight = tempWorld.NearPlaneHeightInViewspace;
				double startViewspaceHeight = world.NearPlaneHeightInViewspace;

				AnimateOrthographicTranslationAndHeight(
					world.EyePosition, startViewspaceHeight,
					result.CameraPosition, endViewspaceHeight
					);
			}
			else
			{
				CameraFittingUtil.Result result = CameraFittingUtil.ComputePerspectiveCameraFit(world, CenterOffsetX, box);
				AnimateTranslation(result.CameraPosition, world.EyePosition);
			}
		}

		// Used for testing.
		public RectangleDouble WorldspaceAabbToBottomScreenspaceRectangle(AxisAlignedBoundingBox box)
		{
			var points = box.GetCorners().Select(v => this.world.WorldspaceToBottomScreenspace(v).Xy);
			var rect = new RectangleDouble(points.First(), points.First());
			foreach (Vector2 v in points.Skip(1))
			{
				rect.ExpandToInclude(v);
			}
			return rect;
		}

		// Used for testing.
		public Vector3 WorldspaceToBottomScreenspace(Vector3 v)
		{
			return this.world.WorldspaceToBottomScreenspace(v);
		}

		private void ApplyVelocity()
		{
			if (isRotating)
			{
				if (HasBeenClosed || currentVelocityPerMs.LengthSquared <= 0)
				{
					ZeroVelocity();
				}

				double msPerUpdate = 1000.0 / updatesPerSecond;
				if (currentVelocityPerMs.LengthSquared > 0)
				{
					if (CurrentTrackingType == TrackBallTransformType.None)
					{
						Vector2 center = LocalBounds.Center;
						rotationStartPosition = center;
						DoRotateAroundOrigin(center + currentVelocityPerMs * msPerUpdate);
						Invalidate();

						currentVelocityPerMs *= .85;
						if (currentVelocityPerMs.LengthSquared < .01 / msPerUpdate)
						{
							ZeroVelocity();
						}
					}
				}
			}
		}

		private Vector3 GetWorldRefPositionForProjectionSwitch()
		{
			return mouseDownWorldPosition;
		}

		private void CalculateMouseDownPostionAndPlane(Vector2 mousePosition)
		{
			FinishProjectionSwitch();

			var rayAtMousePosition = world.GetRayForLocalBounds(mousePosition);

			// TODO: 
			// Check if we are in a GCode View
			var showingGCode = false;
			if (showingGCode)
            {
				// find the layer height that we are currenly showing
				// create a plane at that height
				// check if the ray intersects this plane
				// if we are above the plane set our origin to this intersection
				// if we are below the plane set it as a limit to the cast distance distance

				// Consideration: Think about what to do if we would be hitting the top of the part that is the layer height plane.
				// The issue is that there is no mesh geometry at that height but the user will see gcode that they might think
				// they should be able to click on.
            }

			var intersectionInfo = Object3DControlLayer.Scene.GetBVHData().GetClosestIntersection(rayAtMousePosition);
			var rayAtScreenCenter = world.GetRayForLocalBounds(new Vector2(Width / 2, Height / 2));
			if (intersectionInfo != null)
			{
				// we hit an object in the scene set the position to that
				mouseDownWorldPosition = intersectionInfo.HitPosition;
				hitPlane = new PlaneShape(new Plane(rayAtScreenCenter.directionNormal, mouseDownWorldPosition), null);
			}
			else
			{
				// we did not hit anything
				// find a new 3d mouse position by hitting the screen plane at the distance of the last 3d mouse down position
				hitPlane = new PlaneShape(new Plane(rayAtScreenCenter.directionNormal, mouseDownWorldPosition), null);
				intersectionInfo = hitPlane.GetClosestIntersectionWithinRayDistanceRange(rayAtMousePosition);

				if (intersectionInfo != null)
				{
					mouseDownWorldPosition = intersectionInfo.HitPosition;
				}
				else
				{
					int a = 0;
				}
			}
		}

		public void AnimateRotation(Matrix4X4 newRotation, Action after = null)
		{
			var rotationStart = new Quaternion(world.RotationMatrix);
			var rotationEnd = new Quaternion(newRotation);
			ZeroVelocity();
			var updates = 10;
			Animation.Run(this, .25, updates, (update) =>
			{
				var current = Quaternion.Slerp(rotationStart, rotationEnd, update / (double)updates);
				this.SetRotationWithDisplacement(current);
			}, after);
		}

		public void AnimateTranslation(Vector3 worldStart, Vector3 worldEnd, Action after = null)
		{
			var delta = worldEnd - worldStart;

			ZeroVelocity();
			Animation.Run(this, .25, 10, (update) =>
			{
				world.Translate(delta * .1);
			}, after);
		}

		// To orthographic:
		//     Translate the camera towards infinity by maintaining an invariant perspective plane in worldspace and reducing the FOV to zero.
		//     The animation will switch to true orthographic at the end.
		// To perspective:
		//     Translate the camera from infinity by maintaining an invariant perspective plane in worldspace and increasing the FOV to the default.
		//     The animation will switch out of orthographic in the first frame.
		private void DoSwitchToProjectionMode(
			bool toOrthographic,
			Vector3 worldspaceRefPosition,
			bool animate)
		{
			ZeroVelocity();

			System.Diagnostics.Debug.Assert(toOrthographic != this.world.IsOrthographic); // Starting in the correct projection mode.
			System.Diagnostics.Debug.Assert(_perspectiveModeSwitchFinishAnimation == null); // No existing animation.

			Matrix4X4 originalViewToWorld = this.world.InverseModelviewMatrix;
			
			Vector3 viewspaceRefPosition = worldspaceRefPosition.TransformPosition(this.world.ModelviewMatrix);
			// Don't let this become negative when the ref position is behind the camera.
			double refPlaneHeightInViewspace = Math.Abs(this.world.GetViewspaceHeightAtPosition(viewspaceRefPosition));
			
			double refViewspaceZ = viewspaceRefPosition.Z;

			// Ensure a minimum when going from perspective (in case the camera is zoomed all the way into the hit plane).
			if (toOrthographic)
			{
				double minViewspaceHeight = PerspectiveToOrthographicMinViewspaceHeight;
				if (refPlaneHeightInViewspace < minViewspaceHeight)
				{
					// If this happens, the ref position indicator will appear to drift during the animation.
					refPlaneHeightInViewspace = minViewspaceHeight;
					refViewspaceZ = -WorldView.CalcPerspectiveDistance(minViewspaceHeight, this.world.VFovDegrees);
				}
			}

			double refFOVDegrees = 
				toOrthographic
				? this.world.VFovDegrees // start FOV
				: WorldView.DefaultPerspectiveVFOVDegrees  // end FOV
				;

			const int numUpdates = 10;

			var update = new Action<int>((i) =>
			{
				if (toOrthographic && i >= numUpdates)
				{
					world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(world.Width, world.Height, CenterOffsetX, refPlaneHeightInViewspace, 0, 1);
				}
				else
				{
					double t = i / (double)numUpdates;
					double fov = toOrthographic ? refFOVDegrees * (1 - t) : refFOVDegrees * t;

					double dist = WorldView.CalcPerspectiveDistance(refPlaneHeightInViewspace, fov);
					double eyeZ = refViewspaceZ + dist;

					Vector3 viewspaceEyePosition = new Vector3(0, 0, eyeZ);

					//System.Diagnostics.Trace.WriteLine("{0} {1} {2}".FormatWith(fovDegrees, dist, eyeZ));

					world.CalculatePerspectiveMatrixOffCenter(world.Width, world.Height, CenterOffsetX, WorldView.DefaultNearZ, WorldView.DefaultFarZ, fov);
					world.EyePosition = viewspaceEyePosition.TransformPosition(originalViewToWorld);
				}
			});

			if (animate)
			{
				_perspectiveModeSwitchFinishAnimation = () =>
				{
					update(numUpdates);
				};

				UInt64 serialNumber = ++_perspectiveModeSwitchAnimationSerialNumber;

				Animation.Run(this, 0.25, numUpdates, (i) =>
				{
					if (serialNumber == _perspectiveModeSwitchAnimationSerialNumber)
					{
						update(i);
						if (i >= numUpdates)
						{
							_perspectiveModeSwitchFinishAnimation = null;
						}
					}
				}, null);
			}
			else
			{
				update(numUpdates);
			}
		}

		private void AnimateOrthographicTranslationAndHeight(
			Vector3 startCameraPosition, double startViewspaceHeight,
			Vector3 endCameraPosition, double endViewspaceHeight,
			Action after = null)
		{
			ZeroVelocity();
			Animation.Run(this, .25, 10, (update) =>
			{
				double t = update / 10.0;
				world.EyePosition = Vector3.Lerp(startCameraPosition, endCameraPosition, t);
				// Arbitrary near/far planes. The next projection update will re-fit them.
				double height = startViewspaceHeight * (1 - t) + endViewspaceHeight * t;
				world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(world.Width, world.Height, CenterOffsetX, height, 0, 1);
			}, after);
		}

		internal class MotionQueue
		{
			private readonly List<TimeAndPosition> motionQueue = new List<TimeAndPosition>();

			internal void AddMoveToMotionQueue(Vector2 position, long timeMs)
			{
				if (motionQueue.Count > 4)
				{
					// take off the last one
					motionQueue.RemoveAt(0);
				}

				motionQueue.Add(new TimeAndPosition(position, timeMs));
			}

			internal void Clear()
			{
				motionQueue.Clear();
			}

			internal Vector2 GetVelocityPixelsPerMs()
			{
				if (motionQueue.Count > 1)
				{
					// Get all the movement that is less 100 ms from the last time (the mouse up)
					TimeAndPosition lastTime = motionQueue[motionQueue.Count - 1];
					int firstTimeIndex = motionQueue.Count - 1;
					while (firstTimeIndex > 0 && motionQueue[firstTimeIndex - 1].timeMs + 100 > lastTime.timeMs)
					{
						firstTimeIndex--;
					}

					TimeAndPosition firstTime = motionQueue[firstTimeIndex];

					double milliseconds = lastTime.timeMs - firstTime.timeMs;
					if (milliseconds > 0)
					{
						Vector2 pixels = lastTime.position - firstTime.position;
						Vector2 pixelsPerSecond = pixels / milliseconds;

						return pixelsPerSecond;
					}
				}

				return Vector2.Zero;
			}

			internal struct TimeAndPosition
			{
				internal Vector2 position;

				internal long timeMs;

				internal TimeAndPosition(Vector2 position, long timeMs)
				{
					this.timeMs = timeMs;
					this.position = position;
				}
			}
		}
	}
}