using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl
{
	public class ThemeSelectorWindow : SystemWindow
	{
		GuiWidget currentColorTheme;
		Button closeButton;
		Button saveButton;
		TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		public ThemeSelectorWindow()
			:base(400, 200)
		{
			Title = LocalizedString.Get("Theme Selector").Localize();

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			//Create Header
			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			//Create 'Theme Change' label and add it to Header
			string themeChangeHeader = LocalizedString.Get("Select Theme".Localize());
			TextWidget elementHeader = new TextWidget(string.Format("{0}:", themeChangeHeader), pointSize: 14);
			elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			elementHeader.HAnchor = HAnchor.ParentLeftRight;
			elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

			//Add label to header 
			headerRow.AddChild(elementHeader);
			//Add Header
			topToBottom.AddChild(headerRow);


			//Theme Selector widget container and add themeselector
			FlowLayoutWidget themeChangeWidgetContainer = new FlowLayoutWidget();
			themeChangeWidgetContainer.Padding = new BorderDouble(3);
			themeChangeWidgetContainer.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			GuiWidget currentColorTheme = new GuiWidget();
			currentColorTheme.HAnchor = HAnchor.ParentLeftRight;
			currentColorTheme.VAnchor = VAnchor.ParentBottomTop;
			currentColorTheme.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;


			ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget(colorToChangeTo: currentColorTheme);
			themeSelector.Margin = new BorderDouble(right: 5);
			themeChangeWidgetContainer.AddChild(themeSelector);


			//Create CurrentColorTheme GUI Widgets
			GuiWidget currentColorThemeBorder = new GuiWidget();
			currentColorThemeBorder.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
			currentColorThemeBorder.Margin = new BorderDouble (top: 2, bottom: 2);
			currentColorThemeBorder.Padding = new BorderDouble(4);
			currentColorThemeBorder.BackgroundColor = RGBA_Bytes.White;




			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			FlowLayoutWidget currentColorLabelContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			currentColorLabelContainer.HAnchor = HAnchor.ParentLeftRight;
			currentColorLabelContainer.Margin = new BorderDouble(0, 3, 0, 0);
			currentColorLabelContainer.Padding = new BorderDouble(0, 3, 0, 3);

			string currentColorThemeLabelText = LocalizedString.Get("Currently Selected Theme".Localize());
			TextWidget currentColorThemeHeader = new TextWidget(string.Format("{0}:", currentColorThemeLabelText), pointSize: 14);
			currentColorThemeHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			currentColorThemeHeader.HAnchor = HAnchor.ParentLeftRight;
			currentColorThemeHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;
			currentColorLabelContainer.AddChild(currentColorThemeHeader);


			//
			FlowLayoutWidget currentColorContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			currentColorContainer.HAnchor = HAnchor.ParentLeftRight;
			currentColorContainer.VAnchor = VAnchor.ParentBottomTop;
			currentColorContainer.Padding = new BorderDouble(3);
			currentColorContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			currentColorContainer.AddChild(currentColorThemeBorder);
			currentColorThemeBorder.AddChild(currentColorTheme);

		
			presetsFormContainer.AddChild(themeChangeWidgetContainer);
			topToBottom.AddChild(presetsFormContainer);
			topToBottom.AddChild(currentColorLabelContainer);

			topToBottom.AddChild(currentColorContainer);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0, 3);

			closeButton = textImageButtonFactory.Generate("Close");
			closeButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle((state) =>
				{
						Close();
				});
			};

			saveButton = textImageButtonFactory.Generate("Save");
			saveButton.Click += (sender, e) =>
			{
					UserSettings.Instance.set("ActiveThemeIndex",((GuiWidget)sender).Name);
					ActiveTheme.Instance.LoadThemeSettings(int.Parse(((GuiWidget)sender).Name));//GUIWIDGET
			};


			buttonRow.AddChild(saveButton);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(closeButton);
			topToBottom.AddChild(buttonRow);
			AddChild(topToBottom);


			ShowAsSystemWindow();
		}
	}
}

