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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class EditableProperty
	{
		public IObject3D Item { get; private set; }

		public object Source { get; private set; }

		public PropertyInfo PropertyInfo { get; private set; }

		public EditableProperty(PropertyInfo p, object source)
		{
			this.Source = source;
			this.Item = source as IObject3D;
			this.PropertyInfo = p;
		}

		private string GetDescription(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DescriptionAttribute>().FirstOrDefault();
			return nameAttribute?.Description ?? null;
		}

		public static string GetDisplayName(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
			return nameAttribute?.DisplayName ?? prop.Name.SplitCamelCase();
		}

		public object Value => PropertyInfo.GetGetMethod().Invoke(Source, null);

		/// <summary>
		/// Use reflection to set property value.
		/// </summary>
		/// <param name="value">The value to set through reflection.</param>
		public void SetValue(object value)
		{
			this.PropertyInfo.GetSetMethod().Invoke(Source, new object[] { value });
		}

		public string DisplayName => GetDisplayName(PropertyInfo);

		public string Description => GetDescription(PropertyInfo);

		public Type PropertyType => PropertyInfo.PropertyType;
	}

	public class PublicPropertyEditor : IObject3DEditor
	{
		public string Name => "Property Editor";

		public bool Unlocked { get; } = true;

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(IObject3D) };

		private static readonly Type[] allowedTypes =
		{
			typeof(double), typeof(int), typeof(char), typeof(string), typeof(bool),
			typeof(Color),
			typeof(Vector2), typeof(Vector3),
			typeof(DirectionVector), typeof(DirectionAxis),
			typeof(SelectedChildren),
			typeof(ImageBuffer),
			typeof(List<string>)
		};

		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public GuiWidget Create(IObject3D item, UndoBuffer undoBuffer, ThemeConfig theme)
		{
			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			if (item != null)
			{
				var context = new PPEContext()
				{
					item = item
				};

				// CreateEditor
				AddUnlockLinkIfRequired(context.item, mainContainer, theme);

				GuiWidget scope = mainContainer;

				// Create a field editor for each editable property detected via reflection
				foreach (var property in GetEditablePropreties(context.item))
				{
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<HideFromEditorAttribute>().Any())
					{
						continue;
					}

					// Create SectionWidget for SectionStartAttributes
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<SectionStartAttribute>().FirstOrDefault() is SectionStartAttribute sectionStart)
					{
						var column = new FlowLayoutWidget()
						{
							FlowDirection = FlowDirection.TopToBottom,
							Padding = new BorderDouble(theme.DefaultContainerPadding).Clone(top: 0)
						};

						var section = new SectionWidget(sectionStart.Title, column, theme);
						theme.ApplyBoxStyle(section);

						mainContainer.AddChild(section);

						scope = column;
					}

					// Create SectionWidget for SectionStartAttributes
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<SectionEndAttribute>().Any())
					{
						// Push scope back to mainContainer on
						scope = mainContainer;
					}

					var editor = CreatePropertyEditor(property, undoBuffer, context, theme);
					if (editor != null)
					{
						scope.AddChild(editor);
					}
				}

				AddWebPageLinkIfRequired(context, mainContainer, theme);

				// add in an Update button if applicable
				if (context.item.GetType().GetCustomAttributes(typeof(ShowUpdateButtonAttribute), true).FirstOrDefault() is ShowUpdateButtonAttribute showUpdate)
				{
					var updateButton = new TextButton("Update".Localize(), theme)
					{
						Margin = 5,
						BackgroundColor = theme.MinimalShade,
						HAnchor = HAnchor.Right,
						VAnchor = VAnchor.Absolute
					};
					updateButton.Click += (s, e) =>
					{
						context.item.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					};
					mainContainer.AddChild(updateButton);
				}

				// Init with custom 'UpdateControls' hooks
				(context.item as IPropertyGridModifier)?.UpdateControls(new PublicPropertyChange(context, "Update_Button"));
			}

			return mainContainer;
		}

		private static GuiWidget CreateSettingsRow(EditableProperty property, UIField field, ThemeConfig theme)
		{
			return new SettingsRow(property.DisplayName.Localize(), property.Description.Localize(), field.Content, theme);
		}

		private static FlowLayoutWidget CreateSettingsColumn(EditableProperty property, UIField field)
		{
			return CreateSettingsColumn(property.DisplayName.Localize(), field, property.Description.Localize());
		}

		private static FlowLayoutWidget CreateSettingsColumn(EditableProperty property)
		{
			return CreateSettingsColumn(property.DisplayName.Localize(), property.Description.Localize());
		}

		private static FlowLayoutWidget CreateSettingsColumn(string labelText, UIField field, string toolTipText = null)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};
			row.AddChild(new HorizontalSpacer());
			row.AddChild(field.Content);

			var column = CreateSettingsColumn(labelText, toolTipText);
			column.AddChild(row);

			return column;
		}

		private static FlowLayoutWidget CreateSettingsColumn(string labelText, string toolTipText = null)
		{
			var theme = AppContext.Theme;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(9, 5, 5, 5), // Use hard-coded 9 pixel left margin to match SettingsRow
				ToolTipText = toolTipText
			};

			var label = SettingsRow.CreateSettingsLabel(labelText, toolTipText, theme.TextColor);
			label.VAnchor = VAnchor.Absolute;
			label.HAnchor = HAnchor.Left;

			column.AddChild(label);

			return column;
		}

		public static IEnumerable<EditableProperty> GetEditablePropreties(IObject3D item)
		{
			return item.GetType().GetProperties(OwnedPropertiesOnly)
				.Where(pi => (allowedTypes.Contains(pi.PropertyType) || pi.PropertyType.IsEnum)
					&& pi.GetGetMethod() != null
					&& pi.GetSetMethod() != null)
				.Select(p => new EditableProperty(p, item));
		}

		public static GuiWidget CreatePropertyEditor(EditableProperty property, UndoBuffer undoBuffer, PPEContext context, ThemeConfig theme)
		{
			var object3D = property.Item;
			var propertyGridModifier = property.Item as IPropertyGridModifier;

			GuiWidget rowContainer = null;

			// Get reflected property value once, then test for each case below
			var propertyValue = property.Value;

			void RegisterValueChanged(UIField field, Func<string, object> valueFromString, Func<object, string> valueToString = null)
			{
				field.ValueChanged += (s, e) =>
				{
					var newValue = field.Value;
					var oldValue = property.Value.ToString();
					if (valueToString != null)
					{
						oldValue = valueToString(property.Value);
					}

					// field.Content
					if (undoBuffer != null)
					{
						undoBuffer.AddAndDo(new UndoRedoActions(() =>
						{
							property.SetValue(valueFromString(oldValue));
							object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
							propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
						},
						() =>
						{
							property.SetValue(valueFromString(newValue));
							object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
							propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
						}));
					}
					else
					{
						property.SetValue(valueFromString(newValue));
						object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
						propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
					}
				};
			}

			// create a double editor
			if (propertyValue is double doubleValue)
			{
				var field = new DoubleField(theme);
				field.Initialize(0);
				field.DoubleValue = doubleValue;
				RegisterValueChanged(field, (valueString) => { return double.Parse(valueString); });

				void RefreshField(object s, InvalidateArgs e)
				{
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						double newValue = (double)property.Value;
						if (newValue != field.DoubleValue)
						{
							field.DoubleValue = newValue;
						}
					}
				}

				object3D.Invalidated += RefreshField;
				field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is Color color)
			{
				var field = new ColorField(theme, object3D.Color);
				field.Initialize(0);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.Color);
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is Vector2 vector2)
			{
				var field = new Vector2Field(theme);
				field.Initialize(0);
				field.Vector2 = vector2;
				RegisterValueChanged(field,
					(valueString) => Vector2.Parse(valueString),
					(value) =>
					{
						var s = ((Vector2)value).ToString();
						return s.Substring(1, s.Length - 2);
					});
				rowContainer = CreateSettingsColumn(property, field);
			}
			else if (propertyValue is Vector3 vector3)
			{
				var field = new Vector3Field(theme);
				field.Initialize(0);
				field.Vector3 = vector3;

				RegisterValueChanged(
					field,
					(valueString) => Vector3.Parse(valueString),
					(value) =>
					{
						var s = ((Vector3)value).ToString();
						return s.Substring(1, s.Length - 2);
					});

				rowContainer = CreateSettingsColumn(property, field);
			}
			else if (propertyValue is DirectionVector directionVector)
			{
				var field = new DirectionVectorField(theme);
				field.Initialize(0);
				field.SetValue(directionVector);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.DirectionVector);
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is DirectionAxis directionAxis)
			{
				rowContainer = CreateSettingsColumn(property);

				var field1 = new DirectionVectorField(theme);
				field1.Initialize(0);
				field1.SetValue(new DirectionVector()
				{
					Normal = directionAxis.Normal
				});

				rowContainer.AddChild(new SettingsRow("Axis".Localize(), null, field1.Content, theme));

				// the direction axis
				// the distance from the center of the part
				// create a double editor
				var field2 = new Vector3Field(theme);
				field2.Initialize(0);
				field2.Vector3 = directionAxis.Origin - property.Item.Children.First().GetAxisAlignedBoundingBox().Center;

				var row2 = CreateSettingsColumn("Offset".Localize(), field2);

				// update this when changed
				void UpdateData(object s, InvalidateArgs e)
				{
					field2.Vector3 = ((DirectionAxis)property.Value).Origin - property.Item.Children.First().GetAxisAlignedBoundingBox().Center;
				}

				property.Item.Invalidated += UpdateData;
				field2.Content.Closed += (s, e) =>
				{
					property.Item.Invalidated -= UpdateData;
				};

				// update functions
				field1.ValueChanged += (s, e) =>
				{
					property.SetValue(new DirectionAxis()
					{
						Normal = field1.DirectionVector.Normal,
						Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + field2.Vector3
					});
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};
				field2.ValueChanged += (s, e) =>
				{
					property.SetValue(new DirectionAxis()
					{
						Normal = field1.DirectionVector.Normal,
						Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + field2.Vector3
					});
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer.AddChild(row2);
			}
			else if (propertyValue is SelectedChildren childSelector)
			{
				var showAsList = property.PropertyInfo.GetCustomAttributes(true).OfType<ShowAsListAttribute>().FirstOrDefault() != null;
				if (showAsList)
				{
					UIField field = new ChildrenSelectorListField(property, theme);

					field.Initialize(0);
					RegisterValueChanged(field,
						(valueString) =>
						{
							var childrenSelector = new SelectedChildren();
							foreach (var child in valueString.Split(','))
							{
								childrenSelector.Add(child);
							}

							return childrenSelector;
						});

					rowContainer = CreateSettingsRow(property, field, theme);
				}
				else // show the subtract editor for boolean subtract and subtract and replace
				{
					rowContainer = CreateSettingsColumn(property);
					if (property.Item is OperationSourceContainerObject3D sourceContainer)
					{
						rowContainer.AddChild(CreateSourceChildSelector(childSelector, sourceContainer, theme));
					}
					else
					{
						rowContainer.AddChild(CreateSelector(childSelector, property.Item, theme));
					}
				}
			}
			else if (propertyValue is ImageBuffer imageBuffer)
			{
				rowContainer = CreateSettingsColumn(property);
				rowContainer.AddChild(new ImageWidget(imageBuffer));
			}
#if !__ANDROID__
			else if (propertyValue is List<string> stringList)
			{
				var field = new SurfacedEditorsField(theme, property.Item);
				field.Initialize(0);
				field.ListValue = stringList;
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.ListValue);
				};

				rowContainer = CreateSettingsColumn(property, field);

				rowContainer.Descendants<HorizontalSpacer>().FirstOrDefault()?.Close();
			}
