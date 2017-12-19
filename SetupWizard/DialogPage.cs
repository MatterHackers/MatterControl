/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class DialogPage : GuiWidget
	{
		private FlowLayoutWidget headerRow;
		protected FlowLayoutWidget contentRow;
		private FlowLayoutWidget footerRow;

		private WrappedTextWidget headerLabel;
		private Button cancelButton;

		public Vector2 WindowSize { get; set; }

		protected TextImageButtonFactory textImageButtonFactory { get; } = ApplicationController.Instance.Theme.WizardButtons;
		protected TextImageButtonFactory whiteImageButtonFactory { get; } = ApplicationController.Instance.Theme.WhiteButtonFactory;
		protected LinkButtonFactory linkButtonFactory = ApplicationController.Instance.Theme.LinkButtonFactory;

		protected double labelFontSize = 12 * GuiWidget.DeviceScale;
		protected double errorFontSize = 10 * GuiWidget.DeviceScale;

		private GuiWidget mainContainer;

		public DialogPage(string cancelButtonText = null)
		{
			if (cancelButtonText == null)
			{
				cancelButtonText = "Cancel".Localize();
			}

			if (!UserSettings.Instance.IsTouchScreen)
			{
				this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
			}

			this.AnchorAll();

			cancelButton = textImageButtonFactory.Generate(cancelButtonText);
			cancelButton.Name = "Cancel Wizard Button";

			// Create the main container
			mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(12, 12, 12, 0),
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};
			mainContainer.AnchorAll();

			// Create the header row for the widget
			headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = new BorderDouble(0, 3, 0, 0),
				Padding = new BorderDouble(0, 12),
				HAnchor = HAnchor.Stretch
			};

			headerLabel = new WrappedTextWidget("Setup Wizard".Localize(), pointSize: 24, textColor: ActiveTheme.Instance.SecondaryAccentColor)
			{
				HAnchor = HAnchor.Stretch
			};
			headerRow.AddChild(headerLabel);

			// Create the main control container
			contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(10),
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			// Create the footer (button) container
			footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Left | HAnchor.Right,
				Margin = new BorderDouble(0, 6),
				Padding = new BorderDouble(top: 4, bottom: 2)
			};

			mainContainer.AddChild(headerRow);
			mainContainer.AddChild(contentRow);
			mainContainer.AddChild(footerRow);

#if __ANDROID__
			if (false)
#endif 
			{
				mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
				headerRow.Padding = new BorderDouble(0, 3, 0, 3);

				headerLabel.TextWidget.PointSize = 14;
				headerLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				contentRow.Padding = new BorderDouble(5);
				footerRow.Margin = new BorderDouble(0, 3);
			}

			this.AddChild(mainContainer);
		}

		public DialogWindow WizardWindow { get; set; }

		public string WindowTitle { get; set; }

		public bool AlwaysOnTopOfMain { get; set; } = true;

		public string HeaderText
		{
			get => headerLabel.Text;
			set => headerLabel.Text = value;
		}

		public void AddPageAction(Button button)
		{
			button.Margin = new BorderDouble(right: ApplicationController.Instance.Theme.ButtonSpacing.Left);
			footerRow.AddChild(button);
		}

		protected void SetCancelButtonName(string newName)
		{
			cancelButton.Name = newName;
		}

		protected void HideCancelButton()
		{
			cancelButton.Visible = false;
		}

		public override void OnLoad(EventArgs args)
		{
			// Add 'Close' event listener after derived types have had a chance to register event listeners
			cancelButton.Click += (s, e) =>
			{
				this.OnCancel(out bool abortCancel);

				if (!abortCancel)
				{
					UiThread.RunOnIdle(() => WizardWindow?.Close());
				}
			};

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			base.OnLoad(args);
		}

		protected virtual void OnCancel(out bool abortCancel)
		{
			abortCancel = false;
		}

		public virtual void PageIsBecomingActive()
		{
		}

		public virtual void PageIsBecomingInactive()
		{
		}
	}
}