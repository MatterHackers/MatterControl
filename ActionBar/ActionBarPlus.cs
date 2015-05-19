using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.PrintQueue;
using System;

namespace MatterHackers.MatterControl
{
	public class ActionBarPlus : FlowLayoutWidget
	{
		private QueueDataView queueDataView;

		public ActionBarPlus(QueueDataView queueDataView)
			: base(FlowDirection.TopToBottom)
		{
			this.queueDataView = queueDataView;
			this.Create();
		}

		private event EventHandler unregisterEvents;

		public void Create()
		{
			// Set Display Attributes
			this.HAnchor = HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			// Add Child Elements
			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Responsive)
			{
				this.AddChild(new ActionBar.PrinterActionRow());
			}
			this.AddChild(new PrintStatusRow(queueDataView));
			this.Padding = new BorderDouble(bottom: 6);

			// Add Handlers
			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}
	}

	internal class MessageActionRow : ActionRowBase
	{
		protected override void AddChildElements()
		{
			if (HelpTextWidget.Instance.Parent != null)
			{
				HelpTextWidget.Instance.Parent.RemoveChild(HelpTextWidget.Instance);
			}

			this.AddChild(HelpTextWidget.Instance);
		}

		protected override void Initialize()
		{
			this.Margin = new BorderDouble(0, 3, 0, 0);
		}
	}
}