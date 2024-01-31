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
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
    public static class AmfDocument
	{
		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
			Debugger.Break();
		}

		public static Stream GetCompressedStreamIfRequired(Stream amfStream)
		{
			if (IsZipFile(amfStream))
			{
				var archive = new ZipArchive(amfStream, ZipArchiveMode.Read);
				return archive.Entries[0].Open();
			}

			amfStream.Position = 0;
			return amfStream;
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

		public static IObject3D Load(string amfPath, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			using (var stream = File.OpenRead(amfPath))
			{
				return Load(stream, cancellationToken, reportProgress);
			}
		}

		public static IObject3D Load(Stream fileStream, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			IObject3D root = new Object3D();

			IObject3D context = null;

			int totalMeshes = 0;
			var time = Stopwatch.StartNew();

			var materials = new Dictionary<int, (string name, Color color, int material, PrintOutputTypes output)>();
			var itemPropertiesDictionary = new Dictionary<IObject3D, int>();

			using (var decompressedStream = GetCompressedStreamIfRequired(fileStream))
			{
				using (var reader = XmlReader.Create(decompressedStream))
				{
					List<Vector3> vertices = null;
					Mesh mesh = null;

					var progressData = new ProgressData(fileStream, reportProgress);

					while (reader.Read())
					{
						if (!reader.IsStartElement())
						{
							continue;
						}

						switch (reader.Name)
						{
							case "object":
								break;

							case "mesh":
								break;

							case "vertices":
								vertices = ReadAllVertices(reader, progressData);
								break;

							case "volume":
								context = new Object3D();
								root.Children.Add(context);
								mesh = new Mesh();
								context.SetMeshDirect(mesh);
								totalMeshes += 1;

								string materialId;
								ReadVolume(reader, vertices, mesh, progressData, out materialId);
								int.TryParse(materialId, out int id);
								itemPropertiesDictionary.Add(context, id);
								break;

							case "material":
								ReadProperties(reader, materials);
								break;
						}
					}
				}

				fileStream.Dispose();
			}

			foreach (var keyValue in itemPropertiesDictionary)
			{
				if (materials.TryGetValue(keyValue.Value,
					out (string name, Color color, int material, PrintOutputTypes output) data))
				{
					keyValue.Key.Name = data.name;
					keyValue.Key.Color = data.color;
					keyValue.Key.OutputType = data.output;
				}
			}

			time.Stop();
			Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			reportProgress?.Invoke(1, "");

			return hasValidMesh ? root : null;
		}

		/// <summary>
		/// Writes the mesh to disk in a zip container
		/// </summary>
		/// <param name="item">The object to save</param>
		/// <param name="fileName">The location to save to</param>
		/// <param name="outputInfo">Extra meta data to store in the file</param>
		/// <returns>The results of the save operation</returns>
		public static bool Save(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			try
			{
				var forceAscii = false;
				if (forceAscii || outputInfo?.OutputTypeSetting == MeshOutputSettings.OutputType.Ascii)
				{
					SaveUncompressed(item, fileName, outputInfo);
					return true;
				}
				else
				{
					using (Stream stream = File.OpenWrite(fileName))
					{
						using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
						{
							ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetFileName(fileName));
							using (var entryStream = zipEntry.Open())
							{
								Save(item, entryStream, outputInfo);
							}
						}
					}

					return true;
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return false;
			}
		}

		public static void Save(IObject3D itemToSave, Stream stream, MeshOutputSettings outputInfo)
		{
			using var amfFile = XmlWriter.Create(stream, new XmlWriterSettings()
			{
				Indent = true,
				IndentChars = "  "
			});

			// <?xml version="1.0" encoding="utf-8"?>
			amfFile.WriteStartDocument();

			// <amf unit="millimeter" version="1.1">
			amfFile.WriteStartElement("amf");
			amfFile.WriteAttributeString("unit", "millimeter");
			amfFile.WriteAttributeString("version", "1.1");

			if (outputInfo != null)
			{
				foreach (KeyValuePair<string, string> metaData in outputInfo.MetaDataKeyValue)
				{
					// <metadata type ="{metaData.Key}">{metaData.Value}</metadata>
					amfFile.WriteStartElement("metadata");
					amfFile.WriteAttributeString("type", metaData.Key);
					amfFile.WriteString(metaData.Value);
					amfFile.WriteEndElement();
				}
			}

			{
				var visibleMeshes = itemToSave.VisibleMeshes();
				int totalMeshes = visibleMeshes.Count();

				double ratioPerMesh = 1d / totalMeshes;
				double currentRatio = 0;

				var groupedByNameColorMaterial = visibleMeshes
					.GroupBy(i => (i.Name, i.Color, i.WorldOutputType()));

				int objectId = 0;
				foreach (var group in groupedByNameColorMaterial)
				{
					objectId++;

					// <object id="{objectId}">
					amfFile.WriteStartElement("object");
					amfFile.WriteStartAttribute("id");
					amfFile.WriteValue(objectId);
					amfFile.WriteEndAttribute();
					{
						int vertexCount = 0;
						var meshVertexStart = new List<int>();

						// <mesh>
						amfFile.WriteStartElement("mesh");
						{
							// <vertices>
							amfFile.WriteStartElement("vertices");
							{
								foreach (var item in group)
								{
									var matrix = item.WorldMatrix();
									var mesh = item.Mesh;
									double meshVertCount = (double)mesh.Vertices.Count;

									meshVertexStart.Add(vertexCount);
									for (int vertexIndex = 0; vertexIndex < meshVertCount; vertexIndex++)
									{
										var position = mesh.Vertices[vertexIndex].Transform(matrix);
										outputInfo?.ReportProgress?.Invoke(currentRatio + vertexIndex / meshVertCount * ratioPerMesh * .5, "");

										// <vertex>
										amfFile.WriteStartElement("vertex");
										{
											// <coordinates>
											amfFile.WriteStartElement("coordinates");
											{
												// <x>{position.X}</x>
												amfFile.WriteStartElement("x");
												amfFile.WriteValue(position.X);
												amfFile.WriteEndElement();

												// <y>{position.Y}</y>
												amfFile.WriteStartElement("y");
												amfFile.WriteValue(position.Y);
												amfFile.WriteEndElement();

												// <z>{position.Z}</z>
												amfFile.WriteStartElement("z");
												amfFile.WriteValue(position.Z);
												amfFile.WriteEndElement();
											}
											// </coordinates>
											amfFile.WriteEndElement();
										}
										// </vertex>
										amfFile.WriteEndElement();

										vertexCount++;
									}

									currentRatio += ratioPerMesh * .5;
								}
							}
							// </vertices>
							amfFile.WriteEndElement();

							int meshIndex = 0;
							foreach (var item in group)
							{
								var mesh = item.Mesh;
								int firstVertexIndex = meshVertexStart[meshIndex++];

								// <volume materialid="{objectId}">
								amfFile.WriteStartElement("volume");
								amfFile.WriteStartAttribute("materialid");
								amfFile.WriteValue(objectId);
								amfFile.WriteEndAttribute();

								double faceCount = (double)mesh.Faces.Count;
								for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
								{
									outputInfo?.ReportProgress?.Invoke(currentRatio + faceIndex / faceCount * ratioPerMesh * .5, "");

									Face face = mesh.Faces[faceIndex];

									// <triangle>
									amfFile.WriteStartElement("triangle");
									{
										// <v1>{firstVertexIndex + face.v0}</v1>
										amfFile.WriteStartElement("v1");
										amfFile.WriteValue(firstVertexIndex + face.v0);
										amfFile.WriteEndElement();

										// <v2>{firstVertexIndex + face.v1}</v2>
										amfFile.WriteStartElement("v2");
										amfFile.WriteValue(firstVertexIndex + face.v1);
										amfFile.WriteEndElement();

										// <v3>{firstVertexIndex + face.v2}</v3>
										amfFile.WriteStartElement("v3");
										amfFile.WriteValue(firstVertexIndex + face.v2);
										amfFile.WriteEndElement();
									}
									// </triangle>
									amfFile.WriteEndElement();
								}

								currentRatio += ratioPerMesh * .5;

								// </volume>
								amfFile.WriteEndElement();
							}
						}
						// </mesh>
						amfFile.WriteEndElement();
					}
					// </object>
					amfFile.WriteEndElement();
				}

				var nameColorMaterials = new HashSet<(string name, Color c, PrintOutputTypes output)>();
				foreach (var group in groupedByNameColorMaterial)
				{
					foreach (var item in group)
					{
						nameColorMaterials.Add((item.Name, item.WorldColor(), item.WorldOutputType()));
					}
				}

				int id = 0;
				foreach (var ncm in nameColorMaterials)
				{
					id++;

					// <material id="{id}">
					amfFile.WriteStartElement("material");
					amfFile.WriteStartAttribute("id");
					amfFile.WriteValue(id);
					amfFile.WriteEndAttribute();
					{
						// <metadata type="Name">{ncm.name}</metadata>
						amfFile.WriteStartElement("metadata");
						amfFile.WriteAttributeString("type", "Name");
						amfFile.WriteValue(ncm.name);
						amfFile.WriteEndElement();

						// <metadata type="MaterialIndex">{ncm.material}</metadata>
						amfFile.WriteStartElement("metadata");
						amfFile.WriteAttributeString("type", "MaterialIndex");
						amfFile.WriteEndElement();

						// <color>
						amfFile.WriteStartElement("color");
						{
							// <r>{ncm.c.Red0To1}</r>
							amfFile.WriteStartElement("r");
							amfFile.WriteValue(ncm.c.Red0To1);
							amfFile.WriteEndElement();

							// <g>{ncm.c.Green0To1}</g>
							amfFile.WriteStartElement("g");
							amfFile.WriteValue(ncm.c.Green0To1);
							amfFile.WriteEndElement();

							// <b>{ncm.c.Blue0To1}</b>
							amfFile.WriteStartElement("b");
							amfFile.WriteValue(ncm.c.Blue0To1);
							amfFile.WriteEndElement();
						}
						// </color>
						amfFile.WriteEndElement();

						// <metadata type="OutputType">{ncm.output}</metadata>
						amfFile.WriteStartElement("metadata");
						amfFile.WriteAttributeString("type", "OutputType");
						amfFile.WriteString(ncm.output.ToString());
						amfFile.WriteEndElement();
					}
					// </material>
					amfFile.WriteEndElement();
				}
			}
			// </amf>
			amfFile.WriteEndElement();

			amfFile.WriteEndDocument();

			amfFile.Flush();
		}

		public static void SaveUncompressed(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				Save(item, file, outputInfo);
			}
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

		private static List<Vector3> ReadAllVertices(XmlReader reader, ProgressData progressData)
		{
			var vertices = new List<Vector3>();

			if (reader.ReadToDescendant("vertex"))
			{
				do
				{
					if (reader.ReadToDescendant("coordinates"))
					{
						var vertex = default(Vector3);

						if (reader.ReadToDescendant("x"))
						{
							string nextSibling = null;
							do
							{
								switch (reader.Name)
								{
									case "x":
										vertex.X = reader.ReadElementContentAsDouble();
										nextSibling = "y";
										break;

									case "y":
										vertex.Y = reader.ReadElementContentAsDouble();
										nextSibling = "z";
										break;

									case "z":
										vertex.Z = reader.ReadElementContentAsDouble();
										nextSibling = null;

										break;
								}
							}
							while (nextSibling != null && (reader.Name == nextSibling || reader.ReadToNextSibling(nextSibling)));
						}

						progressData.ReportProgress0To50();

						vertices.Add(vertex);
					}

					MoveToEndElement(reader, "vertex");
				}
				while (reader.ReadToNextSibling("vertex"));
			}

			return vertices;
		}

		private static void ReadProperties(XmlReader reader,
			Dictionary<int, (string name, Color color, int material, PrintOutputTypes output)> materials)
		{
			var idString = reader["id"];

			var name = "";
			var materialIndex = -1;
			var output = PrintOutputTypes.Default;
			if (reader.ReadToDescendant("metadata"))
			{
				ParseData(reader, ref name, ref materialIndex, ref output);
			}

			if (reader.ReadToNextSibling("metadata"))
			{
				ParseData(reader, ref name, ref materialIndex, ref output);
			}

			var color = ColorF.White;
			if (reader.ReadToNextSibling("color"))
			{
				if (reader.ReadToDescendant("r"))
				{
					string nextSibling = null;
					do
					{
						switch (reader.Name)
						{
							case "r":
								color.red = reader.ReadElementContentAsFloat();
								nextSibling = "g";
								break;

							case "g":
								color.green = reader.ReadElementContentAsFloat();
								nextSibling = "b";
								break;

							case "b":
								color.blue = reader.ReadElementContentAsFloat();
								nextSibling = null;
								break;
						}
					}
					while (nextSibling != null && (reader.Name == nextSibling || reader.ReadToNextSibling(nextSibling)));
				}

				MoveToEndElement(reader, "color");
			}

			if (reader.ReadToNextSibling("metadata"))
			{
				ParseData(reader, ref name, ref materialIndex, ref output);
			}

			int.TryParse(idString, out int id);

			materials.Add(id, (name, color.ToColor(), materialIndex, output));
		}

		private static void ParseData(XmlReader reader, ref string name, ref int materialIndex, ref PrintOutputTypes output)
		{
			switch (reader.GetAttribute("type"))
			{
				case "Name":
					name = reader.ReadElementContentAsString();
					break;

				case "MaterialIndex":
					materialIndex = reader.ReadElementContentAsInt();
					break;

				case "OutputType":
					Enum.TryParse<PrintOutputTypes>(reader.ReadElementContentAsString(), out output);
					break;
			}
		}

		private static List<Vector3> ReadVolume(XmlReader reader, List<Vector3> vertices, Mesh mesh, ProgressData progressData, out string id)
		{
			id = reader["materialid"];

			if (reader.ReadToDescendant("triangle"))
			{
				do
				{
					var indices = new int[3];

					if (reader.ReadToDescendant("v1"))
					{
						string nextSibling = null;
						do
						{
							switch (reader.Name)
							{
								case "v1":
									indices[0] = reader.ReadElementContentAsInt();
									nextSibling = "v2";
									break;

								case "v2":
									indices[1] = reader.ReadElementContentAsInt();
									nextSibling = "v3";
									break;

								case "v3":
									indices[2] = reader.ReadElementContentAsInt();
									nextSibling = null;
									break;
							}
						}
						while (nextSibling != null && (reader.Name == nextSibling || reader.ReadToNextSibling(nextSibling)));
					}

					if (indices[0] != indices[1]
						&& indices[0] != indices[2]
						&& indices[1] != indices[2]
						&& vertices[indices[0]] != vertices[indices[1]]
						&& vertices[indices[1]] != vertices[indices[2]]
						&& vertices[indices[2]] != vertices[indices[0]])
					{
						mesh.CreateFace(new Vector3[] { vertices[indices[0]], vertices[indices[1]], vertices[indices[2]] });
					}

					progressData.ReportProgress0To50();

					MoveToEndElement(reader, "triangle");
				}
				while (reader.ReadToNextSibling("triangle"));
			}

			return vertices;
		}

		internal class ProgressData
		{
			private long bytesInFile;

			private Stopwatch maxProgressReport = new Stopwatch();

			private Stream positionStream;

			private Action<double, string> reportProgress;

			internal ProgressData(Stream positionStream, Action<double, string> reportProgress)
			{
				this.reportProgress = reportProgress;
				this.positionStream = positionStream;
				maxProgressReport.Start();
				bytesInFile = (long)positionStream.Length;
			}

			internal void ReportProgress0To50()
			{
				if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					reportProgress(positionStream.Position / (double)bytesInFile * .5, "Loading Mesh");
					maxProgressReport.Restart();
				}
			}
		}

		public static void MoveToEndElement(XmlReader reader, string xname)
		{
			while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Name == xname))
			{
				reader.Read();
			}
		}
	}
}