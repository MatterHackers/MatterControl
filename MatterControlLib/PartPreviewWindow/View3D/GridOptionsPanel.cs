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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GridOptionsPanel : DropButton
	{
		InteractionLayer interactionLayer;

		public GridOptionsPanel(InteractionLayer interactionLayer, ThemeConfig theme)
			: base(theme)
		{
			this.interactionLayer = interactionLayer;
			this.PopupContent = () => ShowGridOptions(theme);

			this.AddChild(new IconButton(AggContext.StaticData.LoadIcon("1694146.png", 16, 16, theme.InvertIcons), theme)
			{
				Selectable = false
			});
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
		}

		private GuiWidget ShowGridOptions(ThemeConfig theme)
		{
			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			var siblingList = new List<GuiWidget>();

			popupMenu.CreateBoolMenuItem(
				"Off".Localize(),
				() => interactionLayer.SnapGridDistance ==  0,
				(isChecked) =>
				{
					interactionLayer.SnapGridDistance = 0;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			var snapSettings = new List<double>()
			{
				.1, .25, .5, 1, 2, 5
			};

			foreach(var snap in snapSettings)
			{
				popupMenu.CreateBoolMenuItem(
					snap.ToString(),
					() => interactionLayer.SnapGridDistance == snap,
					(isChecked) =>
					{
						interactionLayer.SnapGridDistance =  snap;
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);
			}

			// Override menu left padding to improve radio circle -> icon spacing
			foreach (var menuItem in popupMenu.Children)
			{
				//menuItem.Padding = menuItem.Padding.Clone(left: 25);
			}

			return popupMenu;
		}
	}
}