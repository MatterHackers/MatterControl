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

namespace Photon.Parts
{
	public class PhotonAaMatrix
	{
		public int[,] aaMatrix = new int[5, 5];
		private readonly bool[] hasDivisor = new bool[5];

		public int[,] Calc(int[,] source)
		{
			int[,] target = null;

			if (source != null)
			{
				target = (int[,])source.Clone();

				int divisor = 0;
				for (int y = 0; y < 5; y++)
				{
					int rowDivistor = 0;
					for (int x = 0; x < 5; x++)
					{
						rowDivistor += aaMatrix[y, x];
					}
					hasDivisor[y] = (rowDivistor > 0);
					divisor += rowDivistor;
				}

				if (divisor > 0)
				{
					int height = source.Length;
					if (height > 0)
					{
						int width = source.GetLength(0);
						if (width > 0)
						{
							int sum; ;
							int dy;
							int dx;
							for (int y = 0; y < height; y++)
							{
								for (int x = 0; x < width; x++)
								{
									sum = 0;
									for (int cy = -2; cy <= 2; cy++)
									{
										if (hasDivisor[2 + cy])
											for (int cx = -2; cx <= 2; cx++)
											{
												dy = y + cy;
												dx = x + cx;
												if (dy >= 0 && dy < height)
												{
													if (dx >= 0 && dx < width)
													{
														sum += source[dy, dx] * aaMatrix[2 + cy, 2 + cx];
													}
													else
													{
														sum += source[y, x] * aaMatrix[2 + cy, 2 + cx];
													}
												}
												else
												{
													sum += source[y, x] * aaMatrix[2 + cy, 2 + cx];
												}
											}
									}
									target[y, x] = sum / divisor;
								}
							}
						}
					}
				}
			}
			return target;
		}

		public void Clear()
		{
			for (int y = 0; y < 5; y++)
			{
				for (int x = 0; x < 5; x++)
				{
					aaMatrix[y, x] = 0;
				}
			}
		}

		public void Set(int x, int y, int val)
		{
			aaMatrix[y - 1, x - 1] = val;
		}
	}
}