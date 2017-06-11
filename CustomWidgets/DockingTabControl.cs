/*
Copyright (c) 2017, Lars Brubaker
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DockingTabControl : GuiWidget
	{
		GuiWidget topToBottom;

		public override void Initialize()
		{
			base.Initialize();

			Width = 30;
			VAnchor = VAnchor.ParentBottomTop;
			HAnchor = HAnchor.FitToChildren;
			BackgroundColor = RGBA_Bytes.Red;
			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.ParentBottomTop
			};
			AddChild(topToBottom);
		}

		class RemainOpenWrapper : GuiWidget, IIgnoredPopupChild
		{

		}

		public void AddPage(string name, GuiWidget widget)
		{
			TextWidget optionsText = new TextWidget(name);
			PopupButton settingsButton = new PopupButton(optionsText)
			{
				AlignToRightEdge = true,
			};

			settingsButton.PopupContent = new RemainOpenWrapper()
			{
				Width = 500,
				Height = 640,
			};
			settingsButton.PopupContent.AddChild(widget);

			topToBottom.AddChild(settingsButton);
		}
	}
}