#endif
			// create a int editor
			else if (propertyValue is int intValue)
			{
				var field = new IntField(theme);
				field.Initialize(0);
				field.IntValue = intValue;
				RegisterValueChanged(field, (valueString) => { return int.Parse(valueString); });

				void RefreshField(object s, InvalidateArgs e)
				{
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						int newValue = (int)property.Value;
						if (newValue != field.IntValue)
						{
							field.IntValue = newValue;
						}
					}
				}

				object3D.Invalidated += RefreshField;
				field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is bool boolValue)
			{
				// create a bool editor
				var field = new ToggleboxField(theme);
				field.Initialize(0);
				field.Checked = boolValue;

				RegisterValueChanged(field,
					(valueString) => { return valueString == "1"; },
					(value) => { return ((bool)value) ? "1" : "0"; });
				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is string stringValue)
			{
				// create a string editor
				var field = new TextField(theme);
				field.Initialize(0);
				field.SetValue(stringValue, false);
				field.Content.HAnchor = HAnchor.Stretch;
				RegisterValueChanged(field, (valueString) => valueString);
				rowContainer = CreateSettingsRow(property, field, theme);

				var label = rowContainer.Children.First();

				if (field is TextField)
				{
					var spacer = rowContainer.Children.OfType<HorizontalSpacer>().FirstOrDefault();
					spacer.HAnchor = HAnchor.Absolute;
					spacer.Width = Math.Max(0, 100 - label.Width);
				}
			}
			else if (propertyValue is char charValue)
			{
				// create a char editor
				var field = new CharField(theme);
				field.Initialize(0);
				field.SetValue(charValue.ToString(), false);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(Convert.ToChar(field.Value));
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (property.PropertyType.IsEnum)
			{
				// create an enum editor
				UIField field;
				var iconsAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<IconsAttribute>().FirstOrDefault();
				if (iconsAttribute != null)
				{
					field = new IconEnumField(property, iconsAttribute, theme)
					{
						InitialValue = propertyValue.ToString()
					};
				}
				else
				{
					if (property.PropertyType == typeof(NamedTypeFace))
					{
						field = new FontSelectorField(property, theme);
					}
					else
					{
						field = new EnumField(property, theme);
					}
				}

				field.Initialize(0);
				RegisterValueChanged(field,
					(valueString) =>
					{
						return Enum.Parse(property.PropertyType, valueString);
					});

				field.ValueChanged += (s, e) =>
				{
					property.SetValue(Enum.Parse(property.PropertyType, field.Value));
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field, theme);
			}
			else if (propertyValue is IObject3D item
				&& ApplicationController.Instance.Extensions.GetEditorsForType(property.PropertyType)?.FirstOrDefault() is IObject3DEditor iObject3DEditor)
			{
				// Use known IObject3D editors
				rowContainer = iObject3DEditor.Create(item, undoBuffer, theme);
			}

			// remember the row name and widget
			context.editRows.Add(property.PropertyInfo.Name, rowContainer);

			return rowContainer;
		}

		private static GuiWidget CreateSourceChildSelector(SelectedChildren childSelector, OperationSourceContainerObject3D sourceContainer, ThemeConfig theme)
		{
			GuiWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var parentOfSubtractTargets = sourceContainer.SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

			var sourceChildren = parentOfSubtractTargets.Children.ToList();

			var objectChecks = new Dictionary<ICheckbox, IObject3D>();

			var radioSiblings = new List<GuiWidget>();
			for (int i = 0; i < sourceChildren.Count; i++)
			{
				var itemIndex = i;
				var child = sourceChildren[itemIndex];
				var rowContainer = new FlowLayoutWidget();

				GuiWidget selectWidget;
				if (sourceChildren.Count == 2)
				{
					var radioButton = new RadioButton(string.IsNullOrWhiteSpace(child.Name) ? $"{itemIndex}" : $"{child.Name}")
					{
						Checked = childSelector.Contains(child.ID),
						TextColor = theme.TextColor
					};
					radioSiblings.Add(radioButton);
					radioButton.SiblingRadioButtonList = radioSiblings;
					selectWidget = radioButton;
				}
				else
				{
					selectWidget = new CheckBox(string.IsNullOrWhiteSpace(child.Name) ? $"{itemIndex}" : $"{child.Name}")
					{
						Checked = childSelector.Contains(child.ID),
						TextColor = theme.TextColor
					};
				}

				objectChecks.Add((ICheckbox)selectWidget, child);

				rowContainer.AddChild(selectWidget);
				var checkBox = selectWidget as ICheckbox;

				checkBox.CheckedStateChanged += (s, e) =>
				{
					if (s is ICheckbox checkbox)
					{
						if (checkBox.Checked)
						{
							if (!childSelector.Contains(objectChecks[checkbox].ID))
							{
								childSelector.Add(objectChecks[checkbox].ID);
							}
						}
						else
						{
							if (childSelector.Contains(objectChecks[checkbox].ID))
							{
								childSelector.Remove(objectChecks[checkbox].ID);
							}
						}
					}
				};

				tabContainer.AddChild(rowContainer);
			}

			return tabContainer;
		}

		private static GuiWidget CreateSelector(SelectedChildren childSelector, IObject3D parent, ThemeConfig theme)
		{
			GuiWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			void UpdateSelectColors(bool selectionChanged = false)
			{
				foreach (var child in parent.Children.ToList())
				{
					using (child.RebuildLock())
					{
						if (selectionChanged)
						{
							child.Visible = true;
						}
					}
				}
			}

			tabContainer.Closed += (s, e) => UpdateSelectColors();

			var children = parent.Children.ToList();

			var objectChecks = new Dictionary<ICheckbox, IObject3D>();

			var radioSiblings = new List<GuiWidget>();
			for (int i = 0; i < children.Count; i++)
			{
				var itemIndex = i;
				var child = children[itemIndex];
				var rowContainer = new FlowLayoutWidget();

				GuiWidget selectWidget;
				if (children.Count == 2)
				{
					var radioButton = new RadioButton(string.IsNullOrWhiteSpace(child.Name) ? $"{itemIndex}" : $"{child.Name}")
					{
						Checked = childSelector.Contains(child.ID),
						TextColor = theme.TextColor
					};
					radioSiblings.Add(radioButton);
					radioButton.SiblingRadioButtonList = radioSiblings;
					selectWidget = radioButton;
				}
				else
				{
					selectWidget = new CheckBox(string.IsNullOrWhiteSpace(child.Name) ? $"{itemIndex}" : $"{child.Name}")
					{
						Checked = childSelector.Contains(child.ID),
						TextColor = theme.TextColor
					};
				}

				objectChecks.Add((ICheckbox)selectWidget, child);

				rowContainer.AddChild(selectWidget);
				var checkBox = selectWidget as ICheckbox;

				checkBox.CheckedStateChanged += (s, e) =>
				{
					if (s is ICheckbox checkbox)
					{
						if (checkBox.Checked)
						{
							if (!childSelector.Contains(objectChecks[checkbox].ID))
							{
								childSelector.Add(objectChecks[checkbox].ID);
							}
						}
						else
						{
							if (childSelector.Contains(objectChecks[checkbox].ID))
							{
								childSelector.Remove(objectChecks[checkbox].ID);
							}
						}

						if (parent is MeshWrapperObject3D meshWrapper)
						{
							using (meshWrapper.RebuildLock())
							{
								meshWrapper.ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);
							}
						}

						UpdateSelectColors(true);
					}
				};

				tabContainer.AddChild(rowContainer);
				UpdateSelectColors();
			}

			return tabContainer;
		}

		public static void AddUnlockLinkIfRequired(IObject3D item, GuiWidget editControlsContainer, ThemeConfig theme)
		{
			var unlockUrl = ApplicationController.Instance.GetUnlockPage?.Invoke(item);
			if (!item.Persistable
				&& !string.IsNullOrEmpty(unlockUrl))
			{
				editControlsContainer.AddChild(GetUnlockRow(theme, unlockUrl));
			}
		}

		public static GuiWidget GetUnlockRow(ThemeConfig theme, string unlockLinkUrl)
		{
			var detailsLink = new TextIconButton("Unlock".Localize(), AggContext.StaticData.LoadIcon("locked.png", 16, 16, theme.InvertIcons), theme)
			{
				Margin = 5,
				ToolTipText = "Visit MatterHackers.com to Purchase".Localize()
			};
			detailsLink.Click += (s, e) =>
			{
				ApplicationController.Instance.LaunchBrowser(unlockLinkUrl);
			};
			theme.ApplyPrimaryActionStyle(detailsLink);

			return new SettingsRow("Demo Mode".Localize(), null, detailsLink, theme);
		}

		private void AddWebPageLinkIfRequired(PPEContext context, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			if (context.item.GetType().GetCustomAttributes(typeof(WebPageLinkAttribute), true).FirstOrDefault() is WebPageLinkAttribute unlockLink)
			{
				var detailsLink = new TextIconButton(unlockLink.Name.Localize(), AggContext.StaticData.LoadIcon("internet.png", 16, 16, theme.InvertIcons), theme)
				{
					BackgroundColor = theme.MinimalShade,
					ToolTipText = unlockLink.Url,
				};
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.Instance.LaunchBrowser(unlockLink.Url);
				};

				// website row
				editControlsContainer.AddChild(new SettingsRow("Website".Localize(), null, detailsLink, theme));
			}
		}
	}
}