/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public abstract class CanRebuildObject3D : Object3D
	{
		public abstract void Rebuild();
	}

	public class PubicPropertyEditor : IObject3DEditor
	{
		private CanRebuildObject3D item;
		private View3DWidget view3DWidget;
		public string Name => "Property Editor";

		public bool Unlocked { get; } = true;

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.item = item as CanRebuildObject3D;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			if (this.item != null)
			{
				ModifyObject(view3DWidget, mainContainer, theme);
			}

			return mainContainer;
		}

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(CanRebuildObject3D) };

		private static FlowLayoutWidget CreateSettingsRow(string labelText)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5)
			};

			var label = new TextWidget(labelText + ":", pointSize: 11, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 0, 3, 0),
				VAnchor = VAnchor.Center
			};
			rowContainer.AddChild(label);

			rowContainer.AddChild(new HorizontalSpacer());

			return rowContainer;
		}

		private string GetDisplayName(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
			return nameAttribute?.DisplayName ?? prop.Name;
		}

		private void ModifyObject(View3DWidget view3DWidget, FlowLayoutWidget tabContainer, ThemeConfig theme)
		{
			var allowedTypes = new Type[] { typeof(double), typeof(int), typeof(string), typeof(bool) };

			var ownedPropertiesOnly = System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.DeclaredOnly;

			var editableProperties = this.item.GetType().GetProperties(ownedPropertiesOnly)
				.Where(pi => allowedTypes.Contains(pi.PropertyType)
					&& pi.GetGetMethod() != null
					&& pi.GetSetMethod() != null)
				.Select(p => new
				{
					Value = p.GetGetMethod().Invoke(this.item, null),
					DisplayName = GetDisplayName(p),
					PropertyInfo = p
				});

			foreach (var property in editableProperties)
			{
				// create a double editor
				if (property.Value is double doubleValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());
					var doubleEditWidget = new MHNumberEdit(doubleValue, pixelWidth: 50 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true, increment: .05)
					{
						SelectAllOnFocus = true,
						VAnchor = VAnchor.Center
					};
					doubleEditWidget.ActuallNumberEdit.EditComplete += (s, e) =>
					{
						double editValue;
						if (double.TryParse(doubleEditWidget.Text, out editValue))
						{
							property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { editValue });
						}
						item.Rebuild();
					};
					rowContainer.AddChild(doubleEditWidget);
					tabContainer.AddChild(rowContainer);
				}
				// create a int editor
				else if (property.Value is int intValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());
					var intEditWidget = new MHNumberEdit(intValue, pixelWidth: 50 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: false, increment: 1)
					{
						SelectAllOnFocus = true,
						VAnchor = VAnchor.Center
					};
					intEditWidget.ActuallNumberEdit.EditComplete += (s, e) =>
					{
						int editValue;
						if (int.TryParse(intEditWidget.Text, out editValue))
						{
							property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { editValue });
						}
						item.Rebuild();
					};
					rowContainer.AddChild(intEditWidget);
					tabContainer.AddChild(rowContainer);
				}
				// create a bool editor
				else if (property.Value is bool boolValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var doubleEditWidget = new CheckBox("");
					doubleEditWidget.Checked = boolValue;
					doubleEditWidget.CheckedStateChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { doubleEditWidget.Checked });
						item.Rebuild();
					};
					rowContainer.AddChild(doubleEditWidget);
					tabContainer.AddChild(rowContainer);
				}
				// create a string editor
				else if (property.Value is string stringValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());
					var textEditWidget = new MHTextEditWidget(stringValue, pixelWidth: 150 * GuiWidget.DeviceScale)
					{
						SelectAllOnFocus = true,
						VAnchor = VAnchor.Center
					};
					textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { textEditWidget.Text });
						item.Rebuild();
					};
					rowContainer.AddChild(textEditWidget);
					tabContainer.AddChild(rowContainer);
				}
			}

			var updateButton = theme.ButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.Right;
			updateButton.Click += (s, e) =>
			{
				item.Rebuild();
			};
			tabContainer.AddChild(updateButton);
		}
	}
}