using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	/// <summary>
	/// A container control having a header and a content panel, with optional collapse behavior and right aligned widget
	/// </summary>
	public class SectionWidget : FlowLayoutWidget
	{
		private Color borderColor;

		public SectionWidget(string sectionTitle, GuiWidget sectionContent, ThemeConfig theme, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Border = new BorderDouble(bottom: 1);

			borderColor = new Color(theme.Colors.SecondaryTextColor, 50);

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
						Checked = expanded,
					};
					checkbox.CheckedStateChanged += (s, e) =>
					{
						ContentPanel.Visible = checkbox.Checked;
						this.BorderColor = (checkbox.Checked) ? Color.Transparent : borderColor;
					};

					this.BorderColor = BorderColor = (expanded) ? Color.Transparent : borderColor;

					heading = checkbox;
				}
				else
				{
					heading = new TextWidget(sectionTitle, pointSize: pointSize, textColor: theme.Colors.PrimaryTextColor);
				}
				heading.Padding = new BorderDouble(0, 5, 0, 6);

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
			}

			sectionContent.Visible = expanded;

			this.SetContentWidget(sectionContent);
		}

		public GuiWidget ContentPanel { get; private set; }

		public void SetContentWidget(GuiWidget guiWidget)
		{
			// Close old child
			this.ContentPanel?.Close();

			// Apply default rules for panel widget
			guiWidget.HAnchor = HAnchor.Stretch;
			guiWidget.VAnchor = VAnchor.Fit;
			//guiWidget.BackgroundColor = ApplicationController.Instance.Theme.MinimalShade;
			guiWidget.BorderColor = borderColor;
			guiWidget.Border = new BorderDouble(bottom: 1);

			// Set
			this.AddChild(guiWidget);

			// Store
			this.ContentPanel = guiWidget;
		}
	}
}