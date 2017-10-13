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
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private InteractiveScene scene;

		public GroupCommand(InteractiveScene interactiveScene, IObject3D selectedItem)
		{
			this.scene = interactiveScene;
			this.item = selectedItem;
		}

		public static void GroupItems(InteractiveScene scene, IObject3D item)
		{
			if (scene.SelectedItem == item)
			{
				// This is the original do() case. The selection needs to be changed into a group and be selected
				item.ItemType = Object3DTypes.Default;
				scene.SelectedItem = item;
			}
			else
			{
				// make sure there is no selection (or it might contain one of our items needing to group)
				scene.SelectedItem = null;

				// This the undo -> redo() case. The original Selection group has been collapsed and we need to rebuild it
				scene.Children.Modify(children =>
				{
					// Remove all children from the list
					foreach (var child in item.Children)
					{
						children.Remove(child);
					}

					// Add the item
					children.Add(item);
				});

				scene.SelectedItem = item;
			}
		}

		public void Do()
		{
			GroupCommand.GroupItems(scene, item);
		}

		public void Undo()
		{
			UngroupCommand.UngroupItems(scene, item);
		}
	}
}