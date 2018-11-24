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

using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using System.Collections.Generic;
	using System.Linq;
	using MatterHackers.DataConverters3D;
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.SlicerConfiguration;
	using MatterHackers.MeshVisualizer;

	public static class PrinterExtensionMethods
	{
		public static bool InsideBuildVolume(this PrinterConfig printerConfig, IObject3D item)
		{
			if (item.Mesh == null)
			{
				return true;
			}

			var worldMatrix = item.WorldMatrix();
			// probably need , true (require precision)
			var aabb = item.Mesh.GetAxisAlignedBoundingBox(worldMatrix);

			var bed = printerConfig.Bed;

			if (bed.BuildHeight > 0
					&& aabb.maxXYZ.Z >= bed.BuildHeight
				|| aabb.maxXYZ.Z <= 0)
			{
				// object completely below the bed or any part above the build volume
				return false;
			}

			switch (bed.BedShape)
			{
				case BedShape.Rectangular:
					if (aabb.minXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X / 2
						|| aabb.maxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.minXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.maxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						return false;
					}
					break;

				case BedShape.Circular:
					// This could be much better if it checked the actual vertex data of the mesh against the cylinder
					// first check if any of it is outside the bed rect
					if (aabb.minXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X / 2
						|| aabb.maxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.minXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.maxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						// TODO: then check if all of it is outside the bed circle
						return false;
					}
					break;
			}

			return true;
		}

		/// <summary>
		/// Filters items from a given source returning only persistable items inside the Build Volume
		/// </summary>
		/// <param name="source">The source content to filter</param>
		/// <param name="printer">The printer config to consider</param>
		/// <returns></returns>
		public static IEnumerable<IObject3D> PrintableItems(this PrinterConfig printer, IObject3D source)
		{
			return source.VisibleMeshes().Where(item => printer.InsideBuildVolume(item) && item.WorldPersistable());
		}

		/// <summary>
		/// Conditionally cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <returns>A boolean value indicating if the print was canceled</returns>
		public static void CancelPrint(this PrinterConfig printer)
		{
			if (printer.Connection.SecondsPrinted > 120)
			{
				StyledMessageBox.ShowMessageBox(
					(bool response) =>
					{
						if (response)
						{
							UiThread.RunOnIdle(() => printer.Connection.Stop());
						}
					},
					"Cancel the current print?".Localize(),
					"Cancel Print?".Localize(),
					StyledMessageBox.MessageType.YES_NO,
					"Cancel Print".Localize(),
					"Continue Printing".Localize());
			}
			else
			{
				printer.Connection.Stop();
			}
		}
	}
}