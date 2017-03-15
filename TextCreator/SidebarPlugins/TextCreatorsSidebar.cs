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
using MatterHackers.MatterControl.Plugins.BrailleBuilder;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class TextCreatorsSidebar : SideBarPlugin
	{
		private static IObject3D textItem;
		private static IObject3D brailleItem;

		public override GuiWidget CreateSideBarTool(View3DWidget view3DWidget)
		{
			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabButton = view3DWidget.ExpandMenuOptionFactory.GenerateCheckBoxButton("TEXT".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
			tabButton.Margin = new BorderDouble(bottom: 2);
			mainContainer.AddChild(tabButton);

			FlowLayoutWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Visible = false
			};
			mainContainer.AddChild(tabContainer);

			tabButton.CheckedStateChanged += (sender, e) =>
			{
				tabContainer.Visible = tabButton.Checked;
			};

			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonRow.AddChild(
				view3DWidget.Sidebar.CreateAddButton("Text".Localize(), "textcreator.png", () =>
				{
					if(textItem == null)
					{
						var generator = new TextGenerator();
						textItem = generator.CreateText(
							"Text".Localize(),
							1,
							.25,
							1,
							true);
					}

					return textItem;
				}));

			buttonRow.AddChild(
				view3DWidget.Sidebar.CreateAddButton("Braille".Localize(), "braillecreator.png", () =>
				{
					if (brailleItem == null)
					{
						string braille = "Braille".Localize();

						var generator = new BrailleGenerator();
						brailleItem = generator.CreateText(
							braille,
							1,
							.25,
							true,
							braille);
					}

					return brailleItem;
				}));

			tabContainer.AddChild(buttonRow);

			return mainContainer;
		}
	}
}