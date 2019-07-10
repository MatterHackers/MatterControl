using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	public class X3GPrinterDetails
	{
		public Vector3 stepsPerMm;//Make a M command to receive steps and save into these static values (will be sent after successful connect)
		public long extruderStepsPerMm;

		public Vector3 currentPosition;
		public float activeExtruderPosition;
		public float inactiveExtruderPosition;
		public Vector3 targetMovePosition;
		public float targetExtruderPosition;
		public Vector3 positionalOffset;//Used in absolute to simulate 0,0 being at the bottom left of the print bed
		public bool extruderRelativePos;
		public Vector3 homingFeedRate;

		public byte activeExtruderIndex;
		public Vector2 extruderOffset;

		public bool heatingLockout;//boolean that is used to mimic M109, suppresses oks and sends M105s to the printer until target temp is reached
		public int[] targetExtruderTemps;
		public int targetBedTemp;
		public int targetTempForMakerbotStyleCommands;
		public int requiredTemperatureResponseCount;//The number of responses from the printer that corresponds to one M105 (adjusts to extruders & bed heating as required)
		public int teperatureResponseCount;//number of responses for temperature currently received from the printer. resets after hitting target count
		public long dwellTime; //this is set during a dwell command and is reset to zero after the dwell has completed

		public X3GPrinterDetails()
		{
			currentPosition = new Vector3(/*285,150,0*/);//defaults to "far corner" (where the machine thinks it is at 0,0) inverted positional offset
			activeExtruderPosition = 0;
			targetMovePosition = new Vector3();
			targetExtruderPosition = 0;
			positionalOffset = new Vector3(285, 150, 0);
			stepsPerMm = new Vector3(88.8, 88.8, 400); //Default steps per mm in case they are not set
			extruderStepsPerMm = 101;//repG says 96
			homingFeedRate = new Vector3(300, 300, 400);
			activeExtruderIndex = 0;
			targetExtruderTemps = new int[2];
			targetBedTemp = 0;
			inactiveExtruderPosition = 0;
			extruderOffset = new Vector2();
			extruderRelativePos = false;
		}
	}
}
