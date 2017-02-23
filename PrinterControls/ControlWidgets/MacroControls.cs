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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class ActionControls : ControlWidgetBase
	{
		public ActionControls()
		{
			if (!ActiveSliceSettings.Instance.ActionMacros().Any())
			{
				Margin = new BorderDouble();
				return;
			}
			this.AddChild(new ActionControlsWidget());
		}
	}

	public class TerminalControls : ControlWidgetBase
	{
		public TerminalControls()
		{
			if (!ActiveSliceSettings.Instance.ActionMacros().Any())
			{
				Margin = new BorderDouble();
				return;
			}
			this.AddChild(new TerminalWidget(false));
		}
	}

	public class ActionControlsWidget : FlowLayoutWidget
	{
		protected string editWindowLabel;
		protected string label;
		protected FlowLayoutWidget presetButtonsContainer;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public ActionControlsWidget()
			: base(FlowDirection.TopToBottom)
		{
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			this.textImageButtonFactory.FixedHeight = 24 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.fontSize = 12;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.HAnchor = HAnchor.ParentLeftRight;

			// add the widgets to this window
			FlowLayoutWidget groupBox = new FlowLayoutWidget()
			{
				Padding = new BorderDouble(5),
				HAnchor = HAnchor.ParentLeftRight,
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
			};

			groupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			// make sure the client area will get smaller when the contents get smaller
			groupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

			FlowLayoutWidget controlRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			controlRow.Margin = new BorderDouble(top: 5);
			controlRow.HAnchor |= HAnchor.ParentLeftRight;
			{
				this.presetButtonsContainer = GetMacroButtonContainer();
				controlRow.AddChild(this.presetButtonsContainer);
			}

			groupBox.AddChild(controlRow);
			this.AddChild(groupBox);
		}

		private FlowLayoutWidget GetMacroButtonContainer()
		{
			FlowLayoutWidget macroButtonContainer = new FlowLayoutWidget();
			macroButtonContainer.Margin = new BorderDouble(0, 0, 3, 0);
			macroButtonContainer.Padding = new BorderDouble(0, 3, 3, 3);

			if (ActiveSliceSettings.Instance?.ActionMacros().Any() != true)
			{
				return macroButtonContainer;
			}

			foreach (GCodeMacro macro in ActiveSliceSettings.Instance.ActionMacros())
			{
				Button macroButton = textImageButtonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
				macroButton.Margin = new BorderDouble(right: 5);
				macroButton.Click += (s, e) => macro.Run();

				macroButtonContainer.AddChild(macroButton);
			}

			return macroButtonContainer;
		}
	}

	public class MacroControls : ControlWidgetBase
	{
		public MacroControls()
		{
			this.AddChild(new MacroControlsWidget());
		}
	}

	public class MacroControlsWidget : FlowLayoutWidget
	{
		protected string editWindowLabel;
		protected string label;
		protected FlowLayoutWidget presetButtonsContainer;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public MacroControlsWidget()
					: base(FlowDirection.TopToBottom)
		{
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			this.textImageButtonFactory.FixedHeight = 24 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.fontSize = 12;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.HAnchor = HAnchor.ParentLeftRight;

			// add the widgets to this window
			Button editButton;
			AltGroupBox groupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(new TextWidget("Macros".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor), out editButton));
			editButton.Click += (sender, e) =>
			{
				EditMacrosWindow.Show();
			};

			groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			groupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			// make sure the client area will get smaller when the contents get smaller
			groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.FitToChildren;

			FlowLayoutWidget controlRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			controlRow.Margin = new BorderDouble(top: 5);
			controlRow.HAnchor = HAnchor.ParentLeftRight;
			{
				this.presetButtonsContainer = GetMacroButtonContainer();
				controlRow.AddChild(this.presetButtonsContainer);
			}

			groupBox.AddChild(controlRow);
			this.AddChild(groupBox);
		}

		private FlowLayoutWidget GetMacroButtonContainer()
		{
			FLowLeftRightWithWrapping macroContainer = new FLowLeftRightWithWrapping();

			TextWidget noMacrosFound = new TextWidget("No macros are currently set up for this printer.".Localize(), pointSize: 10);
			noMacrosFound.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroContainer.AddChild(noMacrosFound);
			noMacrosFound.Visible = false;

			if (ActiveSliceSettings.Instance?.UserMacros().Any() != true)
			{
				noMacrosFound.Visible = true;
				return macroContainer;
			}

			foreach (GCodeMacro macro in ActiveSliceSettings.Instance.UserMacros())
			{
				Button macroButton = textImageButtonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
				macroButton.Margin = new BorderDouble(right: 5);
				macroButton.Click += (s, e) => macro.Run();

				macroContainer.AddChild(macroButton);
			}

			macroContainer.Children.CollectionChanged += (s, e) =>
			{
				if (!this.HasBeenClosed)
				{
					noMacrosFound.Visible = macroContainer.Children.Count == 0;
				}
			};
			
			return macroContainer;
		}
	}
}