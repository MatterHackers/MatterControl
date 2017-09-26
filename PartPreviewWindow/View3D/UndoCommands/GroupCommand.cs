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

using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private InteractiveScene interactiveScene;

		public GroupCommand(InteractiveScene interactiveScene, IObject3D selectedItem)
		{
			this.interactiveScene = interactiveScene;
			this.item = selectedItem;
		}

		public async void Do()
		{
			if (interactiveScene.SelectedItem == item)
			{
				// This is the original do() case. The selection needs to be changed into a group and selected
				// change it to a standard group
				interactiveScene.SelectedItem.ItemType = Object3DTypes.Group;
			}
			else
			{
				// This the undo -> redo() case. The original Selection group has been collapsed and we need to rebuild it
				interactiveScene.Children.Modify(children =>
				{
					// Remove all children from the scene
					foreach (var child in item.Children)
					{
						children.Remove(child);
					}

					// Add the item
					children.Add(item);
				});

				interactiveScene.SelectedItem = item;
			}
		}

		public void Undo()
		{
			if (!interactiveScene.Children.Contains(item))
			{
				return;
			}

			interactiveScene.Children.Modify(list =>
			{
				// Remove the group
				list.Remove(item);

				// Add all children from the group
				list.AddRange(item.Children);
			});

			interactiveScene.SelectLastChild();
		}
	}
}