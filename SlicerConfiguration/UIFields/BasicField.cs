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
		public event EventHandler ValueChanged;

		private string fieldValue;
		public string Value
		{
			get => fieldValue;
			set
			{
				string convertedValue = this.ConvertValue(value);

				if (fieldValue != convertedValue)
				{
					fieldValue = convertedValue;
					this.OnValueChanged();
				}
				else if (value != convertedValue)
				{
					// If the validated value matches the current value, then UI element values were rejected and must be discarded
					this.OnValueChanged();
				}
			}
		}

		public GuiWidget Content { get; protected set; }

		public string HelpText { get; set; }

		public string Name { get; set; }

		protected virtual string ConvertValue(string newValue)
		{
			return newValue;
		}

		protected virtual void OnValueChanged()
		{
			ValueChanged?.Invoke(this, new EventArgs());
		}
	}

	public class NumberField : BasicField, IUIField
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
					this.Value = numberEdit.Value.ToString();
				}
			};

			this.Content = numberEdit;
		}

		protected override void OnValueChanged()
		{
			int.TryParse(this.Value, out int currentValue);
			numberEdit.ActuallNumberEdit.Value = currentValue;

			base.OnValueChanged();
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
					this.Value = textEditWidget.Text;
				}
			};

			this.Content = textEditWidget;
		}

		protected override void OnValueChanged()
		{
			if (this.Value != textEditWidget.Text)
			{
				textEditWidget.Text = this.Value;
			}

			base.OnValueChanged();
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

		public string Value { get => uiField.Value; set => uiField.Value = value; }

		public GuiWidget Content { get; private set; }

		public event EventHandler ValueChanged;

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
					uiField.Value = valueLocal;
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

			totalContent.DebugShowBounds = true;

			this.Content = totalContent;
		}
	}
}
