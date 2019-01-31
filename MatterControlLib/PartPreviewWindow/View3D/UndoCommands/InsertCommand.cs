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
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InsertCommand : IUndoRedoCommand
	{
		private IEnumerable<IObject3D> items;
		private InteractiveScene scene;
		private bool selectAfterInsert;

		bool firstPass = true;

		public InsertCommand(InteractiveScene scene, IObject3D insertingItem, bool selectAfterInsert = true)
			: this(scene, new IObject3D[] { insertingItem }, selectAfterInsert)
		{
		}

		public InsertCommand(InteractiveScene scene, IEnumerable<IObject3D> insertingItem, bool selectAfterInsert = true)
		{
			this.scene = scene;
			this.items = insertingItem;
			this.selectAfterInsert = selectAfterInsert;
		}

		public void Do()
		{
			if (!firstPass)
			{
				firstPass = false;
			}

			scene.Children.Modify(list => list.AddRange(items));

			scene.SelectedItem = null;
			if (selectAfterInsert)
			{
				foreach (var item in items)
				{
					scene.AddToSelection(item);
				}
			}

			scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));
		}

		public void Undo()
		{
			bool clearSelection = scene.SelectedItem == items;
			scene.Children.Modify(list =>
			{
				foreach (var item in items)
				{
					list.Remove(item);
				}
			});

			if(clearSelection)
			{
				scene.SelectedItem = null;
			}

			scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));
		}
	}
}