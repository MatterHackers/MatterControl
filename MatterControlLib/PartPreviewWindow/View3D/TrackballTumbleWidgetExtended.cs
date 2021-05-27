using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class TrackballTumbleWidgetExtended : GuiWidget
	{
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
		}

		public delegate void NearFarAction(out double zNear, out double zFar);

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
		public bool TurntableEnabled { get; set; }

		public void DoRotateAroundOrigin(Vector2 mousePosition)
		{
			if (isRotating)
			{
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
			RecalculateProjection();

			if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
			{
				switch (TransformState)
				{
					case TrackBallTransformType.Translation:
					case TrackBallTransformType.Rotation:
						var circle = new Ellipse(world.GetScreenPosition(mouseDownWorldPosition), 8 * DeviceScale);
						graphics2D.Render(new Stroke(circle, 2 * DeviceScale), theme.PrimaryAccentColor);
						graphics2D.Render(new Stroke(new Stroke(circle, 4 * DeviceScale), DeviceScale), theme.TextColor.WithAlpha(128));
						break;
				}
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
				Vector2 mouseDelta = mouseEvent.Position - lastScaleMousePosition;
				double zoomDelta = 0;
				if (mouseDelta.Y < 0)
				{
					zoomDelta = -1 * mouseDelta.Y / 100;
				}
				else if (mouseDelta.Y > 0)
				{
					zoomDelta = -mouseDelta.Y / 100.0;
				}

				if (zoomDelta != 0)
				{
					ZoomToMousePosition(mouseDown, zoomDelta);
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
				intersectionInfo = hitPlane.GetClosestIntersection(rayAtMousePosition);
				if (intersectionInfo != null)
				{
					ZoomToWorldPosition(intersectionInfo.HitPosition, zoomDelta);
					mouseDownWorldPosition = intersectionInfo.HitPosition;
				}
				else
				{
					int a = 0;
				}
			}
		}

		public void RecalculateProjection()
		{
			double trackingRadius = Math.Min(Width * .45, Height * .45);
			TrackBallController.ScreenCenter = new Vector2(Width / 2 - CenterOffsetX, Height / 2);

			TrackBallController.TrackBallRadius = trackingRadius;

			var zNear = .1;
			var zFar = 100.0;

			GetNearFar?.Invoke(out zNear, out zFar);

			if (CenterOffsetX != 0)
			{
				this.world.CalculatePerspectiveMatrixOffCenter(sourceWidget.Width, sourceWidget.Height, CenterOffsetX, zNear, zFar);

				this.world.CalculateOrthogrphicMatrixOffCenter(sourceWidget.Width, sourceWidget.Height, CenterOffsetX, zNear, zFar);
			}
			else
			{
				this.world.CalculatePerspectiveMatrix(sourceWidget.Width, sourceWidget.Height, zNear, zFar);
			}
		}

		public void SetRotationCenter(Vector3 worldPosition)
		{
			ZeroVelocity();
			mouseDownWorldPosition = worldPosition;
		}

		public void SetRotationWithDisplacement(Quaternion rotationQ)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			world.SetRotationHoldPosition(mouseDownWorldPosition, rotationQ);
		}

		public void StartRotateAroundOrigin(Vector2 mousePosition)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			rotationStartPosition = mousePosition;

			isRotating = true;
		}

		public void Translate(Vector2 position)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			if (hitPlane != null)
			{
				var rayAtPosition = world.GetRayForLocalBounds(position);
				var hitAtPosition = hitPlane.GetClosestIntersection(rayAtPosition);

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
		}

		public void ZoomToWorldPosition(Vector3 worldPosition, double zoomDelta)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			var unitsPerPixel = world.GetWorldUnitsPerScreenPixelAtPosition(worldPosition);

			// calculate the vector between the camera and the intersection position and move the camera along it by ZoomDelta, then set it's direction
			var zoomVec = (worldPosition - world.EyePosition) * zoomDelta * Math.Min(unitsPerPixel * 100, 1);
			world.Translate(zoomVec);

			Invalidate();
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

		private void CalculateMouseDownPostionAndPlane(Vector2 mousePosition)
		{
			var rayAtMousePosition = world.GetRayForLocalBounds(mousePosition);
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
				intersectionInfo = hitPlane.GetClosestIntersection(rayAtMousePosition);
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