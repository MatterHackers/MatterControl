using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SectionWidget : FlowLayoutWidget
	{
		public SectionWidget(string sectionTitle, Color textColor, GuiWidget sectionContent)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			// Add heading
			this.AddChild(
				new TextWidget(sectionTitle, textColor: textColor)
				{
					Margin = new BorderDouble(0, 3, 0, 6)
				});

			// Add heading separator
			this.AddChild(new HorizontalLine(25)
			{
				Margin = new BorderDouble(0)
			});

			// Force padding and add content widget
			sectionContent.Padding = 8;
			sectionContent.HAnchor = HAnchor.Stretch;
			this.AddChild(sectionContent);
		}
	}
}