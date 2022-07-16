using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class CameraFittingUtilTests
	{
		private const string CoinName = "MatterControl - Coin.stl";

		static Task DoZoomToSelectionTest(bool ortho, bool wideObject)
		{
			return MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab(removeDefaultPhil: wideObject);

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				if (wideObject)
					AddCoinToBed(testRunner, scene);

				if (ortho)
				{
					testRunner.ClickByName("Projection mode button");
					Assert.IsTrue(!view3D.TrackballTumbleWidget.PerspectiveMode);
					testRunner.Delay(1);
					Assert.IsTrue(!view3D.TrackballTumbleWidget.PerspectiveMode);
				}
				else
				{
					Assert.IsTrue(view3D.TrackballTumbleWidget.PerspectiveMode);
				}

				Vector3[] lookAtDirFwds = new Vector3[] {
					new Vector3(0, 0, -1),
					new Vector3(0, 0, 1),
					new Vector3(0, 1, 0),
					new Vector3(1, 1, 0),
					new Vector3(-1, -1, 0),
					new Vector3(0, 1, 1),
					new Vector3(1, 1, 1),
					new Vector3(0, 1, -1),
					new Vector3(1, 1, -1),
				};

				const int topI = 0;
				const int bottomI = 1;

				for (int i = 0; i < lookAtDirFwds.Length; ++i)
				{
					Vector3 lookAtDirFwd = lookAtDirFwds[i];
					Vector3 lookAtDirRight = (i == topI ? -Vector3.UnitY : i == bottomI ? Vector3.UnitY : -Vector3.UnitZ).Cross(lookAtDirFwd);
					Vector3 lookAtDirUp = lookAtDirRight.Cross(lookAtDirFwd).GetNormal();

					var look = Matrix4X4.LookAt(Vector3.Zero, lookAtDirFwd, lookAtDirUp);

					view3D.TrackballTumbleWidget.AnimateRotation(look);
					testRunner.Delay(0.5);

					testRunner.ClickByName("Zoom to selection button");
					testRunner.Delay(0.5);

					var part = testRunner.GetObjectByName(wideObject ? CoinName : "Phil A Ment.stl", out _) as IObject3D;
					AxisAlignedBoundingBox worldspaceAABB = part.GetAxisAlignedBoundingBox();

					Vector2 viewportSize = new Vector2(view3D.TrackballTumbleWidget.Width, view3D.TrackballTumbleWidget.Height);
					RectangleDouble rect = view3D.TrackballTumbleWidget.WorldspaceAabbToBottomScreenspaceRectangle(worldspaceAABB);
					Vector2 screenspacePositionOfWorldspaceCenter = view3D.TrackballTumbleWidget.WorldspaceToBottomScreenspace(worldspaceAABB.Center).Xy;
					double marginPixels = CameraFittingUtil.MarginScale * Math.Min(viewportSize.X, viewportSize.Y);

					const double pixelTolerance = 1e-3;

					// Check that the full object is visible.
					Assert.IsTrue(rect.Left > -pixelTolerance);
					Assert.IsTrue(rect.Bottom > -pixelTolerance);
					Assert.IsTrue(rect.Right < viewportSize.X + pixelTolerance);
					Assert.IsTrue(rect.Top < viewportSize.Y + pixelTolerance);

					// Check for centering.

					bool isPerspectiveFittingWithinMargin =
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.Sphere ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.CenterOnWorldspaceAABB ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.CenterOnViewspaceAABB ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithApproxCentering ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithPerfectCentering;

					// Tightly bounded. At least one axis should be bounded by the margin.
					bool isPerspectiveFittingBoundedByMargin =
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithApproxCentering ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithPerfectCentering;

					bool perspectiveFittingWillCenterTheAABBCenter =
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.Sphere ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.CenterOnWorldspaceAABB ||
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.CenterOnViewspaceAABB;

					bool perspectiveFittingWillCenterTheScreenspaceAABB =
					CameraFittingUtil.PerspectiveFittingAlgorithm == CameraFittingUtil.EPerspectiveFittingAlgorithm.IntersectionOfBoundingPlanesWithPerfectCentering;

					// Always get the same result.
					bool isPerspectiveFittingStable =
					CameraFittingUtil.PerspectiveFittingAlgorithm != CameraFittingUtil.EPerspectiveFittingAlgorithm.TrialAndError;

					bool isXWorldspaceCentered = MathHelper.AlmostEqual(viewportSize.X / 2, screenspacePositionOfWorldspaceCenter.X, pixelTolerance);
					bool isYWorldspaceCentered = MathHelper.AlmostEqual(viewportSize.Y / 2, screenspacePositionOfWorldspaceCenter.Y, pixelTolerance);

					bool isXMarginBounded = MathHelper.AlmostEqual(rect.Left, marginPixels, 1e-3) && MathHelper.AlmostEqual(rect.Right, viewportSize.X - marginPixels, pixelTolerance);
					bool isYMarginBounded = MathHelper.AlmostEqual(rect.Bottom, marginPixels, 1e-3) && MathHelper.AlmostEqual(rect.Top, viewportSize.Y - marginPixels, pixelTolerance);

					bool isXWithinMargin = rect.Left > marginPixels - 1 && rect.Right < viewportSize.X - (marginPixels - 1);
					bool isYWithinMargin = rect.Bottom > marginPixels - 1 && rect.Top < viewportSize.Y - (marginPixels - 1);

					bool isXScreenspaceCentered = MathHelper.AlmostEqual(viewportSize.X / 2, (rect.Left + rect.Right) / 2, pixelTolerance);
					bool isYScreenspaceCentered = MathHelper.AlmostEqual(viewportSize.Y / 2, (rect.Bottom + rect.Top) / 2, pixelTolerance);

					if (ortho)
					{
						// Ortho fitting will always center the screenspace AABB and the center of the object AABB.
						Assert.IsTrue(isXWorldspaceCentered && isYWorldspaceCentered);
						Assert.IsTrue(isXMarginBounded || isYMarginBounded);
						Assert.IsTrue(isXWithinMargin && isYWithinMargin);
						Assert.IsTrue(isXScreenspaceCentered && isYScreenspaceCentered);
					}
					else
					{
						if (isPerspectiveFittingWithinMargin)
							Assert.IsTrue(isXWithinMargin && isYWithinMargin);

						if (isPerspectiveFittingBoundedByMargin)
							Assert.IsTrue(isXMarginBounded || isYMarginBounded);

						if (perspectiveFittingWillCenterTheAABBCenter)
							Assert.IsTrue(isXWorldspaceCentered && isYWorldspaceCentered);

						if (perspectiveFittingWillCenterTheScreenspaceAABB)
							Assert.IsTrue(isXScreenspaceCentered && isYScreenspaceCentered);
					}

					if (ortho || isPerspectiveFittingStable)
					{
						testRunner.ClickByName("Zoom to selection button");
						testRunner.Delay(1);

						RectangleDouble rect2 = view3D.TrackballTumbleWidget.WorldspaceAabbToBottomScreenspaceRectangle(worldspaceAABB);
						Assert.IsTrue(rect2.Equals(rect, pixelTolerance));
					}
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 60 * 3, overrideWidth: 1300, overrideHeight: 800);
		}

		[Test, ChildProcessTest]
		public Task OrthographicZoomToSelectionWide()
		{
			return DoZoomToSelectionTest(true, true);
		}

		[Test, ChildProcessTest]
		public Task OrthographicZoomToSelectionTall()
		{
			return DoZoomToSelectionTest(true, false);
		}

		[Test, ChildProcessTest]
		public Task PerspectiveZoomToSelectionWide()
		{
			return DoZoomToSelectionTest(false, true);
		}

		[Test, ChildProcessTest]
		public Task PerspectiveZoomToSelectionTall()
		{
			return DoZoomToSelectionTest(false, false);
		}

		private static void AddCoinToBed(AutomationRunner testRunner, InteractiveScene scene)
		{
			testRunner.AddItemToBed(partName: "Row Item MatterControl - Coin.stl")
				.Delay(.1)
				.ClickByName(CoinName, offset: new Point2D(-4, 0));
			Assert.IsNotNull(scene.SelectedItem);
		}
	}
}
