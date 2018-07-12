/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeVertexBuffer : IDisposable
	{
		private int indexID;
		private int indexLength;
		private BeginMode pointMode = BeginMode.Triangles;
		private bool disposed = false;

		private int vertexID;
		private int vertexLength;

		public GCodeVertexBuffer(int[] indexData, ColorVertexData[] colorData)
		{
			GL.GenBuffers(1, out vertexID);
			GL.GenBuffers(1, out indexID);

			// Set vertex data
			vertexLength = colorData.Length;
			if (vertexLength > 0)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, vertexID);
				unsafe
				{
					fixed (ColorVertexData* dataPointer = colorData)
					{
						GL.BufferData(BufferTarget.ArrayBuffer, colorData.Length * ColorVertexData.Stride, (IntPtr)dataPointer, BufferUsageHint.StaticDraw);
					}
				}
			}

			// Set index data
			indexLength = indexData.Length;
			if (indexLength > 0)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexID);
				unsafe
				{
					fixed (int* dataPointer = indexData)
					{
						GL.BufferData(BufferTarget.ElementArrayBuffer, indexData.Length * sizeof(int), (IntPtr)dataPointer, BufferUsageHint.StaticDraw);
					}
				}
			}
		}

		public void RenderRange(int offset, int count)
		{
			GL.EnableClientState(ArrayCap.ColorArray);
			GL.EnableClientState(ArrayCap.NormalArray);
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.TextureCoordArray);
			GL.Disable(EnableCap.Texture2D);

			GL.EnableClientState(ArrayCap.IndexArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, vertexID);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexID);

			GL.ColorPointer(4, ColorPointerType.UnsignedByte, ColorVertexData.Stride, new IntPtr(0));
			GL.NormalPointer(NormalPointerType.Float, ColorVertexData.Stride, new IntPtr(4));
			GL.VertexPointer(3, VertexPointerType.Float, ColorVertexData.Stride, new IntPtr(4 + 3 * 4));

			// ** Draw **
			GL.DrawRangeElements(
				pointMode,
				0,
				indexLength,
				count,
				DrawElementsType.UnsignedInt,
				new IntPtr(offset * 4));

			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			GL.DisableClientState(ArrayCap.IndexArray);

			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
			GL.DisableClientState(ArrayCap.ColorArray);
		}

		protected virtual void Dispose(bool disposing)
		{
			// release unmanaged resources
			if (!disposed)
			{
				UiThread.RunOnIdle(() =>
				{
					GL.DeleteBuffers(1, ref vertexID);
					GL.DeleteBuffers(1, ref indexID);
				});

				disposed = true;
			}

			if (disposing)
			{
				// release other Managed objects
				// if (resource!= null) resource.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~GCodeVertexBuffer()
		{
			Dispose(false);
		}
	}
}