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

using ObjParser.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ObjParser
{
	public class Obj
	{
		public List<ObjFace> FaceList;
		public List<ObjTextureVertex> TextureList;
		public List<ObjVertex> VertexList;

		/// <summary>
		/// Constructor. Initializes VertexList, FaceList and TextureList.
		/// </summary>
		public Obj()
		{
			VertexList = new List<ObjVertex>();
			FaceList = new List<ObjFace>();
			TextureList = new List<ObjTextureVertex>();
		}

		public string Material { get; set; } = "";
		public Extent Size { get; set; }

		public string UseMtl { get; set; }

		/// <summary>
		/// Load .obj from a filepath.
		/// </summary>
		/// <param name="file"></param>
		public void LoadObj(string path)
		{
			LoadObj(File.ReadAllLines(path));
		}

		/// <summary>
		/// Load .obj from a stream.
		/// </summary>
		/// <param name="file"></param>
		public void LoadObj(Stream data)
		{
			using (var reader = new StreamReader(data))
			{
				LoadObj(reader.ReadToEnd().Split(Environment.NewLine.ToCharArray()));
			}
		}

		/// <summary>
		/// Load .obj from a list of strings.
		/// </summary>
		/// <param name="data"></param>
		public void LoadObj(IEnumerable<string> data)
		{
			foreach (var line in data)
			{
				ProcessLine(line);
			}

			updateSize();
		}

		public void WriteObjFile(string path, string[] headerStrings)
		{
			using (var outStream = File.OpenWrite(path))
			using (var writer = new StreamWriter(outStream))
			{
				// Write some header data
				WriteHeader(writer, headerStrings);

				if (!string.IsNullOrEmpty(Material))
				{
					writer.WriteLine("mtllib " + Material);
				}

				VertexList.ForEach(v => writer.WriteLine(v));
				TextureList.ForEach(tv => writer.WriteLine(tv));
				string lastUseMtl = "";
				foreach (ObjFace face in FaceList)
				{
					if (face.UseMtl != null && !face.UseMtl.Equals(lastUseMtl))
					{
						writer.WriteLine("usemtl " + face.UseMtl);
						lastUseMtl = face.UseMtl;
					}
					writer.WriteLine(face);
				}
			}
		}

		/// <summary>
		/// Parses and loads a line from an OBJ file.
		/// Currently only supports V, VT, F and MTLLIB prefixes
		/// </summary>
		private void ProcessLine(string line)
		{
			string[] lineParts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (lineParts.Length > 0)
			{
				switch (lineParts[0])
				{
					case "usemtl":
						UseMtl = lineParts[1];
						break;

					case "mtllib":
						Material = lineParts[1];
						break;

					case "v":
						ObjVertex v = new ObjVertex();
						v.LoadFromStringArray(lineParts);
						VertexList.Add(v);
						v.Index = VertexList.Count();
						break;

					case "f":
						ObjFace f = new ObjFace();
						f.LoadFromStringArray(lineParts);
						f.UseMtl = UseMtl;
						FaceList.Add(f);
						break;

					case "vt":
						ObjTextureVertex vt = new ObjTextureVertex();
						vt.LoadFromStringArray(lineParts);
						TextureList.Add(vt);
						vt.Index = TextureList.Count();
						break;
				}
			}
		}

		/// <summary>
		/// Sets our global object size with an extent object
		/// </summary>
		private void updateSize()
		{
			// If there are no vertices then size should be 0.
			if (VertexList.Count == 0)
			{
				Size = new Extent
				{
					XMax = 0,
					XMin = 0,
					YMax = 0,
					YMin = 0,
					ZMax = 0,
					ZMin = 0
				};

				// Avoid an exception below if VertexList was empty.
				return;
			}

			Size = new Extent
			{
				XMax = VertexList.Max(v => v.X),
				XMin = VertexList.Min(v => v.X),
				YMax = VertexList.Max(v => v.Y),
				YMin = VertexList.Min(v => v.Y),
				ZMax = VertexList.Max(v => v.Z),
				ZMin = VertexList.Min(v => v.Z)
			};
		}

		private void WriteHeader(StreamWriter writer, string[] headerStrings)
		{
			if (headerStrings == null || headerStrings.Length == 0)
			{
				writer.WriteLine("# Generated by ObjParser");
				return;
			}

			foreach (var line in headerStrings)
			{
				writer.WriteLine("# " + line);
			}
		}
	}
}