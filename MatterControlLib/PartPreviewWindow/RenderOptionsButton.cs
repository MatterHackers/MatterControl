/*
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RenderOptionsButton : DropButton
	{
		public RenderOptionsButton(ThemeConfig theme, InteractionLayer interactionLayer)
			: base (theme)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			this.AddChild(new IconButton(AggContext.StaticData.LoadIcon("web.png", theme.InvertIcons), theme)
			{
				Selectable = false
			});

			this.PopupContent = () =>
			{
				var menuTheme = AppContext.MenuTheme;

				var subPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Padding = theme.DefaultContainerPadding,
					BackgroundColor = menuTheme.BackgroundColor,
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit
				};

				subPanel.BoundsChanged += (s, e) =>
				{
					Console.WriteLine();
				};

				foreach (var drawable in ApplicationController.Instance.DragDropData.View3DWidget.InteractionLayer.Drawables)
				{
					var row = new SettingsRow(drawable.Title, drawable.Description, theme);
					subPanel.AddChild(row);

					var toggleSwitch = new RoundedToggleSwitch(theme)
					{
						Checked = drawable.Enabled
					};
					toggleSwitch.CheckedStateChanged += (s, e) =>
					{
						drawable.Enabled = toggleSwitch.Checked;
					};
					row.AddChild(toggleSwitch);
				}

				foreach (var drawable in ApplicationController.Instance.DragDropData.View3DWidget.InteractionLayer.ItemDrawables)
				{
					var row = new SettingsRow(drawable.Title, drawable.Description, theme);
					subPanel.AddChild(row);

					var toggleSwitch = new RoundedToggleSwitch(theme)
					{
						Checked = drawable.Enabled
					};
					toggleSwitch.CheckedStateChanged += (s, e) =>
					{
						drawable.Enabled = toggleSwitch.Checked;
					};
					row.AddChild(toggleSwitch);
				}

				subPanel.Width = 400;

				return subPanel;
			};
		}
	}
}
