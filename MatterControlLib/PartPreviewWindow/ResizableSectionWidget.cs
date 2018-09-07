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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ResizableSectionWidget : SectionWidget
	{
		public BottomResizeContainer ResizeContainer { get; }

		public event EventHandler Resized;

		public ResizableSectionWidget(string sectionTitle, double initialHeight, GuiWidget sectionContent, ThemeConfig theme, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true, string serializationKey = null, bool defaultExpansion = false, bool setContentVAnchor = true)
			: base(sectionTitle, new GuiWidget(), theme, rightAlignedContent, headingPointSize, expandingContent, expanded, serializationKey, defaultExpansion, setContentVAnchor)
		{
			this.VAnchor = VAnchor.Fit;

			this.ResizeContainer = new BottomResizeContainer(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = initialHeight
			};
			this.ResizeContainer.Resized += (s, e) =>
			{
				this.Resized?.Invoke(this, null);
			};
			this.ResizeContainer.AddChild(sectionContent);

			// A wrapping container to fix resize quirks - GuiWidget with H:Stretch V:Fit that can be hidden and shown and allow the ResizeContainer can keep it's size
			var resizeWrapper = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = checkbox.Checked
			};
			resizeWrapper.AddChild(this.ResizeContainer);

			this.SetContentWidget(resizeWrapper);
		}
	}
}