using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SectionWidget : FlowLayoutWidget
	{
		private GuiWidget contentWidget;

		public SectionWidget(string sectionTitle, Color textColor, GuiWidget sectionContent, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			var theme = ApplicationController.Instance.Theme;

			if (!string.IsNullOrEmpty(sectionTitle))
			{
				// Add heading
				var pointSize = (headingPointSize) == -1 ? theme.H1PointSize : headingPointSize;

				GuiWidget heading;

				if (expandingContent)
				{
					var checkbox = new ExpandCheckboxButton(sectionTitle, pointSize: pointSize)
					{
						HAnchor = HAnchor.Stretch,
						Checked = expanded
					};
					checkbox.CheckedStateChanged += (s, e) =>
					{
						contentWidget.Visible = checkbox.Checked;
					};

					heading = checkbox;
				}
				else
				{
					heading = new TextWidget(sectionTitle, pointSize: pointSize, textColor: textColor);
				}
				heading.Margin = new BorderDouble(0, 3, 0, 6);

				if (rightAlignedContent == null)
				{
					this.AddChild(heading);
				}
				else
				{
					var headingRow = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch
					};
					headingRow.AddChild(heading);
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

			sectionContent.Visible = expanded;

			this.SetContentWidget(sectionContent);
		}

		public void SetContentWidget(GuiWidget guiWidget)
		{
			contentWidget?.Close();

			contentWidget = guiWidget;
			contentWidget.HAnchor = HAnchor.Stretch;
			contentWidget.VAnchor = VAnchor.Fit;
			contentWidget.BackgroundColor = ApplicationController.Instance.Theme.MinimalShade;

			this.AddChild(contentWidget);
		}
	}
}