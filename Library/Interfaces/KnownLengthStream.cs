/*
Copyright (c) 2017, John Lewin
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
using System.IO;

namespace MatterHackers.MatterControl.Library
{
	// Replace with KnownLength stream. Callers can 
	public class KnownLengthStream : Stream
	{
		protected Stream BaseStream;

		public KnownLengthStream(Stream sourceStream, long Length)
		{
			this.Length = Length;
		}

		public override bool CanRead => this.BaseStream.CanRead;

		public override bool CanSeek => this.BaseStream.CanRead;

		public override bool CanWrite => this.BaseStream.CanRead;

		public override long Length { get; }

		public override long Position
		{
			get { return this.BaseStream.Position; }
			set { this.BaseStream.Position = value; }
		}

		public override void Flush()
		{
			this.BaseStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return this.BaseStream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return this.BaseStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			this.BaseStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			this.BaseStream.Write(buffer, offset, count);
		}
	}

	public class StreamAndLength : IDisposable
	{
		public long Length { get; set; }
		public Stream Stream { get; set; }

		public void Dispose()
		{
			this.Stream?.Dispose();
		}
	}
}
