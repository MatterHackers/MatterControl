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

/**
* by bn on 01/07/2018.
*/

namespace Photon.Parts
{
	public class PhotonFilePreview
	{
		private readonly int dataSize;
		private readonly int imageAddress;
		private int[] imageData;
		private readonly int p1;
		private readonly int p2;
		private readonly int p3;
		private readonly int p4;
		private byte[] rawImageData;
		private readonly int resolutionX;
		private readonly int resolutionY;

		public PhotonFilePreview(int previewAddress, byte[] file)
		{
			byte[] data = ArraysEmulation.CopyOfRange(file, previewAddress, previewAddress + 32);
			var ds = new BinaryReader(new MemoryStream(data));

			resolutionX = ds.ReadInt32();
			resolutionY = ds.ReadInt32();
			imageAddress = ds.ReadInt32();
			dataSize = ds.ReadInt32();
			p1 = ds.ReadInt32();
			p2 = ds.ReadInt32();
			p3 = ds.ReadInt32();
			p4 = ds.ReadInt32();

			rawImageData = ArraysEmulation.CopyOfRange(file, imageAddress, imageAddress + dataSize);

			DecodeImageData();
		}

		public int GetByteSize()
		{
			return 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + dataSize;
		}

		public int[] GetImageData()
		{
			return imageData;
		}

		public int GetResolutionX()
		{
			return resolutionX;
		}

		public int GetResolutionY()
		{
			return resolutionY;
		}

		public void Save(BinaryWriter os, int startAddress)
		{
			os.Write(resolutionX);
			os.Write(resolutionY);
			os.Write(startAddress + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4);
			os.Write(dataSize);
			os.Write(p1);
			os.Write(p2);
			os.Write(p3);
			os.Write(p4);
			os.Write(rawImageData, 0, dataSize);
		}

		public void UnLink()
		{
			rawImageData = null;
			imageData = null;
		}

		private void DecodeImageData()
		{
			imageData = new int[resolutionX * resolutionY];
			int d = 0;
			for (int i = 0; i < dataSize; i++)
			{
				int dot = rawImageData[i] & 0xFF | ((rawImageData[++i] & 0xFF) << 8);

				int color = ((dot & 0xF800) << 8) | ((dot & 0x07C0) << 5) | ((dot & 0x001F) << 3);

				//            int red = ((dot >> 11) & 0x1F) << 3;
				//            int green = ((dot >> 6) & 0x1F) << 3;
				//            int blue = (dot & 0x1F) << 3;
				//            color = red<<16 | green<<8 | blue;

				int repeat = 1;
				if ((dot & 0x0020) == 0x0020)
				{
					repeat += rawImageData[++i] & 0xFF | ((rawImageData[++i] & 0x0F) << 8);
				}

				while (repeat > 0)
				{
					imageData[d++] = color;
					repeat--;
				}
			}
		}
	}
}
