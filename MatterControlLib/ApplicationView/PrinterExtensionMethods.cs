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
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using MatterHackers.Agg;
	using MatterHackers.DataConverters3D;
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.SlicerConfiguration;
	using MatterHackers.VectorMath;

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
					&& aabb.MaxXYZ.Z >= bed.BuildHeight
				|| aabb.MaxXYZ.Z <= 0)
			{
				// object completely below the bed or any part above the build volume
				return false;
			}

			switch (bed.BedShape)
			{
				case BedShape.Rectangular:
					if (aabb.MinXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X / 2
						|| aabb.MaxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.MinXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.MaxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						return false;
					}
					break;

				case BedShape.Circular:
					// This could be much better if it checked the actual vertex data of the mesh against the cylinder
					// first check if any of it is outside the bed rect
					if (aabb.MinXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X / 2
						|| aabb.MaxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.MinXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.MaxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						// TODO: then check if all of it is outside the bed circle
						return false;
					}
					break;
			}

			return true;
		}

		private static bool InsideHotendBounds(this PrinterConfig printer, IObject3D item)
		{
			if (printer.Settings.Helpers.HotendCount() == 1)
			{
				return true;
			}

			var materialIndex = item.WorldMaterialIndex();
			if (materialIndex == -1)
			{
				materialIndex = 0;
			}

			bool isWipeTower = item?.OutputType == PrintOutputTypes.WipeTower;

			// Determine if the given item is outside the bounds of the given extruder
			if (materialIndex < printer.Settings.ToolBounds.Length
				|| isWipeTower)
			{
				var itemAABB = item.WorldAxisAlignedBoundingBox();
				var itemBounds = new RectangleDouble(new Vector2(itemAABB.MinXYZ), new Vector2(itemAABB.MaxXYZ));

				var activeHotends = new HashSet<int>(new[] { materialIndex });

				if (isWipeTower)
				{
					activeHotends.Add(0);
					activeHotends.Add(1);
				}

				// Validate against active hotends
				foreach (var hotendIndex in activeHotends)
				{
					var hotendBounds = printer.Settings.ToolBounds[hotendIndex];
					if (!hotendBounds.Contains(itemBounds))
					{
						return false;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Filters items from a given source returning only persistable items inside the Build Volume
		/// </summary>
		/// <param name="source">The source content to filter</param>
		/// <param name="printer">The printer config to consider</param>
		/// <returns>An enumerable set of printable items</returns>
		public static IEnumerable<IObject3D> PrintableItems(this PrinterConfig printer, IObject3D source)
		{
			return source.VisibleMeshes().Where(item => item.WorldPersistable()
														&& printer.InsideBuildVolume(item)
														&& printer.InsideHotendBounds(item)
														&& !item.GetType().GetCustomAttributes(typeof(NonPrintableAttribute), true).Any());
		}

		/// <summary>
		/// Conditionally cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <param name="abortCancel">The action to run if the user aborts the Cancel operation</param>
		public static void CancelPrint(this PrinterConfig printer, Action abortCancel = null)
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
						else
						{
							abortCancel?.Invoke();
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