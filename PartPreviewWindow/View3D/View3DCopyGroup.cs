/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private async void MakeCopyOfGroup()
		{
			if (Scene.HasSelection)
			{
				processingProgressControl.ProcessType = "Making Copy".Localize() + ":";
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				// Copy selected item
				IObject3D newItem = await Task.Run(() =>
				{
					var clonedItem = Scene.SelectedItem.Clone();
					PlatingHelper.MoveToOpenPosition(clonedItem, Scene);

					return clonedItem;
				});

				if (HasBeenClosed)
				{
					return;
				}

				InsertNewItem(newItem);

				UnlockEditControls();
				PartHasBeenChanged();

				// TODO: jlewin - why do we need to reset the scale?

				// now set the selection to the new copy
				Scene.Children.Last().ExtraData.CurrentScale = Scene.SelectedItem.ExtraData.CurrentScale;

				Debugger.Break(); // Revise undo for scene_bundle
				// UndoBuffer.Add(new CopyUndoCommand(this, SelectedMeshGroupIndex));

			}
		}

		public void InsertNewItem(IObject3D newItem)
		{
			// Create and perform a new insert operation
			var insertOperation = new InsertCommand(this, newItem);
			insertOperation.Do();

			// Store the operation for undo/redo
			UndoBuffer.Add(insertOperation);
		}
	}
}