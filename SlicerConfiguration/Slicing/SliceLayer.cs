/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.Slicing
{
	public class SliceLayer
	{
		public struct Segment
		{
			internal Vector2 start;

			public Vector2 Start { get { return start; } }

			internal Vector2 end;

			public Vector2 End { get { return end; } }

			internal Segment(Vector2 start, Vector2 end)
			{
				this.start = start;
				this.end = end;
			}
		}

		private double zHeight;

		public double ZHeight { get { return zHeight; } }

		private List<Segment> unorderedSegments = new List<Segment>();

		public List<Segment> UnorderedSegments { get { return unorderedSegments; } }

		private List<VertexStorage> perimeters;

		public List<VertexStorage> Perimeters { get { return perimeters; } }

		public SliceLayer(double zHeight)
		{
			this.zHeight = zHeight;
		}
	}
}