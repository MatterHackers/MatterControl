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

using System;

public static class ArraysEmulation
{
	public static void Arraycopy(byte[] emptyRow, int v1, byte[] vs, int v2, int width)
	{
		throw new NotImplementedException();
	}

	public static byte[] CopyOfRange(byte[] src, int start, int end)
	{
		int len = end - start;
		var dest = new byte[len];
		// note i is always from 0
		for (int i = 0; i < len; i++)
		{
			dest[i] = src[start + i]; // so 0..n = 0+x..n+x
		}
		return dest;
	}

	public static void Fill<T>(T[] array, int start, int end, T value)
	{
		if (array == null)
		{
			throw new ArgumentNullException("array is null");
		}

		if (start < 0 || start >= end)
		{
			throw new ArgumentOutOfRangeException("fromIndex");
		}
		if (end >= array.Length)
		{
			throw new ArgumentOutOfRangeException("toIndex");
		}
		for (int i = start; i < end; i++)
		{
			array[i] = value;
		}
	}

	internal static void Arraycopy(int[] emptyCol, int v1, int[] pixels, int v2, int height)
	{
		throw new NotImplementedException();
	}
}