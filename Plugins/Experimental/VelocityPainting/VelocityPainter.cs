/*
// Velocity Painting by [Mark Wheadon](https://github.com/MarkWheadon) is licensed under a [Creative Commons Attribution 4.0
// International License](http://creativecommons.org/licenses/by/4.0/).
// Based on a work at https://github.com/MarkWheadon/velocity-painting.
*/

using System;
using System.IO;
using MatterHackers.Agg.Image;

public partial class VelocityPainter
{
	int imageWidth;
	int imageHeight;

	int speedRange;

	double lastZOutput = double.MinValue;

	private ProjectionMode projectionMode;

	private ImageBuffer image;

	public VelocityPainter(StreamWriter streamWriter, Stream inputStream)
	{
		this.outputStream = streamWriter;
		this.inputStream = new StreamReader(inputStream);
	}

	public double printCentreX { get; set; } = 0;
	public double printCentreY { get; set; } = 0;

	public double projectedImageWidth { get; set; } = 0;
	public double projectedImageHeight { get; set; } = 0;

	public double xOffset { get; set; }
	public double yOffset { get; set; }


	private int lowSpeed;
	private int highSpeed;
	private int outsideSpeed;

	public int OutsideSpeed
	{
		// Convert from/to millimeters per second
		get => outsideSpeed / 60;
		set => outsideSpeed = value * 60;
	}

	public int HighSpeed
	{
		// Convert from/to millimeters per second
		get => highSpeed / 60;
		set => highSpeed = value * 60;
	}

	public int LowSpeed
	{
		// Convert from/to millimeters per second
		get => lowSpeed / 60;
		set => lowSpeed = value * 60;
	}

	public int targetSpeed { get; set; } = 0;
	public double zOffset { get; set; } = 0;

	private StreamWriter outputStream;
	private StreamReader inputStream;

	private double oldZ { get; set; } = double.MinValue;

