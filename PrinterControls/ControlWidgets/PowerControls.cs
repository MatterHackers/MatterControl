/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class PowerControls : ControlWidgetBase
	{
		private event EventHandler unregisterEvents;
		
		private CheckBox atxPowertoggleSwitch;

		public PowerControls()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(this.UpdateControlVisibility, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.AtxPowerStateChanged.RegisterEvent(this.UpdatePowerSwitch, ref unregisterEvents);

			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.ParentBottomTop;
		}

		private void UpdateControlVisibility(object sender, EventArgs args)
		{
			this.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_power_control);
			this.SetEnableLevel(PrinterConnectionAndCommunication.Instance.PrinterIsConnected ? EnableLevel.Enabled : EnableLevel.Disabled);
		}

		private void UpdatePowerSwitch(object sender, EventArgs args)
		{
			this.atxPowertoggleSwitch.Checked = PrinterConnectionAndCommunication.Instance.AtxPowerEnabled;
		}

		protected override void AddChildElements()
		{
			AltGroupBox fanControlsGroupBox = new AltGroupBox(new TextWidget("ATX Power Control".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
			fanControlsGroupBox.Margin = new BorderDouble(0);
			fanControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			fanControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			this.AddChild(fanControlsGroupBox);

			atxPowertoggleSwitch = ImageButtonFactory.CreateToggleSwitch(false);
			atxPowertoggleSwitch.Margin = new BorderDouble(6, 0, 6, 6);
			atxPowertoggleSwitch.CheckedStateChanged += (sender, e) =>
			{
				PrinterConnectionAndCommunication.Instance.AtxPowerEnabled = atxPowertoggleSwitch.Checked;
			};

			FlowLayoutWidget paddingContainer = new FlowLayoutWidget();
			paddingContainer.Padding = new BorderDouble(3, 5, 3, 0);
			{
				paddingContainer.AddChild(atxPowertoggleSwitch);
			}
			fanControlsGroupBox.AddChild(paddingContainer);

			UpdateControlVisibility(null, null);
		}

		private void SetDisplayAttributes()
		{
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;

			this.textImageButtonFactory.FixedWidth = 38 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.FixedHeight = 20 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.fontSize = 10;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}
	}
}