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
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public interface IRebuildable
	{
		void Rebuild();
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class SortableAttribute : Attribute
	{
	}

	public class PublicPropertyEditor : IObject3DEditor
	{
		private IObject3D item;
		private View3DWidget view3DWidget;
		public string Name => "Property Editor";

		public bool Unlocked { get; } = true;

		private static Type[] allowedTypes = 
		{
			typeof(double), typeof(int), typeof(string), typeof(bool), typeof(DirectionVector), typeof(DirectionAxis)
		};

		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.item = item;

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

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(IRebuildable) };

		private static FlowLayoutWidget CreateSettingsRow(string labelText, string toolTipText = null)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5),
				ToolTipText = toolTipText
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

		private string GetDescription(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DescriptionAttribute>().FirstOrDefault();
			return nameAttribute?.Description ?? null;
		}

		private void ModifyObject(View3DWidget view3DWidget, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			var rebuildable = item as IRebuildable;

			var editableProperties = this.item.GetType().GetProperties(OwnedPropertiesOnly)
				.Where(pi => (allowedTypes.Contains(pi.PropertyType) || pi.PropertyType.IsEnum)
					&& pi.GetGetMethod() != null
					&& pi.GetSetMethod() != null)
				.Select(p => new
				{
					Value = p.GetGetMethod().Invoke(this.item, null),
					DisplayName = GetDisplayName(p),
					Description = GetDescription(p),
					PropertyType = p.PropertyType,
					PropertyInfo = p
				});

			foreach (var property in editableProperties)
			{
				// create a double editor
				if (property.Value is double doubleValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var field = new DoubleField();
					field.Initialize(0);
					field.DoubleValue = doubleValue;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { field.DoubleValue });
						rebuildable?.Rebuild();
					};

					rowContainer.AddChild(field.Content);
					editControlsContainer.AddChild(rowContainer);
				}
				else if (property.Value is Vector2 vector2)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var field = new Vector2Field();
					field.Initialize(0);
					field.Vector2 = vector2;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { field.Vector2 });
						rebuildable?.Rebuild();
					};

					rowContainer.AddChild(field.Content);
					editControlsContainer.AddChild(rowContainer);
				}
				else if (property.Value is Vector3 vector3)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var field = new Vector3Field();
					field.Initialize(0);
					field.Vector3 = vector3;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { field.Vector3 });
						rebuildable?.Rebuild();
					};

					rowContainer.AddChild(field.Content);
					editControlsContainer.AddChild(rowContainer);
				}
				else if (property.Value is DirectionVector directionVector)
				{
					bool simpleEdit = true;
					if (simpleEdit)
					{
						FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

						var dropDownList = new DropDownList("Name".Localize(), theme.Colors.PrimaryTextColor, Direction.Down, pointSize: theme.DefaultFontSize);

						var orderedItems = new string[] { "Right", "Back", "Up" };

						foreach (var orderItem in orderedItems)
						{
							MenuItem newItem = dropDownList.AddItem(orderItem);

							var localOredrItem = orderItem;
							newItem.Selected += (sender, e) =>
							{
								switch(dropDownList.SelectedValue)
								{
									case "Right":
										property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionVector() { Normal = Vector3.UnitX } });
										break;
									case "Back":
										property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionVector() { Normal = Vector3.UnitY } });
										break;
									case "Up":
										property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionVector() { Normal = Vector3.UnitZ } });
										break;
								}

								rebuildable?.Rebuild();
							};
						}

						dropDownList.SelectedLabel = "Right";
						rowContainer.AddChild(dropDownList);
						editControlsContainer.AddChild(rowContainer);
					}
					else // edit the vector
					{
						FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

						var field = new Vector3Field();
						field.Initialize(0);
						field.Vector3 = directionVector.Normal;
						field.ValueChanged += (s, e) =>
						{
							property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionVector() { Normal = field.Vector3 } });
							rebuildable?.Rebuild();
						};

						rowContainer.AddChild(field.Content);
						editControlsContainer.AddChild(rowContainer);
					}
				}
				else if (property.Value is DirectionAxis directionAxis)
				{
					// add in the position
					FlowLayoutWidget originRowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var originField = new Vector3Field();
					originField.Initialize(0);
					originField.Vector3 = directionAxis.Origin;

					var normalField = new Vector3Field();
					normalField.Initialize(0);
					normalField.Vector3 = directionAxis.Normal;

					originField.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionAxis() { Origin = originField.Vector3, Normal = normalField.Vector3 } });
						rebuildable?.Rebuild();
					};

					originRowContainer.AddChild(originField.Content);
					editControlsContainer.AddChild(originRowContainer);

					// add in the direction
					FlowLayoutWidget directionRowContainer = CreateSettingsRow(property.DisplayName.Localize());

					normalField.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { new DirectionAxis() { Origin = originField.Vector3, Normal = normalField.Vector3 } });
						rebuildable?.Rebuild();
					};

					directionRowContainer.AddChild(normalField.Content);
					editControlsContainer.AddChild(directionRowContainer);

					// update tihs when changed
					EventHandler updateData = (object s, EventArgs e) =>
					{
						originField.Vector3 = ((DirectionAxis)property.PropertyInfo.GetGetMethod().Invoke(this.item, null)).Origin;
					};
					item.Invalidated += updateData;
					editControlsContainer.Closed += (s, e) =>
					{
						item.Invalidated -= updateData;
					};
				}
				// create a int editor
				else if (property.Value is int intValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var field = new IntField();
					field.Initialize(0);
					field.IntValue = intValue;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { field.IntValue });
						rebuildable?.Rebuild();
					};

					rowContainer.AddChild(field.Content);
					editControlsContainer.AddChild(rowContainer);
				}
				// create a bool editor
				else if (property.Value is bool boolValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize(), property.Description.Localize());

					var field = new ToggleboxField(ApplicationController.Instance.Theme.Colors.PrimaryTextColor);
					field.Initialize(0);
					field.Checked = boolValue;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { field.Checked });
						rebuildable?.Rebuild();
					};

					rowContainer.AddChild(field.Content);
					editControlsContainer.AddChild(rowContainer);
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
						rebuildable?.Rebuild();
					};
					rowContainer.AddChild(textEditWidget);
					editControlsContainer.AddChild(rowContainer);
				}
				// create a enum editor
				else if (property.PropertyType.IsEnum)
				{
					// Enum keyed on name to friendly name
					var enumItems = Enum.GetNames(property.PropertyType).Select(enumName =>
					{
						return new
						{
							Key = enumName,
							Value = enumName.Replace('_', ' ')
						};
					});

					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var dropDownList = new DropDownList("Name".Localize(), theme.Colors.PrimaryTextColor, Direction.Down, pointSize: theme.DefaultFontSize);

					var sortableAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<SortableAttribute>().FirstOrDefault();
					var orderedItems = sortableAttribute != null ? enumItems.OrderBy(n => n.Value) : enumItems;

					foreach (var orderItem in orderedItems)
					{
						MenuItem newItem = dropDownList.AddItem(orderItem.Value);

						var localOredrItem = orderItem;
						newItem.Selected += (sender, e) =>
						{
							property.PropertyInfo.GetSetMethod().Invoke(
								this.item,
								new Object[] { Enum.Parse(property.PropertyType, localOredrItem.Key) });
							rebuildable?.Rebuild();
						};
					}

					dropDownList.SelectedLabel = property.Value.ToString().Replace('_', ' ');
					rowContainer.AddChild(dropDownList);
					editControlsContainer.AddChild(rowContainer);
				}
			}

			var updateButton = theme.ButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.Right;
			updateButton.Click += (s, e) =>
			{
				rebuildable?.Rebuild();
			};
			editControlsContainer.AddChild(updateButton);
		}
	}
}