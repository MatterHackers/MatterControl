/*
Copyright (c) 2017, Matt Moening, Lars Brubaker, John Lewin
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
	public class SplitButton : FlowLayoutWidget
	{
		private DropDownMenu altChoices;

		private Button DefaultButton { get; }

		public SplitButton(string buttonText, Direction direction = Direction.Down)
			: base(FlowDirection.LeftToRight)
		{
			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;

			this.DefaultButton = CreateDefaultButton(buttonText);
			this.DefaultButton.VAnchor = VAnchor.ParentCenter;

			altChoices = CreateDropDown(direction);

			AddChild(this.DefaultButton);
			AddChild(altChoices);
		}

		public SplitButton(Button button, DropDownMenu menu)
			: base(FlowDirection.LeftToRight)
		{
			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;

			this.DefaultButton = button;
			this.DefaultButton.VAnchor = VAnchor.ParentCenter;

			altChoices = menu;

			AddChild(this.DefaultButton);
			AddChild(altChoices);
		}

		private DropDownMenu CreateDropDown(Direction direction)
		{
			return new DropDownMenu("", direction)
			{
				VAnchor = VAnchor.ParentCenter,
				MenuAsWideAsItems = false,
				AlignToRightEdge = true,
				Height = this.DefaultButton.Height
			};
		}

		private Button CreateDefaultButton(string buttonText)
		{
			var buttonFactory = new TextImageButtonFactory()
			{
				FixedHeight = 30 * GuiWidget.DeviceScale,
				normalFillColor = RGBA_Bytes.White,
				normalTextColor = RGBA_Bytes.Black,
				hoverTextColor = RGBA_Bytes.Black,
				hoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				borderWidth = 1,
				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200)
			};

			return buttonFactory.Generate(buttonText, centerText: true);
		}
	}
}