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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class EditableProperty
	{
		public IObject3D Item { get; private set; }

		public object source;
		public PropertyInfo PropertyInfo { get; private set; }

		public EditableProperty(PropertyInfo p, object source)
		{
			this.source = source;
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

		public object Value => PropertyInfo.GetGetMethod().Invoke(source, null);

		/// <summary>
		/// Use reflection to set property value
		/// </summary>
		/// <param name="value"></param>
		public void SetValue(object value)
		{
			this.PropertyInfo.GetSetMethod().Invoke(source, new Object[] { value });
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

		private static Type[] allowedTypes =
		{
			typeof(double), typeof(int), typeof(char), typeof(string), typeof(bool),
			typeof(Color),
			typeof(Vector2), typeof(Vector3),
			typeof(DirectionVector), typeof(DirectionAxis),
			typeof(ChildrenSelector),
			typeof(ImageBuffer),
			typeof(List<string>)
		};

		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public GuiWidget Create(IObject3D item, ThemeConfig theme)
		{
			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			// TODO: Long term we should have a solution where editors can extend Draw and Undo without this hack
			var view3DWidget = ApplicationController.Instance.DragDropData.View3DWidget;
			var undoBuffer = view3DWidget.sceneContext.Scene.UndoBuffer;

			if (item is IEditorDraw editorDraw)
			{
				// TODO: Putting the drawing code in the IObject3D means almost certain bindings to MatterControl in IObject3D. If instead
				// we had a UI layer object that used binding to register scene drawing hooks for specific types, we could avoid the bindings
				view3DWidget.InteractionLayer.DrawGlOpaqueContent += editorDraw.DrawEditor;
				mainContainer.Closed += (s, e) =>
				{
					view3DWidget.InteractionLayer.DrawGlOpaqueContent -= editorDraw.DrawEditor;
				};
			}

			if (item != null)
			{
				var context = new PPEContext()
				{
					item = item
				};

				// CreateEditor
				AddUnlockLinkIfRequired(context, mainContainer, theme);

				// Create a field editor for each editable property detected via reflection
				foreach (var property in GetEditablePropreties(context.item))
				{
					var editor = CreatePropertyEditor(property, undoBuffer, context, theme);
					if (editor != null)
					{
						mainContainer.AddChild(editor);
					}
				}

				AddWebPageLinkIfRequired(context, mainContainer, theme);

				// add in an Update button if applicable
				var showUpdate = context.item.GetType().GetCustomAttributes(typeof(ShowUpdateButtonAttribute), true).FirstOrDefault() as ShowUpdateButtonAttribute;
				if (showUpdate != null)
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
						context.item.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					};
					mainContainer.AddChild(updateButton);
				}

				// Init with custom 'UpdateControls' hooks
				(context.item as IPropertyGridModifier)?.UpdateControls(new PublicPropertyChange(context, "Update_Button"));
			}

			return mainContainer;
		}

		private static FlowLayoutWidget CreateSettingsRow(EditableProperty property, UIField field)
		{
			var row = CreateSettingsRow(property.DisplayName.Localize(), property.Description.Localize());
			row.AddChild(field.Content);

			return row;
		}

		public static FlowLayoutWidget CreateSettingsRow(string labelText, string toolTipText = null)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5),
				ToolTipText = toolTipText
			};

			var label = new TextWidget(labelText + ":", pointSize: 11, textColor: AppContext.Theme.TextColor)
			{
				Margin = new BorderDouble(0, 0, 3, 0),
				VAnchor = VAnchor.Center
			};
			rowContainer.AddChild(label);

			rowContainer.AddChild(new HorizontalSpacer());

			return rowContainer;
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
			var column = CreateSettingsColumn(labelText, toolTipText);
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};
			row.AddChild(new HorizontalSpacer());
			row.AddChild(field.Content);
			column.AddChild(row);
			return column;
		}

		private static FlowLayoutWidget CreateSettingsColumn(string labelText, string toolTipText = null)
		{
			var columnContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5),
				ToolTipText = toolTipText
			};

			var label = new TextWidget(labelText + ":", pointSize: 11, textColor: AppContext.Theme.TextColor)
			{
				Margin = new BorderDouble(0, 3, 0, 0),
				HAnchor = HAnchor.Left
			};
			columnContainer.AddChild(label);

			return columnContainer;
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

					//field.Content
					undoBuffer.AddAndDo(new UndoRedoActions(() =>
					{
						property.SetValue(valueFromString(oldValue));
						object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
						propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
					},
					() =>
					{
						property.SetValue(valueFromString(newValue));
						object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
						propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
					}));
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
					if (e.InvalidateType == InvalidateType.Properties)
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

				rowContainer = CreateSettingsRow(property, field);
			}
			else if (propertyValue is Color color)
			{
				var field = new ColorField(theme, object3D.Color);
				field.Initialize(0);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.Color);
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field);
			}
			else if (propertyValue is Vector2 vector2)
			{
				var field = new Vector2Field(theme);
				field.Initialize(0);
				field.Vector2 = vector2;
				RegisterValueChanged(field,
					(valueString) => { return Vector2.Parse(valueString); },
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
				RegisterValueChanged(field,
					(valueString) => { return Vector3.Parse(valueString); },
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
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field);
			}
			else if (propertyValue is DirectionAxis directionAxis)
			{
				rowContainer = CreateSettingsColumn(property);
				var newDirectionVector = new DirectionVector()
				{
					Normal = directionAxis.Normal
				};
				var row1 = CreateSettingsRow("Axis".Localize());
				var field1 = new DirectionVectorField(theme);
				field1.Initialize(0);
				field1.SetValue(newDirectionVector);
				row1.AddChild(field1.Content);

				rowContainer.AddChild(row1);

				// the direction axis
				// the distance from the center of the part
				// create a double editor
				var field2 = new Vector3Field(theme);
				field2.Initialize(0);
				field2.Vector3 = directionAxis.Origin - property.Item.Children.First().GetAxisAlignedBoundingBox().Center;
				var row2 = CreateSettingsColumn("Offset", field2);

				// update this when changed
				EventHandler<InvalidateArgs> updateData = (s, e) =>
				{
					field2.Vector3 = ((DirectionAxis)property.Value).Origin - property.Item.Children.First().GetAxisAlignedBoundingBox().Center;
				};
				property.Item.Invalidated += updateData;
				field2.Content.Closed += (s, e) =>
				{
					property.Item.Invalidated -= updateData;
				};

				// update functions
				field1.ValueChanged += (s, e) =>
				{
					property.SetValue(new DirectionAxis()
					{
						Normal = field1.DirectionVector.Normal,
						Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + field2.Vector3
					});
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};
				field2.ValueChanged += (s, e) =>
				{
					property.SetValue(new DirectionAxis()
					{
						Normal = field1.DirectionVector.Normal,
						Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + field2.Vector3
					});
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer.AddChild(row2);
			}
			else if (propertyValue is ChildrenSelector childSelector)
			{
				var showAsList = property.PropertyInfo.GetCustomAttributes(true).OfType<ShowAsListAttribute>().FirstOrDefault() != null;
				if (showAsList)
				{
					UIField field = new ChildrenSelectorListField(property, theme);

					field.Initialize(0);
					RegisterValueChanged(field,
						(valueString) => 
						{
							var childrenSelector = new ChildrenSelector();
							foreach (var child in valueString.Split(','))
							{
								childrenSelector.Add(child);
							}
							return childrenSelector;
						});

					rowContainer = CreateSettingsRow(property, field);
				}
				else // show the subtarct editor for boolean subtract and subtract and replace
				{
					rowContainer = CreateSettingsColumn(property);
					rowContainer.AddChild(CreateSelector(childSelector, property.Item, theme));
				}
			}
			else if (propertyValue is ImageBuffer imageBuffer)
			{
				rowContainer = CreateSettingsColumn(property);
				rowContainer.AddChild(CreateImageDisplay(imageBuffer, property.Item, theme));
			}
#if !__ANDROID__
			else if (propertyValue is List<string> stringList)
			{
				var selectedItem = ApplicationController.Instance.DragDropData.SceneContext.Scene.SelectedItem;

				var field = new SurfacedEditorsField(theme, selectedItem);
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
					if (e.InvalidateType == InvalidateType.Properties)
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

				rowContainer = CreateSettingsRow(property, field);
			}
			// create a bool editor
			else if (propertyValue is bool boolValue)
			{
				var field = new ToggleboxField(theme);
				field.Initialize(0);
				field.Checked = boolValue;

				RegisterValueChanged(field, 
					(valueString) => { return valueString == "1"; },
					(value) => { return ((bool)(value)) ? "1" : "0"; });
				rowContainer = CreateSettingsRow(property, field);
			}
			// create a string editor
			else if (propertyValue is string stringValue)
			{
				var field = new TextField(theme);
				field.Initialize(0);
				field.SetValue(stringValue, false);
				field.Content.HAnchor = HAnchor.Stretch;
				RegisterValueChanged(field, (valueString) => valueString);
				rowContainer = CreateSettingsRow(property, field);

				var label = rowContainer.Children.First();

				if (field is TextField)
				{
					var spacer = rowContainer.Children.OfType<HorizontalSpacer>().FirstOrDefault();
					spacer.HAnchor = HAnchor.Absolute;
					spacer.Width = Math.Max(0, 100 - label.Width);
				}

			}
			// create a char editor
			else if (propertyValue is char charValue)
			{
				var field = new CharField(theme);
				field.Initialize(0);
				field.SetValue(charValue.ToString(), false);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(Convert.ToChar(field.Value));
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field);
			}
			// create an enum editor
			else if (property.PropertyType.IsEnum)
			{
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
					field = new EnumField(property, theme);
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
					object3D?.Invalidate(new InvalidateArgs(context.item, InvalidateType.Properties, undoBuffer));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field);
			}
			// Use known IObject3D editors
			else if (propertyValue is IObject3D item
				&& ApplicationController.Instance.GetEditorsForType(property.PropertyType)?.FirstOrDefault() is IObject3DEditor iObject3DEditor)
			{
				rowContainer = iObject3DEditor.Create(item, theme);
			}

			// remember the row name and widget
			context.editRows.Add(property.PropertyInfo.Name, rowContainer);

			return rowContainer;
		}

		private static GuiWidget CreateImageDisplay(ImageBuffer imageBuffer, IObject3D parent, ThemeConfig theme)
		{
			return new ImageWidget(imageBuffer);
		}

		private static GuiWidget CreateSelector(ChildrenSelector childSelector, IObject3D parent, ThemeConfig theme)
		{
			GuiWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			void UpdateSelectColors(bool selectionChanged = false)
			{
				foreach (var child in parent.Children.ToList())
				{
					using (child.RebuildLock())
					{
						if (!childSelector.Contains(child.ID)
							|| tabContainer.HasBeenClosed)
						{
							child.Color = new Color(child.WorldColor(), 255);
						}
						else
						{
							child.Color = new Color(child.WorldColor(), 200);
						}

						if (selectionChanged)
						{
							child.Visible = true;
						}
					}
				}
			}

			tabContainer.Closed += (s, e) => UpdateSelectColors();

			var children = parent.Children.ToList();

			Dictionary<ICheckbox, IObject3D> objectChecks = new Dictionary<ICheckbox, IObject3D>();

			List<GuiWidget> radioSiblings = new List<GuiWidget>();
			for (int i = 0; i < children.Count; i++)
			{
				var itemIndex = i;
				var child = children[itemIndex];
				FlowLayoutWidget rowContainer = new FlowLayoutWidget();

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
				ICheckbox checkBox = selectWidget as ICheckbox;

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

						if(parent is MeshWrapperObject3D meshWrapper)
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

			/*
			bool operationApplied = parent.Descendants()
				.Where((obj) => obj.OwnerID == parent.ID)
				.Where((objId) => objId.Mesh != objId.Children.First().Mesh).Any();

			bool selectionHasBeenMade = parent.Descendants()
				.Where((obj) => obj.OwnerID == parent.ID && obj.OutputType == PrintOutputTypes.Hole)
				.Any();

			if (!operationApplied && !selectionHasBeenMade)
			{
				// select the last item
				if (tabContainer.Descendants().Where((d) => d is ICheckbox).Last() is ICheckbox lastCheckBox)
				{
					lastCheckBox.Checked = true;
				}
			}
			else
			{
				updateButton.Enabled = !operationApplied;
			}
			*/

			return tabContainer;
		}

		private void AddUnlockLinkIfRequired(PPEContext context, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			var unlockLink = context.item.GetType().GetCustomAttributes(typeof(UnlockLinkAttribute), true).FirstOrDefault() as UnlockLinkAttribute;
			if (unlockLink != null
				&& !string.IsNullOrEmpty(unlockLink.UnlockPageLink)
				&& !context.item.Persistable)
			{
				var row = CreateSettingsRow(context.item.Persistable ? "Registered".Localize() : "Demo Mode".Localize());

				var detailsLink = new TextIconButton("Unlock".Localize(), AggContext.StaticData.LoadIcon("locked.png", 16, 16, theme.InvertIcons), theme)
				{
					Margin = 5
				};
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.Instance.LaunchBrowser(UnlockLinkAttribute.UnlockPageBaseUrl + unlockLink.UnlockPageLink);
				};
				row.AddChild(detailsLink);
				theme.ApplyPrimaryActionStyle(detailsLink);

				editControlsContainer.AddChild(row);
			}
		}

		private void AddWebPageLinkIfRequired(PPEContext context, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			var unlockLink = context.item.GetType().GetCustomAttributes(typeof(WebPageLinkAttribute), true).FirstOrDefault() as WebPageLinkAttribute;
			if (unlockLink != null)
			{
				var row = CreateSettingsRow("Website".Localize());

				var detailsLink = new TextIconButton(unlockLink.Name.Localize(), AggContext.StaticData.LoadIcon("internet.png", 16, 16, theme.InvertIcons), theme)
				{
					BackgroundColor = theme.MinimalShade,
					ToolTipText = unlockLink.Url,
				};
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.Instance.LaunchBrowser(unlockLink.Url);
				};
				row.AddChild(detailsLink);
				editControlsContainer.AddChild(row);
			}
		}
	}
}