using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl.OpenGl;

/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeVertexBuffer : IDisposable
	{
		public int myIndexId;
		public int myIndexLength;
		public BeginMode myMode = BeginMode.Triangles;
		public int myVertexId;
		public int myVertexLength;
		public GCodeVertexBuffer()
		{
			GL.GenBuffers(1, out myVertexId);
			GL.GenBuffers(1, out myIndexId);
		}

		public void Dispose()
		{
			if (myVertexId != -1)
			{
				int holdVertexId = myVertexId;
				int holdIndexId = myIndexId;
				UiThread.RunOnIdle(() =>
				{
					GL.DeleteBuffers(1, ref holdVertexId);
					GL.DeleteBuffers(1, ref holdIndexId);
				});

				myVertexId = -1;
			}
		}

		~GCodeVertexBuffer()
		{
			Dispose();
		}

		public void renderRange(int offset, int count)
		{
			GL.EnableClientState(ArrayCap.ColorArray);
			GL.EnableClientState(ArrayCap.NormalArray);
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.TextureCoordArray);
			GL.Disable(EnableCap.Texture2D);

			GL.EnableClientState(ArrayCap.IndexArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, myVertexId);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, myIndexId);

			GL.ColorPointer(4, ColorPointerType.UnsignedByte, ColorVertexData.Stride, new IntPtr(0));
			GL.NormalPointer(NormalPointerType.Float, ColorVertexData.Stride, new IntPtr(4));
			GL.VertexPointer(3, VertexPointerType.Float, ColorVertexData.Stride, new IntPtr(4 + 3 * 4));

			GL.DrawRangeElements(myMode, 0, myIndexLength, count, DrawElementsType.UnsignedInt, new IntPtr(offset * 4));

			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			GL.DisableClientState(ArrayCap.IndexArray);

			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
			GL.DisableClientState(ArrayCap.ColorArray);
		}

		public void SetIndexData(int[] data)
		{
			SetIndexData(data, data.Length);
		}

		public void SetIndexData(int[] data, int count)
		{
			myIndexLength = count;
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, myIndexId);
			unsafe
			{
				fixed (int* dataPointer = data)
				{
					GL.BufferData(BufferTarget.ElementArrayBuffer, data.Length * sizeof(int), (IntPtr)dataPointer, BufferUsageHint.StaticDraw);
				}
			}
		}

		public void SetVertexData(ColorVertexData[] data)
		{
			SetVertexData(data, data.Length);
		}

		public void SetVertexData(ColorVertexData[] data, int count)
		{
			myVertexLength = count;
			GL.BindBuffer(BufferTarget.ArrayBuffer, myVertexId);
			unsafe
			{
				fixed (ColorVertexData* dataPointer = data)
				{
					GL.BufferData(BufferTarget.ArrayBuffer, data.Length * ColorVertexData.Stride, (IntPtr)dataPointer, BufferUsageHint.StaticDraw);
				}
			}
		}
	}
}