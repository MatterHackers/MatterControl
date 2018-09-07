/*
Copyright (c) 2018, John Lewin
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
using System.Collections.ObjectModel;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.RenderOpenGl;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewStyleButton : PopupButton
	{
		private IconButton iconButton;
		private BedConfig sceneContext;

		private Dictionary<RenderTypes, ImageBuffer> viewIcons;

		public ViewStyleButton(BedConfig sceneContext, ThemeConfig theme)
		{
			this.sceneContext = sceneContext;
			this.DynamicPopupContent = () => ShowViewOptions(sceneContext, theme);
			this.AlignToRightEdge = true;

			viewIcons = new Dictionary<RenderTypes, ImageBuffer>()
			{
				[RenderTypes.Shaded] = AggContext.StaticData.LoadIcon("view_shaded.png", theme.InvertIcons),
				[RenderTypes.Outlines] = AggContext.StaticData.LoadIcon("view_outlines.png", theme.InvertIcons),
				[RenderTypes.Polygons] = AggContext.StaticData.LoadIcon("view_polygons.png", theme.InvertIcons),
				[RenderTypes.Materials] = AggContext.StaticData.LoadIcon("view_materials.png", theme.InvertIcons),
				[RenderTypes.Overhang] = AggContext.StaticData.LoadIcon("view_overhang.png", theme.InvertIcons),
			};

			this.AddChild(iconButton = new IconButton(viewIcons[sceneContext.ViewState.RenderType], theme));
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			UserSettings.Instance.SettingChanged += UserSettings_SettingChanged;
		}

		private void UserSettings_SettingChanged(object sender, StringEventArgs e)
		{
			if (e.Data == UserSettingsKey.defaultRenderSetting)
			{
				iconButton.SetIcon(viewIcons[sceneContext.ViewState.RenderType]);
			}
		}

		private GuiWidget ShowViewOptions(BedConfig sceneContext, ThemeConfig theme)
		{
			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			var siblingList = new List<GuiWidget>();

			popupMenu.CreateBoolMenuItem(
				"Shaded".Localize(),
				viewIcons[RenderTypes.Shaded],
				() => sceneContext.ViewState.RenderType == RenderTypes.Shaded,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Shaded;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Outlines".Localize(),
				viewIcons[RenderTypes.Outlines],
				() => sceneContext.ViewState.RenderType == RenderTypes.Outlines,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Outlines;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Polygons".Localize(),
				viewIcons[RenderTypes.Polygons],
				() => sceneContext.ViewState.RenderType == RenderTypes.Polygons,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Polygons;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Materials".Localize(),
				viewIcons[RenderTypes.Materials],
				() => sceneContext.ViewState.RenderType == RenderTypes.Materials,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Materials;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Overhang".Localize(),
				AggContext.StaticData.LoadIcon("view_overhang.png", theme.InvertIcons),
				() => sceneContext.ViewState.RenderType == RenderTypes.Overhang,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Overhang;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			return popupMenu;
		}
	}
}
