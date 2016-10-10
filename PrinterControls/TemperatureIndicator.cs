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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public abstract class TemperatureControlBase : FlowLayoutWidget
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected TextWidget actualTempIndicator;
		protected Button tempOffButton;
		protected FlowLayoutWidget presetButtonsContainer;

		protected EditableNumberDisplay targetTemperatureDisplay;

		protected string label;
		protected string editWindowLabel;
		protected int extruderIndex0Based = 0;

		protected TemperatureControlBase(int extruderIndex0Based, string label, string editWindowLabel)
			: base(FlowDirection.TopToBottom)
		{
			this.extruderIndex0Based = extruderIndex0Based;
			this.label = label;
			this.editWindowLabel = editWindowLabel;
			SetDisplayAttributes();
		}

		protected abstract double GetActualTemperature();

		protected abstract double GetTargetTemperature();

		protected abstract void SetTargetTemperature(double targetTemp);

		protected abstract string GetTemperaturePresets();

		protected abstract double GetPreheatTemperature();

		protected abstract void SetTemperaturePresets(object sender, EventArgs stringEvent);

		protected abstract string HelpText { get; }

		private void SetDisplayAttributes()
		{
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;

			this.textImageButtonFactory.FixedWidth = 38 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.FixedHeight = 20 * GuiWidget.DeviceScale;
			this.textImageButtonFactory.fontSize = 10;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.HAnchor = HAnchor.ParentLeftRight;
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				return base.LocalBounds;
			}
			set
			{
				base.LocalBounds = value;
			}
		}

		protected FlowLayoutWidget tempSliderContainer;
		private EditTemperaturePresetsWindow editSettingsWindow;

		protected void AddChildElements()
		{
			Button editButton;
			AltGroupBox groupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(label, out editButton));
			editButton.Click += (sender, e) =>
			{
				if (editSettingsWindow == null)
				{
					editSettingsWindow = new EditTemperaturePresetsWindow(editWindowLabel, GetTemperaturePresets(), SetTemperaturePresets);
					editSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editSettingsWindow = null; };
				}
				else
				{
					editSettingsWindow.BringToFront();
				}
			};

			groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			groupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			// make sure the client area will get smaller when the contents get smaller
			groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.FitToChildren;

			FlowLayoutWidget controlRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			controlRow.Margin = new BorderDouble(top: 2);
			controlRow.HAnchor |= HAnchor.ParentLeftRight;
			{
				// put in the temperature slider and preset buttons

				tempSliderContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);

				{
					GuiWidget sliderLabels = GetSliderLabels();

					tempSliderContainer.HAnchor = HAnchor.ParentLeftRight;
					tempSliderContainer.AddChild(sliderLabels);
					tempSliderContainer.Visible = false;
				}
				GuiWidget spacer = new GuiWidget(0, 10);
				spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

				// put in the temperature indicators
				{
					FlowLayoutWidget temperatureIndicator = new FlowLayoutWidget();
					temperatureIndicator.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
					temperatureIndicator.Margin = new BorderDouble(bottom: 0);
					temperatureIndicator.Padding = new BorderDouble(0, 3);

					// put in the actual temperature controls
					{
						FlowLayoutWidget extruderActualIndicator = new FlowLayoutWidget(Agg.UI.FlowDirection.LeftToRight);

						extruderActualIndicator.Margin = new BorderDouble(3, 0);
						string extruderActualLabelTxt = LocalizedString.Get("Actual");
						string extruderActualLabelTxtFull = string.Format("{0}: ", extruderActualLabelTxt);
						TextWidget extruderActualLabel = new TextWidget(extruderActualLabelTxtFull, pointSize: 10);
						extruderActualLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						extruderActualLabel.VAnchor = VAnchor.ParentCenter;

						actualTempIndicator = new TextWidget(string.Format("{0:0.0}°C", GetActualTemperature()), pointSize: 12);
						actualTempIndicator.AutoExpandBoundsToText = true;
						actualTempIndicator.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						actualTempIndicator.VAnchor = VAnchor.ParentCenter;

						extruderActualIndicator.AddChild(extruderActualLabel);
						extruderActualIndicator.AddChild(actualTempIndicator);

						string extruderAboutLabelTxt = LocalizedString.Get("Target");
						string extruderAboutLabelTxtFull = string.Format("{0}: ", extruderAboutLabelTxt);

						TextWidget extruderTargetLabel = new TextWidget(extruderAboutLabelTxtFull, pointSize: 10);
						extruderTargetLabel.Margin = new BorderDouble(left: 10);
						extruderTargetLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						extruderTargetLabel.VAnchor = VAnchor.ParentCenter;

						extruderActualIndicator.AddChild(extruderTargetLabel);
						temperatureIndicator.AddChild(extruderActualIndicator);
					}

					// put in the target temperature controls
					temperatureIndicator.AddChild(GetTargetTemperatureDisplay());

					FlowLayoutWidget helperTextWidget = GetHelpTextWidget();

					LinkButtonFactory linkFactory = new LinkButtonFactory();
					linkFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
					linkFactory.fontSize = 10;

					Button helpTextLink = linkFactory.Generate("?");

					helpTextLink.Click += (sender, e) =>
					{
						helperTextWidget.Visible = !helperTextWidget.Visible;
					};

					this.presetButtonsContainer = GetPresetsContainer();
					temperatureIndicator.AddChild(new HorizontalSpacer());
					temperatureIndicator.AddChild(presetButtonsContainer);

					controlRow.AddChild(temperatureIndicator);
				}
			}

			groupBox.AddChild(controlRow);

			this.AddChild(groupBox);
		}

		private FlowLayoutWidget GetPresetsContainer()
		{
			FlowLayoutWidget presetsContainer = new FlowLayoutWidget();
			presetsContainer.Margin = new BorderDouble(3, 0);

			string presetsLabelTxt = LocalizedString.Get("Presets");
			string presetsLabelTxtFull = string.Format("{0}: ", presetsLabelTxt);

			TextWidget presetsLabel = new TextWidget(presetsLabelTxtFull, pointSize: 10);
			presetsLabel.Margin = new BorderDouble(right: 5);
			presetsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			presetsLabel.VAnchor = VAnchor.ParentCenter;
			//presetsContainer.AddChild(presetsLabel);

			SortedDictionary<double, string> labels = GetTemperaturePresetLabels();

			foreach (KeyValuePair<double, string> keyValue in labels)
			{
				Button tempButton = textImageButtonFactory.Generate(keyValue.Value);
				tempButton.Margin = new BorderDouble(right: 5);
				presetsContainer.AddChild(tempButton);

				// We push the value into a temp double so that the function will not point to a shared keyValue instance.
				double temp = keyValue.Key;
				tempButton.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						SetTargetTemperature(temp);
						tempSliderContainer.Visible = false;
					});
				};
			}

			this.textImageButtonFactory.FixedWidth = 76 * GuiWidget.DeviceScale;
			{
				Button tempButton = textImageButtonFactory.Generate("Preheat".Localize().ToUpper());
				tempButton.Margin = new BorderDouble(right: 5);
				presetsContainer.AddChild(tempButton);

				// We push the value into a temp double so that the function will not point to a shared keyValue instance.
				tempButton.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						SetTargetTemperature(GetPreheatTemperature());
						tempSliderContainer.Visible = false;
					});
				};
			}
			this.textImageButtonFactory.FixedWidth = 38 * GuiWidget.DeviceScale;

			return presetsContainer;
		}

		private EditableNumberDisplay GetTargetTemperatureDisplay()
		{
			targetTemperatureDisplay = new EditableNumberDisplay(textImageButtonFactory, string.Format("{0:0.0}°C", GetTargetTemperature()), string.Format("{0:0.0}°C", 240.2));
			targetTemperatureDisplay.EditEnabled += (sender, e) =>
			{
				tempSliderContainer.Visible = true;
			};

			targetTemperatureDisplay.EditComplete += (sender, e) =>
			{
				SetTargetTemperature(targetTemperatureDisplay.GetValue());
			};
			return targetTemperatureDisplay;
		}

		private FlowLayoutWidget GetHelpTextWidget()
		{
			FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
			double textRegionWidth = 260 * GuiWidget.DeviceScale;
			allText.Margin = new BorderDouble(3);
			allText.Padding = new BorderDouble(3);
			allText.HAnchor = HAnchor.ParentLeftRight;
			allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

			double helpPointSize = 10;
			EnglishTextWrapping wrapper = new EnglishTextWrapping(helpPointSize);
			List<string> wrappedText = wrapper.WrapText(HelpText, textRegionWidth - allText.Padding.Width);
			foreach (string line in wrappedText)
			{
				GuiWidget helpWidget = new TextWidget(line, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
				allText.AddChild(helpWidget);
			}

			allText.MinimumSize = new Vector2(textRegionWidth, allText.MinimumSize.y);
			allText.Visible = false;
			return allText;
		}

		protected SortedDictionary<double, string> GetTemperaturePresetLabels()
		{
			string sliderLabelDefinitions = GetTemperaturePresets();

			SortedDictionary<double, string> labels = new SortedDictionary<double, string>() { };
			labels.Add(0.0, "OFF");

			string[] labelItems = sliderLabelDefinitions.Split(',');
			for (int i = 0; i < labelItems.Length / 2; i++)
			{
				string name = labelItems[i * 2];
				double temp;
				double.TryParse(labelItems[i * 2 + 1], out temp);

				//Ignore temp values that already exits
				if (temp > 0 && !labels.ContainsKey(temp))
				{
					labels.Add(temp, name);
				}
			}

			return labels;
		}

		protected GuiWidget GetSliderLabels()
		{
			GuiWidget sliderLabels = new GuiWidget();
			sliderLabels.HAnchor = HAnchor.ParentLeftRight;
			sliderLabels.Height = 20 * GuiWidget.DeviceScale;
			{
				double buttonOffset = -10 * GuiWidget.DeviceScale;
				var offPosition = buttonOffset;

				tempOffButton = textImageButtonFactory.Generate("Off");
				tempOffButton.OriginRelativeParent = new Vector2(offPosition, 0);

				//sliderLabels.AddChild(tempOffButton);

				SortedDictionary<double, string> labels = GetTemperaturePresetLabels();

				bool firstElement = true;
				double minButtonPosition = 0;
				foreach (KeyValuePair<double, string> keyValue in labels)
				{
					if (firstElement)
					{
						minButtonPosition = buttonOffset;
						firstElement = false;
					}
					else
					{
						double wantedButtonPosition = buttonOffset;
						minButtonPosition = Math.Max(minButtonPosition + textImageButtonFactory.FixedWidth + 3, wantedButtonPosition);
					}
					Button tempButton = textImageButtonFactory.Generate(keyValue.Value);
					tempButton.OriginRelativeParent = new Vector2(minButtonPosition, 0);

					sliderLabels.AddChild(tempButton);

					// We push the value into a temp double so that the function will not point to a shared keyValue instance.
					double temp = keyValue.Key;
					tempButton.Click += (sender, e) =>
					{
						SetTargetTemperature(temp);
						tempSliderContainer.Visible = false;
					};
				}
			}

			sliderLabels.HAnchor = HAnchor.FitToChildren;
			sliderLabels.VAnchor = VAnchor.FitToChildren;
			sliderLabels.MinimumSize = new Vector2(sliderLabels.Width, sliderLabels.Height);
			return sliderLabels;
		}

		private double MaxTemp
		{
			get
			{
				string presets = GetTemperaturePresets();
				string[] list = presets.Split(',');
				double max = 0;
				foreach (string item in list)
				{
					double value = 0;
					double.TryParse(item, out value);
					max = Math.Max(max, value);
				}
				return max;
			}
		}

		protected void onTemperatureRead(Object sender, EventArgs e)
		{
			TemperatureEventArgs tempArgs = e as TemperatureEventArgs;
			if (tempArgs != null && tempArgs.Index0Based == extruderIndex0Based)
			{
				actualTempIndicator.Text = string.Format("{0:0.0}°C", tempArgs.Temperature);
			}
		}

		protected void onTemperatureSet(Object sender, EventArgs e)
		{
			TemperatureEventArgs tempArgs = e as TemperatureEventArgs;
			if (tempArgs != null && tempArgs.Index0Based == extruderIndex0Based)
			{
				SetTargetTemperature(tempArgs.Temperature);
			}
		}
	}

	public class ExtruderTemperatureControlWidget : TemperatureControlBase
	{
		public ExtruderTemperatureControlWidget()
			: base(0, LocalizedString.Get("Extruder Temperature"), LocalizedString.Get("Extruder Temperature Settings"))
		{
			AddChildElements();
			AddHandlers();
		}

		public ExtruderTemperatureControlWidget(int extruderIndex0Based)
			: base(extruderIndex0Based, string.Format("{0} {1}", "Extruder Temperature".Localize(), extruderIndex0Based + 1), LocalizedString.Get("Extruder Temperature Settings"))
		{
			AddChildElements();
			AddHandlers();
		}

		override protected string HelpText
		{
			get { return "Override the current extruder temperature. While printing, the extruder temperature is normally determined by the 'Slice Settings'."; }
		}

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.ExtruderTemperatureRead.RegisterEvent(onTemperatureRead, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.ExtruderTemperatureSet.RegisterEvent(onTemperatureSet, ref unregisterEvents);
			tempOffButton.Click += new EventHandler(onOffButtonClicked);
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			base.OnClosed(e);
		}

		private void onOffButtonClicked(object sender, EventArgs e)
		{
			SetTargetTemperature(0);
		}

		protected override string GetTemperaturePresets()
		{
			string default_presets = ",0,,0,,0,250";

			string presets;
			string presetKey = string.Format("Extruder{0}PresetTemps", extruderIndex0Based + 1);
			if (UserSettings.Instance.get(presetKey) == null)
			{
				UserSettings.Instance.set(presetKey, default_presets);
			}
			presets = UserSettings.Instance.get(presetKey);
			return presets;
		}

		protected override void SetTemperaturePresets(object seder, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null && stringEvent.Data != null)
			{
				UserSettings.Instance.set(string.Format("Extruder{0}PresetTemps", extruderIndex0Based + 1), stringEvent.Data);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

		protected override double GetPreheatTemperature()
		{
			return ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(extruderIndex0Based);
		}

		protected override double GetTargetTemperature()
		{
			return PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderIndex0Based);
		}

		protected override double GetActualTemperature()
		{
			return PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(extruderIndex0Based);
		}

		protected override void SetTargetTemperature(double targetTemp)
		{
			UiThread.RunOnIdle(() =>
			{
				double goalTemp = (int)(targetTemp + .5);
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
					&& PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.HeatingExtruder
					&& goalTemp != PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderIndex0Based))
				{
					string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.";
					string message = string.Format("The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}", PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderIndex0Based), sliceSettingsNote);
					StyledMessageBox.ShowMessageBox(null, message, "Waiting For Extruder To Heat");
				}
				else
				{
					PrinterConnectionAndCommunication.Instance.SetTargetExtruderTemperature(extruderIndex0Based, (int)(targetTemp + .5));
					string displayString = string.Format("{0:0.0}°C", PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderIndex0Based));
					targetTemperatureDisplay.SetDisplayString(displayString);
				}
			});
		}
	}

	public class BedTemperatureControlWidget : TemperatureControlBase
	{
		public BedTemperatureControlWidget()
			: base(0, LocalizedString.Get("Bed Temperature"), LocalizedString.Get("Bed Temperature Settings"))
		{
			AddChildElements();
			AddHandlers();
			this.Name = "Bed Temperature Controls Widget";
		}

		override protected string HelpText
		{
			get { return "Override the current bed temperature. While printing, the bed temperature is normally determined by the 'Slice Settings'."; }
		}

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.BedTemperatureRead.RegisterEvent(onTemperatureRead, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.BedTemperatureSet.RegisterEvent(onTemperatureSet, ref unregisterEvents);
			tempOffButton.Click += new EventHandler(onOffButtonClicked);
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void onOffButtonClicked(object sender, EventArgs e)
		{
			SetTargetTemperature(0);
		}

		protected override string GetTemperaturePresets()
		{
			string default_presets = ",0,,0,,0,150";
			string presets;

			if (UserSettings.Instance.get("BedPresetTemps") == null)
			{
				UserSettings.Instance.set("BedPresetTemps", default_presets);
			}
			presets = UserSettings.Instance.get("BedPresetTemps");

			return presets;
		}

		protected override void SetTemperaturePresets(object seder, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null && stringEvent.Data != null)
			{
				UserSettings.Instance.set("BedPresetTemps", stringEvent.Data);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

		protected override double GetPreheatTemperature()
		{
			return ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.bed_temperature); 
		}

		protected override double GetActualTemperature()
		{
			return PrinterConnectionAndCommunication.Instance.ActualBedTemperature;
		}

		protected override double GetTargetTemperature()
		{
			return PrinterConnectionAndCommunication.Instance.TargetBedTemperature;
		}

		protected override void SetTargetTemperature(double targetTemp)
		{
			double goalTemp = (int)(targetTemp + .5);
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
				&& PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.HeatingBed
				&& goalTemp != PrinterConnectionAndCommunication.Instance.TargetBedTemperature)
			{
				string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.";
				string message = string.Format("The bed is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting bed temperature in 'Slice Settings' -> 'Filament'.\n\n{1}", PrinterConnectionAndCommunication.Instance.TargetBedTemperature, sliceSettingsNote);
				StyledMessageBox.ShowMessageBox(null, message, "Waiting For Bed To Heat");
			}
			else
			{
				PrinterConnectionAndCommunication.Instance.TargetBedTemperature = goalTemp;
				string displayString = string.Format("{0:0.0}°C", PrinterConnectionAndCommunication.Instance.TargetBedTemperature);
				targetTemperatureDisplay.SetDisplayString(displayString);
			}
		}
	}
}