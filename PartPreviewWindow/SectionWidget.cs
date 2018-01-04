using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SectionWidget : FlowLayoutWidget
	{
		public SectionWidget(string sectionTitle, Color textColor, GuiWidget sectionContent, GuiWidget rightAlignedContent = null, int headingPointSize = -1)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			var theme = ApplicationController.Instance.Theme;

			if (!string.IsNullOrEmpty(sectionTitle))
			{
				// Add heading
				var pointSize = (headingPointSize) == -1 ? theme.H1PointSize : headingPointSize;
				var textWidget = new TextWidget(sectionTitle, pointSize: pointSize, textColor: textColor, bold: false)
				{
					Margin = new BorderDouble(0, 3, 0, 6)
				};

				if (rightAlignedContent == null)
				{
					this.AddChild(textWidget);
				}
				else
				{
					var headingRow = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch
					};
					headingRow.AddChild(textWidget);
					headingRow.AddChild(new HorizontalSpacer());
					headingRow.AddChild(rightAlignedContent);
					this.AddChild(headingRow);
				}

				// Add heading separator
				this.AddChild(new HorizontalLine(25)
				{
					Margin = new BorderDouble(0)
				});
			}

			// Force padding and add content widget
			sectionContent.HAnchor = HAnchor.Stretch;
			sectionContent.BackgroundColor = ApplicationController.Instance.Theme.MinimalShade;
			this.AddChild(sectionContent);
		}
	}
}