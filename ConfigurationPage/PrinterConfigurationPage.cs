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
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class PrinterConfigurationScrollWidget : ScrollableWidget
	{
		public PrinterConfigurationScrollWidget()
			: base(true)
		{
			ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			AnchorAll();
			AddChild(new PrinterConfigurationWidget());
		}
	}

	public class PrinterConfigurationWidget : GuiWidget
	{
		private readonly int TallButtonHeight = 25;

		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		public PrinterConfigurationWidget()
		{
			SetDisplayAttributes();

			HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			VAnchor = Agg.UI.VAnchor.FitToChildren;

			FlowLayoutWidget mainLayoutContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainLayoutContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			mainLayoutContainer.VAnchor = Agg.UI.VAnchor.FitToChildren;
			mainLayoutContainer.Padding = new BorderDouble(top: 10);

			if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				mainLayoutContainer.AddChild(new CalibrationSettingsWidget());
			}

			mainLayoutContainer.AddChild(new HardwareSettingsWidget());

			CloudSettingsWidget cloudGroupbox = new CloudSettingsWidget();
			mainLayoutContainer.AddChild(cloudGroupbox);

			ApplicationSettingsWidget applicationGroupbox = new ApplicationSettingsWidget();
			mainLayoutContainer.AddChild(applicationGroupbox);

			AddChild(mainLayoutContainer);

			AddHandlers();
			//SetVisibleControls();
		}

		private void AddThemeControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			DisableableWidget container = new DisableableWidget();

			AltGroupBox themeControlsGroupBox = new AltGroupBox("Theme Settings".Localize());
			themeControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			themeControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			themeControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			themeControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
			themeControlsGroupBox.Height = 78;

			FlowLayoutWidget colorSelectorContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			colorSelectorContainer.HAnchor = HAnchor.ParentLeftRight;

			GuiWidget currentColorThemeBorder = new GuiWidget();
			currentColorThemeBorder.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
			currentColorThemeBorder.Margin = new BorderDouble(top: 2, bottom: 2);
			currentColorThemeBorder.Padding = new BorderDouble(4);
			currentColorThemeBorder.BackgroundColor = RGBA_Bytes.White;

			GuiWidget currentColorTheme = new GuiWidget();
			currentColorTheme.HAnchor = HAnchor.ParentLeftRight;
			currentColorTheme.VAnchor = VAnchor.ParentBottomTop;
			currentColorTheme.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget(colorToChangeTo: currentColorTheme);
			themeSelector.Margin = new BorderDouble(right: 5);

			themeControlsGroupBox.AddChild(colorSelectorContainer);
			colorSelectorContainer.AddChild(themeSelector);
			colorSelectorContainer.AddChild(currentColorThemeBorder);
			currentColorThemeBorder.AddChild(currentColorTheme);
			container.AddChild(themeControlsGroupBox);
			controlsTopToBottomLayout.AddChild(container);
		}

		private void RestartApplication()
		{
			UiThread.RunOnIdle(() =>
			{
				//horrible hack - to be replaced
				GuiWidget parent = this;
				while (parent as MatterControlApplication == null)
				{
					parent = parent.Parent;
				}
				MatterControlApplication app = parent as MatterControlApplication;
				app.RestartOnClose = true;
				app.Close();
			});
		}

		private void LanguageDropList_SelectionChanged(object sender, EventArgs e)
		{
			string languageCode = ((Agg.UI.DropDownList)sender).SelectedLabel;
			if (languageCode != UserSettings.Instance.get("Language"))
			{
				UserSettings.Instance.set("Language", languageCode);
			}
		}

		public void AddReleaseOptions(FlowLayoutWidget controlsTopToBottom)
		{
			AltGroupBox releaseOptionsGroupBox = new AltGroupBox("Update Feed".Localize());

			releaseOptionsGroupBox.Margin = new BorderDouble(0);
			releaseOptionsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			releaseOptionsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			releaseOptionsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			releaseOptionsGroupBox.VAnchor = Agg.UI.VAnchor.ParentTop;
			releaseOptionsGroupBox.Height = 68;

			FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
			controlsContainer.HAnchor |= HAnchor.ParentCenter;

			var releaseOptionsDropList = new DropDownList("Development");
			releaseOptionsDropList.Margin = new BorderDouble(0, 3);

			MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Release", "release");
			releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Pre-Release", "pre-release");
			preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Development", "development");
			developmentDropDownItem.Selected += new EventHandler(FixTabDot);

			releaseOptionsDropList.MinimumSize = new Vector2(releaseOptionsDropList.LocalBounds.Width, releaseOptionsDropList.LocalBounds.Height);

			List<string> acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };
			string currentUpdateFeedType = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);

			if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, "release");
			}

			releaseOptionsDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);

			releaseOptionsDropList.SelectionChanged += new EventHandler(ReleaseOptionsDropList_SelectionChanged);

			controlsContainer.AddChild(releaseOptionsDropList);
			releaseOptionsGroupBox.AddChild(controlsContainer);
			controlsTopToBottom.AddChild(releaseOptionsGroupBox);
		}

		private void FixTabDot(object sender, EventArgs e)
		{
			UpdateControlData.Instance.CheckForUpdateUserRequested();
		}

		private void ReleaseOptionsDropList_SelectionChanged(object sender, EventArgs e)
		{
			string releaseCode = ((DropDownList)sender).SelectedValue;
			if (releaseCode != UserSettings.Instance.get(UserSettingsKey.UpdateFeedType))
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, releaseCode);
			}
		}

		private static GuiWidget CreateSeparatorLine()
		{
			GuiWidget topLine = new GuiWidget(10, 1);
			topLine.Margin = new BorderDouble(0, 5);
			topLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			topLine.BackgroundColor = RGBA_Bytes.White;
			return topLine;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			base.OnClosed(e);
		}

		private void SetDisplayAttributes()
		{
			//this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;
			this.textImageButtonFactory.fontSize = 11;

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.linkButtonFactory.fontSize = 11;
		}

		private EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			this.Invalidate();
		}
	}
}