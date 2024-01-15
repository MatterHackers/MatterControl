/*  The MIT License(MIT)

//  Copyright(c) 2015 Stefan Gordon

//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
*/

using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ObjParser.Types
{
	public class ObjFace : IObjArray
	{
		public const int MinimumDataLength = 4;
		public const string Prefix = "f";

		public int[] TextureVertexIndexList { get; set; }
		public string UseMtl { get; set; }
		public int[] VertexIndexList { get; set; }

		public void LoadFromStringArray(string[] data)
		{
			if (data.Length < MinimumDataLength)
				throw new ArgumentException("Input array must be of minimum length " + MinimumDataLength, "data");

			if (!data[0].ToLower().Equals(Prefix))
				throw new ArgumentException("Data prefix must be '" + Prefix + "'", "data");

			int vcount = data.Count() - 1;
			VertexIndexList = new int[vcount];
			TextureVertexIndexList = new int[vcount];

			bool success;

			for (int i = 0; i < vcount; i++)
			{
				string[] parts = data[i + 1].Split('/');

				int vindex;
				success = int.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out vindex);
				if (!success) throw new ArgumentException("Could not parse parameter as int");
				VertexIndexList[i] = vindex;

				if (parts.Count() > 1)
				{
					success = int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out vindex);
					if (success)
					{
						TextureVertexIndexList[i] = vindex;
					}
				}
			}
		}

		// HACKHACK this will write invalid files if there are no texture vertices in
		// the faces, need to identify that and write an alternate format
		public override string ToString()
		{
			StringBuilder b = new StringBuilder();
			b.Append("f");

			for (int i = 0; i < VertexIndexList.Count(); i++)
			{
				if (i < TextureVertexIndexList.Length)
				{
					b.AppendFormat(" {0}/{1}", VertexIndexList[i], TextureVertexIndexList[i]);
				}
				else
				{
					b.AppendFormat(" {0}", VertexIndexList[i]);
				}
			}

			return b.ToString();
		}
	}
}