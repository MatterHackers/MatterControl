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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Library.Widgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PublicPropertyEditor : IObject3DEditor
	{
		public string Name => "Property Editor";

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(IObject3D) };

		private static readonly Type[] AllowedTypes =
		{
			typeof(double), typeof(int), typeof(char), typeof(string), typeof(bool),
			typeof(StringOrExpression),
			typeof(DoubleOrExpression),
			typeof(IntOrExpression),
			typeof(Color),
			typeof(Vector2), typeof(Vector3), typeof(Vector4),
			typeof(DirectionVector), typeof(DirectionAxis),
			typeof(SelectedChildren),
			typeof(ImageBuffer),
			typeof(Histogram),
			typeof(List<string>),
			typeof(PrinterSettingsLayer),

		};
		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		private SafeList<SettingsRow> rows = new SafeList<SettingsRow>();

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
				
				AddMarkDownDescription(context.item, mainContainer, theme);

				GuiWidget scope = mainContainer;

				rows.Clear();

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

						bool expanded = true;

						var sectionState = item as ISectionState;
						if (sectionState != null)
						{
							expanded = sectionState.GetSectionExpansion(sectionStart.Title);
						}

						var section = new SectionWidget(sectionStart.Title, column, theme, expanded: expanded);
						theme.ApplyBoxStyle(section);

						if (sectionState != null)
						{
							section.ExpandedChanged += (s, e) => sectionState.SectionExpansionChanged(sectionStart.Title, e);
						}

						mainContainer.AddChild(section);

						scope = column;
					}

					// Create SectionWidget for SectionStartAttributes
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<SectionEndAttribute>().Any())
					{
						// Push scope back to mainContainer on
						scope = mainContainer;
					}

					var editor = CreatePropertyEditor(rows, property, undoBuffer, context, theme);
					if (editor != null)
					{
						scope.AddChild(editor);
					}
				}

				AddWebPageLinkIfRequired(context.item, mainContainer, theme);

				// add in an Update button if applicable
				var showUpdate = context.item.GetType().GetCustomAttributes(typeof(ShowUpdateButtonAttribute), true).FirstOrDefault() as ShowUpdateButtonAttribute;
				if (showUpdate?.Show == true)
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

				// add any function buttons last
				AddFunctionButtons(item, mainContainer, theme);

				// Init with custom 'UpdateControls' hooks
				(context.item as IPropertyGridModifier)?.UpdateControls(new PublicPropertyChange(context, "Update_Button"));
			}

			return mainContainer;
		}

		private void AddFunctionButtons(IObject3D item, FlowLayoutWidget mainContainer, ThemeConfig theme)
		{
			if (item is IEditorButtonProvider editorButtonProvider)
			{
				foreach (var editorButtonData in editorButtonProvider.GetEditorButtonsData())
				{
					var editorButton = new TextButton(editorButtonData.Name, theme)
					{
						Margin = 5,
						ToolTipText = editorButtonData.HelpText,
						BackgroundColor = theme.MinimalShade,
					};
					if (editorButtonData.PrimaryAction)
					{
						theme.ApplyPrimaryActionStyle(editorButton);
					}

					var row = new SettingsRow("".Localize(), null, editorButton, theme);
					editorButtonData.SetStates?.Invoke(editorButton, row);
					editorButton.Click += (s, e) =>
					{
						editorButtonData.Action?.Invoke();
					};

					mainContainer.AddChild(row);
				}
			}
		}

		private static SettingsRow CreateSettingsRow(EditableProperty property, GuiWidget content, ThemeConfig theme, SafeList<SettingsRow> rows = null)
		{
			var row = new SettingsRow(property.DisplayName.Localize(), property.Description, content, theme);
			if (rows != null)
			{
				rows.Add(row);
				row.SetTextRightMargin(rows);
			}
			return row;
		}

		private static FlowLayoutWidget CreateSettingsColumn(EditableProperty property, UIField field, bool fullWidth = false)
		{
			return CreateSettingsColumn(property.DisplayName.Localize(), field, property.Description, fullWidth: fullWidth);
		}

		private static FlowLayoutWidget CreateSettingsColumn(EditableProperty property)
		{
			return CreateSettingsColumn(property.DisplayName.Localize(), property.Description);
		}

		private static FlowLayoutWidget CreateSettingsColumn(string labelText, UIField field, string toolTipText = null, bool fullWidth = false)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};

			if (!fullWidth)
			{
				row.AddChild(new HorizontalSpacer());
			}

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

			if (!string.IsNullOrEmpty(labelText))
			{
				var label = SettingsRow.CreateSettingsLabel(labelText, toolTipText, theme.TextColor);
				label.VAnchor = VAnchor.Absolute;
				label.HAnchor = HAnchor.Left;

				column.AddChild(label);
			}

			return column;
		}

		public static IEnumerable<EditableProperty> GetEditablePropreties(IObject3D item)
		{
			return item.GetType().GetProperties(OwnedPropertiesOnly)
				.Where(pi => (AllowedTypes.Contains(pi.PropertyType) || pi.PropertyType.IsEnum)
					&& pi.GetGetMethod() != null
					&& pi.GetSetMethod() != null)
				.Select(p => new EditableProperty(p, item));
		}

		public static IEnumerable<EditableProperty> GetExecutableFunctions(IObject3D item)
		{
			BindingFlags buttonFunctionsOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

			return item.GetType().GetProperties(buttonFunctionsOnly)
				.Where(pi => (AllowedTypes.Contains(pi.PropertyType) || pi.PropertyType.IsEnum)
					&& pi.GetGetMethod() != null
					&& pi.GetSetMethod() != null)
				.Select(p => new EditableProperty(p, item));
		}

		public static GuiWidget CreatePropertyEditor(SafeList<SettingsRow> rows, EditableProperty property, UndoBuffer undoBuffer, PPEContext context, ThemeConfig theme)
		{
			if (property == null
				|| context == null)
            {
				return null;
            }

			var localItem = context.item;
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
					if (newValue == oldValue)
					{
						return;
					}
					if (valueToString != null)
					{
						oldValue = valueToString(property.Value);
					}

					// field.Content
					if (undoBuffer != null
						&& e.UserInitiated)
					{
						undoBuffer.AddAndDo(new UndoRedoActions(() =>
						{
							property.SetValue(valueFromString(oldValue));
							object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
							propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
						},
						() =>
						{
							property.SetValue(valueFromString(newValue));
							object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
							propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
						}));
					}
					else
					{
						property.SetValue(valueFromString(newValue));
						object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
						propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
					}
				};
			}

			var readOnly = property.PropertyInfo.GetCustomAttributes(true).OfType<ReadOnlyAttribute>().FirstOrDefault() != null;

			// create a double editor
			if (propertyValue is double doubleValue)
			{
				if (readOnly)
				{
					var valueField = new TextWidget(string.Format("{0:n}", doubleValue), textColor: theme.TextColor, pointSize: 10)
					{
						AutoExpandBoundsToText = true
					};

					rowContainer = new SettingsRow(property.DisplayName.Localize(),
						property.Description,
						valueField,
						theme);

					void RefreshField(object s, InvalidateArgs e)
					{
						if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
						{
							double newValue = (double)property.Value;
							valueField.Text = string.Format("{0:n}", newValue);
						}
					}

					object3D.Invalidated += RefreshField;
					valueField.Closed += (s, e) => object3D.Invalidated -= RefreshField;
				}
				else // normal edit row
				{
					var field = new DoubleField(theme);
					field.Initialize(0);
					field.DoubleValue = doubleValue;
					field.ClearUndoHistory();
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
					field.Content.Descendants<InternalTextEditWidget>().First().Name = property.DisplayName + " Edit";
					field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;

					if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
					{
						field.Content.Descendants<InternalNumberEdit>().First().MaxDecimalsPlaces = decimalPlaces.Number;
					}

					rowContainer = CreateSettingsRow(property,
						PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) => { return double.Parse(valueString); }, theme),
						theme);
				}
			}
			else if (propertyValue is Color color)
			{
				var field = new ColorField(theme, object3D.Color, null, false);
				field.Initialize(0);
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.Color);
					object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field.Content, theme);
			}
			else if (propertyValue is Vector2 vector2)
			{
				var field = new Vector2Field(theme);
				field.Initialize(0);
				field.Vector2 = vector2;
				field.ClearUndoHistory();

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
				field.ClearUndoHistory();

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
			else if (propertyValue is Vector4 vector4)
			{
				var field = new Vector4Field(theme);
				if (property.PropertyInfo.GetCustomAttributes(true).OfType<VectorFieldLabelsAttribute>().FirstOrDefault() is VectorFieldLabelsAttribute vectorFieldLabels)
				{
					field.Labels = vectorFieldLabels.Labels;
				}

				field.Initialize(0);
				field.Vector4 = vector4;
				field.ClearUndoHistory();

				RegisterValueChanged(
					field,
					(valueString) => Vector4.Parse(valueString),
					(value) =>
					{
						var s = ((Vector4)value).ToString();
						return s.Substring(1, s.Length - 2);
					});

				rowContainer = CreateSettingsColumn(property, field);
			}
			else if (propertyValue is DirectionVector directionVector)
			{
				var field = new DirectionVectorField(theme);
				field.Initialize(0);
				field.SetValue(directionVector);
				field.ClearUndoHistory();

				field.ValueChanged += (s, e) =>
				{
					property.SetValue(field.DirectionVector);
					object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field.Content, theme);
			}
			else if (propertyValue is DirectionAxis directionAxis)
			{
				rowContainer = CreateSettingsColumn(property);

				var field1 = new DirectionVectorField(theme);
				field1.Initialize(0);
				field1.ClearUndoHistory();

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
				field2.ClearUndoHistory();

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
					object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};
				field2.ValueChanged += (s, e) =>
				{
					property.SetValue(new DirectionAxis()
					{
						Normal = field1.DirectionVector.Normal,
						Origin = property.Item.Children.First().GetAxisAlignedBoundingBox().Center + field2.Vector3
					});
					object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer.AddChild(row2);
			}
			else if (propertyValue is PrinterSettingsLayer printerSettingsLayer)
			{
				var printerProfile = new PrinterSettings();
				rowContainer = AddMaterialWidget.CreateSetingsList(printerProfile, printerSettingsLayer, theme);

				rowContainer.Children.First().AddChild(new HorizontalLine(Color.Green)
                {
					Height = 4 * GuiWidget.DeviceScale
                }, 0);

				rowContainer.Children.First().AddChild(new HorizontalLine(Color.Green)
				{
					Height = 4 * GuiWidget.DeviceScale
				});
			}
			else if (propertyValue is SelectedChildren childSelector)
			{
				if (property.PropertyInfo.GetCustomAttributes(true).OfType<ShowAsListAttribute>().FirstOrDefault() is ShowAsListAttribute showAsList)
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

					rowContainer = CreateSettingsRow(property, field.Content, theme);
				}
				else // show the subtract editor for boolean subtract and subtract and replace
				{
					rowContainer = CreateSettingsColumn(property);
					if (property.Item is OperationSourceContainerObject3D sourceContainer)
					{
						Action selected = null;
						var showUpdate = localItem.GetType().GetCustomAttributes(typeof(ShowUpdateButtonAttribute), true).FirstOrDefault() as ShowUpdateButtonAttribute;
						if (showUpdate == null
							|| !showUpdate.SuppressPropertyChangeUpdates)
						{
							selected = () =>
							{
								object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
							};
						}

						rowContainer.AddChild(CreateSourceChildSelector(childSelector, sourceContainer, theme, selected));
					}
					else
					{
						rowContainer.AddChild(CreateSelector(childSelector, property.Item, theme));
					}
				}
			}
			else if (propertyValue is ImageBuffer imageBuffer)
			{
				var imageDisplayAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<ImageDisplayAttribute>().FirstOrDefault();

				rowContainer = CreateSettingsColumn(property);
				GuiWidget imageWidget;
				if (imageDisplayAttribute?.Stretch == true)
				{
					var responsiveImageWidget = new ResponsiveImageWidget(imageBuffer);
					responsiveImageWidget.RenderCheckerboard = true;
					imageWidget = responsiveImageWidget;
				}
				else
				{
					imageWidget = new ImageWidget(imageBuffer);
				}

				if (imageDisplayAttribute != null)
				{
					imageWidget.MaximumSize = new Vector2(imageDisplayAttribute.MaxXSize * GuiWidget.DeviceScale, int.MaxValue);
					imageWidget.Margin = imageDisplayAttribute.GetMargin();
				}
				else
				{
					imageWidget.Margin = new BorderDouble(0, 3);
				}

				ImageBuffer GetImageCheckingForErrors()
				{
					var image = imageBuffer;
					if (object3D is ImageObject3D imageObject2)
					{
						image = imageObject2.Image;
					}

					// Show image load error if needed
					if (image == null)
					{
						image = new ImageBuffer(185, 185).SetPreMultiply();
						var graphics2D = image.NewGraphics2D();

						graphics2D.FillRectangle(0, 0, 185, 185, theme.MinimalShade);
						graphics2D.Rectangle(0, 0, 185, 185, theme.SlightShade);
						graphics2D.DrawString("Error Loading Image".Localize() + "...", 10, 185 / 2, baseline: Agg.Font.Baseline.BoundsCenter, color: Color.Red, pointSize: theme.DefaultFontSize, drawFromHintedCach: true);
					}

					return image;
				}

				void UpdateEditorImage()
				{
					if (imageWidget is ResponsiveImageWidget responsive)
					{
						responsive.Image = GetImageCheckingForErrors();
					}
					else
					{
						((ImageWidget)imageWidget).Image = GetImageCheckingForErrors();
					}
				}

				void RefreshField(object s, InvalidateArgs e)
				{
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						UpdateEditorImage();
					}
				}

				object3D.Invalidated += RefreshField;
				imageWidget.Closed += (s, e) => object3D.Invalidated -= RefreshField;

				if (object3D is IEditorWidgetModifier editorWidgetModifier)
				{
					editorWidgetModifier.ModifyEditorWidget(imageWidget, theme, UpdateEditorImage);
				}

				rowContainer.AddChild(imageWidget);
			}
			else if (propertyValue is Histogram histogram)
			{
				rowContainer = CreateSettingsColumn(property);
				var histogramWidget = histogram.NewEditWidget(theme);
				rowContainer.AddChild(histogramWidget);
				void RefreshField(object s, InvalidateArgs e)
				{
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						if (object3D is IImageProvider imageProvider)
						{
							var _ = imageProvider.Image;
						}
					}
				}

				object3D.Invalidated += RefreshField;
				rowContainer.Closed += (s, e) => object3D.Invalidated -= RefreshField;
			}
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
			// create a int editor
			else if (propertyValue is int intValue)
			{
				if (readOnly)
				{
					string FormateInt(int value)
					{
						if (property.PropertyInfo.GetCustomAttributes(true).OfType<DisplayAsTimeAttribute>().FirstOrDefault() != null)
						{
							var minutes = intValue / 60;
							var hours = minutes / 60;
							return $"{hours:00}:{minutes % 60:00}:{intValue % 60:00}";
						}
						else
						{
							return string.Format("{0:n0}", intValue);
						}
					}

					var valueField = new TextWidget(FormateInt(intValue),
						textColor: theme.TextColor,
						pointSize: 10)
					{
						AutoExpandBoundsToText = true,
						Margin = new BorderDouble(0, 0, 7, 0),
					};

					rowContainer = new SettingsRow(property.DisplayName.Localize(),
						property.Description,
						valueField,
						theme);

					void RefreshField(object s, InvalidateArgs e)
					{
						if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
						{
							int newValue = (int)property.Value;
							valueField.Text = string.Format(FormateInt(intValue), newValue);
						}
					}

					object3D.Invalidated += RefreshField;
					valueField.Closed += (s, e) => object3D.Invalidated -= RefreshField;
				}
				else // normal edit row
				{
					var field = new IntField(theme);
					field.Initialize(0);
					field.IntValue = intValue;
					field.ClearUndoHistory();

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

					rowContainer = CreateSettingsRow(property, field.Content, theme);
				}
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
				rowContainer = CreateSettingsRow(property, field.Content, theme);
			}
			else if (propertyValue is DoubleOrExpression doubleExpresion)
			{
				// create a string editor
				var field = new ExpressionField(theme)
				{
					Name = property.DisplayName + " Field"
				};
				field.Initialize(0);
				if (doubleExpresion.Expression.Contains("="))
				{
					field.SetValue(doubleExpresion.Expression, false);
				}
				else // make sure it is formatted
				{
					var format = "0." + new string('#', 5);
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
					{
						format = "0." + new string('#', Math.Min(10, decimalPlaces.Number));
					}

					field.SetValue(doubleExpresion.Value(object3D).ToString(format), false);
				}

				field.ClearUndoHistory();
				RegisterValueChanged(field,
					(valueString) =>
					{
						doubleExpresion.Expression = valueString;
						return doubleExpresion;
					},
					(value) =>
					{
						return ((DoubleOrExpression)value).Expression;
					});

				rowContainer = CreateSettingsRow(property,
					PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) =>
					{
						doubleExpresion.Expression = valueString;
						return doubleExpresion;
					}, theme),
					theme,
					rows);

				void RefreshField(object s, InvalidateArgs e)
				{
					// This code only executes when the in scene controls are updating the objects data and the display needs to tack them.
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						var newValue = (DoubleOrExpression)property.Value;
						// if (newValue.Expression != field.Value)
						{
							// we should never be in the situation where there is an '=' as the in scene controls should be disabled
							if (newValue.Expression.StartsWith("="))
							{
								field.TextValue = newValue.Expression;
							}
							else
							{
								var format = "0." + new string('#', 5);
								if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
								{
									format = "0." + new string('#', Math.Min(10, decimalPlaces.Number));
								}

								var rawValue = newValue.Value(object3D);
								field.TextValue = rawValue.ToString(format);
							}
						}
					}
				}

				object3D.Invalidated += RefreshField;
				field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;
			}
			else if (propertyValue is IntOrExpression intExpresion)
			{
				// create a string editor
				var field = new ExpressionField(theme)
				{
					Name = property.DisplayName + " Field"
				};
				field.Initialize(0);
				if (intExpresion.Expression.Contains("="))
				{
					field.SetValue(intExpresion.Expression, false);
				}
				else // make sure it is formatted
				{
					var format = "0." + new string('#', 5);
					if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
					{
						format = "0." + new string('#', Math.Min(10, decimalPlaces.Number));
					}

					field.SetValue(intExpresion.Value(object3D).ToString(format), false);
				}

				field.ClearUndoHistory();
				RegisterValueChanged(field,
					(valueString) =>
					{
						intExpresion.Expression = valueString;
						return intExpresion;
					},
					(value) =>
					{
						return ((IntOrExpression)value).Expression;
					});

				rowContainer = CreateSettingsRow(property,
					PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) =>
					{
						intExpresion.Expression = valueString;
						return intExpresion;
					}, theme),
					theme,
					rows);

				void RefreshField(object s, InvalidateArgs e)
				{
					// This code only executes when the in scene controls are updating the objects data and the display needs to tack them.
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						var newValue = (IntOrExpression)property.Value;
						// if (newValue.Expression != field.Value)
						{
							// we should never be in the situation where there is an '=' as the in scene controls should be disabled
							if (newValue.Expression.StartsWith("="))
							{
								field.TextValue = newValue.Expression;
							}
							else
							{
								var format = "0." + new string('#', 5);
								if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
								{
									format = "0." + new string('#', Math.Min(10, decimalPlaces.Number));
								}

								var rawValue = newValue.Value(object3D);
								field.TextValue = rawValue.ToString(format);
							}
						}
					}
				}

				object3D.Invalidated += RefreshField;
				field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;
			}
			else if (propertyValue is string stringValue)
			{
				if (property.PropertyInfo.GetCustomAttributes(true).OfType<GoogleSearchAttribute>().FirstOrDefault() != null)
				{
					rowContainer = NewImageSearchWidget(theme);
				}
				else if (object3D is AssetObject3D assetObject
					&& property.PropertyInfo.Name == "AssetPath")
				{
					// This is the AssetPath property of an asset object, add a button to set the AssetPath from a file
					// Change button
					var changeButton = new TextButton(property.Description, theme)
					{
						BackgroundColor = theme.MinimalShade,
						Margin = 3
					};

					rowContainer = new SettingsRow(property.DisplayName,
						null,
						changeButton,
						theme);


					changeButton.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							ImageObject3D.ShowOpenDialog(assetObject);
						});
					};
				}
				else
				{
					if (readOnly)
					{
						WrappedTextWidget wrappedTextWidget = null;
						if (!string.IsNullOrEmpty(property.DisplayName))
						{
							rowContainer = new GuiWidget()
							{
								HAnchor = HAnchor.Stretch,
								VAnchor = VAnchor.Fit,
								Margin = 9
							};

							var displayName = rowContainer.AddChild(new TextWidget(property.DisplayName,
								textColor: theme.TextColor,
								pointSize: 10)
							{
								VAnchor = VAnchor.Center,
							});

							var wrapContainer = new GuiWidget()
							{
								Margin = new BorderDouble(displayName.Width + displayName.Margin.Width + 15, 3, 3, 3),
								HAnchor = HAnchor.Stretch,
								VAnchor = VAnchor.Fit
							};
							wrappedTextWidget = new WrappedTextWidget(stringValue, textColor: theme.TextColor, pointSize: 10)
							{
								HAnchor = HAnchor.Stretch
							};
							wrappedTextWidget.TextWidget.HAnchor = HAnchor.Right;
							wrapContainer.AddChild(wrappedTextWidget);
							rowContainer.AddChild(wrapContainer);
						}
						else
						{
							rowContainer = wrappedTextWidget = new WrappedTextWidget(stringValue,
													textColor: theme.TextColor,
													pointSize: 10)
							{
								Margin = 9
							};
						}

						void RefreshField(object s, InvalidateArgs e)
						{
							if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
							{
								wrappedTextWidget.Text = property.Value.ToString();
							}
						}

						object3D.Invalidated += RefreshField;
						wrappedTextWidget.Closed += (s, e) => object3D.Invalidated -= RefreshField;
					}
					else // normal edit row
					{
						if (property.PropertyInfo.GetCustomAttributes(true).OfType<MultiLineEditAttribute>().FirstOrDefault() != null)
						{
							// create a a multi-line string editor
							var field = new MultilineStringField(theme);
							field.Initialize(0);
							field.SetValue(stringValue, false);
							field.ClearUndoHistory();
							field.Content.HAnchor = HAnchor.Stretch;
							field.Content.Descendants<ScrollableWidget>().FirstOrDefault().MaximumSize = new Vector2(double.MaxValue, 200);
							field.Content.Descendants<ScrollingArea>().FirstOrDefault().Parent.VAnchor = VAnchor.Top;
							field.Content.MinimumSize = new Vector2(0, 100 * GuiWidget.DeviceScale);
							field.Content.Margin = new BorderDouble(0, 0, 0, 5);
							RegisterValueChanged(field, (valueString) => valueString);
							rowContainer = CreateSettingsColumn(property, field, fullWidth: true);
						}
						else
						{
							// create a string editor
							var field = new TextField(theme);
							field.Initialize(0);
							field.SetValue(stringValue, false);
							field.ClearUndoHistory();
							field.Content.HAnchor = HAnchor.Stretch;
							RegisterValueChanged(field, (valueString) => valueString);
							rowContainer = CreateSettingsRow(property, field.Content, theme, rows);
						}
					}
				}
			}
			else if (propertyValue is StringOrExpression stringOrExpression)
			{
				if (property.PropertyInfo.GetCustomAttributes(true).OfType<MultiLineEditAttribute>().FirstOrDefault() != null)
				{
					// create a a multi-line string editor
					var field = new MultilineStringField(theme);
					field.Initialize(0);
					field.SetValue(stringOrExpression.Expression, false);
					field.ClearUndoHistory();
					field.Content.HAnchor = HAnchor.Stretch;
					field.Content.Descendants<ScrollableWidget>().FirstOrDefault().MaximumSize = new Vector2(double.MaxValue, 200);
					field.Content.Descendants<ScrollingArea>().FirstOrDefault().Parent.VAnchor = VAnchor.Top;
					field.Content.MinimumSize = new Vector2(0, 100 * GuiWidget.DeviceScale);
					field.Content.Margin = new BorderDouble(0, 0, 0, 5);
					RegisterValueChanged(field,
						(valueString) => new StringOrExpression(valueString),
						(value) =>
						{
							return ((StringOrExpression)value).Expression;
						});
					rowContainer = CreateSettingsColumn(property, field, fullWidth: true);
				}
				else
				{
					// create a string editor
					var field = new TextField(theme);
					field.Initialize(0);
					field.SetValue(stringOrExpression.Expression, false);
					field.ClearUndoHistory();
					field.Content.HAnchor = HAnchor.Stretch;
					RegisterValueChanged(field,
						(valueString) => new StringOrExpression(valueString),
						(value) =>
						{
							return ((StringOrExpression)value).Expression;
						});
					rowContainer = CreateSettingsColumn(property, field, fullWidth: true);
				}
			}
			else if (propertyValue is char charValue)
			{
				// create a char editor
				var field = new CharField(theme);
				field.Initialize(0);
				field.SetValue(charValue.ToString(), false);
				field.ClearUndoHistory();
				field.ValueChanged += (s, e) =>
				{
					property.SetValue(Convert.ToChar(field.Value));
					object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
					propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
				};

				rowContainer = CreateSettingsRow(property, field.Content, theme);
			}
			else if (property.PropertyType.IsEnum)
			{
				// create an enum editor
				UIField field;
				var enumDisplayAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<EnumDisplayAttribute>().FirstOrDefault();
				var addToSettingsRow = true;
				if (enumDisplayAttribute != null)
				{
					field = new EnumDisplayField(property, enumDisplayAttribute, theme)
					{
						InitialValue = propertyValue.ToString(),
					};

					if (enumDisplayAttribute.Mode == EnumDisplayAttribute.PresentationMode.Tabs)
					{
						addToSettingsRow = false;
					}
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
					if (property.Value.ToString() != field.Value)
					{
						property.SetValue(Enum.Parse(property.PropertyType, field.Value));
						object3D?.Invalidate(new InvalidateArgs(localItem, InvalidateType.Properties));
						propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
					}
				};

				if (addToSettingsRow)
				{
					rowContainer = CreateSettingsRow(property, field.Content, theme);
				}
				else
				{
					// field.Content.Margin = new BorderDouble(3, 0);
					field.Content.HAnchor = HAnchor.Stretch;
					rowContainer = field.Content;
				}

				void RefreshField(object s, InvalidateArgs e)
				{
					if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
					{
						var newValue = property.Value.ToString();
						if (field.Content is MHDropDownList dropDown)
						{
							if (field.Value != newValue)
							{
								field.SetValue(newValue, false);
								dropDown.SelectedValue = newValue;
							}
						}
					}
				}

				if (object3D != null)
				{
					object3D.Invalidated += RefreshField;
					field.Content.Closed += (s, e) => object3D.Invalidated -= RefreshField;
				}

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

		public static GuiWidget NewImageSearchWidget(ThemeConfig theme, string postPend = "silhouette")
		{
			var searchRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5, 0)
			};

			var searchField = new ThemedTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "Search Google for images")
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center
			};
			searchRow.AddChild(searchField);
			var searchButton = new IconButton(StaticData.Instance.LoadIcon("icon_search_24x24.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Search".Localize(),
			};
			searchRow.AddChild(searchButton);

			void DoSearch(object s, EventArgs e)
			{
				var search = HttpUtility.UrlEncode(searchField.Text);
				if (!string.IsNullOrEmpty(search))
				{
					ApplicationController.LaunchBrowser($"http://www.google.com/search?q={search} {postPend}&tbm=isch");
				}
			};

			searchField.ActualTextEditWidget.EditComplete += DoSearch;
			searchButton.Click += DoSearch;
			return searchRow;
		}

		private static GuiWidget CreateSourceChildSelector(SelectedChildren childSelector, OperationSourceContainerObject3D sourceContainer, ThemeConfig theme, Action selectionChanged)
		{
			GuiWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 3, 0, 0),
			};

			var parentOfSubtractTargets = sourceContainer.SourceContainer.FirstWithMultipleChildrenDescendantsAndSelf();

			var sourceChildren = parentOfSubtractTargets.Children.ToList();

			var objectChecks = new Dictionary<ICheckbox, IObject3D>();

			var radioSiblings = new List<GuiWidget>();
			for (int i = 0; i < sourceChildren.Count; i++)
			{
				var itemIndex = i;
				var child = sourceChildren[itemIndex];
				var rowContainer = new FlowLayoutWidget()
				{
					Padding = new BorderDouble(15, 0, 0, 3)
				};

				GuiWidget selectWidget;
				if (sourceChildren.Count == 2)
				{
					var radioButton = new RadioButton(string.IsNullOrWhiteSpace(child.Name) ? $"{itemIndex}" : $"{child.Name}")
					{
						Checked = childSelector.Contains(child.ID),
						TextColor = theme.TextColor,
						Margin = 0,
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
						TextColor = theme.TextColor,
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

						selectionChanged?.Invoke();
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
			(string url, GuiWidget markdownWidget)? unlockdata = null;

			if (item.GetType().GetCustomAttributes(typeof(RequiresPermissionsAttribute), true).FirstOrDefault() is RequiresPermissionsAttribute unlockLink
				&& !ApplicationController.Instance.UserHasPermission(item))
			{
				unlockdata = ApplicationController.Instance.GetUnlockData?.Invoke(item, theme);
			}
			else if (!item.Persistable)
			{
				// find the first self or child that is not authorized
				var permission = item.DescendantsAndSelf()
					.Where(i => !i.Persistable && !ApplicationController.Instance.UserHasPermission(i));

				if (permission.Any())
				{
					var unlockItem = permission.First();
					unlockdata = ApplicationController.Instance.GetUnlockData?.Invoke(unlockItem, theme);
				}
			}

			if (unlockdata != null && !string.IsNullOrEmpty(unlockdata.Value.url))
			{
				if (unlockdata.Value.markdownWidget != null)
				{
					unlockdata.Value.markdownWidget.VAnchor = VAnchor.Fit;
					editControlsContainer.AddChild(unlockdata.Value.markdownWidget);
				}

				editControlsContainer.AddChild(GetUnlockRow(theme, unlockdata.Value.url));
			}
		}

		public static void AddMarkDownDescription(IObject3D item, GuiWidget editControlsContainer, ThemeConfig theme)
		{
			if (item.GetType().GetCustomAttributes(typeof(MarkDownDescriptionAttribute), true).FirstOrDefault() is MarkDownDescriptionAttribute markdownDescription)
			{
				var markdownWidget = new MarkdownWidget(theme)
				{
					Padding = new BorderDouble(left: theme.DefaultContainerPadding / 2),
					Markdown = markdownDescription.Markdown,
					VAnchor = VAnchor.Fit
				};
				
				editControlsContainer.AddChild(markdownWidget);
			}
		}

		public static GuiWidget GetUnlockRow(ThemeConfig theme, string url)
		{
			var detailsLink = new TextIconButton("Unlock".Localize(), StaticData.Instance.LoadIcon("locked.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Margin = 5,
				ToolTipText = "Visit MatterHackers.com to Purchase".Localize()
			};
			detailsLink.Click += (s, e) =>
			{
				ApplicationController.LaunchBrowser(url);
			};
			theme.ApplyPrimaryActionStyle(detailsLink);

			return new SettingsRow("Demo Mode".Localize(), null, detailsLink, theme);
		}

		public static void AddWebPageLinkIfRequired(IObject3D item, FlowLayoutWidget editControlsContainer, ThemeConfig theme)
		{
			if (item.GetType().GetCustomAttributes(typeof(WebPageLinkAttribute), true).FirstOrDefault() is WebPageLinkAttribute unlockLink)
			{
				var detailsLink = new TextIconButton(unlockLink.ButtonName.Localize(), StaticData.Instance.LoadIcon("internet.png", 16, 16).SetToColor(theme.TextColor), theme)
				{
					BackgroundColor = theme.MinimalShade,
					ToolTipText = unlockLink.Url,
				};
				detailsLink.Click += (s, e) =>
				{
					ApplicationController.LaunchBrowser(unlockLink.Url);
				};

				// website row
				editControlsContainer.AddChild(new SettingsRow(unlockLink.RowName, null, detailsLink, theme));
			}
		}
	}
}