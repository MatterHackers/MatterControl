using System;
using MatterControl.Printing;
using MatterHackers.VectorMath;

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

		public Vector3 ApplyLeveling(Vector3 inPosition)
		{
			return Vector3Ex.TransformPosition(inPosition, bedLevelMatrix);
		}

		public Vector3 ApplyLevelingRotation(Vector3 inPosition)
		{
			return Vector3Ex.TransformVector(inPosition, bedLevelMatrix);
		}

		public string ApplyLeveling(Vector3 currentDestination, string lineBeingSent)
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

					newLine = newLine + String.Format("X{0:0.##} Y{1:0.##} Z{2:0.###}", outPosition.X, outPosition.Y, outPosition.Z);
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