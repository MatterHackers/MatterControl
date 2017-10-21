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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class TextEditor : IObject3DEditor
	{
		private TextObject injectedItem = null;
		private SolidSlider spacingScrollBar;
		private TextGenerator textGenerator;
		private MHTextEditWidget textToAddWidget;
		private View3DWidget view3DWidget;
		public string Name { get; } = "Text";
		public bool Unlocked { get; } = true;

		public GuiWidget Create(IObject3D item, View3DWidget parentView3D, ThemeConfig theme)
		{
			var scene = parentView3D.InteractionLayer.Scene;

			injectedItem = scene?.SelectedItem as TextObject;

			textGenerator = new TextGenerator();
			this.view3DWidget = parentView3D;

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};

			textToAddWidget = new MHTextEditWidget("", messageWhenEmptyAndNotSelected: "Text".Localize())
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5),
				Text = injectedItem.Text,
				Width = 50
			};
			textToAddWidget.ActualTextEditWidget.EnterPressed += (s, e) => RebuildText(textToAddWidget.Text);
			container.AddChild(textToAddWidget);

			spacingScrollBar = theme.CreateSolidSlider(container, "Spacing:".Localize(), .5, 1);
			spacingScrollBar.ValueChanged += (sender, e) =>
			 {
				 if (injectedItem != null)
				 {
					 RebuildText(textToAddWidget.Text);
				 }
			 };

			Button updateButton = theme.ButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.Right;
			updateButton.Click += (s, e) => RebuildText(textToAddWidget.Text);
			container.AddChild(updateButton);

			return container;
		}

		public IEnumerable<Type> SupportedTypes()
		{
			return new Type[] { typeof(TextObject) };
		}

		internal void SetInitialFocus()
		{
			textToAddWidget.Focus();
		}

		private void ModifyInjectedItem(Action<IObject3D> modifier)
		{
			// Create a copy of the injected group
			IObject3D workItem = injectedItem.Clone();

			// Invoke the passed in action
			modifier(workItem);

			// Modify the scene graph, swapping in the modified item
			var scene = view3DWidget.InteractionLayer.Scene;

			scene.Children.Modify(childList =>
			{
				childList.Remove(injectedItem);
				childList.Add(workItem);
			});

			// Update the injected item and the scene selection
			injectedItem = workItem as TextObject;
			scene.SelectedItem = injectedItem;
		}

		private async void RebuildText(string text)
		{
			injectedItem.Text = text;

			// Clear prior selection
			if (text.Length <= 0)
			{
				return;
			}

			view3DWidget.LockEditControls();

			var generatedItem = await Task.Run(() =>
			{
				Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

				return textGenerator.CreateText(text, spacingScrollBar.Value);
			});

			var scene = view3DWidget.InteractionLayer.Scene;
			scene.Children.Modify(list =>
			{
				var item = list.Find(child => child == injectedItem);

				item.Children.Modify(childList =>
				{
					childList.Clear();
					childList.AddRange(generatedItem.Children);
				});
			});

			view3DWidget.UnlockEditControls();
		}
	}
}