/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class PrinterConfigurationScrollWidget : ScrollableWidget, IIgnoredPopupChild
	{
		public PrinterConfigurationScrollWidget()
			: base(true)
		{
			this.ScrollArea.HAnchor |= HAnchor.ParentLeftRight;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			this.AnchorAll();
			this.AddChild(new PrinterConfigurationWidget(ApplicationController.Instance.Theme.BreadCrumbButtonFactorySmallMargins));
		}
	}

	public class PrinterConfigurationWidget : FlowLayoutWidget
	{
		public PrinterConfigurationWidget(TextImageButtonFactory buttonFactory)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.FitToChildren;
			this.Padding = new BorderDouble(top: 10);

			if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				this.AddChild(new CalibrationSettingsWidget(buttonFactory));
			}

			this.AddChild(new CloudSettingsWidget(buttonFactory));
			this.AddChild(new ApplicationSettingsWidget(buttonFactory));
		}
	}
}