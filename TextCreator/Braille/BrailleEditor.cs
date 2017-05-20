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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public class BrailleEditor : IObject3DEditor
	{
		private MHTextEditWidget textToAddWidget;
		private CheckBox includeText;
		private CheckBox useGrade2;

		private BrailleGenerator brailleGenerator;
		private View3DWidget view3DWidget;

		private TextObject injectedItem = null;

		public bool Unlocked { get; } = true;

		public IEnumerable<Type> SupportedTypes()
		{
			return new Type[] { typeof(TextObject) };
		}

		public GuiWidget Create(IObject3D item, View3DWidget parentView3D)
		{
			injectedItem = parentView3D.Scene?.SelectedItem as TextObject;

			brailleGenerator = new BrailleGenerator();
			this.view3DWidget = parentView3D;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			FlowLayoutWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.AbsolutePosition,
				Visible = true,
				Width = view3DWidget.WhiteButtonFactory.FixedWidth
			};
			mainContainer.AddChild(tabContainer);

			textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter Text Here".Localize())
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(5),
				Text = injectedItem.Text
			};
			textToAddWidget.ActualTextEditWidget.EnterPressed += (s, e) => RebuildText(textToAddWidget.Text);
			tabContainer.AddChild(textToAddWidget);

			includeText = new CheckBox(new CheckBoxViewText("Include Text".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor))
			{
				ToolTipText = "Show normal text above the braille".Localize(),
				Checked = false,
				Margin = new BorderDouble(10, 5),
				HAnchor = HAnchor.ParentLeft
			};

			tabContainer.AddChild(includeText);
			includeText.CheckedStateChanged += (s, e) => RebuildText(textToAddWidget.Text);

			useGrade2 = new CheckBox(new CheckBoxViewText("Use Grade 2".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
			useGrade2.ToolTipText = "Experimental support for Braille grade 2 (contractions)".Localize();
			useGrade2.Checked = false;
			useGrade2.Margin = new BorderDouble(10, 5);
			useGrade2.HAnchor = HAnchor.ParentLeft;
			tabContainer.AddChild(useGrade2);
			useGrade2.CheckedStateChanged += (sender, e) =>
			{
				RebuildText(textToAddWidget.Text);
			};
			
			Button updateButton = view3DWidget.textImageButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.ParentRight;
			updateButton.Click += (s, e) => RebuildText(textToAddWidget.Text);
			tabContainer.AddChild(updateButton);

			// put in a link to the wikipedia article
			{
				LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
				linkButtonFactory.fontSize = 10;
				linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;

				Button moreAboutBrailleLink = linkButtonFactory.Generate("About Braille".Localize());
				moreAboutBrailleLink.Margin = new BorderDouble(10, 5);
				moreAboutBrailleLink.HAnchor = HAnchor.ParentLeft;
				moreAboutBrailleLink.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						MatterControlApplication.Instance.LaunchBrowser("https://en.wikipedia.org/wiki/Braille");
					});
				};

				tabContainer.AddChild(moreAboutBrailleLink);
			}

			return mainContainer;
		}

		public string Name { get; } = "Braille";

		private async void RebuildText(string brailleText)
		{
			injectedItem.Text = brailleText;

			if (brailleText.Length <= 0)
			{
				return;
			}

			if (useGrade2.Checked)
			{
				brailleText = BrailleGrade2.ConvertString(brailleText);
			}

			brailleGenerator.ResetSettings();

			//processingProgressControl.ProcessType = "Inserting Text".Localize();
			//processingProgressControl.Visible = true;
			//processingProgressControl.PercentComplete = 0;

			view3DWidget.LockEditControls();

			var generatedItem = await Task.Run(() =>
			{
				Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

				return brailleGenerator.CreateText(
					brailleText,
					1,
					1,
					includeText.Checked,
					injectedItem.Text);
			});

			view3DWidget.Scene.ModifyChildren(children =>
			{
				// Find the injected item
				var item = children.Find(child => child == injectedItem);

				// Clear and refresh its children
				item.Children.Clear();
				item.Children.AddRange(generatedItem.Children);
			});

			//PlatingHelper.MoveToOpenPosition(injectedItem, view3DWidget.Scene);

			//view3DWidget.InsertNewItem(injectedItem);

			view3DWidget.UnlockEditControls();
		}

		private void RebuildBase()
		{
			if (view3DWidget.Scene.HasChildren && injectedItem != null)
			{
				var newBaseplate = brailleGenerator.CreateBaseplate(injectedItem);
				if(newBaseplate == null)
				{
					return;
				}

				// Remove the old base and create and add a new one
				view3DWidget.Scene.ModifyChildren(children =>
				{
					children.RemoveAll(child => child is BraileBasePlate);
					children.Add(newBaseplate);
				});
			}
		}

		internal void SetInitialFocus()
		{
			textToAddWidget.Focus();
		}
	}
}