/*
Copyright (c) 2016, Lars Brubaker
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

namespace MatterHackers.MatterControl
{
	public class AltGroupBox : FlowLayoutWidget
	{
		private GuiWidget groupBoxLabel;

		public RGBA_Bytes TextColor
		{
			get
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					return textBox.TextColor;
				}
				return RGBA_Bytes.White;
			}
			set
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					textBox.TextColor = value;
				}
			}
		}

		public RGBA_Bytes BorderColor { get; set; } = RGBA_Bytes.Black;

		public GuiWidget ClientArea { get; }

		public AltGroupBox()
			: this("")
		{
		}

		public AltGroupBox(GuiWidget groupBoxLabel)
			: base(FlowDirection.TopToBottom)
		{
			this.Padding = new BorderDouble(5);
			this.Margin = new BorderDouble(0);
			this.groupBoxLabel = groupBoxLabel;
			this.HAnchor = Agg.UI.HAnchor.Stretch;

			if (groupBoxLabel != null)
			{
				groupBoxLabel.Margin = new BorderDouble(0);
				groupBoxLabel.HAnchor = HAnchor.Stretch;
				base.AddChild(groupBoxLabel);
			}

			this.ClientArea = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			base.AddChild(this.ClientArea);
		}

		public AltGroupBox(string title)
			: this(new TextWidget(title, pointSize: 12))
		{
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			this.ClientArea.AddChild(childToAdd, indexInChildrenList);
		}

		public override string Text
		{
			get
			{
				if (groupBoxLabel != null)
				{
					return groupBoxLabel.Text;
				}

				return "";
			}
			set
			{
				if (groupBoxLabel != null)
				{
					groupBoxLabel.Text = value;
				}
			}
		}
	}
}