/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private View3DWidget view3DWidget;

		public GroupCommand(View3DWidget view3DWidget, IObject3D groupingItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = groupingItem;
		}

		public void Do()
		{
			if (view3DWidget.Scene.Children.Contains(item))
			{
				// If the item exits, it's likely still a selection group and we simply need to toggle that off
				item.ItemType = Object3DTypes.Group;
			}
			else
			{
				// Otherwise it's been removed and we need to re-add it
				view3DWidget.Scene.ModifyChildren(children =>
				{
					// Remove all children from the scene
					foreach (var child in item.Children)
					{
						children.Remove(child);
					}

					// Add the item
					children.Add(item);
				});
			}

			view3DWidget.Scene.Select(item);

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			if (!view3DWidget.Scene.Children.Contains(item))
			{
				return;
			}

			view3DWidget.Scene.ModifyChildren(children =>
			{
				// Remove the group
				children.Remove(item);

				// Add all children from the group
				children.AddRange(item.Children);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}
	}
}