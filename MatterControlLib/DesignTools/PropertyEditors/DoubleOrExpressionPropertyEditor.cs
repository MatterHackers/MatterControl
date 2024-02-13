/*
Copyright (c) 2024, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Linq;

namespace Matter_CAD_Lib.DesignTools.PropertyEditors
{
    public class DoubleOrExpressionPropertyEditor : IPropertyEditorFactory
    {
        public static void Register()
        {
            PropertyEditor.RegisterEditor(typeof(DoubleOrExpression), new DoubleOrExpressionPropertyEditor());
        }

        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is DoubleOrExpression doubleExpresion)
            {
                var theme = propertyEditor.Theme;
                var undoBuffer = propertyEditor.UndoBuffer;

                var propertyIObject3D = property.Source as IObject3D;

                GuiWidget rowContainer;
                {
                    // create a string editor
                    var field = new ExpressionField(theme)
                    {
                        Name = property.DisplayName + " Field"
                    };
                    field.Initialize(ref tabIndex);
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

                        field.SetValue(doubleExpresion.Value(propertyIObject3D).ToString(format), false);
                    }

                    field.ClearUndoHistory();
                    PropertyEditor.RegisterValueChanged(property, undoBuffer, context,
                        field,
                        (valueString) =>
                        {
                            doubleExpresion.Expression = valueString;
                            return doubleExpresion;
                        },
                        (value) =>
                        {
                            return ((DoubleOrExpression)value).Expression;
                        });

                    rowContainer = propertyEditor.CreateSettingsRow(property,
                        PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) =>
                        {
                            doubleExpresion.Expression = valueString;
                            return doubleExpresion;
                        }, theme),
                        theme);

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

                                    var rawValue = newValue.Value(propertyIObject3D);
                                    field.TextValue = rawValue.ToString(format);
                                }
                            }
                        }
                    }

                    propertyIObject3D.Invalidated += RefreshField;
                    field.Content.Closed += (s, e) => propertyIObject3D.Invalidated -= RefreshField;
                }

                return rowContainer;
            }

            return null;
        }
    }
}