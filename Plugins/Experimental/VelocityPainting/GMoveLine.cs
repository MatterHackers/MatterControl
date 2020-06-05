/*
// Velocity Painting by [Mark Wheadon](https://github.com/MarkWheadon) is licensed under a [Creative Commons Attribution 4.0
// International License](http://creativecommons.org/licenses/by/4.0/).
// Based on a work at https://github.com/MarkWheadon/velocity-painting.
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class VelocityPainter
{
	private class GMoveLine
	{
		private static Regex GMoves = new Regex("G[01]\\s*([XYZFE]-*[\\d\\.]+\\s*)*", RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public double X { get; private set; }
		public double Y { get; private set; }
		public double Z { get; private set; }
		public double E { get; private set; }
		public double F { get; private set; }

		static double lastX = double.MinValue;
		static double lastY = double.MinValue;
		static double lastZ = double.MinValue;
		static double lastE = 0;
		static double lastF = double.MinValue;

		public static void Restart()
		{
			lastX = double.MinValue;
			lastY = double.MinValue;
			lastZ = double.MinValue;
			lastE = 0;
			lastF = double.MinValue;
		}

		public GMoveLine(string line)
		{
			double axisValue;
			var results = ConvertToDictionary(line);

			if (results.TryGetValue("X", out axisValue))
			{
				lastX = axisValue;
			}
			if (results.TryGetValue("Y", out axisValue))
			{
				lastY = axisValue;
			}
			if (results.TryGetValue("Z", out axisValue))
			{
				lastZ = axisValue;
			}
			if (results.TryGetValue("E", out axisValue))
			{
				lastE = axisValue;
			}
			if (results.TryGetValue("F", out axisValue))
			{
				lastF = axisValue;
			}
			this.X = lastX;
			this.Y = lastY;
			this.Z = lastZ;
			this.E = lastE;
			this.F = lastF;
		}

		private Dictionary<string, double> ConvertToDictionary(string line)
		{
			var d = new Dictionary<string, double>();

			Match match = GMoves.Match(line);
			if (match.Success)
			{
				var captures = match.Groups[1].Captures;

				foreach (Capture capture in captures)
				{
					double value;
					if (double.TryParse(capture.Value.Substring(1), out value))
					{
						d.Add(capture.Value.Substring(0, 1), value);
					}
				}
			}

			return d;
		}
	}
}