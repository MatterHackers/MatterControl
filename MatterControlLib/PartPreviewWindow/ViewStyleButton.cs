﻿/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.RenderOpenGl;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewStyleButton : DropButton
	{
		private ThemedIconButton iconButton;
		private ISceneContext sceneContext;

		private Dictionary<RenderTypes, (ImageBuffer image, string toolTip)> viewData;
		private PopupMenu popupMenu;

		public ViewStyleButton(ISceneContext sceneContext, ThemeConfig theme)
			: base(theme)
		{
			this.sceneContext = sceneContext;
			this.PopupContent = ShowViewOptions;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			viewData = new Dictionary<RenderTypes, (ImageBuffer image, string toolTip)>
			{
				[RenderTypes.Shaded] = (StaticData.Instance.LoadIcon("view_shaded.png", 16, 16), "View Mode = Shaded".Localize()),
				[RenderTypes.Outlines] = (StaticData.Instance.LoadIcon("view_outlines.png", 16, 16), "View Mode = Outlines".Localize()),
				[RenderTypes.Polygons] = (StaticData.Instance.LoadIcon("view_polygons.png", 16, 16), "View Mode = Polygons".Localize()),
				[RenderTypes.NonManifold] = (StaticData.Instance.LoadIcon("view_polygons.png", 16, 16), "View Mode = Non-Manifold".Localize()),
				[RenderTypes.Overhang] = (StaticData.Instance.LoadIcon("view_overhang.png", 16, 16), "View Mode = Overhangs".Localize()),
			};

			var renderType = sceneContext.ViewState.RenderType;
			this.AddChild(iconButton = new ThemedIconButton(viewData[renderType].image, theme)
			{
				Selectable = false
			});

            ToolTipText = viewData[renderType].toolTip;

			UserSettings.Instance.SettingChanged += UserSettings_SettingChanged;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listener
			UserSettings.Instance.SettingChanged -= UserSettings_SettingChanged;

			base.OnClosed(e);
		}

		private void UserSettings_SettingChanged(object sender, StringEventArgs e)
		{
			if (e.Data == UserSettingsKey.defaultRenderSetting)
			{
				var renderType = sceneContext.ViewState.RenderType;
				iconButton.SetIcon(viewData[renderType].image);
				if (!this.MenuVisible)
				{
					this.FlashBackground(theme.PrimaryAccentColor.WithContrast(theme.TextColor, 6).ToColor());
				}
				ToolTipText = viewData[renderType].toolTip;

				popupMenu?.Close();
				popupMenu = null;
			}
		}

		private GuiWidget ShowViewOptions()
		{
			popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

			var siblingList = new List<GuiWidget>();

			popupMenu.CreateBoolMenuItem(
				"Shaded".Localize(),
				viewData[RenderTypes.Shaded].image,
				() => sceneContext.ViewState.RenderType == RenderTypes.Shaded,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Shaded;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Outlines (default)".Localize(),
				viewData[RenderTypes.Outlines].image,
				() => sceneContext.ViewState.RenderType == RenderTypes.Outlines,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Outlines;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

#if DEBUG
			popupMenu.CreateBoolMenuItem(
				"Non-Manifold".Localize(),
				viewData[RenderTypes.Polygons].image,
				() => sceneContext.ViewState.RenderType == RenderTypes.NonManifold,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.NonManifold;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);
#endif

			popupMenu.CreateBoolMenuItem(
				"Polygons".Localize(),
				viewData[RenderTypes.Polygons].image,
				() => sceneContext.ViewState.RenderType == RenderTypes.Polygons,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Polygons;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Overhang".Localize(),
				viewData[RenderTypes.Overhang].image,
				() => sceneContext.ViewState.RenderType == RenderTypes.Overhang,
				(isChecked) =>
				{
					sceneContext.ViewState.RenderType = RenderTypes.Overhang;
				},
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			// Override menu left padding to improve radio circle -> icon spacing
			foreach(var menuItem in popupMenu.Children)
			{
				menuItem.Padding = menuItem.Padding.Clone(left: 25);
			}

			return popupMenu;
		}
	}
}
