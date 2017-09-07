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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class BasicField
	{
		public event EventHandler<FieldChangedEventArgs> ValueChanged;

		public void SetValue(string newValue, bool userInitiated)
		{
			string convertedValue = this.ConvertValue(newValue);

			if (this.Value != convertedValue)
			{
				this.Value = convertedValue;
				this.OnValueChanged(new FieldChangedEventArgs(userInitiated));
			}
			else if (newValue != convertedValue)
			{
				// If the validated value matches the current value, then UI element values were rejected and must be discarded
				this.OnValueChanged(new FieldChangedEventArgs(userInitiated));
			}
		}

		public string Value { get; private set; }

		public GuiWidget Content { get; protected set; }

		public string HelpText { get; set; }

		public string Name { get; set; }

		protected virtual string ConvertValue(string newValue)
		{
			return newValue;
		}

		protected virtual void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			ValueChanged?.Invoke(this, fieldChangedEventArgs);
		}
	}

	public class IntField : NumberField
	{
		private int intValue;

		protected override string ConvertValue(string newValue)
		{
			decimal.TryParse(this.Value, out decimal currentValue);
			intValue = (int)currentValue;

			return intValue.ToString();
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			numberEdit.ActuallNumberEdit.Value = intValue;
			base.OnValueChanged(fieldChangedEventArgs);
		}
	}

	public abstract class NumberField : BasicField, IUIField
	{
		protected MHNumberEdit numberEdit;

		private readonly int ControlWidth = (int)(60 * GuiWidget.DeviceScale + .5);

		public virtual void Initialize(int tabIndex)
		{
			numberEdit = new MHNumberEdit(0, pixelWidth: ControlWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				SelectAllOnFocus = true,
				Name = this.Name,
			};
			numberEdit.ActuallNumberEdit.EditComplete += (s, e) =>
			{
				if (this.Value != numberEdit.Value.ToString())
				{
					this.SetValue(
						numberEdit.Value.ToString(),
						userInitiated: true);
				}
			};

			this.Content = numberEdit;
		}
	}

	public class TextField : BasicField, IUIField
	{
		protected MHTextEditWidget textEditWidget;

		private readonly int ControlWidth = (int)(60 * GuiWidget.DeviceScale + .5);

		public virtual void Initialize(int tabIndex)
		{
			textEditWidget = new MHTextEditWidget("", pixelWidth: ControlWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				SelectAllOnFocus = true,
				Name = this.Name,
			};
			textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
			{
				if (this.Value != textEditWidget.Text)
				{
					this.SetValue(
						textEditWidget.Text, 
						userInitiated: true);
				}
			};

			this.Content = textEditWidget;
		}


		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			if (this.Value != textEditWidget.Text)
			{
				textEditWidget.Text = this.Value;
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}

	public class DropMenuWrappedField : IUIField
	{
		private IUIField uiField;
		private SliceSettingData settingData;

		public DropMenuWrappedField(IUIField uiField, SliceSettingData settingData)
		{
			this.settingData = settingData;
			this.uiField = uiField;
		}

		public void SetValue(string newValue, bool userInitiated)
		{
			uiField.SetValue(newValue, userInitiated);
		}

		public string Value { get => uiField.Value; }

		public GuiWidget Content { get; private set; }

		public event EventHandler<FieldChangedEventArgs> ValueChanged;

		public void Initialize(int tabIndex)
		{
			var totalContent = new FlowLayoutWidget();

			var selectableOptions = new DropDownList("Custom", maxHeight: 200);
			selectableOptions.Margin = new BorderDouble(0, 0, 10, 0);

			foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
			{
				string valueLocal = nameValue.Value;

				MenuItem newItem = selectableOptions.AddItem(nameValue.MenuName);
				if (uiField.Value == valueLocal)
				{
					selectableOptions.SelectedLabel = nameValue.MenuName;
				}

				newItem.Selected += (s, e) =>
				{
					uiField.SetValue(valueLocal, userInitiated: true);
				};
			}

			totalContent.AddChild(selectableOptions);

			uiField.Content.VAnchor = VAnchor.Center;
			totalContent.AddChild(uiField.Content);

			EventHandler localUnregisterEvents = null;

			ActiveSliceSettings.SettingChanged.RegisterEvent((sender, e) =>
			{
				if (e is StringEventArgs stringArgs
					&& stringArgs.Data == settingData.SlicerConfigName)
				{
					bool foundSetting = false;
					foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
					{
						string localName = nameValue.MenuName;
						string newSliceSettingValue = uiField.Value;
						if (newSliceSettingValue == nameValue.Value)
						{
							selectableOptions.SelectedLabel = localName;
							foundSetting = true;
							break;
						}
					}

					if (!foundSetting)
					{
						selectableOptions.SelectedLabel = "Custom";
					}
				}
			}, ref localUnregisterEvents);

			totalContent.Closed += (s, e) =>
			{
				localUnregisterEvents?.Invoke(s, null);
			};

			this.Content = totalContent;
		}
	}
}
