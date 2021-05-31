/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetEditor : IObject3DEditor, IObject3DControlsProvider
	{
		private SheetObject3D sheetObject;

		string IObject3DEditor.Name => "Sheet Editor";

		IEnumerable<Type> IObject3DEditor.SupportedTypes() => new[] { typeof(SheetObject3D) };

		public GuiWidget Create(IObject3D item, UndoBuffer undoBuffer, ThemeConfig theme)
		{
			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch
			};

			sheetObject = item as SheetObject3D;
			var sheetData = sheetObject.SheetData;
			var countWidth = 10 * GuiWidget.DeviceScale;
			var cellWidth = 50 * GuiWidget.DeviceScale;

			// put in the edit row
			var editRow = topToBottom.AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			});
			var editNameField = editRow.AddChild(new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Absolute,
				Width = cellWidth,
			});
			var editSelectionField = editRow.AddChild(new MHTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "Select cell to edit".Localize())
			{
				HAnchor = HAnchor.Stretch,
			});

			// put in the header row
			var topRow = topToBottom.AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			});

			
			topRow.AddChild(new GuiWidget(cellWidth, 1));
			for (int x = 0; x < sheetData.Width; x++)
			{
				topRow.AddChild(new TextWidget(((char)('A' + x)).ToString())
				{
					HAnchor = HAnchor.Absolute,
					Width = cellWidth,
					TextColor = theme.TextColor
				});
			}
			
			for (int y=0; y<sheetData.Height; y++)
			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
				};
				topToBottom.AddChild(row);

				// add row count
				row.AddChild(new TextWidget((y + 1).ToString())
				{
					TextColor = theme.TextColor,
				});

				for (int x=0; x<sheetData.Width; x++)
				{
					var edit = new MHTextEditWidget(sheetData[x, y], theme, cellWidth)
					{
						SelectAllOnFocus = true,
					};

					row.AddChild(edit);
					var capturedX = x;
					var capturedY = y;
					edit.ActualTextEditWidget.EditComplete += (s, e) =>
					{
						sheetData[capturedX, capturedY] = edit.ActualTextEditWidget.Text;
						Recalculate();
					};
				}
			}

			return topToBottom;
		}

		private void Recalculate()
		{
			sheetObject.Invalidate(InvalidateType.SheetUpdated);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
		}
	}
}
