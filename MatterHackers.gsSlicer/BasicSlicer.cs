/*
Copyright (c) 2019, John Lewin
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cotangent;
using g3;
using gs;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.gsBundle
{
	public class BasicSlicer : IObjectSlicer
	{
		public async Task<bool> Slice(IEnumerable<IObject3D> printableItems, PrinterSettings printerSettings, string filePath, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{

			using (var outputStream = File.OpenWrite(filePath))
			{
				var sourceMeshes = new List<DMesh3>();

				foreach (var item in printableItems.Where(d => d.MeshPath != null))
				{
					string sourceFilePath = await item.ResolveFilePath(null, cancellationToken);

					// Load Mesh
					if (File.Exists(sourceFilePath))
					{
						var mesh = StandardMeshReader.ReadMesh(sourceFilePath);
						if (mesh != null)
						{
							sourceMeshes.Add(mesh);
						}

						var printCenter = printerSettings.GetValue<Vector2>(SettingsKey.print_center);
						ApplyTransform(mesh, item.WorldMatrix(), printCenter);
					}
				}

				PrintSettings settings = LoadSettingsForPrinter(printerSettings);

				// Construct slicer
				var slicer = new GeometrySlicer();
				slicer.SliceMeshes(sourceMeshes, settings);

				bool valid = slicer.ExtractResultsIfValid(out PrintMeshAssembly meshes, out PlanarSliceStack slices);

				// Construct GCode generator
				var pathGenerator = new ToolpathGenerator();
				pathGenerator.CreateToolPaths(meshes, slices, settings);

				// Write GCode file
				var gcodeWriter = new StandardGCodeWriter();

				var streamWriter = new StreamWriter(outputStream);

				gcodeWriter.WriteFile(pathGenerator.CurrentGCode, streamWriter);

				return true;
			}
		}

		private static PrintSettings LoadSettingsForPrinter(PrinterSettings printerSettings)
		{
			var ss = new MatterHackersPrinter(printerSettings);

			var settings = new PrintSettings();
			settings.UpdateFromSettings(ss);

			return settings;
		}

		// Modeled after FlipLeftRightCoordSystems and ConvertYUpToZUp examples
		public static void ApplyTransform(IDeformableMesh mesh, Matrix4X4 matrix, Vector2 printCenter)
		{
			int NV = mesh.MaxVertexID;
			for (int vid = 0; vid < NV; ++vid)
			{
				if (mesh.IsVertex(vid))
				{
					Vector3d v = mesh.GetVertex(vid);

					// Transform point to MatterControl bed translation
					var vec3 = new Vector3(v.x, v.y, v.z);
					var transformed = vec3.Transform(matrix);

					// Update and reset
					v.x = transformed.X - printCenter.X;
					v.y = transformed.Y - printCenter.Y;
					v.z = transformed.Z;

					mesh.SetVertex(vid, v);
				}
			}
		}

		public Dictionary<string, ExportField> Exports { get; } = new Dictionary<string, ExportField>()
		{
			[SettingsKey.external_perimeter_speed] = new ExportField(""),
			[SettingsKey.make] = new ExportField(""),
			[SettingsKey.model] = new ExportField(""),
			[SettingsKey.build_height] = new ExportField(""),
			[SettingsKey.nozzle_diameter] = new ExportField(""),
			[SettingsKey.filament_diameter] = new ExportField(""),
			[SettingsKey.has_heated_bed] = new ExportField(""),
			[SettingsKey.has_hardware_leveling] = new ExportField(""),
			[SettingsKey.layer_height] = new ExportField(""),
			[SettingsKey.temperature1] = new ExportField(""),
			[SettingsKey.bed_temperature] = new ExportField(""),
			[SettingsKey.retract_length] = new ExportField(""),
			[SettingsKey.retract_speed] = new ExportField(""),
			[SettingsKey.travel_speed] = new ExportField(""),
			[SettingsKey.first_layer_speed] = new ExportField(""),
			[SettingsKey.bottom_solid_layers] = new ExportField(""),
			[SettingsKey.top_solid_layers] = new ExportField("")
		};

		public bool ValidateFile(string filePath)
		{
			// TODO: Implement solution
			System.Diagnostics.Debugger.Break();
			return true;
		}
	}
}
