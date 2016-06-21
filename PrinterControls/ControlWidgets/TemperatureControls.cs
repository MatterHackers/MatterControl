using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class TemperatureControls : ControlWidgetBase
	{
		public List<DisableableWidget> ExtruderWidgetContainers = new List<DisableableWidget>();
		public DisableableWidget BedTemperatureControlWidget;

		protected override void AddChildElements()
		{
			AltGroupBox temperatureGroupBox = new AltGroupBox(new TextWidget("Temperature".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
			temperatureGroupBox.Margin = new BorderDouble(0);

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			mainContainer.HAnchor = HAnchor.ParentLeftRight;
			mainContainer.Margin = new BorderDouble(left: 0);

			temperatureGroupBox.AddChild(mainContainer);
			RGBA_Bytes separatorLineColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);

			int numberOfHeatedExtruders = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
			if (numberOfHeatedExtruders > 1)
			{
				for (int i = 0; i < numberOfHeatedExtruders; i++)
				{
					DisableableWidget extruderTemperatureControlWidget = new DisableableWidget();
					extruderTemperatureControlWidget.AddChild(new ExtruderTemperatureControlWidget(i));
					mainContainer.AddChild(extruderTemperatureControlWidget);
					mainContainer.AddChild(new HorizontalLine(separatorLineColor));
					ExtruderWidgetContainers.Add(extruderTemperatureControlWidget);
				}
			}
			else
			{
				DisableableWidget extruderTemperatureControlWidget = new DisableableWidget();
				extruderTemperatureControlWidget.AddChild(new ExtruderTemperatureControlWidget());
				mainContainer.AddChild(extruderTemperatureControlWidget);
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
				ExtruderWidgetContainers.Add(extruderTemperatureControlWidget);
			}

			BedTemperatureControlWidget = new DisableableWidget();
			BedTemperatureControlWidget.AddChild(new BedTemperatureControlWidget());

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				mainContainer.AddChild(BedTemperatureControlWidget);
			}

			this.AddChild(temperatureGroupBox);
		}
	}
}