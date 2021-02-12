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

using MatterHackers.Agg;
using System.Collections;
using System.Collections.Generic;

/**
* by bn on 02/07/2018.
*/

namespace Photon.Parts
{
	public class PhotonLayer
	{
		public static readonly byte CONNECTED = 0x03;
		public static readonly byte ISLAND = 0x02;
		public static readonly byte OFF = 0x00;
		public static readonly byte SUPPORTED = 0x01;
		private static int[] emptyCol;
		private static byte[] emptyRow;
		private static byte[] scratchPad;
		private readonly int height;
		private byte[][] iArray;
		private int islandCount = 0;
		private int[] pixels;
		private int[] rowIslands;
		private readonly int width;

		public PhotonLayer(int width, int height)
		{
			this.width = width;
			this.height = height;

			iArray = new byte[height][];
			for (int i = 0; i < height; i++)
			{
				iArray[i] = new byte[width];
			}
			pixels = new int[height];
			rowIslands = new int[height];

			if (emptyRow == null || emptyRow.Length < width)
			{
				emptyRow = new byte[width];
			}

			if (emptyCol == null || emptyCol.Length < height)
			{
				emptyCol = new int[height];
			}

			if (scratchPad == null || scratchPad.Length < width * height)
			{
				scratchPad = new byte[width * height];
			}
		}

		public static List<PhotonRow> GetRows(byte[] packedLayerImage, int width, bool isCalculated)
		{
			var colors = new Dictionary<byte, Color>
			{
				{ OFF, Color.Black }
			};
			if (isCalculated)
			{
				colors.Add(SUPPORTED, new Color("#008800"));
			}
			else
			{
				colors.Add(SUPPORTED, new Color("#000088"));
			}
			colors.Add(CONNECTED, new Color("#FFFF00"));
			colors.Add(ISLAND, new Color("#FF0000"));
			var rows = new List<PhotonRow>();
			int resolutionX = width - 1;
			var currentRow = new PhotonRow();
			rows.Add(currentRow);
			int x = 0;
			if (packedLayerImage != null)
			{ // when user tries to show a layer before its calculated
				for (int i = 0; i < packedLayerImage.Length; i++)
				{
					byte rle = packedLayerImage[i];
					byte colorCode = (byte)((rle & 0x60) >> 5);
					Color color = colors[colorCode];
					bool extended = (rle & 0x80) == 0x80;
					int length = rle & 0x1F;
					if (extended)
					{
						i++;
						length = (length << 8) | packedLayerImage[i] & 0x00ff;
					}
					currentRow.lines.Add(new PhotonLine(color, length));
					x += length;

					if (x >= resolutionX)
					{
						currentRow = new PhotonRow();
						rows.Add(currentRow);
						x = 0;
					}
				}
			}
			return rows;
		}

		public void Clear()
		{
			for (int y = 0; y < height; y++)
			{
				ArraysEmulation.Arraycopy(emptyRow, 0, iArray[y], 0, width);
			}
			ArraysEmulation.Arraycopy(emptyCol, 0, pixels, 0, height);
			ArraysEmulation.Arraycopy(emptyCol, 0, rowIslands, 0, height);
		}

