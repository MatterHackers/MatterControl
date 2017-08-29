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

using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DeleteCommand : IUndoRedoCommand
	{
		private List<IObject3D> items = new List<IObject3D>();

		private View3DWidget view3DWidget;

		public DeleteCommand(View3DWidget view3DWidget, IObject3D deletingItem)
		{
			this.view3DWidget = view3DWidget;
			if (deletingItem.ItemType == Object3DTypes.SelectionGroup)
			{
				var childrenToAdd = deletingItem.Children;
				// push whatever happend to the selection into the objects before saving them
				view3DWidget.Scene.ClearSelection();
				// save them in our list
				foreach (var item in childrenToAdd)
				{
					items.Add(item);
				}
			}
			else
			{
				this.items.Add(deletingItem);
			}
		}

		public void Do()
		{
			view3DWidget.Scene.ClearSelection();

			view3DWidget.Scene.ModifyChildren(children =>
			{
				foreach (var item in items)
				{
					children.Remove(item);
				}
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.Scene.ModifyChildren(children =>
			{
				foreach (var item in items)
				{
					children.Add(item);
				}
			});

			view3DWidget.Scene.ClearSelection();
			foreach (var item in items)
			{
				view3DWidget.Scene.AddToSelection(item);
			}

			view3DWidget.PartHasBeenChanged();
		}
	}
}