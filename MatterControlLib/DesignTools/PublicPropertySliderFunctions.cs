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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public static class PublicPropertySliderFunctions
	{
		public static GuiWidget GetFieldContentWithSlider(EditableProperty property, EditorContext context, UIField field, UndoBuffer undoBuffer, Func<string, object> valueFromString, ThemeConfig theme)
		{
			var sliderAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<SliderAttribute>().FirstOrDefault();
			if (sliderAttribute != null)
			{
				var min = sliderAttribute.Min;
				var max = sliderAttribute.Max;
				var delta = max - min;

				// the slider values go from 0 to 1 and are then mapped during translation to the field
				var slider = new Slider(new Vector2(0, 0), 80 * GuiWidget.DeviceScale, 0, 1)
				{
					VAnchor = VAnchor.Center,
				};

				slider.View.TrackColor = theme.TextColor;
				slider.View.ThumbColor = theme.PrimaryAccentColor;
				slider.View.TrackHeight = 1 * GuiWidget.DeviceScale;

				Func<double> getFieldValue = null;

				double GetSlider0To1FromField()
				{
					return GetSlider0To1FromValue(getFieldValue());
				}

				double GetSlider0To1FromValue(double value)
				{
					var mapped0To1 = Math.Max(0, Math.Min(1, (value - min) / delta));
					return Easing.CalculateInverse(sliderAttribute.EasingType, sliderAttribute.EaseOption, mapped0To1);
				}

				double GetFieldFromSlider0To1()
				{
					var fieldValue = Easing.Calculate(sliderAttribute.EasingType,
						sliderAttribute.EaseOption,
						slider.Value) * delta + min;

					var snapGridDistance = sliderAttribute.SnapDistance;
					if (sliderAttribute.UseSnappingGrid)
					{
						snapGridDistance = SnapGridDistance();
					}
					if (snapGridDistance > 0)
					{
						// snap this position to the grid
						// snap this position to the grid
						fieldValue = ((int)((fieldValue / snapGridDistance) + .5)) * snapGridDistance;
					}

					return fieldValue;
				}

				double SnapGridDistance()
				{
					var view3DWidget = slider.Parents<View3DWidget>().FirstOrDefault();
					if (view3DWidget != null)
					{
						var object3DControlLayer = view3DWidget.Descendants<Object3DControlsLayer>().FirstOrDefault();
						if (object3DControlLayer != null)
						{
							return object3DControlLayer.SnapGridDistance;
						}
					}

					return 0;
				}

				var changeDueToSlider = false;
				var changeDueToField = false;

				slider.ValueChanged += (s, e) =>
				{
					if (!changeDueToField)
					{
						changeDueToSlider = true;
						SetValue(property, context, valueFromString, GetFieldFromSlider0To1());
						changeDueToSlider = false;
					}
				};

				double sliderDownValue = 0;
				slider.SliderGrabed += (s, e) =>
				{
					sliderDownValue = getFieldValue();
				};

				slider.SliderReleased += (s, e) =>
				{
					var currentValue = GetFieldFromSlider0To1();

					changeDueToSlider = true;
					SetValue(property, context, valueFromString, currentValue);
					changeDueToSlider = false;

					// save the undo information
					undoBuffer.Add(new UndoRedoActions(() =>
					{
						SetValue(property, context, valueFromString, sliderDownValue);
					},
					() =>
					{
						SetValue(property, context, valueFromString, currentValue);
					}));
				};

				GuiWidget content = null;
				var sliderRightMargin = 11;
				if (field is DoubleField doubleField)
				{
					getFieldValue = () => doubleField.DoubleValue;

					doubleField.ValueChanged += (s, e) =>
					{
						changeDueToField = true;
						slider.Value = GetSlider0To1FromField();
						changeDueToField = false;
					};

					content = new FlowLayoutWidget();
					content.AddChild(slider);
					content.AddChild(new GuiWidget()
					{
						Width = sliderRightMargin * GuiWidget.DeviceScale,
						Height = 3
					});
					content.AddChild(field.Content);
				}
				else if (field is ExpressionField expressionField)
				{
					getFieldValue = () =>
					{
						if (double.TryParse(expressionField.Value, out double value))
						{
							return value;
						}

						return 0;
					};

					expressionField.ValueChanged += (s, e) =>
					{
						if (!changeDueToSlider)
						{
							changeDueToField = true;
							slider.Value = GetSlider0To1FromField();
							changeDueToField = false;
						}
					};

					expressionField.Content.Descendants<TextWidget>().First().TextChanged += (s, e) =>
					{
						if (!changeDueToSlider)
						{
							changeDueToField = true;
							var textWidget = expressionField.Content.Descendants<TextWidget>().First();
							double.TryParse(textWidget.Text, out double textWidgetValue);
							slider.Value = GetSlider0To1FromValue(textWidgetValue);
							changeDueToField = false;
						}
					};

					var leftHold = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch,
						Margin = new BorderDouble(0, 0, 66 / GuiWidget.DeviceScale + sliderRightMargin, 0)
					};
					leftHold.AddChild(new HorizontalSpacer());
					leftHold.AddChild(slider);
					field.Content.AddChild(leftHold, 0);

					content = field.Content;
				}

				// set the initial value of the slider
				changeDueToField = true;
				slider.Value = GetSlider0To1FromField();
				changeDueToField = false;

				return content;
			}

			return field.Content;
		}

		private static void SetValue(EditableProperty property, EditorContext context, Func<string, object> valueFromString, double sliderDownValue)
		{
			var localItem = context.Item;
            var localObject3D = localItem as Object3D;
            var object3D = property.Source as IObject3D;
			var propertyGridModifier = property.Source as IPropertyGridModifier;

			property.SetValue(valueFromString(sliderDownValue.ToString()));
			object3D?.Invalidate(new InvalidateArgs(localObject3D, InvalidateType.Properties));
			propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));

			object3D?.Invalidate(new InvalidateArgs(localObject3D, InvalidateType.DisplayValues));
		}
	}
}