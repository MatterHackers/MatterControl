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
using System.Linq;
using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D.UndoCommands
{
    public class ReplaceCommand : IUndoRedoCommand
	{
		private IEnumerable<IObject3D> removeItems;
		private IEnumerable<IObject3D> addItems;
		private bool maintainCenterAndZHeight;

		public ReplaceCommand(IEnumerable<IObject3D> removeItems, IEnumerable<IObject3D> addItems, bool maintainCenterAndZHeight = true)
		{
			this.maintainCenterAndZHeight = maintainCenterAndZHeight;
			var firstParent = removeItems.First().Parent;
			if (firstParent == null)
			{
				throw new Exception("The remove item(s) must already be in the scene (have a parent).");
			}

			if (removeItems.Any())
			{
				foreach (var removeItem in removeItems)
				{
					if (firstParent != removeItem.Parent)
					{
						throw new Exception("All the remove items must be siblings");
					}
				}
			}

			this.removeItems = removeItems;
			this.addItems = addItems;
		}

		public void Do()
		{
			var firstParent = removeItems.First().Parent;
			using (firstParent.RebuildLock())
			{
				var aabb = removeItems.GetAxisAlignedBoundingBox();

				firstParent.Children.Modify(list =>
				{
					foreach (var child in removeItems)
					{
						list.Remove(child);
					}

					list.AddRange(addItems);
				});

				// attempt to hold the items that we are adding to the same position as the items we are replacing
				// first get the bounds of all the items being added
				var aabb2 = addItems.GetAxisAlignedBoundingBox();

				if (aabb2.Size != Vector3.Zero)
				{
					// then move the all to account for the old center and bed position
					foreach (var item in addItems)
					{
						if (maintainCenterAndZHeight
							&& aabb.ZSize > 0)
						{
							// move our center back to where our center was
							var centerDelta = (aabb.Center - aabb2.Center);
							centerDelta.Z = 0;
							item.Matrix *= Matrix4X4.CreateTranslation(centerDelta);

							// Make sure we also maintain our height
							item.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, aabb.MinXYZ.Z - aabb2.MinXYZ.Z));
						}
					}
				}
			}

			firstParent.Invalidate(new InvalidateArgs(firstParent, InvalidateType.Children | InvalidateType.Matrix));
		}

		public void Undo()
		{
			var firstParent = removeItems.First().Parent;
			firstParent.Children.Modify(list =>
			{
				foreach (var child in addItems)
				{
					list.Remove(child);
				}

				list.AddRange(removeItems);
				firstParent.Invalidate(new InvalidateArgs(firstParent, InvalidateType.Children));
			});
		}
	}
}