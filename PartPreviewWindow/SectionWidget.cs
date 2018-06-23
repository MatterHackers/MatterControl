using System;
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
		protected ExpandCheckboxButton checkbox;
		private bool setContentVAnchor;

		public SectionWidget(string sectionTitle, GuiWidget sectionContent, ThemeConfig theme, GuiWidget rightAlignedContent = null, int headingPointSize = -1, bool expandingContent = true, bool expanded = true, string serializationKey = null, bool defaultExpansion = false, bool setContentVAnchor = true)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Border = new BorderDouble(bottom: 1);
			this.BorderColor = theme.GetBorderColor(50);

			this.setContentVAnchor = setContentVAnchor;

			if (!string.IsNullOrEmpty(sectionTitle))
			{
				// Add heading
				var pointSize = (headingPointSize) == -1 ? theme.DefaultFontSize : headingPointSize;

				if (serializationKey != null)
				{
					string dbValue = UserSettings.Instance.get(serializationKey);
					expanded = dbValue == "1" || (dbValue == null && defaultExpansion);
				}

				checkbox = new ExpandCheckboxButton(sectionTitle, theme, pointSize: pointSize, expandable: expandingContent)
				{
					HAnchor = HAnchor.Stretch,
					Checked = expanded,
					Padding = 0
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

		public GuiWidget ContentPanel { get; private set; }

		public void SetContentWidget(GuiWidget guiWidget)
		{
			// Close old child
			this.ContentPanel?.Close();

			// Apply default rules for panel widget
			guiWidget.HAnchor = HAnchor.Stretch;

			if (setContentVAnchor)
			{
				guiWidget.VAnchor = VAnchor.Fit;
			}

			// Set
			this.AddChild(guiWidget);

			// Store
			this.ContentPanel = guiWidget;
			this.ContentPanel.BorderColor = Color.Transparent;
		}

		public int BorderRadius { get; set; } = 0;

		public bool ExpandableWhenDisabled { get; set; }

		public override bool Enabled
		{
			get => (this.ExpandableWhenDisabled) ? true: base.Enabled;
			set
			{
				if (this.ExpandableWhenDisabled)
				{
					this.ContentPanel.Enabled = value;
				}
				else
				{
					base.Enabled = value;
				}
			}
		}

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