	public void Generate(ImageBuffer colourImage, ProjectionMode projectionMode)
	{
		this.projectionMode = projectionMode;

		bool isProjection = projectionMode == ProjectionMode.ProjectX
			|| projectionMode == ProjectionMode.ProjectY
			|| projectionMode == ProjectionMode.ProjectZ;

		/*
		if (isProjection)
		{
			// TODO: Get/use projectedImageWidth
			projectedImageWidth = 0;
		} */

		// ConvertToGreyscale
		// this.image = colourImage->convert(preset=>"grey");

		// TODO: Get working in app, this works for porting:  Convert image to grayscale externally
		this.image = colourImage;

		// Get image dimensions
		imageWidth = this.image.Width;
		imageHeight = this.image.Height;

		GMoveLine.Restart();

		// Compute speed range
		speedRange = highSpeed - lowSpeed;

		/*
		if ((projectionMode  != "-spherical") && (((projectionMode  == "-cylinderZ") || projectedImageWidth  == "-") && projectedImageHeight  == "-")) {
			print STDERR <<END;
		0: you must set either the image width or its height, or both.
		END
			exit 1;
		}*/

		/* ******************************************************** */
		/*               TODO: Scale width/height
		/* ******************************************************** */

		/*
		if (defined projectedImageWidth && projectedImageWidth == "-") {
			projectedImageWidth = projectedImageHeight * imageWidth / imageHeight;
		}

		if (defined projectedImageHeight && projectedImageHeight == "-") {
			projectedImageHeight = projectedImageWidth * imageHeight / imageWidth;
		}*/

		var maxVecLength = .1; // Longest vector in mm. Splits longer vectors. Very small -> long processing times.

		double oldX = double.MinValue;
		double oldY = double.MinValue;
		double oldE = 0;
		double currentZ = double.MinValue;

		double lastZOutput = -1;

		string line;

		while(null != (line = inputStream.ReadLine()))
		//foreach (string line in allLines)
		{
			bool hasMove = line.StartsWith("G0") || line.StartsWith("G1");

			GMoveLine gmove = null;

			if(hasMove)
			{
				gmove = new GMoveLine(line);
			}

			double x = (hasMove) ? gmove.X : double.MinValue;
			double y = (hasMove) ? gmove.Y : double.MinValue;
			double z = (hasMove) ? gmove.Z : double.MinValue;
			double e = (hasMove) ? gmove.E : 0;
			double f = (hasMove) ? gmove.F : double.MinValue;

			z = (z != double.MinValue) ? z : currentZ;

			if (x != double.MinValue)
			{
				// If this is the first move and oldZ is yet to be defined
				if (oldZ == double.MinValue)
				{
					outMove(x, y, z, e, false);
				}
				else
				{
					var xd = x - oldX;
					var yd = y - oldY;
					var zd = z - oldZ;
					var ed = e - oldE;

					var length = Math.Sqrt(xd * xd + yd * yd + zd * zd);

					if (length <= maxVecLength)
					{
						outMove(x, y, z, e, false);
					}
					else
					{
						var lastSegOut = 0;
						var oSlow = surfaceSpeed(oldX, oldY, oldZ);

						double nSegs = (int)(length / maxVecLength + 0.5);

						var xDelta = xd / nSegs;
						var yDelta = yd / nSegs;
						var zDelta = zd / nSegs;
						var eDelta = ed / nSegs;

						// Break the source G0/1 move into segments of maxVecLength
						for (var i = 1; i <= nSegs; i++)
						{
							var nx = oldX + xDelta * i;
							var ny = oldY + yDelta * i;
							var nz = oldZ + zDelta * i;

							var slow = surfaceSpeed(nx, ny, nz);
							if (slow != oSlow && i > 1)
							{
								// pattern has changed. Time to output the vector so far
								outMove(
									oldX + xDelta * (i - 1),
									oldY + yDelta * (i - 1),
									oldZ + zDelta * (i - 1),
									oldE + eDelta * (i - 1),
									true);

								oSlow = slow;
								lastSegOut = i;
							}
						}

						if (lastSegOut != nSegs)
						{
							outMove(
								x,
								y,
								z,
								oldE + eDelta * (nSegs - 1),
								lastSegOut != 0);
						}
					}
				}

				oldX = x;
				oldY = y;
				oldZ = z;
				oldE = e;
			}
			else
			{
				if (gmove != null)
				{
					if (gmove.X != double.MinValue)
					{
						oldX = gmove.X;
					}

					if (gmove.Y != double.MinValue)
					{
						oldY = gmove.Y;
					}

					if (gmove.Z != double.MinValue)
					{
						currentZ = oldZ = gmove.Z;
					}

					if (gmove.E != double.MinValue)
					{
						oldE = gmove.E;
					}
				}

				outputStream.WriteLine(line);
			}
		}
	}

	public enum ProjectionMode
	{
		CylinderZ,
		ProjectX,
		ProjectY,
		ProjectZ,
		Spherical
	}

	private double surfaceSpeed(double x, double y, double z)
	{
		switch (projectionMode)
		{
			case ProjectionMode.CylinderZ:
				return surfaceSpeedCylinderZ(x, y, z);

			case ProjectionMode.ProjectX:
				return surfaceSpeedProjectX(x, y, z);

			case ProjectionMode.ProjectY:
				return surfaceSpeedProjectY(x, y, z);

			case ProjectionMode.ProjectZ:
				return surfaceSpeedProjectZ(x, y, z);

			case ProjectionMode.Spherical:
				xOffset = 0;
				yOffset = 0;
				return surfaceSpeedSpherical(x, y, z);
		}

		return double.MinValue;
	}

	private bool outsideImage(double imageX, double imageY)
	{
		return imageX < 0
			|| imageX >= imageWidth
			|| imageY < 0
			|| imageY >= imageHeight;
	}

	private double surfaceSpeedProjectY(double x, double y, double z)
	{
		var xNormalized = (y - printCentreY + (double)projectedImageWidth / 2.0) / (double)projectedImageWidth;
		var zNormalized = (z - zOffset) / (double)projectedImageHeight;

		var imageX = xNormalized * imageWidth;
		var imageY = zNormalized * (double)imageHeight;

		if (outsideImage(imageX, imageY))
		{
			// return highSpeed;
			return outsideSpeed;
		}

		var grey = greyAt(imageX, imageY);
		var greyAmount = grey / 255;

		return lowSpeed + greyAmount * (double)speedRange;
	}

