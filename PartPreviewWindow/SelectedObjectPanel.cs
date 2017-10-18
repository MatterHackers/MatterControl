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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget
	{
		private IObject3D item = new Object3D();

		private GuiWidget editorPanel;
		private TextWidget itemName;

		public SelectedObjectPanel(View3DWidget view3DWidget, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor |= HAnchor.Right;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = new BorderDouble(8, 10);
			this.MinimumSize = new VectorMath.Vector2(200, 0);

			this.AddChild(itemName = new TextWidget("", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(bottom: 10)
			});

			var behavior3DTypeButtons = new FlowLayoutWidget();
			this.AddChild(behavior3DTypeButtons);

			var buttonMargin = new BorderDouble(2, 5);

			// put in the button for making the behavior solid
			var solidButtonView = theme.ButtonFactory.Generate("Color".Localize());
			var solidBehaviorButton = new PopupButton(solidButtonView)
			{
				Name = "Solid Colors",
				AlignToRightEdge = true,
				PopupContent = new ColorSwatchSelector(item, view3DWidget)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					BackgroundColor = RGBA_Bytes.White
				},
				Margin = buttonMargin
			};
			solidBehaviorButton.Click += (s, e) =>
			{
				item.OutputType = PrintOutputTypes.Solid;
			};

			behavior3DTypeButtons.AddChild(solidBehaviorButton);

			var objectActionList = new DropDownList("Actions", maxHeight: 200)
			{
				HAnchor = HAnchor.Stretch
			};

			foreach (var namedAction in ApplicationController.Instance.RegisteredSceneOperations())
			{
				var menuItem = objectActionList.AddItem(namedAction.Title.Localize());
				menuItem.Click += (s, e) =>
				{
					namedAction.Action.Invoke(ApplicationController.Instance.ActivePrinter.Bed.Scene);
				};
			}

			this.AddChild(objectActionList);

			this.AddChild(editorPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(top: 10)
			});
		}

		public void SetActiveItem(IObject3D selectedItem, GuiWidget editorWidget)
		{
			this.itemName.Text = selectedItem.Name ?? selectedItem.GetType().Name;

			this.item = selectedItem;

			this.editorPanel.RemoveAllChildren();

			this.editorPanel.AddChild(editorWidget);

			this.Visible = true;
		}
	}
}