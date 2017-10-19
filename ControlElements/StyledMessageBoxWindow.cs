/*
Copyright (c) 2017, Lars Brubaker, Kevin Pope, John Lewin
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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class StyledMessageBox : SystemWindow
	{
		private string unwrappedMessage;
		private TextWidget messageContainer;
		private FlowLayoutWidget middleRowContainer;

		private Action<bool> responseCallback;

		public enum MessageType { OK, YES_NO };
		private double extraTextScaling = 1;

		public static void ShowMessageBox(Action<bool> callback, string message, string caption, MessageType messageType = MessageType.OK, string yesOk = "", string noCancel = "")
		{
			ShowMessageBox(callback, message, caption, null, messageType, yesOk, noCancel);
		}

		public static void ShowMessageBox(Action<bool> callback, string message, string caption, GuiWidget[] extraWidgetsToAdd, MessageType messageType, string yesOk = "", string noCancel = "")
		{
			StyledMessageBox messageBox = new StyledMessageBox(callback, message, caption, messageType, extraWidgetsToAdd, 400, 300, yesOk, noCancel);
			messageBox.CenterInParent = true;
			messageBox.ShowAsSystemWindow();
		}

		public StyledMessageBox(Action<bool> callback, string message, string windowTitle, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height, string yesOk, string noCancel)
			: base(width, height)
		{
			if (UserSettings.Instance.IsTouchScreen)
			{
				extraTextScaling = 1.33333;
			}

			if (yesOk == "")
			{
				if (messageType == MessageType.OK)
				{
					yesOk = "Ok".Localize();
				}
				else
				{
					yesOk = "Yes".Localize();
				}
			}
			if (noCancel == "")
			{
				noCancel = "No".Localize();
			}

			responseCallback = callback;
			unwrappedMessage = message;
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			if (UserSettings.Instance.IsTouchScreen)
			{
				topToBottom.Padding = new BorderDouble(12, 12, 13, 8);
			}
			else
			{
				topToBottom.Padding = new BorderDouble(3, 0, 3, 5);
			}

			// Creates Header
			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Creates Text and adds into header
			{
				TextWidget elementHeader = new TextWidget(windowTitle, pointSize: 14 * extraTextScaling);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.Stretch;
				elementHeader.VAnchor = Agg.UI.VAnchor.Bottom;

				headerRow.AddChild(elementHeader);
				topToBottom.AddChild(headerRow);
			}

			// Creates container in the middle of window
			middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.Stretch;
				middleRowContainer.VAnchor = VAnchor.Stretch;
				// normally the padding for the middle container should be just (5) all around. The has extra top space
				middleRowContainer.Padding = new BorderDouble(5, 5, 5, 15);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			messageContainer = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 12 * extraTextScaling);
			messageContainer.AutoExpandBoundsToText = true;
			messageContainer.HAnchor = Agg.UI.HAnchor.Left;
			middleRowContainer.AddChild(messageContainer);

			if (extraWidgetsToAdd != null)
			{
				foreach (GuiWidget widget in extraWidgetsToAdd)
				{
					middleRowContainer.AddChild(widget);
				}
			}

			topToBottom.AddChild(middleRowContainer);

			//Creates button container on the bottom of window
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.Stretch;
				buttonRow.Padding = new BorderDouble(0, 3);
			}

			var textImageButtonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			switch (messageType)
			{
				case MessageType.YES_NO:
					{
						Title = "MatterControl - " + "Please Confirm".Localize();
						Button yesButton = textImageButtonFactory.Generate(yesOk);
						yesButton.Name = "Yes Button";
						yesButton.Click += okButton_Click;
						yesButton.Cursor = Cursors.Hand;
						buttonRow.AddChild(yesButton);

						buttonRow.AddChild(new HorizontalSpacer());

						Button noButton = textImageButtonFactory.Generate(noCancel);
						noButton.Name = "No Button";
						noButton.Click += noButton_Click;
						noButton.Cursor = Cursors.Hand;
						buttonRow.AddChild(noButton);
					}
					break;

				case MessageType.OK:
					{
						Title = "MatterControl - " + "Alert".Localize();
						Button okButton = textImageButtonFactory.Generate(yesOk);
						okButton.Name = "Ok Button";
						okButton.Cursor = Cursors.Hand;
						okButton.Click += okButton_Click;
						buttonRow.AddChild(okButton);
					}
					break;

				default:
					throw new NotImplementedException();
			}

			topToBottom.AddChild(buttonRow);
			this.AddChild(topToBottom);

			IsModal = true;
			AdjustTextWrap();
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			AdjustTextWrap();
			base.OnBoundsChanged(e);
		}

		private void AdjustTextWrap()
		{
			if (messageContainer != null)
			{
				double wrappingSize = middleRowContainer.Width - (middleRowContainer.Padding.Width + messageContainer.Margin.Width);
				if (wrappingSize > 0)
				{
					EnglishTextWrapping wrapper = new EnglishTextWrapping(12 * extraTextScaling * GuiWidget.DeviceScale);
					string wrappedMessage = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
					messageContainer.Text = wrappedMessage;
				}
			}
		}

		private void noButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(Close);
			if (responseCallback != null)
			{
				responseCallback(false);
			}
		}

		private void okButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(Close);
			if (responseCallback != null)
			{
				responseCallback(true);
			}
		}
	}
}