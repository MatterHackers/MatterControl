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
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetEditorWidget : FlowLayoutWidget
	{
		public SheetData SheetData { get; private set; }
		Point2D selectedCell = new Point2D(-1, -1);
		Dictionary<(int, int), GuiWidget> CellWidgetsByLocation = new Dictionary<(int, int), GuiWidget>();

        public UndoBuffer UndoBuffer { get; }

        private ThemeConfig theme;
		private ThemedTextEditWidget editSelectedName;
		public ThemedTextEditWidget EditSelectedExpression { get; private set; }
		private GridWidget gridWidget;

		public SheetEditorWidget(SheetData sheetData, SheetObject3D sheetObject, UndoBuffer undoBuffer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.UndoBuffer = undoBuffer;
			this.theme = theme;
			HAnchor = HAnchor.MaxFitOrStretch;

			this.SheetData = sheetData;
			var cellEditNameWidth = 80 * GuiWidget.DeviceScale;

			// put in the edit row
			var editSelectionGroup = this.AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			});

			editSelectedName = new ThemedTextEditWidget("", theme, cellEditNameWidth, messageWhenEmptyAndNotSelected: "Name".Localize())
			{
				HAnchor = HAnchor.Absolute,
			};
			editSelectedName.ActualTextEditWidget.EditComplete += SelectedName_EditComplete;
			editSelectionGroup.AddChild(editSelectedName);
			EditSelectedExpression = new ThemedTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "Select cell to edit".Localize())
			{
				HAnchor = HAnchor.Stretch,
			};
			editSelectionGroup.AddChild(EditSelectedExpression);
			EditSelectedExpression.ActualTextEditWidget.EditComplete += ActualTextEditWidget_EditComplete1;

			gridWidget = new GridWidget(sheetData.Width + 1, sheetData.Height + 1, theme: theme);

			this.AddChild(gridWidget);

			for (int x = 0; x < sheetData.Width; x++)
			{
				var letterCell = gridWidget.GetCell(x + 1, 0);
				letterCell.AddChild(new TextWidget(((char)('A' + x)).ToString())
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					TextColor = theme.TextColor,
				});
			
				letterCell.BackgroundColor = theme.SlightShade;
			}

			gridWidget.SetColumnWidth(0, 20);

			for (int y = 0; y < sheetData.Height; y++)
			{
				// add row count
				var numCell = gridWidget.GetCell(0, y + 1);
				numCell.AddChild(new TextWidget((y + 1).ToString())
				{
					TextColor = theme.TextColor,
					VAnchor = VAnchor.Center,
				});

				numCell.BackgroundColor = theme.SlightShade;

				for (int x = 0; x < sheetData.Width; x++)
				{
					var capturedX = x;
					var capturedY = y;

					var edit = new SheetFieldWidget(this, x, y, theme);

					CellWidgetsByLocation.Add((capturedX, capturedY), edit);

					edit.MouseUp += (s, e) => SelectCell(capturedX, capturedY);

					gridWidget.GetCell(x + 1, y + 1).AddChild(edit);
				}

				gridWidget.ExpandToFitContent();
			}

			if (sheetObject != null)
			{
				PublicPropertyEditor.AddWebPageLinkIfRequired(sheetObject, this, theme);
			}
		}

		private void ActualTextEditWidget_EditComplete1(object sender, EventArgs e)
		{
			if (selectedCell.x == -1)
			{
				return;
			}

			SheetData[selectedCell.x, selectedCell.y].Expression = EditSelectedExpression.Text;
			CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].Text = EditSelectedExpression.Text;
			SheetData.Recalculate();
		}

		private void SelectedName_EditComplete(object sender, EventArgs e)
		{
			if (selectedCell.x == -1)
			{
				return;
			}

			var existingNames = new HashSet<string>();
			for (int y = 0; y < SheetData.Height; y++)
			{
				for (int x = 0; x < SheetData.Width; x++)
				{
					if (x != selectedCell.x || y != selectedCell.y)
					{
						var currentName = SheetData[x, y].Name;
						if (!string.IsNullOrEmpty(currentName))
						{
							existingNames.Add(currentName);
						}
					}
				}
			}

			// first replace spaces with '_'
			var name = editSelectedName.Text.Replace(' ', '_');
			// next make sure we don't have the exact name already
			name = agg_basics.GetNonCollidingName(name, existingNames, false);
			editSelectedName.Text = name;
			SheetData[selectedCell.x, selectedCell.y].Name = name;
			SheetData.Recalculate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// draw the selected widget
			var x = selectedCell.x;
			var y = selectedCell.y;
			if (x < 0 || x >= SheetData.Width || y < 0 || y >= SheetData.Height)
			{
				// out of bounds
				return;
			}

			var cell = gridWidget.GetCell(x + 1, y + 1);
			var bounds = cell.TransformToParentSpace(this, cell.LocalBounds);
			graphics2D.Rectangle(bounds, theme.PrimaryAccentColor);
		}

		public void SelectCell(int x, int y)
		{
			if (x < 0 || x >= SheetData.Width || y < 0 || y >= SheetData.Height)
			{
				// out of bounds
				return;
			}

			if (selectedCell.x != -1)
			{
				CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].BorderColor = Color.Transparent;
			}
			selectedCell.x = x;
			selectedCell.y = y;
			CellWidgetsByLocation[(selectedCell.x, selectedCell.y)].BorderColor = theme.PrimaryAccentColor;
			EditSelectedExpression.Text = SheetData[x, y].Expression;
			if (string.IsNullOrEmpty(SheetData[x, y].Name))
			{
				editSelectedName.Text = $"{(char)('A' + x)}{y + 1}";
			}
			else
			{
				editSelectedName.Text = SheetData[x, y].Name;
			}

			gridWidget.GetCell(x + 1, y + 1).Children.FirstOrDefault()?.Focus();
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
				return new SheetEditorWidget(sheetObject.SheetData, sheetObject, undoBuffer, theme);
			}

			return null;
		}
	}
}
