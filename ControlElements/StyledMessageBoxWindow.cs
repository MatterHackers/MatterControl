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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public static class StyledMessageBox
	{
		public enum MessageType { OK, YES_NO };

		public static void ShowMessageBox(string message, string caption, MessageType messageType = MessageType.OK, string yesOk = "", string noCancel = "")
		{
			ShowMessageBox(null, message, caption, null, messageType, yesOk, noCancel);
		}

		public static void ShowMessageBox(Action<bool> callback, string message, string caption, MessageType messageType = MessageType.OK, string yesOk = "", string noCancel = "")
		{
			ShowMessageBox(callback, message, caption, null, messageType, yesOk, noCancel);
		}

		public static void ShowMessageBox(Action<bool> callback, string message, string caption, GuiWidget[] extraWidgetsToAdd, MessageType messageType, string yesOk = "", string noCancel = "")
		{
			DialogWindow.Show(
				new MessageBoxPage(callback, message, caption, messageType, extraWidgetsToAdd, 400, 300, yesOk, noCancel, ApplicationController.Instance.Theme));
		}

		private class MessageBoxPage : DialogPage
		{
			private string unwrappedMessage;
			private TextWidget messageContainer;
			private Action<bool> responseCallback;

			private double extraTextScaling = (UserSettings.Instance.IsTouchScreen) ? 1.33333 : 1;

			public MessageBoxPage(Action<bool> callback, string message, string caption, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height, string yesOk, string noCancel, ThemeConfig theme)
				: base((noCancel == "") ? "No".Localize() : noCancel)
			{
				this.WindowSize = new VectorMath.Vector2(width, height);

				if (yesOk == "")
				{
					yesOk = (messageType == MessageType.OK) ? "Ok".Localize() : "Yes".Localize();
				}

				this.HeaderText = caption;
				//this.IsModal = true;

				responseCallback = callback;
				unwrappedMessage = message;

				contentRow.AddChild(messageContainer = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 12 * extraTextScaling)
				{
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.Left
				});

				if (extraWidgetsToAdd != null)
				{
					foreach (GuiWidget widget in extraWidgetsToAdd)
					{
						contentRow.AddChild(widget);
					}
				}

				Button affirmativeButton = textImageButtonFactory.Generate(yesOk);
				affirmativeButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(this.WizardWindow.Close);

					// If applicable, invoke the callback
					responseCallback?.Invoke(true);
				};

				this.AddPageAction(affirmativeButton);

				switch (messageType)
				{
					case MessageType.YES_NO:
						this.WindowTitle = "MatterControl - " + "Please Confirm".Localize();
						affirmativeButton.Name = "Yes Button";
						this.SetCancelButtonName("No Button");
						break;

					case MessageType.OK:
						this.WindowTitle = "MatterControl - " + "Alert".Localize();
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
				if (messageContainer != null)
				{
					double wrappingSize = contentRow.Width - (contentRow.Padding.Width + messageContainer.Margin.Width);
					if (wrappingSize > 0)
					{
						var wrapper = new EnglishTextWrapping(12 * extraTextScaling * GuiWidget.DeviceScale);
						messageContainer.Text = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
					}
				}
			}

			protected override void OnCancel(out bool abortCancel)
			{
				responseCallback?.Invoke(false);
				base.OnCancel(out abortCancel);
			}
		}

	}
}
 