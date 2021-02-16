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

/**
 * by bn on 14/07/2018.
 */

namespace Photon.Parts
{
	public class PhotonMatix
	{
		public int[,] calcMatrix = new int[5, 5];

		public void Calc()
		{
			int[,] temp = new int[3, 3];
			for (int yi = 0; yi < 3; yi++)
			{
				for (int xi = 0; xi < 3; xi++)
				{
					if (calcMatrix[yi + 1, xi + 1] == PhotonLayer.OFF)
					{
						temp[yi, xi] = Calc(xi + 1, yi + 1);
					}
				}
			}
			for (int yi = 0; yi < 3; yi++)
			{
				for (int xi = 0; xi < 3; xi++)
				{
					if (calcMatrix[yi + 1, xi + 1] == PhotonLayer.OFF)
					{
						calcMatrix[yi + 1, xi + 1] = temp[yi, xi];
					}
				}
			}
		}

		public void Clear()
		{
			for (int y = 0; y < 5; y++)
			{
				for (int x = 0; x < 5; x++)
				{
					calcMatrix[y, x] = 0;
				}
			}
		}

		public void Level()
		{
			for (int yi = 0; yi < 5; yi++)
			{
				for (int xi = 0; xi < 5; xi++)
				{
					if (calcMatrix[yi, xi] < 4) calcMatrix[yi, xi] = 0;
				}
			}
		}

		public int Set(int x, int y, byte[][] iArray, int width, int height)
		{
			int blanks = 0;
			int x0 = x - 2;
			int y0 = y - 2;
			for (int yi = 0; yi < 5; yi++)
			{
				for (int xi = 0; xi < 5; xi++)
				{
					int y2 = y0 + yi;
					int x2 = x0 + xi;
					if (y2 >= 0 && y2 < height && x2 >= 0 && x2 < width)
					{
						var value = iArray[y2][x2];
						if (value == PhotonLayer.SUPPORTED
							|| value == PhotonLayer.CONNECTED)
						{
							calcMatrix[yi, xi] = 16;
						}
						else if (value == PhotonLayer.ISLAND)
						{
							calcMatrix[yi, xi] = 4;
						}
						else if (value == PhotonLayer.OFF)
						{
							if (yi > 0 && yi < 4 && xi > 0 && xi < 4)
							{
								blanks++;
							}
						}
					}
				}
			}

			return blanks;
		}

		private int Calc(int x, int y)
		{
			return (calcMatrix[y - 1, x] / 4) + (calcMatrix[y, x - 1] / 4) + (calcMatrix[y, x + 1] / 4) + (calcMatrix[y + 1, x] / 4);
		}
	}
}