	private double greyAt(double dx, double dy)
	{
		// TODO: Conversion from double to int needs reconsidered
		int x = (int)Math.Floor(dx);
		int y = (int)Math.Floor(dy);

		//Console.WriteLine("{0}/{1} {2}/{3}", dx, dy, x, y);

		// Get pixel color
		var color = image.GetPixel(x, y);
		return color.red;
	}

	private void outMove(double x, double y, double z, double e, bool extra)
	{
		string added = extra ? " ; added" : "";
		double fspeed = surfaceSpeed(x, y, z);

		outputStream.Write("G1 ");

		if (x != double.MinValue)
		{
			outputStream.Write("X{0:0.000} ", x);
		}

		if (y != double.MinValue)
		{
			outputStream.Write("Y{0:0.000} ", y);
		}

		if (z != lastZOutput && z != double.MinValue)
		{
			outputStream.Write("Z{0:0.000} ", z);
		}

		if (e != double.MinValue)
		{
			outputStream.Write("E{0:0.000} ", e);
		}

		outputStream.Write("F{0:0.000} ", fspeed);

		outputStream.Write(Environment.NewLine);

		lastZOutput = z;
	}

	private double surfaceSpeedProjectX(double x, double y, double z)
	{

		var xNormalized = (x - printCentreX + projectedImageWidth / 2) / projectedImageWidth;
		var zNormalized = (z - zOffset) / projectedImageHeight;

		var imageX = xNormalized * imageWidth;
		var imageY = imageHeight - zNormalized * imageHeight;

		if (outsideImage(imageX, imageY))
		{
			// return highSpeed;
			return lowSpeed;
		}

		return lowSpeed + greyAt(imageX, imageY) * speedRange;
	}

	private double surfaceSpeedProjectZ(double x, double y, double z)
	{

		var xNormalized = (x - printCentreX + projectedImageWidth / 2) / projectedImageWidth;
		var yNormalized = (y - printCentreY + projectedImageHeight / 2) / projectedImageHeight;

		var imageX = xNormalized * imageWidth;
		var imageY = yNormalized * imageHeight;

		if (outsideImage(imageX, imageY))
		{
			// return highSpeed;
			return lowSpeed;
		}

		return lowSpeed + greyAt(imageX, imageY) * speedRange;
	}

	private double surfaceSpeedSpherical(double x, double y, double z)
	{

		var theta = Math.Atan2(y - yOffset - printCentreY, x - xOffset - printCentreX) + Math.PI; // 0 to 2pi
		var xNormalized = theta / (2 * Math.PI);

		theta = Math.Atan2(z - zOffset, x - xOffset - printCentreX) + Math.PI; // 0 to 2pi
		var zNormalized = theta / (2 * Math.PI);

		double imageX = xNormalized * imageWidth;
		double imageY = imageHeight - zNormalized * imageHeight;

		if (imageX < 0 || imageX >= imageWidth || imageY < 0 || imageY >= imageHeight)
		{
			return highSpeed;
			// return lowSpeed;
		}

		return lowSpeed + greyAt(imageX, imageY) * speedRange;
	}

	private double surfaceSpeedCylinderZ(double x, double y, double z)
	{

		var zNormalized = (z - zOffset) / projectedImageHeight;

		var theta = Math.Atan2(y - printCentreY, x - printCentreX) + Math.PI; // 0 to 2pi
		var xNormalized = theta / (2 * Math.PI);

		var imageX = xNormalized * imageWidth;
		var imageY = imageHeight - zNormalized * imageHeight;

		if (outsideImage(imageX, imageY))
		{
			return highSpeed;
			// return lowSpeed;
		}

		return lowSpeed + greyAt(imageX, imageY) * speedRange;
	}
}