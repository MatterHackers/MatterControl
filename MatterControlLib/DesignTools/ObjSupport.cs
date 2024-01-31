/*
Copyright (c) 2017, John Lewin, Lars Brubaker
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using ObjParser;

namespace MatterHackers.DataConverters3D
{
    public static class ObjSupport
	{
		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
			Debugger.Break();
		}

		public static Stream GetCompressedStreamIfRequired(Stream objStream)
		{
			if (IsZipFile(objStream))
			{
				var archive = new ZipArchive(objStream, ZipArchiveMode.Read);
				return archive.Entries[0].Open();
			}

			objStream.Position = 0;
			return objStream;
		}

		public static long GetEstimatedMemoryUse(string fileLocation)
		{
			try
			{
				using (Stream stream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					if (IsZipFile(stream))
					{
						return (long)(stream.Length * 57);
					}
					else
					{
						return (long)(stream.Length * 3.7);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return 0;
			}
		}

		public static IObject3D Load(string objPath, Action<double, string> reportProgress = null, IObject3D source = null)
		{
			using (var stream = File.OpenRead(objPath))
			{
				return Load(stream, reportProgress, source);
			}
		}

		public static IObject3D Load(Stream fileStream, Action<double, string> reportProgress = null, IObject3D source = null)
		{
			IObject3D root = source ?? new Object3D();

			var time = Stopwatch.StartNew();

			// LOAD THE MESH DATA
			var objFile = new Obj();
			objFile.LoadObj(fileStream);

			IObject3D context = new Object3D();
			root.Children.Add(context);

			var mesh = new Mesh();
			context.SetMeshDirect(mesh);

			foreach (var vertex in objFile.VertexList)
			{
				mesh.Vertices.Add(new Vector3Float(vertex.X, vertex.Y, vertex.Z));
			}

			foreach (var face in objFile.FaceList)
			{
				for (int i = 0; i < face.VertexIndexList.Length; i++)
				{
					if (face.VertexIndexList[i] >= objFile.TextureList.Count)
					{
						int a = 0;
					}
				}

				mesh.Faces.Add(face.VertexIndexList[0] - 1, face.VertexIndexList[1] - 1, face.VertexIndexList[2] - 1, mesh.Vertices);
				if (face.VertexIndexList.Length == 4)
				{
					// add the other side of the quad
					mesh.Faces.Add(face.VertexIndexList[0] - 1, face.VertexIndexList[2] - 1, face.VertexIndexList[3] - 1, mesh.Vertices);
				}
			}

			// load and apply any texture
			if (objFile.Material != "")
			{
				// TODO: have consideration for this being in a shared zip file
				string pathToObj = Path.GetDirectoryName(((FileStream)fileStream).Name);
				//Try-catch block for when objFile.Material is not found
				try
				{
					using (var materialsStream = File.OpenRead(Path.Combine(pathToObj, objFile.Material)))
					{
						var mtl = new Mtl();
						mtl.LoadMtl(materialsStream);

						foreach (var material in mtl.MaterialList)
						{
							if (!string.IsNullOrEmpty(material.DiffuseTextureFileName))
							{
								var pathToTexture = Path.Combine(pathToObj, material.DiffuseTextureFileName);
								if (File.Exists(pathToTexture))
								{
									var diffuseTexture = new ImageBuffer();

									// TODO: have consideration for this being in a shared zip file
									using (var imageStream = File.OpenRead(pathToTexture))
									{
										if (Path.GetExtension(material.DiffuseTextureFileName).ToLower() == ".tga")
										{
											ImageTgaIO.LoadImageData(diffuseTexture, imageStream, 32);
										}
										else
										{
											ImageIO.LoadImageData(imageStream, diffuseTexture);
										}
									}

									if (diffuseTexture.Width > 0 && diffuseTexture.Height > 0)
									{
										int meshFace = 0;
										for (int objFace = 0; objFace < objFile.FaceList.Count; objFace++, meshFace++)
										{
											var face = mesh.Faces[meshFace];

											var faceData = objFile.FaceList[objFace];

											int textureIndex0 = faceData.TextureVertexIndexList[0] - 1;
											var uv0 = new Vector2Float(objFile.TextureList[textureIndex0].X, objFile.TextureList[textureIndex0].Y);
											int textureIndex1 = faceData.TextureVertexIndexList[1] - 1;
											var uv1 = new Vector2Float(objFile.TextureList[textureIndex1].X, objFile.TextureList[textureIndex1].Y);
											int textureIndex2 = faceData.TextureVertexIndexList[2] - 1;
											var uv2 = new Vector2Float(objFile.TextureList[textureIndex2].X, objFile.TextureList[textureIndex2].Y);

											mesh.FaceTextures.Add(meshFace, new FaceTextureData(diffuseTexture, uv0, uv1, uv2));

											if (faceData.TextureVertexIndexList.Length == 4)
											{
												meshFace++;

												int textureIndex3 = faceData.TextureVertexIndexList[3] - 1;
												var uv3 = new Vector2Float(objFile.TextureList[textureIndex3].X, objFile.TextureList[textureIndex3].Y);

												mesh.FaceTextures.Add(meshFace, new FaceTextureData(diffuseTexture, uv0, uv2, uv3));
											}
										}

										context.Color = Color.White;
										root.Color = Color.White;
									}
								}
							}
						}
					}
				}
				catch (FileNotFoundException)
				{
					// Just continue as if obj.Material == "" to show object
				}
			}

			time.Stop();
			Debug.WriteLine(string.Format("OBJ Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			reportProgress?.Invoke(1, "");

			return hasValidMesh ? root : null;
		}

		/// <summary>
		/// Writes the mesh to disk in a zip container
		/// </summary>
		/// <param name="item">The mesh to save</param>
		/// <param name="fileName">The file path to save at</param>
		/// <param name="outputInfo">Extra meta data to store in the file</param>
		/// <returns>The results of the save operation</returns>
		public static bool Save(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			try
			{
				using (Stream stream = File.OpenWrite(fileName))
				using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetFileName(fileName));
					using (var entryStream = zipEntry.Open())
					{
						return Save(item, entryStream, outputInfo);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return false;
			}
		}

		public static bool Save(IObject3D item, Stream stream, MeshOutputSettings outputInfo)
		{
			throw new NotImplementedException();
		}

		public static bool SaveUncompressed(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				return Save(item, file, outputInfo);
			}
		}

		private static string Indent(int index)
		{
			return new string(' ', index * 2);
		}

		private static bool IsZipFile(Stream fs)
		{
			int elements = 4;
			if (fs.Length < elements)
			{
				return false;
			}

			byte[] fileToken = new byte[elements];

			fs.Position = 0;
			fs.Read(fileToken, 0, elements);
			fs.Position = 0;

			// Zip files should start with the expected four byte token
			return BitConverter.ToString(fileToken) == "50-4B-03-04";
		}
	}
}