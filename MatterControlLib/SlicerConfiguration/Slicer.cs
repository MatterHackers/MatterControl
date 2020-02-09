/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class Slicer
	{
		public static List<bool> ExtrudersUsed = new List<bool>();

		public static bool RunInProcess { get; set; } = false;

		public static void GetExtrudersUsed(List<bool> extrudersUsed, IEnumerable<IObject3D> printableItems, PrinterSettings settings, bool checkForMeshFile)
		{
			extrudersUsed.Clear();

			if (!printableItems.Any())
			{
				return;
			}

			int extruderCount = settings.GetValue<int>(SettingsKey.extruder_count);
			// Make sure we only consider 1 extruder if in spiral vase mode
			if (settings.GetValue<bool>(SettingsKey.spiral_vase)
				&& extrudersUsed.Count(used => used == true) > 1)
			{
				extruderCount = 1;
			}

			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				extrudersUsed.Add(false);
			}

			// If we have support enabled and are using an extruder other than 0 for it
			if (printableItems.Any(i => i.WorldOutputType() == PrintOutputTypes.Support))
			{
				if (settings.GetValue<int>(SettingsKey.support_material_extruder) != 0)
				{
					int supportExtruder = Math.Max(0, Math.Min(extruderCount - 1, settings.GetValue<int>(SettingsKey.support_material_extruder) - 1));
					extrudersUsed[supportExtruder] = true;
				}
			}

			// If we have raft enabled and are using an extruder other than 0 for it
			if (settings.GetValue<bool>(SettingsKey.create_raft))
			{
				if (settings.GetValue<int>(SettingsKey.raft_extruder) != 0)
				{
					int raftExtruder = Math.Max(0, Math.Min(extruderCount - 1, settings.GetValue<int>(SettingsKey.raft_extruder) - 1));
					extrudersUsed[raftExtruder] = true;
				}
			}

			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				IEnumerable<IObject3D> itemsThisExtruder = GetItemsForExtruder(printableItems, extruderCount, extruderIndex, checkForMeshFile);
				extrudersUsed[extruderIndex] |= itemsThisExtruder.Any();
			}
		}

		public static bool T1OrGreaterUsed(PrinterConfig printer)
		{
			var scene = printer.Bed.Scene;

			var extrudersUsed = new List<bool>();
			Slicer.GetExtrudersUsed(extrudersUsed, printer.PrintableItems(scene), printer.Settings, false);

			for (int i = 1; i < extrudersUsed.Count; i++)
			{
				if (extrudersUsed[i])
				{
					return true;
				}
			}

			return false;
		}

		public static IEnumerable<IObject3D> GetItemsForExtruder(IEnumerable<IObject3D> meshItemsOnBuildPlate, int extruderCount, int extruderIndex, bool checkForMeshFile)
		{
			var itemsThisExtruder = meshItemsOnBuildPlate.Where((item) =>
				(!checkForMeshFile || (File.Exists(item.MeshPath) // Drop missing files
					|| File.Exists(Path.Combine(Object3D.AssetsPath, item.MeshPath))))
				&& (item.WorldMaterialIndex() == extruderIndex
					|| (extruderIndex == 0
						&& (item.WorldMaterialIndex() >= extruderCount || item.WorldMaterialIndex() == -1)))
				&& (item.WorldOutputType() == PrintOutputTypes.Solid || item.WorldOutputType() == PrintOutputTypes.Default));

			return itemsThisExtruder;
		}

		public static Task<bool> SliceItem(IObject3D object3D, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			return printer.Settings.Slicer.Slice(printer.PrintableItems(object3D), printer.Settings, gcodeFilePath, progressReporter, cancellationToken);
		}
	}
}
