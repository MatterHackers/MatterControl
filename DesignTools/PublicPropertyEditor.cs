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
		public PropertyInfo PropertyInfo { get; private set; }
		public EditableProperty(PropertyInfo p, IObject3D item)
		{
			this.Item = item;
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

		public object Value => PropertyInfo.GetGetMethod().Invoke(Item, null);
		public string DisplayName => GetDisplayName(PropertyInfo);
		public string Description => GetDescription(PropertyInfo);
		public Type PropertyType => PropertyInfo.PropertyType;
	}

	public class PublicPropertyEditor : IObject3DEditor
	{
		public string Name => "Property Editor";

		public bool Unlocked { get; } = true;

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(IPublicPropertyObject) };

		private static Type[] allowedTypes =
		{
			typeof(double), typeof(int), typeof(char), typeof(string), typeof(bool),
			typeof(Vector2), typeof(Vector3),
			typeof(DirectionVector), typeof(DirectionAxis),
			typeof(ChildrenSelector)
		};

		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			var context = new PPEContext()
			{
				view3DWidget = view3DWidget,
				item = item
			};

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			if(item is IEditorDraw editorDraw)
			{
				view3DWidget.InteractionLayer.DrawGlOpaqueContent += editorDraw.DrawEditor;
				mainContainer.Closed += (s, e) =>
				{
					view3DWidget.InteractionLayer.DrawGlOpaqueContent -= editorDraw.DrawEditor;
				};
			}

			if (context.item != null)
			{
				this.CreateEditor(context, view3DWidget, mainContainer, theme);
			}

			return mainContainer;
		}

		private static FlowLayoutWidget CreateSettingsRow(EditableProperty property, GuiWidget widget = null)
		{
			var row = CreateSettingsRow(property.DisplayName.Localize(), property.Description.Localize());

			if (widget != null)
			{ 
				row.AddChild(widget);
			}

			return row;
		}

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

		private static FlowLayoutWidget CreateSettingsColumn(EditableProperty property)
		{
			return CreateSettingsColumn(property.DisplayName.Localize(), property.Description.Localize());
		}

		private static FlowLayoutWidget CreateSettingsColumn(string labelText, string toolTipText = null)
		{
			var columnContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5),
				ToolTipText = toolTipText
			};

			var label = new TextWidget(labelText + ":", pointSize: 11, textColor: ActiveTheme.Instance.PrimaryTextColor)
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

		private void CreateEditor(PPEContext context, View3DWidget view3DWidget, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			var undoBuffer = view3DWidget.sceneContext.Scene.UndoBuffer;

			var rebuildable = context.item as IPublicPropertyObject;
			var propertyGridModifier = context.item as IPropertyGridModifier;

			var editableProperties = GetEditablePropreties(context.item);

			AddWebPageLinkIfRequired(context, editControlsContainer, theme);
			AddUnlockLinkIfRequired(context, editControlsContainer, theme);

			foreach (var property in editableProperties)
			{
				AddPropertyEditor(this, view3DWidget, editControlsContainer, theme, undoBuffer, rebuildable, propertyGridModifier, property, context);
			}

			var hideUpdate = context.item.GetType().GetCustomAttributes(typeof(HideUpdateButtonAttribute), true).FirstOrDefault() as HideUpdateButtonAttribute;
			if (hideUpdate == null)
			{
				var updateButton = theme.ButtonFactory.Generate("Update".Localize());
				updateButton.Margin = new BorderDouble(5);
				updateButton.HAnchor = HAnchor.Right;
				updateButton.Click += (s, e) =>
				{
					rebuildable?.Rebuild(undoBuffer);
				};
				editControlsContainer.AddChild(updateButton);
			}

			// make sure the ui is set right to start
			propertyGridModifier?.UpdateControls(context);
		}

		private static void AddPropertyEditor(PublicPropertyEditor publicPropertyEditor,
			View3DWidget view3DWidget, FlowLayoutWidget editControlsContainer, ThemeConfig theme,
			UndoBuffer undoBuffer, IPublicPropertyObject rebuildable, IPropertyGridModifier propertyGridModifier,
			EditableProperty property, PPEContext context)
		{
			GuiWidget rowContainer = null;

			// create a double editor
			if (property.Value is double doubleValue)
			{
				var field = new DoubleField();
				field.Initialize(0);
				field.DoubleValue = doubleValue;
				field.ValueChanged += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { field.DoubleValue });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, field.Content);
			}
			else if (property.Value is Vector2 vector2)
			{
				var field = new Vector2Field();
				field.Initialize(0);
				field.Vector2 = vector2;
				field.ValueChanged += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { field.Vector2 });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, field.Content);
			}
			else if (property.Value is Vector3 vector3)
			{
				var field = new Vector3Field();
				field.Initialize(0);
				field.Vector3 = vector3;
				field.ValueChanged += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { field.Vector3 });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, field.Content);
			}
			else if (property.Value is DirectionVector directionVector)
			{
				bool simpleEdit = true;
				if (simpleEdit)
				{
					var dropDownList = new DropDownList("Name".Localize(), theme.Colors.PrimaryTextColor, Direction.Down, pointSize: theme.DefaultFontSize)
					{
						BorderColor = theme.GetBorderColor(75)
					};

					foreach (var orderItem in new string[] { "Right", "Back", "Up" })
					{
						MenuItem newItem = dropDownList.AddItem(orderItem);

						var localOredrItem = orderItem;
						newItem.Selected += (sender, e) =>
						{
							switch (dropDownList.SelectedValue)
							{
								case "Right":
									property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionVector() { Normal = Vector3.UnitX } });
									break;
								case "Back":
									property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionVector() { Normal = Vector3.UnitY } });
									break;
								case "Up":
									property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionVector() { Normal = Vector3.UnitZ } });
									break;
							}

							rebuildable?.Rebuild(undoBuffer);
							propertyGridModifier?.UpdateControls(context);
						};
					}

					dropDownList.SelectedLabel = "Right";

					rowContainer = CreateSettingsRow(property, dropDownList);

				}
				else // edit the vector
				{
					var field = new Vector3Field();
					field.Initialize(0);
					field.Vector3 = directionVector.Normal;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionVector() { Normal = field.Vector3 } });
						rebuildable?.Rebuild(undoBuffer);
						propertyGridModifier?.UpdateControls(context);
					};

					rowContainer = CreateSettingsRow(property, field.Content);
				}
			}
			else if (property.Value is DirectionAxis directionAxis)
			{
				bool simpleAxis = true;
				if (simpleAxis)
				{
					// the direction axis
					// the distance from the center of the part
					// create a double editor
					var field = new DoubleField();
					field.Initialize(0);
					field.DoubleValue = directionAxis.Origin.X - property.Item.Children.First().GetAxisAlignedBoundingBox().Center.X;
					field.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[]
						{
							new DirectionAxis()
							{
								Normal = Vector3.UnitZ, Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + new Vector3(field.DoubleValue, 0, 0)
							}
						});
						rebuildable?.Rebuild(undoBuffer);
						propertyGridModifier?.UpdateControls(context);
					};

					// update tihs when changed
					EventHandler<InvalidateArgs> updateData = (s, e) =>
					{
						field.DoubleValue = ((DirectionAxis)property.PropertyInfo.GetGetMethod().Invoke(property.Item, null)).Origin.X - property.Item.Children.First().GetAxisAlignedBoundingBox().Center.X;
					};
					property.Item.Invalidated += updateData;
					editControlsContainer.Closed += (s, e) =>
					{
						property.Item.Invalidated -= updateData;
					};

					rowContainer = CreateSettingsRow(property, field.Content);
				}
				else
				{
					// add in the position
					FlowLayoutWidget originRowContainer = CreateSettingsRow(property);

					var originField = new Vector3Field();
					originField.Initialize(0);
					originField.Vector3 = directionAxis.Origin;

					var normalField = new Vector3Field();
					normalField.Initialize(0);
					normalField.Vector3 = directionAxis.Normal;

					originField.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionAxis() { Origin = originField.Vector3, Normal = normalField.Vector3 } });
						rebuildable?.Rebuild(undoBuffer);
						propertyGridModifier?.UpdateControls(context);
					};

					originRowContainer.AddChild(originField.Content);
					editControlsContainer.AddChild(originRowContainer);

					// add in the direction
					FlowLayoutWidget directionRowContainer = CreateSettingsRow(property);

					normalField.ValueChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { new DirectionAxis() { Origin = originField.Vector3, Normal = normalField.Vector3 } });
						rebuildable?.Rebuild(undoBuffer);
						propertyGridModifier?.UpdateControls(context);
					};

					directionRowContainer.AddChild(normalField.Content);
					editControlsContainer.AddChild(directionRowContainer);

					// update tihs when changed
					EventHandler<InvalidateArgs> updateData = (s, e) =>
					{
						originField.Vector3 = ((DirectionAxis)property.PropertyInfo.GetGetMethod().Invoke(property.Item, null)).Origin;
					};
					property.Item.Invalidated += updateData;
					editControlsContainer.Closed += (s, e) =>
					{
						property.Item.Invalidated -= updateData;
					};
				}
			}
			else if (property.Value is ChildrenSelector childSelector)
			{
				rowContainer = CreateSettingsColumn(property);
				rowContainer.AddChild(CreateSelector(childSelector, property.Item, theme));
			}
			// create a int editor
			else if (property.Value is int intValue)
			{
				var field = new IntField();
				field.Initialize(0);
				field.IntValue = intValue;
				field.ValueChanged += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { field.IntValue });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, field.Content);
			}
			// create a bool editor
			else if (property.Value is bool boolValue)
			{
				var field = new ToggleboxField(theme);
				field.Initialize(0);
				field.Checked = boolValue;
				field.ValueChanged += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { field.Checked });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, field.Content);
			}
			// create a string editor
			else if (property.Value is string stringValue)
			{
				var textEditWidget = new MHTextEditWidget(stringValue, pixelWidth: 150 * GuiWidget.DeviceScale)
				{
					SelectAllOnFocus = true,
					VAnchor = VAnchor.Center
				};
				textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
				{
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { textEditWidget.Text });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};

				rowContainer = CreateSettingsRow(property, textEditWidget);
			}
			// create a char editor
			else if (property.Value is char charValue)
			{
				var textEditWidget = new MHTextEditWidget(charValue.ToString(), pixelWidth: 150 * GuiWidget.DeviceScale)
				{
					SelectAllOnFocus = true,
					VAnchor = VAnchor.Center
				};
				textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
				{
					if (textEditWidget.Text.Length < 1)
					{
						textEditWidget.Text = "a";
					}
					if (textEditWidget.Text.Length > 1)
					{
						textEditWidget.Text = textEditWidget.Text.Substring(0, 1);
					}
					property.PropertyInfo.GetSetMethod().Invoke(property.Item, new Object[] { textEditWidget.Text[0] });
					rebuildable?.Rebuild(undoBuffer);
					propertyGridModifier?.UpdateControls(context);
				};
				rowContainer = CreateSettingsRow(property, textEditWidget);
			}
			// create an enum editor
			else if (property.PropertyType.IsEnum)
			{
				rowContainer = CreateEnumEditor(context, rebuildable,
						property, property.PropertyType, property.Value, property.DisplayName,
						theme, undoBuffer);
			}
			// Use known IObject3D editors
			else if (property.Value is IObject3D object3D
				&& ApplicationController.Instance.GetEditorsForType(property.PropertyType)?.FirstOrDefault() is IObject3DEditor editor)
			{
				rowContainer = editor.Create(object3D, view3DWidget, theme);
			}

			editControlsContainer.AddChild(rowContainer);

			// remember the row name and widget
			context.editRows.Add(property.PropertyInfo.Name, rowContainer);
		}

		private static GuiWidget CreateSelector(ChildrenSelector childSelector, IObject3D parent, ThemeConfig theme)
		{
			GuiWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			void UpdateSelectColors(bool selectionChanged = false)
			{
				foreach (var child in parent.Children.ToList())
				{
					child.SuspendRebuild();
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
					child.ResumeRebuild();
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
						TextColor = ActiveTheme.Instance.PrimaryTextColor
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
						TextColor = ActiveTheme.Instance.PrimaryTextColor
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
							meshWrapper.SuspendRebuild();
							meshWrapper.ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);
							meshWrapper.ResumeRebuild();
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

				Button detailsLink = theme.ButtonFactory.Generate("Unlock".Localize(), AggContext.StaticData.LoadIcon("locked.png", 16, 16));
				detailsLink.BackgroundColor = theme.Colors.PrimaryAccentColor.AdjustContrast(theme.Colors.PrimaryTextColor, 10).ToColor();
				detailsLink.Margin = new BorderDouble(5);
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.Instance.LaunchBrowser(UnlockLinkAttribute.UnlockPageBaseUrl + unlockLink.UnlockPageLink);
				};
				row.AddChild(detailsLink);
				editControlsContainer.AddChild(row);
			}
		}

		private void AddWebPageLinkIfRequired(PPEContext context, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			var unlockLink = context.item.GetType().GetCustomAttributes(typeof(WebPageLinkAttribute), true).FirstOrDefault() as WebPageLinkAttribute;
			if (unlockLink != null)
			{
				var row = CreateSettingsRow(unlockLink.Name.Localize());

				Button detailsLink = theme.ButtonFactory.Generate("Open", AggContext.StaticData.LoadIcon("internet.png", 16, 16));
				detailsLink.Margin = new BorderDouble(5);
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.Instance.LaunchBrowser(unlockLink.Url);
				};
				row.AddChild(detailsLink);
				editControlsContainer.AddChild(row);
			}
		}

		private static GuiWidget CreateEnumEditor(PPEContext context, IPublicPropertyObject item,
			EditableProperty property, Type propertyType, object value, string displayName,
			ThemeConfig theme,
			UndoBuffer undoBuffer)
		{
			var propertyGridModifier = item as IPropertyGridModifier;

			// Enum keyed on name to friendly name
			var enumItems = Enum.GetNames(propertyType).Select(enumName =>
			{
				return new
				{
					Key = enumName,
					Value = enumName.Replace('_', ' ')
				};
			});

			FlowLayoutWidget rowContainer = CreateSettingsRow(displayName);

			var iconsAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<IconsAttribute>().FirstOrDefault();
			if (iconsAttribute != null)
			{
				int index = 0;
				foreach (var enumItem in enumItems)
				{
					var localIndex = index;
					ImageBuffer iconImage = null;
					var iconPath = iconsAttribute.IconPaths[localIndex];
					if (!string.IsNullOrWhiteSpace(iconPath))
					{
						if (iconsAttribute.Width > 0)
						{
							iconImage = AggContext.StaticData.LoadIcon(iconPath, iconsAttribute.Width, iconsAttribute.Height);
						}
						else
						{
							iconImage = AggContext.StaticData.LoadIcon(iconPath);
						}
						var radioButton = new RadioButton(new ImageWidget(iconImage));
						radioButton.ToolTipText = enumItem.Key;
						// set it if checked
						if (enumItem.Value == value.ToString())
						{
							radioButton.Checked = true;
							if (localIndex != 0
								|| !iconsAttribute.Item0IsNone)
							{
								radioButton.BackgroundColor = new Color(Color.Black, 100);
							}
						}

						rowContainer.AddChild(radioButton);

						var localItem = enumItem;
						radioButton.CheckedStateChanged += (sender, e) =>
						{
							if (radioButton.Checked)
							{
								property.PropertyInfo.GetSetMethod().Invoke(
									property.Item,
									new Object[] { Enum.Parse(propertyType, localItem.Key) });
								item?.Rebuild(undoBuffer);
								propertyGridModifier?.UpdateControls(context);
								if (localIndex != 0
									|| !iconsAttribute.Item0IsNone)
								{
									radioButton.BackgroundColor = new Color(Color.Black, 100);
								}
							}
							else
							{
								radioButton.BackgroundColor = Color.Transparent;
							}
						};
						index++;
					}
				}
			}
			else
			{
				var dropDownList = new DropDownList("Name".Localize(), theme.Colors.PrimaryTextColor, Direction.Down, pointSize: theme.DefaultFontSize)
				{
					BorderColor = theme.GetBorderColor(75)
				};

				var sortableAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<SortableAttribute>().FirstOrDefault();
				var orderedItems = sortableAttribute != null ? enumItems.OrderBy(n => n.Value) : enumItems;

				foreach (var orderItem in orderedItems)
				{
					MenuItem newItem = dropDownList.AddItem(orderItem.Value);

					var localOrderedItem = orderItem;
					newItem.Selected += (sender, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(
							property.Item,
							new Object[] { Enum.Parse(propertyType, localOrderedItem.Key) });
						item?.Rebuild(undoBuffer);
						propertyGridModifier?.UpdateControls(context);
					};
				}

				dropDownList.SelectedLabel = value.ToString().Replace('_', ' ');
				rowContainer.AddChild(dropDownList);
			}

			return rowContainer;
		}
	}
}