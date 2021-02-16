/*
 * MIT License
 *
 * Copyright (c) 2018 Bonosoft, 2021 Lars Brubaker c# port
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.IO;

namespace Photon.Parts
{
	public class PhotonFilePrintParameters
	{
		public int bottomLayerCount;
		public float bottomLiftDistance = 5.0f;
		public float bottomLiftSpeed = 300.0f;

		public float bottomLightOffDelay = 0.0f;
		public float costDollars = 0;
		public float liftingDistance = 5.0f;
		public float liftingSpeed = 300.0f;
		public float lightOffDelay = 0.0f;
		public int p1;
		public int p2;
		public int p3;
		public int p4;
		public float retractSpeed = 300.0f;

		public float volumeMl = 0;
		public float weightG = 0;

		public PhotonFilePrintParameters(int bottomLayerCount)
		{
			this.bottomLayerCount = bottomLayerCount;
		}

		public PhotonFilePrintParameters(int parametersPos, byte[] file)
		{
			byte[] data = ArraysEmulation.CopyOfRange(file, parametersPos, parametersPos + GetByteSize());
			var ds = new BinaryReader(new MemoryStream(data));

			bottomLiftDistance = ds.ReadSingle();
			bottomLiftSpeed = ds.ReadSingle();

			liftingDistance = ds.ReadSingle();
			liftingSpeed = ds.ReadSingle();
			retractSpeed = ds.ReadSingle();

			volumeMl = ds.ReadSingle();
			weightG = ds.ReadSingle();
			costDollars = ds.ReadSingle();

			bottomLightOffDelay = ds.ReadSingle();
			lightOffDelay = ds.ReadSingle();
			bottomLayerCount = ds.ReadInt32();

			p1 = ds.ReadInt32();
			p2 = ds.ReadInt32();
			p3 = ds.ReadInt32();
			p4 = ds.ReadInt32();
		}

		public int GetByteSize()
		{
			return 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4;
		}

		public void Save(BinaryWriter os)
		{
			os.Write(bottomLiftDistance);
			os.Write(bottomLiftSpeed);

			os.Write(liftingDistance);
			os.Write(liftingSpeed);
			os.Write(retractSpeed);

			os.Write(volumeMl);
			os.Write(weightG);
			os.Write(costDollars);

			os.Write(bottomLightOffDelay);
			os.Write(lightOffDelay);
			os.Write(bottomLayerCount);

			os.Write(p1);
			os.Write(p2);
			os.Write(p3);
			os.Write(p4);
		}
	}
}