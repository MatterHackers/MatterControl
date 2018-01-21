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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsOverflowMenu : OverflowMenuButton
	{
		public SliceSettingsOverflowMenu(PrinterConfig printer, ThemeConfig theme)
			: base(theme)
		{
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.AlignToRightEdge = true;
			this.Name = "Slice Settings Overflow Menu";

			var popupMenu = new PopupMenu(ApplicationController.Instance.Theme);

			popupMenu.CreateMenuItem("View Just My Settings".Localize()).Click += (s, e) =>
			{
				this.TabView.FilterToOverrides();
			};

			popupMenu.CreateHorizontalLine();

			popupMenu.CreateBoolMenuItem(
				"Show Help".Localize(),
				() => ApplicationController.Instance.ShowHelpControls,
				(value) => ApplicationController.Instance.ShowHelpControls = value);

			this.PopupContent = popupMenu;
		}

		// On load walk back to the first ancestor with background colors and copy
		public override void OnLoad(EventArgs args)
		{
			var firstBackgroundColor = this.Parents<GuiWidget>().Where(p => p.BackgroundColor.Alpha0To1 == 1).FirstOrDefault()?.BackgroundColor;
			this.BackgroundColor = firstBackgroundColor ?? Color.Transparent;

			base.OnLoad(args);
		}

		public SliceSettingsTabView TabView { get; internal set; }
	}
}