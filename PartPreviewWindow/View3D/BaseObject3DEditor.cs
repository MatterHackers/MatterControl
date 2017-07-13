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
using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class BaseObject3DEditor : IObject3DEditor
	{
		private IObject3D item;
		private View3DWidget view3DWidget;

		public string Name => "General";

		public bool Unlocked => true;

		public IEnumerable<Type> SupportedTypes()
		{
			return new Type[] { typeof(Object3D) };
		}

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.item = item;
			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			FlowLayoutWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.AbsolutePosition,
				Visible = true,
				Width = theme.WhiteButtonFactory.FixedWidth
			};
			mainContainer.AddChild(tabContainer);

			Button changeColorButton = theme.textImageButtonFactory.Generate("Color".Localize());
			changeColorButton.Margin = new BorderDouble(5);
			changeColorButton.HAnchor = HAnchor.ParentRight;
			Random rand = new Random();
			changeColorButton.Click += (s, e) =>
			{
				item.Color = new RGBA_Bytes(rand.Next(255), rand.Next(255), rand.Next(255));
				view3DWidget.Invalidate();
			};

			tabContainer.AddChild(changeColorButton);

			return mainContainer;
		}
	}
}