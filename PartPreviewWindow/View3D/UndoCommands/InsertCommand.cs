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
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InsertCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private Matrix4X4 originalTransform;
		private View3DWidget view3DWidget;

		bool firstPass = true;

		public InsertCommand(View3DWidget view3DWidget, IObject3D insertingItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = insertingItem;
			this.originalTransform = insertingItem.Matrix;
		}

		public void Do()
		{
			if (!firstPass)
			{
				item.Matrix = originalTransform;
			}

			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Add(item);
			});

			firstPass = false;

			view3DWidget.Scene.Select(item);

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Remove(item);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}
	}
	/*
	internal class CopyUndoCommand : IUndoRedoCommand
	{
		private int newItemIndex;
		private View3DWidget view3DWidget;

		IObject3D addedObject3D;

		bool wasLastItem;

		public CopyUndoCommand(View3DWidget view3DWidget, int newItemIndex)
		{
			this.view3DWidget = view3DWidget;
			this.newItemIndex = newItemIndex;

			addedObject3D = view3DWidget.Scene.Children[newItemIndex];

			wasLastItem = view3DWidget.Scene.Children.Last() == addedObject3D;
		}

		public void Undo()
		{
			view3DWidget.Scene.Children.RemoveAt(newItemIndex);

			if (wasLastItem)
			{
				view3DWidget.Scene.SelectLastChild();
			}
			view3DWidget.PartHasBeenChanged();
		}

		public void Do()
		{
			view3DWidget.Scene.Children.Insert(newItemIndex, addedObject3D);
			view3DWidget.Invalidate();
			view3DWidget.Scene.SelectLastChild();
		}
	} */
}