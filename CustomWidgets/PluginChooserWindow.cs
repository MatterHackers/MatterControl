/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.CreatorPlugins
{
	public class PluginChooserWindow : SystemWindow
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected TextImageButtonFactory unlockButtonFactory = new TextImageButtonFactory();
		private List<GuiWidget> listWithValues = new List<GuiWidget>();

		private ImageBuffer LoadImage(string imageName)
		{
			string path = Path.Combine("Icons", imageName);
			ImageBuffer buffer = new ImageBuffer(10, 10);

			StaticData.Instance.LoadImage(path, buffer);

			return buffer;
		}

		public PluginChooserWindow()
			: base(360, 300)
		{
			AddElements();
			ShowAsSystemWindow();
			MinimumSize = new Vector2(360, 300);
			AddHandlers();
		}

		private event EventHandler unregisterEvents;

		protected void AddHandlers()
		{
			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(Reload);
		}

		public void TriggerReload(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(Reload);
		}

		public void Reload()
		{
			this.RemoveAllChildren();
			this.AddElements();
		}

		public void AddElements()
		{
			Title = LocalizedString.Get("Design Add-ons");

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			{
				string elementHeaderLabelBeg = LocalizedString.Get("Select a Design Tool");
				string elementHeaderLabelFull = string.Format("{0}:", elementHeaderLabelBeg);
				string elementHeaderLabel = elementHeaderLabelFull;
				TextWidget elementHeader = new TextWidget(string.Format(elementHeaderLabel), pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			GuiWidget presetsFormContainer = new GuiWidget();
			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			FlowLayoutWidget pluginRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			pluginRowContainer.AnchorAll();
			presetsFormContainer.AddChild(pluginRowContainer);

			unlockButtonFactory.Margin = new BorderDouble(10, 0);
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				unlockButtonFactory.normalFillColor = new RGBA_Bytes(0, 0, 0, 100);
				unlockButtonFactory.normalBorderColor = new RGBA_Bytes(0, 0, 0, 100);
				unlockButtonFactory.hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
				unlockButtonFactory.hoverBorderColor = new RGBA_Bytes(0, 0, 0, 50);
			}
			else
			{
				unlockButtonFactory.normalFillColor = new RGBA_Bytes(0, 0, 0, 50);
				unlockButtonFactory.normalBorderColor = new RGBA_Bytes(0, 0, 0, 50);
				unlockButtonFactory.hoverFillColor = new RGBA_Bytes(0, 0, 0, 100);
				unlockButtonFactory.hoverBorderColor = new RGBA_Bytes(0, 0, 0, 100);
			}

			foreach (CreatorInformation creatorInfo in RegisteredCreators.Instance.Creators)
			{
				FlowLayoutWidget pluginListingContainer = new FlowLayoutWidget();
				pluginListingContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				pluginListingContainer.BackgroundColor = RGBA_Bytes.White;
				pluginListingContainer.Padding = new BorderDouble(0);
				pluginListingContainer.Margin = new BorderDouble(6, 0, 6, 6);

				ClickWidget pluginRow = new ClickWidget();
				pluginRow.Margin = new BorderDouble(6, 0, 6, 0);
				pluginRow.Height = 38;
				pluginRow.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

				FlowLayoutWidget macroRow = new FlowLayoutWidget();
				macroRow.AnchorAll();
				macroRow.BackgroundColor = RGBA_Bytes.White;

				if (creatorInfo.iconPath != "")
				{
					ImageBuffer imageBuffer = LoadImage(creatorInfo.iconPath);
					ImageWidget imageWidget = new ImageWidget(imageBuffer);
					imageWidget.VAnchor = Agg.UI.VAnchor.ParentCenter;
					macroRow.AddChild(imageWidget);
				}

				bool userHasPermission;
				if (!creatorInfo.paidAddOnFlag)
				{
					userHasPermission = true;
				}
				else
				{
					userHasPermission = creatorInfo.permissionFunction();
				}

				string addOnDescription;
				addOnDescription = creatorInfo.description;
				TextWidget buttonLabel = new TextWidget(addOnDescription, pointSize: 14);
				buttonLabel.Margin = new BorderDouble(left: 10);
				buttonLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
				macroRow.AddChild(buttonLabel);

				if (!userHasPermission)
				{
					TextWidget demoLabel = new TextWidget("(" + "demo".Localize() + ")", pointSize: 10);

					demoLabel.Margin = new BorderDouble(left: 4);
					demoLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
					macroRow.AddChild(demoLabel);
				}

				macroRow.AddChild(new HorizontalSpacer());

				CreatorInformation callCorrectFunctionHold = creatorInfo;
				pluginRow.Click += (sender, e) =>
				{
					if (RegisteredCreators.Instance.Creators.Count > 0)
					{
						UiThread.RunOnIdle(CloseOnIdle, callCorrectFunctionHold);
					}
					else
					{
						UiThread.RunOnIdle(CloseOnIdle);
					}
				};

				pluginRow.Cursor = Cursors.Hand;
				macroRow.Selectable = false;
				pluginRow.AddChild(macroRow);

				pluginListingContainer.AddChild(pluginRow);

				if (!userHasPermission)
				{
					Button unlockButton = unlockButtonFactory.Generate("Unlock".Localize());
					unlockButton.Margin = new BorderDouble(0);
					unlockButton.Cursor = Cursors.Hand;
					unlockButton.Click += (sender, e) =>
					{
						callCorrectFunctionHold.unlockFunction();
					};
					pluginListingContainer.AddChild(unlockButton);
				}

				pluginRowContainer.AddChild(pluginListingContainer);
				if (callCorrectFunctionHold.unlockRegisterFunction != null)
				{
					callCorrectFunctionHold.unlockRegisterFunction(TriggerReload, ref unregisterEvents);
				}
			}

			topToBottom.AddChild(presetsFormContainer);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			Button cancelPresetsButton = textImageButtonFactory.Generate(LocalizedString.Get("Cancel"));
			cancelPresetsButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(CloseOnIdle);
			};

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);

			AddChild(topToBottom);
		}

		private void CloseOnIdle(object state)
		{
			Close();
			CreatorInformation callCorrectFunctionHold = state as CreatorInformation;
			if (callCorrectFunctionHold != null)
			{
				UiThread.RunOnIdle(() =>
				{
					callCorrectFunctionHold.functionToLaunchCreator(null, null);
				});
			}
		}
	}
}