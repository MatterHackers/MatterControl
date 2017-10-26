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
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class UngroupCommand : IUndoRedoCommand
	{
		private IObject3D originalItem;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;

		public UngroupCommand(View3DWidget view3DWidget, InteractiveScene scene, IObject3D ungroupingItem)
		{
			this.originalItem = ungroupingItem;
			this.view3DWidget = view3DWidget;
			this.scene = scene;
		}

		public void Do()
		{
			if (!scene.Children.Contains(originalItem))
			{
				return;
			}

			scene.Children.Modify(list =>
			{
				// Remove the group
				list.Remove(originalItem);

				// Apply transform
				foreach(var child in originalItem.Children)
				{
					child.Matrix *= originalItem.Matrix;
				}

				// Add all children from the group
				list.AddRange(originalItem.Children);
			});

			scene.SelectLastChild();
			scene.Invalidate();
		}

		public void Undo()
		{
			// Remove the children from the Scene root, add the original item back into the root
			scene.Children.Modify(list =>
			{
				foreach(var child in originalItem.Children)
				{
					if (list.Contains(child))
					{
						list.Remove(child);
					}

					Matrix4X4 inverseMatrix = originalItem.Matrix;
					inverseMatrix.Invert();

					child.Matrix = inverseMatrix * child.Matrix;
				}

				list.Add(originalItem);
			});

			scene.SelectLastChild();
			scene.Invalidate();
		}
	}
}