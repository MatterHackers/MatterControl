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
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.gsBundle
{
	public class SlaSlicer : IObjectSlicer
	{
		public async Task<bool> Slice(IEnumerable<IObject3D> printableItems, PrinterSettings printerSettings, string filePath, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			using (var outputStream = File.OpenWrite(filePath))
			{
				foreach (var item in printableItems.Where(d => d.MeshPath != null))
				{
					//string sourceFilePath = await item.ResolveFilePath(null, cancellationToken);

					//// Load Mesh
					//if (File.Exists(sourceFilePath))
					//{
					//	var mesh = StandardMeshReader.ReadMesh(sourceFilePath);
					//	if (mesh != null)
					//	{
					//		sourceMeshes.Add(mesh);
					//	}

					//	var printCenter = printerSettings.GetValue<Vector2>(SettingsKey.print_center);
					//	ApplyTransform(mesh, item.WorldMatrix(), printCenter);
					//}
				}

				//var settings = LoadSettingsForPrinter(printerSettings);

				// Construct slicer
				//var slicer = new GeometrySlicer();
				//slicer.SliceMeshes(sourceMeshes, settings);

				//bool valid = slicer.ExtractResultsIfValid(out PrintMeshAssembly meshes, out PlanarSliceStack slices);

				//// Construct GCode generator
				//var pathGenerator = new ToolpathGenerator();
				//pathGenerator.CreateToolPaths(meshes, slices, settings);

				//// Write GCode file
				//var gcodeWriter = new StandardGCodeWriter();

				//var streamWriter = new StreamWriter(outputStream);

				//gcodeWriter.WriteFile(pathGenerator.CurrentGCode, streamWriter);

				return true;
			}
		}

		public Dictionary<string, ExportField> Exports { get; } = new Dictionary<string, ExportField>()
		{
			[SettingsKey.bed_size] = new ExportField(""),
			[SettingsKey.build_height] = new ExportField(""),
			[SettingsKey.make] = new ExportField(""),
			[SettingsKey.model] = new ExportField(""),
			[SettingsKey.resin_cost] = new ExportField(""),
			[SettingsKey.resin_density] = new ExportField(""),
			[SettingsKey.sla_auto_support] = new ExportField(""),
			[SettingsKey.sla_base_exposure_time] = new ExportField(""),
			[SettingsKey.sla_base_min_off_time] = new ExportField(""),
			[SettingsKey.sla_min_off_time] = new ExportField(""),
			[SettingsKey.sla_base_lift_distance] = new ExportField(""),
			[SettingsKey.sla_lift_distance] = new ExportField(""),
			[SettingsKey.sla_base_lift_speed] = new ExportField(""),
			[SettingsKey.sla_lift_speed] = new ExportField(""),
			[SettingsKey.sla_base_layers] = new ExportField(""),
			[SettingsKey.sla_create_raft] = new ExportField(""),
			[SettingsKey.sla_exposure_time] = new ExportField(""),
			[SettingsKey.sla_layer_height] = new ExportField(""),
			[SettingsKey.sla_printable_area_inset] = new ExportField(""),
			[SettingsKey.sla_resolution] = new ExportField(""),
			[SettingsKey.slice_engine] = new ExportField(""),
			[SettingsKey.sla_mirror_mode] = new ExportField(""),
			[SettingsKey.sla_decend_speed] = new ExportField(""),
		};

		public bool ValidateFile(string filePath)
		{
			// TODO: Implement solution
			System.Diagnostics.Debugger.Break();
			return true;
		}

		public PrinterType PrinterType => PrinterType.SLA;
	}
}
