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

using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetFieldWidget : GuiWidget
	{
		private SheetEditorWidget sheetEditorWidget;
		private int x;
		private int y;
		private TextWidget content;
		private string undoContent;

		private SheetData SheetData => sheetEditorWidget.SheetData;

		private enum EditModes
		{
			Unknown,
			QuickText,
			FullEdit
		}

		private EditModes EditMode { get; set; }

		public SheetFieldWidget(SheetEditorWidget sheetEditorWidget, int x, int y, ThemeConfig theme)
		{
			this.sheetEditorWidget = sheetEditorWidget;
			this.x = x;
			this.y = y;
			this.Name = $"Cell {x},{y}";

			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;
			Selectable = true;

			content = new TextWidget("")
			{
				TextColor = theme.TextColor,
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Bottom,
			};

			SheetData.Recalculated += (s, e) =>
			{
				UpdateContents();
			};

			UpdateContents();

			this.AddChild(content);
		}

		private void UpdateContents()
		{
			var expression = SheetData[x, y].Expression;
			if (expression.StartsWith("="))
			{
				content.Text = SheetData.EvaluateExpression(expression);
			}
			else
			{
				content.Text = expression;
			}
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyPress(keyPressEvent);
			if (!keyPressEvent.Handled)
			{
				if (keyPressEvent.KeyChar < 32
					&& keyPressEvent.KeyChar != 13
					&& keyPressEvent.KeyChar != 9)
				{
					return;
				}

				if (EditMode == EditModes.Unknown)
				{
					EditMode = EditModes.QuickText;
					this.content.Text = "";
				}

				this.content.Text += keyPressEvent.KeyChar.ToString();

				UpdateSheetEditField();
			}
		}

		private void UpdateSheetEditField()
		{
			this.sheetEditorWidget.EditSelectedExpression.Text = this.content.Text;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// handle the tab early so it does not get consumed with switching to the next widget
			if (!keyEvent.Handled)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Tab:
						Navigate(1, 0);
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;
				}
			}

			
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Escape:
						// reset the selction to what it was before we started editing
						this.content.Text = undoContent;
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						// and go back to the unknown state
						EditMode = EditModes.Unknown;
						UpdateSheetEditField();
						break;

					case Keys.Left:
						Navigate(-1, 0);
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Down:
						Navigate(0, 1);
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Right:
						Navigate(1, 0);
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Up:
						Navigate(0, -1);
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Enter:
						switch (EditMode)
						{
							// go into full edit
							case EditModes.Unknown:
								EditMode = EditModes.FullEdit;
								break;

							// finish edit and move down
							case EditModes.QuickText:
							case EditModes.FullEdit:
								Navigate(0, 1);
								// make sure we know we are edit complete
								EditComplete();
								break;
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Delete:
						switch (EditMode)
						{
							case EditModes.Unknown:
								// delete content
								this.content.Text = "";
								break;

							case EditModes.QuickText:
								// do nothing
								break;

							case EditModes.FullEdit:
								// delete from front
								break;
						}
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						UpdateSheetEditField();
						break;

					case Keys.Back:
						switch (EditMode)
						{
							case EditModes.Unknown:
								// delete text
								this.content.Text = "";
								break;

							case EditModes.QuickText:
								// delete from back
								if (this.content.Text.Length > 0)
								{
									this.content.Text = this.content.Text.Substring(0, this.content.Text.Length - 1);
								}
								break;

							case EditModes.FullEdit:
								// delete from back
								break;
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						UpdateSheetEditField();
						break;
				}
			}
		}

		public override void OnFocusChanged(System.EventArgs e)
		{
			base.OnFocusChanged(e);

			if (this.Focused)
			{
				undoContent = this.content.Text;
				EditMode = EditModes.Unknown;
			}
			else
			{
				EditComplete();
			}
		}

		private void EditComplete()
		{
			if (this.content.Text != undoContent)
			{
				// make sure the is a sheet update
				SheetData[x, y].Expression = this.content.Text;
				SheetData.Recalculate();
			}
			EditMode = EditModes.Unknown;
			undoContent = this.content.Text;
		}

		private void Navigate(int xOffset, int yOffset)
		{
			sheetEditorWidget.SelectCell(x + xOffset, y + yOffset);
		}
	}
}
