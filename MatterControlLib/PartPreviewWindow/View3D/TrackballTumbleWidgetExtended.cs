using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class TrackballTumbleWidgetExtended : GuiWidget
	{
		public NearFarAction GetNearFar;
		private double _centerOffsetX = 0;
		private Vector2 currentVelocityPerMs = new Vector2();
		private readonly MotionQueue motionQueue = new MotionQueue();
		private RunningInterval runningInterval;
		private readonly GuiWidget sourceWidget;
		private readonly int updatesPerSecond = 30;
		private WorldView world;
		private Object3DControlsLayer Object3DControlLayer;
		public float ZoomDelta { get; set; } = 0.2f;
		public TrackBallController TrackBallController { get; }
		public TrackBallTransformType CurrentTrackingType { get; set; } = TrackBallTransformType.None;
		public TrackBallTransformType TransformState { get; set; }

		// tracks the displacement of the camera to accurately rotate, translate and zoom
		public Vector3 DisplacementVec = Vector3.Zero;

		private bool isRotating = false;
		private Vector3 lastRotationOrigin = Vector3.Zero;
		private Vector3 lastTranslationOrigin = Vector3.Zero;
		private Vector2 lastScaleMousePosition = Vector2.Zero;
		private Vector3 rotateVec = Vector3.Zero;
		private Vector3 rotateVecOriginal = Vector3.Zero;
		private Vector2 mouseDownPosition = Vector2.Zero;

		public TrackballTumbleWidgetExtended(WorldView world, GuiWidget sourceWidget, Object3DControlsLayer Object3DControlLayer)
		{
			AnchorAll();
			TrackBallController = new TrackBallController(world);
			this.world = world;
			this.sourceWidget = sourceWidget;
			this.Object3DControlLayer = Object3DControlLayer;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (MouseCaptured)
			{
				Vector2 currentMousePosition = GetMousePosition(mouseEvent);
				ZeroVelocity();

				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
					{
						switch (TransformState)
						{
							case TrackBallTransformType.Rotation:
								CurrentTrackingType = TrackBallTransformType.Rotation;
								StartRotateAroundOrigin(currentMousePosition);
								break;

							case TrackBallTransformType.Translation:
								CurrentTrackingType = TrackBallTransformType.Translation;
								mouseDownPosition = currentMousePosition;
								break;

							case TrackBallTransformType.Scale:
								CurrentTrackingType = TrackBallTransformType.Scale;
								mouseDownPosition = currentMousePosition;
								lastScaleMousePosition = currentMousePosition;
								break;
						}
					}
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					if (CurrentTrackingType == TrackBallTransformType.None)
					{
						CurrentTrackingType = TrackBallTransformType.Translation;
						mouseDownPosition = currentMousePosition;
					}
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					if (CurrentTrackingType == TrackBallTransformType.None)
					{
						CurrentTrackingType = TrackBallTransformType.Rotation;
						StartRotateAroundOrigin(currentMousePosition);
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			Vector2 currentMousePosition = GetMousePosition(mouseEvent);

			if (CurrentTrackingType == TrackBallTransformType.Rotation)
			{
				motionQueue.AddMoveToMotionQueue(currentMousePosition, UiThread.CurrentTimerMs);
				DoRotateAroundOrigin(currentMousePosition);
			}
			else if (CurrentTrackingType == TrackBallTransformType.Translation)
			{
				Translate(currentMousePosition);
			}
			else if (CurrentTrackingType == TrackBallTransformType.Scale)
			{
				Vector2 mouseDelta = currentMousePosition - lastScaleMousePosition;
				double zoomDelta = 1;
				if (mouseDelta.Y < 0)
				{
					zoomDelta = -(-1 * mouseDelta.Y / 100);
				}
				else if (mouseDelta.Y > 0)
				{
					zoomDelta = +(1 * mouseDelta.Y / 100);
				}

				ZoomToScreenPosition(currentMousePosition, zoomDelta);
				lastScaleMousePosition = currentMousePosition;
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

		private Vector3 IntersectPlane(Vector3 rayP, Vector3 rayD)
		{
			return IntersectPlane(Vector3.Zero, new Vector3(0, 0, 1), rayP, rayD);
		}

		private Vector3 IntersectPlane(Vector3 planeP, Vector3 planeN, Vector3 rayP, Vector3 rayD)
		{
			var d = Vector3Ex.Dot(planeN, rayD);
			var t = -(Vector3Ex.Dot(rayP, planeN) + d) / d;
			return rayP + t * rayD;
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (this.ContainsFirstUnderMouseRecursive())
			{
				ZoomToScreenPosition(GetMousePosition(mouseEvent), mouseEvent.WheelDelta > 0 ? -ZoomDelta : ZoomDelta);
			}
		}

		private Vector2 GetMousePosition(MouseEventArgs mouseEvent)
		{
			Vector2 currentMousePosition;
			if (mouseEvent.NumPositions == 1)
			{
				currentMousePosition.X = mouseEvent.X;
				currentMousePosition.Y = mouseEvent.Y;
			}
			else
			{
				currentMousePosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
			}
			return currentMousePosition;
		}

		public void StartRotateAroundOrigin(Vector2 mousePosition)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			isRotating = true;
			mouseDownPosition = mousePosition;
			Ray rayToCenter = world.GetRayForLocalBounds(new Vector2(Width / 2, Height / 2));
			IntersectInfo intersectionInfo = Object3DControlLayer.Scene.GetBVHData().GetClosestIntersection(rayToCenter);
			Vector3 hitPos = intersectionInfo == null ? Vector3.Zero : -intersectionInfo.HitPosition;

			if (hitPos == Vector3.Zero)
			{
				hitPos = -IntersectPlane(rayToCenter.origin, new Vector3(rayToCenter.directionNormal).GetNormal());
				if (hitPos.Length > 1000)
				{
					hitPos = Vector3.Zero;
				}
			}

			if (hitPos == Vector3.Zero)
			{
				hitPos = lastRotationOrigin;
			}

			rotateVec = hitPos - DisplacementVec;
			rotateVecOriginal = rotateVec;
			lastRotationOrigin = hitPos;
		}

		public void DoRotateAroundOrigin(Vector2 mousePosition)
		{
			if (isRotating)
			{
				Quaternion activeRotationQuaternion = TrackBallController.GetRotationForMove(TrackBallController.ScreenCenter, TrackBallController.TrackBallRadius, mouseDownPosition, mousePosition, false);
				mouseDownPosition = mousePosition;
				world.Translate(rotateVec);
				rotateVec = Vector3Ex.TransformVector(rotateVec, world.RotationMatrix);

				world.Rotate(activeRotationQuaternion);

				rotateVec = Vector3Ex.TransformVector(rotateVec, Matrix4X4.Invert(world.RotationMatrix));
				world.Translate(-rotateVec);

				Invalidate();
			}
		}

		public void EndRotateAroundOrigin()
		{
			if (isRotating)
			{
				isRotating = false;
				DisplacementVec += rotateVecOriginal - rotateVec;
				Invalidate();
			}
		}

		public void SetRotationWithDisplacement(Quaternion rotationQ)
		{
			StartRotateAroundOrigin(Vector2.Zero);

			Matrix4X4 rotationM = Matrix4X4.CreateRotation(rotationQ);
			world.Translate(rotateVec);
			rotateVec = Vector3Ex.TransformVector(rotateVec, world.RotationMatrix);

			world.RotationMatrix = rotationM;

			rotateVec = Vector3Ex.TransformVector(rotateVec, Matrix4X4.Invert(world.RotationMatrix));
			world.Translate(-rotateVec);

			EndRotateAroundOrigin();
		}

		public void Translate(Vector2 position)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			Ray rayToCenter = world.GetRayForLocalBounds(new Vector2(Width / 2, Height / 2));
			Vector3 hitPos = IntersectPlane(rayToCenter.origin, new Vector3(rayToCenter.directionNormal)).GetNormal();

			if (hitPos == Vector3.Zero)
			{
				hitPos = lastTranslationOrigin;
			}

			double distanceToCenter = (hitPos - Vector3Ex.Transform(Vector3.Zero, world.InverseModelviewMatrix)).Length;
			Vector2 mouseDelta = position - mouseDownPosition;
			var offset = new Vector3(mouseDelta.X, mouseDelta.Y, 0);
			offset = Vector3Ex.TransformPosition(offset, Matrix4X4.Invert(world.RotationMatrix));
			offset *= distanceToCenter / 1000;
			DisplacementVec += offset;
			world.Translate(offset);

			mouseDownPosition = position;
			lastTranslationOrigin = hitPos;
			Invalidate();
		}

		public void ZoomToScreenPosition(Vector2 screenPosition, double zoomDelta)
		{
			if (isRotating)
			{
				ZeroVelocity();
			}

			Ray ray = world.GetRayForLocalBounds(screenPosition);
			IntersectInfo intersectionInfo = Object3DControlLayer.Scene.GetBVHData().GetClosestIntersection(ray);
			Vector3 hitPos = intersectionInfo == null ? Vector3.Zero : intersectionInfo.HitPosition;

			// if no object is found under the mouse trace from center to xy plane
			if (hitPos == Vector3.Zero)
			{
				Ray rayToCenter = world.GetRayForLocalBounds(new Vector2(Width / 2, Height / 2));
				hitPos = IntersectPlane(rayToCenter.origin, new Vector3(rayToCenter.directionNormal).GetNormal());
			}

			// calculate the vector between the camera and the intersection position and move the camera along it by ZoomDelta, then set it's direction
			Vector3 zoomVec = (hitPos - world.EyePosition) * zoomDelta;

			DisplacementVec += zoomVec;
			world.Translate(zoomVec);

			Invalidate();
		}

		public void Reset(Vector3 BedCenter)
		{
			ZeroVelocity();
			DisplacementVec = Vector3.Zero;
			lastRotationOrigin = Vector3.Zero;
			lastTranslationOrigin = Vector3.Zero;
			rotateVec = Vector3.Zero;
			rotateVecOriginal = Vector3.Zero;
			mouseDownPosition = Vector2.Zero;
			DisplacementVec = BedCenter;
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			RecalculateProjection();

			base.OnDraw(graphics2D);
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
				this.world.CalculateProjectionMatrixOffCenter(sourceWidget.Width, sourceWidget.Height, CenterOffsetX, zNear, zFar);
			}
			else
			{
				this.world.CalculateProjectionMatrix(sourceWidget.Width, sourceWidget.Height, zNear, zFar);
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
						mouseDownPosition = center;
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