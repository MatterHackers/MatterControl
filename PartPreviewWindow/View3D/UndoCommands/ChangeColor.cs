/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ChangeColor : IUndoRedoCommand
	{
		List<PrintOutputTypes> itemsPrintOutputType = new List<PrintOutputTypes>();
		List<Color> itemsColor = new List<Color>();
		List<IObject3D> itemsToChange = new List<IObject3D>();
		Color color;

		public ChangeColor(IObject3D selectedItem, Color color)
		{
			this.color = color;
			if (selectedItem is SelectionGroup)
			{
				SetData(selectedItem.Children.ToList());
			}
			else
			{
				SetData(new List<IObject3D> { selectedItem });
			}
		}

		void SetData(List<IObject3D> itemsToChange)
		{ 
			foreach (var item in itemsToChange)
			{
				this.itemsToChange.Add(item);
				this.itemsColor.Add(item.Color);
				this.itemsPrintOutputType.Add(item.OutputType);
			}
		}

		void IUndoRedoCommand.Do()
		{
			foreach(var item in this.itemsToChange)
			{
				item.OutputType = PrintOutputTypes.Solid;
				item.Color = color;
			}
		}

		void IUndoRedoCommand.Undo()
		{
			for(int i=0; i< this.itemsToChange.Count; i++)
			{
				itemsToChange[i].OutputType = itemsPrintOutputType[i];
				itemsToChange[i].Color = itemsColor[i];
			}
		}
	}
}
