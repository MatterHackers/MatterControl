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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GridOptionsPanel : FlowLayoutWidget, IIgnoredPopupChild
	{
		public GridOptionsPanel(InteractionLayer interactionLayer) : base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;

			this.AddChild(new TextWidget("Snap Grid".Localize())
			{
				Margin = new BorderDouble(35, 2, 8, 8),
				TextColor = Color.Gray,
				HAnchor = HAnchor.Left
			});

			var snapSettings = new Dictionary<double, string>()
			{
				{ 0, "Off" },
				{ .1, "0.1" },
				{ .25, "0.25" },
				{ .5, "0.5" },
				{ 1, "1" },
				{ 2, "2" },
				{ 5, "5" },
			};

			var dropDownList = new DropDownList("Custom", ActiveTheme.Instance.PrimaryTextColor, Direction.Down)
			{
				TextColor = Color.Black,
				Margin = new BorderDouble(35, 15, 35, 5),
				HAnchor = HAnchor.Left
			};

			foreach (var snapSetting in snapSettings)
			{
				MenuItem newItem = dropDownList.AddItem(snapSetting.Value);
				if (interactionLayer.SnapGridDistance == snapSetting.Key)
				{
					dropDownList.SelectedLabel = snapSetting.Value;
				}

				newItem.Selected += (sender, e) =>
				{
					interactionLayer.SnapGridDistance = snapSetting.Key;
				};
			}
			this.AddChild(dropDownList);
		}
	}
}