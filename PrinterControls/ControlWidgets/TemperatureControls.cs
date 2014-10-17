using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;

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

			TextWidget subheader = new TextWidget("Temporarily override target temperature", pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor);
			subheader.Margin = new BorderDouble(bottom:6);
			mainContainer.AddChild(subheader);

            temperatureGroupBox.AddChild(mainContainer);
            RGBA_Bytes separatorLineColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);

            int numberOfHeatedExtruders = 1;
            if (ActiveSliceSettings.Instance.GetActiveValue("extruders_share_temperature") == "0")
            {
                numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
            }

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

            if (ActiveSliceSettings.Instance.HasHeatedBed())
            {
                mainContainer.AddChild(BedTemperatureControlWidget);
            }

            this.AddChild(temperatureGroupBox);
        }
    }
}
