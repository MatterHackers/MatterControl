﻿/*
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
using Markdig.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public static class StyledMessageBox
	{
		public enum MessageType
		{
			OK,
			YES_NO,
			YES_NO_WITHOUT_HIGHLIGHT
		}

		public static void ShowMessageBox(string message,
			string caption,
			MessageType messageType = MessageType.OK,
			string yesOk = "",
			string noCancel = "",
			bool useMarkdown = false,
			int width = 400,
			int height = 300)
		{
			ShowMessageBox(null, message, caption, null, messageType, yesOk, noCancel, useMarkdown, 0, width, height);
		}

		public static void ShowMessageBox(Action<bool> callback,
			string message,
			string caption,
			MessageType messageType = MessageType.OK,
			string yesOk = "",
			string noCancel = "",
			bool useMarkdown = false,
			int instanceIndex = 0,
			int width = 400,
			int height = 300)
		{
			ShowMessageBox(callback, message, caption, null, messageType, yesOk, noCancel, useMarkdown, instanceIndex, width, height);
		}

		public static void ShowMessageBox(Action<bool> callback,
			string message,
			string caption,
			GuiWidget[] extraWidgetsToAdd,
			MessageType messageType,
			string yesOk = "",
			string noCancel = "",
			bool useMarkdown = false,
			int instanceIndex = 0,
			int width = 400,
			int height = 300)
		{
			DialogWindow.Show(
				new MessageBoxPage(callback, message, caption, messageType, extraWidgetsToAdd, width, height, yesOk, noCancel, ApplicationController.Instance.Theme, useMarkdown),
				instanceIndex);
		}

		public class MessageBoxPage : DialogPage
		{
			private string unwrappedMessage;
			private GuiWidget messageContainer;
			private Action<bool> responseCallback;
			private bool haveResponded = false;

			public MessageBoxPage(Action<bool> callback, string message, string caption, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height, string yesOk, string noCancel, ThemeConfig theme, bool useMarkdown = false)
				: base((noCancel == "") ? "No".Localize() : noCancel)
			{
				this.WindowSize = new Vector2(width * GuiWidget.DeviceScale, height * GuiWidget.DeviceScale);

				if (yesOk == "")
				{
					yesOk = (messageType == MessageType.OK) ? "Ok".Localize() : "Yes".Localize();
				}

				this.HeaderText = caption;
				// this.IsModal = true;

				responseCallback = callback;
				unwrappedMessage = message;

				if (useMarkdown)
				{
					contentRow.AddChild(messageContainer = new MarkdownWidget(theme)
					{
						Markdown = message,
					});
				}
				else
				{
					var scrollable = new ScrollableWidget(true);
					scrollable.AnchorAll();
					scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
					contentRow.AddChild(scrollable);

					scrollable.AddChild(messageContainer = new TextWidget(message, textColor: theme.TextColor, pointSize: 12)
					{
						AutoExpandBoundsToText = true,
						HAnchor = HAnchor.Left
					});
				}

				if (extraWidgetsToAdd != null)
				{
					foreach (GuiWidget widget in extraWidgetsToAdd)
					{
						contentRow.AddChild(widget);
					}
				}

				var affirmativeButton = theme.CreateDialogButton(yesOk);
				affirmativeButton.Click += (s, e) =>
				{
					// If applicable, invoke the callback
					responseCallback?.Invoke(true);
					haveResponded = true;

					this.DialogWindow.Close();
				};

				this.AddPageAction(affirmativeButton, messageType != MessageType.YES_NO_WITHOUT_HIGHLIGHT);

				switch (messageType)
				{
					case MessageType.YES_NO_WITHOUT_HIGHLIGHT:
					case MessageType.YES_NO:
						this.WindowTitle = "Matter CAD - " + "Please Confirm".Localize();
						affirmativeButton.Name = "Yes Button";
						this.SetCancelButtonName("No Button");
						break;

					case MessageType.OK:
						this.WindowTitle = "Matter CAD - " + "Alert".Localize();
						affirmativeButton.Name = "Ok Button";
						this.HideCancelButton();
						break;

					default:
						throw new NotImplementedException();
				}

				this.AdjustTextWrap();
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				AdjustTextWrap();
				base.OnBoundsChanged(e);
			}

			private void AdjustTextWrap()
			{
				if (messageContainer != null
					&& messageContainer is TextWidget textWidget)
				{
					double wrappingSize = contentRow.Width - (contentRow.Padding.Width + messageContainer.Margin.Width);
					if (wrappingSize > 0)
					{
						var wrapper = new EnglishTextWrapping(textWidget.PointSize * GuiWidget.DeviceScale);
						messageContainer.Text = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
					}
				}
			}

			public override void OnClosed(EventArgs e)
			{
				if (!haveResponded)
				{
					responseCallback?.Invoke(false);
				}

				base.OnClosed(e);
			}

			protected override void OnCancel(out bool abortCancel)
			{
				responseCallback?.Invoke(false);
				haveResponded = true;
				base.OnCancel(out abortCancel);
			}
		}


		public enum ResponseType
		{
			YES,
			NO,
			CANCEL
		}

		public static void ShowYNCMessageBox(Action<ResponseType> callback,
			string message,
			string caption,
			string yesText = "",
			string noText = "",
			string cancelText = "",
			bool useMarkdown = false,
			int instanceIndex = 0,
			int width = 450,
			int height = 300)
		{
			ShowYNCMessageBox(callback, message, caption, null, yesText, noText, cancelText, useMarkdown, instanceIndex, width, height);
		}

		public static void ShowYNCMessageBox(Action<ResponseType> callback,
			string message,
			string caption,
			GuiWidget[] extraWidgetsToAdd,
			string yesText = "",
			string noText = "",
			string cancelText = "",
			bool useMarkdown = false,
			int instanceIndex = 0,
			int width = 450,
			int height = 300)
		{
			DialogWindow.Show(
				new YesNoCancelMessageBox(callback, message, caption, extraWidgetsToAdd, width, height, yesText, noText, cancelText, ApplicationController.Instance.Theme, useMarkdown),
				instanceIndex);
		}

		public class YesNoCancelMessageBox : DialogPage
		{
			private string unwrappedMessage;
			private GuiWidget messageContainer;
			private Action<ResponseType> responseCallback;
			private bool haveResponded = false;

			public YesNoCancelMessageBox(Action<ResponseType> callback, string message, string caption, GuiWidget[] extraWidgetsToAdd, double width, double height, string yesText, string noText, string cancelText, ThemeConfig theme, bool useMarkdown = false)
				: base((cancelText == "") ? "Cancel".Localize() : cancelText)
			{
				this.WindowSize = new Vector2(width * GuiWidget.DeviceScale, height * GuiWidget.DeviceScale);

				if (yesText == "")
				{
					yesText = "Yes".Localize();
				}

				this.HeaderText = caption;
				// this.IsModal = true;

				responseCallback = callback;
				unwrappedMessage = message;

				if (useMarkdown)
				{
					contentRow.AddChild(messageContainer = new MarkdownWidget(theme)
					{
						Markdown = message,
					});
				}
				else
				{
					var scrollable = new ScrollableWidget(true);
					scrollable.AnchorAll();
					scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
					contentRow.AddChild(scrollable);

					scrollable.AddChild(messageContainer = new TextWidget(message, textColor: theme.TextColor, pointSize: 12)
					{
						AutoExpandBoundsToText = true,
						HAnchor = HAnchor.Left
					});
				}

				if (extraWidgetsToAdd != null)
				{
					foreach (GuiWidget widget in extraWidgetsToAdd)
					{
						contentRow.AddChild(widget);
					}
				}

				var yesButton = theme.CreateDialogButton(yesText);
				yesButton.Name = "Yes Button";
				yesButton.Click += (s, e) =>
				{
					// If applicable, invoke the callback
					responseCallback?.Invoke(ResponseType.YES);
					haveResponded = true;

					this.DialogWindow.Close();
				};

				this.AddPageAction(yesButton, true);

				var noButton = theme.CreateDialogButton(noText);
				noButton.Name = "No Button";
				noButton.Click += (s, e) =>
				{
					// If applicable, invoke the callback
					responseCallback?.Invoke(ResponseType.NO);
					haveResponded = true;

					this.DialogWindow.Close();
				};

				this.AddPageAction(noButton);

				this.WindowTitle = "Matter CAD - " + "Please Confirm".Localize();
				this.SetCancelButtonName("Cancel Button");

				this.AdjustTextWrap();
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				AdjustTextWrap();
				base.OnBoundsChanged(e);
			}

			private void AdjustTextWrap()
			{
				if (messageContainer != null
					&& messageContainer is TextWidget textWidget)
				{
					double wrappingSize = contentRow.Width - (contentRow.Padding.Width + messageContainer.Margin.Width);
					if (wrappingSize > 0)
					{
						var wrapper = new EnglishTextWrapping(textWidget.PointSize * GuiWidget.DeviceScale);
						messageContainer.Text = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
					}
				}
			}

			public override void OnClosed(EventArgs e)
			{
				if (!haveResponded)
				{
					responseCallback?.Invoke(ResponseType.CANCEL);
				}

				base.OnClosed(e);
			}

			protected override void OnCancel(out bool abortCancel)
			{
				responseCallback?.Invoke(ResponseType.CANCEL);
				haveResponded = true;
				base.OnCancel(out abortCancel);
			}
		}
	}
}
