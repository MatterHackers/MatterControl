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

using System.Collections;

/**
* by bn on 01/07/2018.
*/

namespace Photon.Parts
{
	public static class BitArrayExtensions
	{
		/// <summary>
		/// returns the next set bit or -1 if no more set bits
		/// </summary>
		/// <param name="bitArray">The array to check</param>
		/// <param name="startIndex">The index to start checking from</param>
		/// <returns>The next set bit or -1</returns>
		public static int NextSetBit(this BitArray bitArray, int startIndex)
		{
			int offset = startIndex >> 6;
			long mask = 1L << startIndex;
			while (offset < bitArray.Length)
			{
				var h = bitArray[offset] ? 1 : 0;
				do
				{
					if ((h & mask) != 0)
					{
						return startIndex;
					}

					mask <<= 1;
					startIndex++;
				}
				while (mask != 0);

				mask = 1;
				offset++;
			}

			return -1;
		}

		public static void Set(this BitArray bitArray, int start, int end)
		{
			for (int i = start; i < end; i++)
			{
				bitArray[i] = true;
			}
		}
	}


}