		public int Fixlayer()
		{
			var photonMatix = new PhotonMatix();
			var dots = new List<PhotonDot>();
			if (islandCount > 0)
			{
				for (int y = 0; y < height; y++)
				{
					if (rowIslands[y] > 0)
					{
						for (int x = 0; x < width; x++)
						{
							if (iArray[y][x] == ISLAND)
							{
								photonMatix.Clear();
								int blanks = photonMatix.Set(x, y, iArray, width, height);
								if (blanks > 0)
								{ // one or more neighbor pixels are OFF
									photonMatix.Calc();
									photonMatix.Level();
									photonMatix.Calc();

									for (int ry = 0; ry < 3; ry++)
									{
										for (int rx = 0; rx < 3; rx++)
										{
											int iy = y - 1 + ry;
											int ix = x - 1 + rx;
											if (iArray[iy][ix] == OFF)
											{
												if (photonMatix.calcMatrix[1 + ry, 1 + rx] > 3)
												{
													dots.Add(new PhotonDot(ix, iy));
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			foreach (var dot in dots)
			{
				Island(dot.x, dot.y);
			}
			return dots.Count;
		}

		public byte Get(int x, int y)
		{
			return iArray[y][x];
		}

		public void Island(int x, int y)
		{
			iArray[y][x] = ISLAND;
			rowIslands[y]++;
			islandCount++;
			pixels[y]++;
		}

		public byte[] PackImageData()
		{
			int ptr = 0;

			for (int y = 0; y < height; y++)
			{
				if (pixels[y] == 0)
				{
					ptr = AddPhotonRLE(ptr, true, width);
				}
				else
				{
					byte current = OFF;
					int length = 0;
					for (int x = 0; x < width; x++)
					{
						byte next = iArray[y][x];
						if (next != current)
						{
							if (length > 0)
							{
								ptr = AddPhotonRLE(ptr, current == OFF, length);
							}
							current = next;
							length = 1;
						}
						else
						{
							length++;
						}
					}
					if (length > 0)
					{
						ptr = AddPhotonRLE(ptr, current == OFF, length);
					}
				}
			}
			byte[] img = new byte[ptr];
			ArraysEmulation.Arraycopy(scratchPad, 0, img, 0, ptr);
			return img;
		}

		public byte[] PackLayerImage()
		{
			int ptr = 0;
			for (int y = 0; y < height; y++)
			{
				if (pixels[y] == 0)
				{
					ptr = Add(ptr, OFF, width);
				}
				else
				{
					byte current = OFF;
					int length = 0;
					for (int x = 0; x < width; x++)
					{
						byte next = iArray[y][x];
						if (next != current)
						{
							if (length > 0)
							{
								ptr = Add(ptr, current, length);
							}
							current = next;
							length = 1;
						}
						else
						{
							length++;
						}
					}
					if (length > 0)
					{
						ptr = Add(ptr, current, length);
					}
				}
			}
			byte[] img = new byte[ptr];
			ArraysEmulation.Arraycopy(scratchPad, 0, img, 0, ptr);
			return img;
		}

		public void Reduce()
		{
			// Double reduce to handle single line connections.
			for (int i = 0; i < 2; i++)
			{
				if (islandCount > 0)
				{
					for (int y = 0; y < height; y++)
					{
						if (rowIslands[y] > 0)
						{
							for (int x = 0; x < width; x++)
							{
								if (iArray[y][x] == ISLAND)
								{
									if (Connected(x, y))
									{
										MakeConnected(x, y);
										CheckUp(x, y);
										if (rowIslands[y] == 0)
										{
											break;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		public void Remove(int x, int y, byte type)
		{
			iArray[y][x] = OFF;
			if (type == ISLAND)
			{
				rowIslands[y]--;
				islandCount--;
			}

			pixels[y]--;
		}

		public int RemoveIslands()
		{
			int count = 0;
			if (islandCount > 0)
			{
				for (int y = 0; y < height; y++)
				{
					if (rowIslands[y] > 0)
					{
						for (int x = 0; x < width; x++)
						{
							if (iArray[y][x] == ISLAND)
							{
								Remove(x, y, ISLAND);
								++count;
							}
						}
					}
				}
			}
			return count;
		}

		public int SetIslands(List<BitArray> islandRows)
		{
			int islands = 0;
			for (int y = 0; y < height; y++)
			{
				var bitSet = new BitArray(width - 1);
				if (rowIslands[y] > 0)
				{
					for (int x = 0; x < width; x++)
					{
						if (iArray[y][x] == ISLAND)
						{
							bitSet[x] = true;
						}
					}
				}
				islandRows.Add(bitSet);
				islands += rowIslands[y];
			}
			return islands;
		}

		public void Supported(int x, int y)
		{
			iArray[y][x] = SUPPORTED;
			pixels[y]++;
		}

		public void UnLink()
		{
			iArray = null;
			pixels = null;
			rowIslands = null;
		}

		public void UnpackLayerImage(byte[] packedLayerImage)
		{
			Clear();
			int x = 0;
			int y = 0;
			int imageLength = packedLayerImage.Length;
			for (int i = 0; i < imageLength; i++)
			{
				byte rle = packedLayerImage[i];
				byte colorCode = (byte)((rle & 0x60) >> 5);

				bool extended = (rle & 0x80) == 0x80;
				int length = rle & 0x1F;
				if (extended)
				{
					i++;
					length = (length << 8) | packedLayerImage[i] & 0x00ff;
				}

				ArraysEmulation.Fill(iArray[y], x, x + length, colorCode);

				if (colorCode == SUPPORTED)
				{
					pixels[y] += length;
				}
				else if (colorCode == CONNECTED)
				{
					pixels[y] += length;
				}
				else if (colorCode == ISLAND)
				{
					rowIslands[y] += length;
					islandCount += length;
					pixels[y] += length;
				}

				x += length;
				if (x >= width)
				{
					y++;
					x = 0;
				}
			}
		}

		public void UnSupported(int x, int y)
		{
			iArray[y][x] = CONNECTED;
			pixels[y]++;
		}

		private int Add(int ptr, byte current, int length)
		{
			if (length < 32)
			{
				scratchPad[ptr++] = (byte)((current << 5) | (length & 0x1f));
			}
			else
			{
				scratchPad[ptr++] = (byte)(0x80 | (current << 5) | (length >> 8 & 0x00FF));
				scratchPad[ptr++] = (byte)(length & 0x00FF);
			}
			return ptr;
		}

		private int AddPhotonRLE(int ptr, bool off, int length)
		{
			while (length > 0)
			{
				int lineLength = length < 125 ? length : 125; // max storage length of 0x7D (125) ?? Why not 127?
				scratchPad[ptr++] = (byte)((off ? 0x00 : 0x80) | (lineLength & 0x7f));
				length -= lineLength;
			}

			return ptr;
		}

		private void CheckBackUp(int x, int y)
		{
			if (y > 0 && rowIslands[y - 1] > 0 && iArray[y - 1][x] == ISLAND)
			{
				MakeConnected(x, y - 1);
				CheckBackUp(x, y - 1);
			}
			if (x > 0 && rowIslands[y] > 0 && iArray[y][x - 1] == ISLAND)
			{
				MakeConnected(x - 1, y);
				CheckBackUp(x - 1, y);
			}
		}

		private void CheckFrontUp(int x, int y)
		{
			if (y > 0 && rowIslands[y - 1] > 0 && iArray[y - 1][x] == ISLAND)
			{
				MakeConnected(x, y - 1);
				CheckFrontUp(x, y - 1);
			}
			if (x < (width - 1) && rowIslands[y] > 0 && iArray[y][x + 1] == ISLAND)
			{
				MakeConnected(x + 1, y);
				CheckFrontUp(x + 1, y);
			}
		}

		private void CheckUp(int x, int y)
		{
			if (y > 0 && rowIslands[y - 1] > 0 && iArray[y - 1][x] == ISLAND)
			{
				MakeConnected(x, y - 1);
				CheckUp(x, y - 1);
			}
			if (x > 0 && rowIslands[y] > 0 && iArray[y][x - 1] == ISLAND)
			{
				MakeConnected(x - 1, y);
				CheckBackUp(x - 1, y);
			}
			if (x < (width - 1) && rowIslands[y] > 0 && iArray[y][x + 1] == ISLAND)
			{
				MakeConnected(x + 1, y);
				CheckFrontUp(x + 1, y);
			}
		}

		private bool Connected(int x, int y)
		{
			return x > 0 && (iArray[y][x - 1] & 0x01) == SUPPORTED
					|| x < (width - 1) && (iArray[y][x + 1] & 0x01) == SUPPORTED
					|| y > 0 && (iArray[y - 1][x] & 0x01) == SUPPORTED
					|| (y < (height - 1) && (iArray[y + 1][x] & 0x01) == SUPPORTED);
		}

		private void MakeConnected(int x, int y)
		{
			iArray[y][x] = CONNECTED;
			rowIslands[y]--;
			islandCount--;
		}

		/**
         * Get a layer image for drawing.
         * <p/>
         * This will decode the RLE packed layer information and return a list of rows, with color and length information
         *
         * @param packedLayerImage The packed layer image information
         * @param width            The width of the current layer, used to change rows
         * @return A list with the
         */
	}
}