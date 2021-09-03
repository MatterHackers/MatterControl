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
	public class SheetEditorWidget : FlowLayoutWidget
	{
		private SheetData sheetData;
		Point2D selectedCell = new Point2D(-1, -1);
		Dictionary<(int, int), GuiWidget> CellWidgetsByLocation = new Dictionary<(int, int), GuiWidget>();
		private ThemeConfig theme;
		private MHTextEditWidget editSelectedName;
		private MHTextEditWidget editSelectedExpression;

		public SheetEditorWidget(SheetData sheetData, UndoBuffer undoBuffer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			HAnchor = HAnchor.MaxFitOrStretch;

			this.sheetData = sheetData;
			var countWidth = 10 * GuiWidget.DeviceScale;
			var cellWidth = 50 * GuiWidget.DeviceScale;

			// put in the edit row
			var editSelectionGroup = this.AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			});

			editSelectedName = new MHTextEditWidget("", theme, cellWidth, messageWhenEmptyAndNotSelected: "Name".Localize())
			{
				HAnchor = HAnchor.Absolute,
				SelectAllOnFocus = true,
			};
			editSelectedName.ActualTextEditWidget.EditComplete += SelectedName_EditComplete;
			editSelectionGroup.AddChild(editSelectedName);
			editSelectedExpression = new MHTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "Select cell to edit".Localize())
			{
				HAnchor = HAnchor.Stretch,
				SelectAllOnFocus = true,
			};
			editSelectionGroup.AddChild(editSelectedExpression);
			editSelectedExpression.ActualTextEditWidget.EditComplete += ActualTextEditWidget_EditComplete1;

			var dataGrid = new GridWidget(sheetData.Width + 1, sheetData.Height + 1, theme: theme);

			this.AddChild(dataGrid);

			for (int x = 0; x < sheetData.Width; x++)
			{
				dataGrid.GetCell(x + 1, 0).AddChild(new TextWidget(((char)('A' + x)).ToString())
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					TextColor = theme.TextColor,
				});
			}

			dataGrid.SetColumnWidth(0, 20);

			for (int y = 0; y < sheetData.Height; y++)
			{
				// add row count
				dataGrid.GetCell(0, y + 1).AddChild(new TextWidget((y + 1).ToString())
				{
					TextColor = theme.TextColor,
					VAnchor = VAnchor.Center
				});

				for (int x = 0; x < sheetData.Width; x++)
				{
					var capturedX = x;
					var capturedY = y;

					var edit = new MHTextEditWidget(sheetData[x, y].Expression, theme)
					{
						HAnchor = HAnchor.Stretch,
						SelectAllOnFocus = true,
					};

					edit.VAnchor |= VAnchor.Center;

					CellWidgetsByLocation.Add((capturedX, capturedY), edit);

					edit.MouseDown += (s, e) => SelectCell(capturedX, capturedY);

					dataGrid.GetCell(x + 1, y + 1).AddChild(edit);
					edit.ActualTextEditWidget.EditComplete += (s, e) =>
					{
						editSelectedExpression.Text = edit.Text;
						sheetData[capturedX, capturedY].Expression = edit.Text;
						sheetData.Recalculate();
					};
				}

				dataGrid.ExpandToFitContent();
			}
		}

		private void ActualTextEditWidget_EditComplete1(object sender, EventArgs e)
		{
			if (selectedCell.x == -1)
			{
				return;
			}

			sheetData[selectedCell.x, selectedCell.y].Expression = editSelectedExpression.Text;
			CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].Text = editSelectedExpression.Text;
		}

		private void SelectedName_EditComplete(object sender, EventArgs e)
		{
			if (selectedCell.x == -1)
			{
				return;
			}

			var existingNames = new HashSet<string>();
			for (int y = 0; y < sheetData.Height; y++)
			{
				for (int x = 0; x < sheetData.Width; x++)
				{
					if (x != selectedCell.x || y != selectedCell.y)
					{
						var currentName = sheetData[x, y].Name;
						if (!string.IsNullOrEmpty(currentName))
						{
							existingNames.Add(currentName);
						}
					}
				}
			}

			var name = agg_basics.GetNonCollidingName(editSelectedName.Text, existingNames);
			editSelectedName.Text = name;
			sheetData[selectedCell.x, selectedCell.y].Name = name;
			sheetData.Recalculate();
		}

		private void SelectCell(int x, int y)
		{
			if (selectedCell.x != -1)
			{
				CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].BorderColor = Color.Transparent;
			}
			selectedCell.x = x;
			selectedCell.y = y;
			CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].BorderColor = theme.PrimaryAccentColor;
			editSelectedExpression.Text = sheetData[x, y].Expression;
			if (string.IsNullOrEmpty(sheetData[x, y].Name))
			{
				editSelectedName.Text = $"{(char)('A' + x)}{y + 1}";
			}
			else
			{
				editSelectedName.Text = sheetData[x, y].Name;
			}
		}
	}

	public class SheetEditor : IObject3DEditor
	{
		string IObject3DEditor.Name => "Sheet Editor";

		IEnumerable<Type> IObject3DEditor.SupportedTypes() => new[] { typeof(SheetObject3D) };

		public GuiWidget Create(IObject3D item, UndoBuffer undoBuffer, ThemeConfig theme)
		{
			if (item is SheetObject3D sheetObject)
			{
				return new SheetEditorWidget(sheetObject.SheetData, undoBuffer, theme);
			}

			return null;
		}
	}
}
