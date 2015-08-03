using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class PrintLevelingPlane
	{
		private Matrix4X4 bedLevelMatrix = Matrix4X4.Identity;

		// private constructor
		private PrintLevelingPlane()
		{
		}

		static private PrintLevelingPlane instance;

		static public PrintLevelingPlane Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new PrintLevelingPlane();
				}

				return instance;
			}
		}

		public void SetPrintLevelingEquation(Vector3 position0, Vector3 position1, Vector3 position2, Vector2 bedCenter)
		{
			if (position0 == position1 || position1 == position2 || position2 == position0)
			{
				return;
			}

			Plane planeOfPoints = new Plane(position0, position1, position2);

			Ray ray = new Ray(new Vector3(bedCenter, 0), Vector3.UnitZ);
			bool inFront;
			double distanceToPlaneAtBedCenter = planeOfPoints.GetDistanceToIntersection(ray, out inFront);

			Matrix4X4 makePointsFlatMatrix = Matrix4X4.CreateTranslation(-bedCenter.x, -bedCenter.y, -distanceToPlaneAtBedCenter);
			makePointsFlatMatrix *= Matrix4X4.CreateRotation(planeOfPoints.planeNormal, Vector3.UnitZ);
			makePointsFlatMatrix *= Matrix4X4.CreateTranslation(bedCenter.x, bedCenter.y, 0);//distanceToPlaneAtBedCenter);

			bedLevelMatrix = Matrix4X4.Invert(makePointsFlatMatrix);
		}

		public Vector3 ApplyLeveling(Vector3 inPosition)
		{
			return Vector3.TransformPosition(inPosition, bedLevelMatrix);
		}

		public Vector3 ApplyLevelingRotation(Vector3 inPosition)
		{
			return Vector3.TransformVector(inPosition, bedLevelMatrix);
		}

		public string ApplyLeveling(Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode, string lineBeingSent)
		{
			if ((lineBeingSent.StartsWith("G0") || lineBeingSent.StartsWith("G1"))
				&& lineBeingSent.Length > 2
				&& lineBeingSent[2] == ' ')
			{
				double extruderDelta = 0;
				GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
				double feedRate = 0;
				GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

				string newLine = "G1 ";

				if (lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z"))
				{
					Vector3 outPosition = PrintLevelingPlane.Instance.ApplyLeveling(currentDestination);
					if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
					{
						Vector3 relativeMove = Vector3.Zero;
						GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref relativeMove.x);
						GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref relativeMove.y);
						GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref relativeMove.z);
						outPosition = PrintLevelingPlane.Instance.ApplyLevelingRotation(relativeMove);
					}

					newLine = newLine + String.Format("X{0:0.##} Y{1:0.##} Z{2:0.###}", outPosition.x, outPosition.y, outPosition.z);
				}

				if (extruderDelta != 0)
				{
					newLine = newLine + String.Format(" E{0:0.###}", extruderDelta);
				}
				if (feedRate != 0)
				{
					newLine = newLine + String.Format(" F{0:0.##}", feedRate);
				}

				lineBeingSent = newLine;
			}

			return lineBeingSent;
		}
	}
}