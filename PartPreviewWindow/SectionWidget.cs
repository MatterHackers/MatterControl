using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.CustomWidgets
{
	/// <summary>
	/// A container control having a header and a content panel, with optional collapse behavior and right aligned widget. Additionally support persistent expansion state via serializationKey
	/// </summary>
	public class SectionWidget : FlowLayoutWidget, IIgnoredPopupChild
	{
		private ExpandCheckboxButton checkbox;

		public SectionWidget(string sectionTitle, GuiWidget sectionContent, ThemeConfig theme, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true, string serializationKey = null, bool defaultExpansion = false)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Border = new BorderDouble(bottom: 1);
			this.SeperatorColor = new Color(theme.Colors.SecondaryTextColor, 50);

			if (!string.IsNullOrEmpty(sectionTitle))
			{
				// Add heading
				var pointSize = (headingPointSize) == -1 ? theme.DefaultFontSize : headingPointSize;

				if (serializationKey != null)
				{
					string dbValue = UserSettings.Instance.get(serializationKey);
					expanded = dbValue == "1" || (dbValue == null && defaultExpansion);
				}

				checkbox = new ExpandCheckboxButton(sectionTitle, pointSize: pointSize, expandable: expandingContent)
				{
					HAnchor = HAnchor.Stretch,
					Checked = expanded,
					Padding = new BorderDouble(0, 5, 0, 6)
				};
				checkbox.CheckedStateChanged += (s, e) =>
				{
					ContentPanel.Visible = checkbox.Checked;
					// TODO: Remove this Height = 10 and figure out why the layout engine is not sizing these correctly without this.
					ContentPanel.Height = 10;
				};

				if (serializationKey != null)
				{
					checkbox.CheckedStateChanged += (s, e) =>
					{
						UserSettings.Instance.set(serializationKey, checkbox.Checked ? "1" : "0");
					};
				}

				if (rightAlignedContent == null)
				{
					this.AddChild(checkbox);
				}
				else
				{
					var headingRow = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch
					};
					headingRow.AddChild(checkbox);
					headingRow.AddChild(new HorizontalSpacer());
					headingRow.AddChild(rightAlignedContent);
					this.AddChild(headingRow);
				}
			}

			sectionContent.Visible = expanded;

			this.SetContentWidget(sectionContent);
		}

		public ICheckbox Checkbox => checkbox;

		private Color _seperatorColor;
		public Color SeperatorColor
		{
			get => _seperatorColor;
			set
			{
				if (value != _seperatorColor)
				{
					_seperatorColor = value;
					this.BorderColor = _seperatorColor;
				}
			}
		}

		public GuiWidget ContentPanel { get; private set; }

		public void SetContentWidget(GuiWidget guiWidget)
		{
			// Close old child
			this.ContentPanel?.Close();

			// Apply default rules for panel widget
			guiWidget.HAnchor = HAnchor.Stretch;
			guiWidget.VAnchor = VAnchor.Fit;

			// Set
			this.AddChild(guiWidget);

			// Store
			this.ContentPanel = guiWidget;
		}

		public int BorderRadius { get; set; } = 0;

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (this.BorderRadius > 0)
			{
				var rect = new RoundedRect(this.LocalBounds, this.BorderRadius);
				graphics2D.Render(rect, this.BackgroundColor);
			}
			else
			{
				base.OnDrawBackground(graphics2D);
			}
		}
	}
}