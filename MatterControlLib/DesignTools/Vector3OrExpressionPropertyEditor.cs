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
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
    public class Vector3OrExpressionPropertyEditor : IPropertyEditorFactory
    {
        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is Vector3OrExpression vector3Expresion)
            {
                var theme = propertyEditor.Theme;
                var undoBuffer = propertyEditor.UndoBuffer;

                // create a string editor
                var field2 = new TextField(theme);
                field2.Initialize(ref tabIndex);
                field2.SetValue(vector3Expresion.Expression, false);
                field2.ClearUndoHistory();
                field2.Content.HAnchor = HAnchor.Stretch;
                PropertyEditor.RegisterValueChanged(property, undoBuffer, context,
                    field2,
                    (valueString) => new Vector3OrExpression(valueString),
                    (value) =>
                    {
                        return ((Vector3OrExpression)value).Expression;
                    });
                return PropertyEditor.CreateSettingsColumn(property, field2, fullWidth: true);




                var propertyIObject3D = property.Source as IObject3D;

                var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
                {
                    HAnchor = HAnchor.Stretch,
                    VAnchor = VAnchor.Fit,
                    Margin = theme.DefaultContainerPadding
                };

                // add 

                for (int i = 0; i < 3; i++)
                {
                    GuiWidget rowContainer;

                    // create a string editor
                    var field = new ExpressionField(theme)
                    {
                        Name = property.DisplayName + " Field"
                    };
                    field.Initialize(ref tabIndex);
                    if (vector3Expresion.Expression.Contains("="))
                    {
                        field.SetValue(vector3Expresion.Expression, false);
                    }
                    else // make sure it is formatted
                    {
                        var format = "0." + new string('#', 5);
                        if (property.PropertyInfo.GetCustomAttributes(true).OfType<MaxDecimalPlacesAttribute>().FirstOrDefault() is MaxDecimalPlacesAttribute decimalPlaces)
                        {
                            format = "0." + new string('#', Math.Min(10, decimalPlaces.Number));
                        }

                        //field.SetValue(vector3Expresion.Value(propertyIObject3D).ToString(format), false);
                    }

                    field.ClearUndoHistory();
                    PropertyEditor.RegisterValueChanged(property, undoBuffer, context,
                        field,
                        (valueString) =>
                        {
                            vector3Expresion.Expression = valueString;
                            return vector3Expresion;
                        },
                        (value) =>
                        {
                            return ((Vector3OrExpression)value).Expression;
                        });

                    rowContainer = propertyEditor.CreateSettingsRow(property,
                        PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) =>
                        {
                            vector3Expresion.Expression = valueString;
                            return vector3Expresion;
                        }, theme),
                        theme);

                    void RefreshField(object s, InvalidateArgs e)
                    {
                        // This code only executes when the in scene controls are updating the objects data and the display needs to tack them.
                        if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
                        {
                            var newValue = (Vector3OrExpression)property.Value;
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
                                    //field.TextValue = rawValue.ToString(format);
                                }
                            }
                        }
                    }

                    propertyIObject3D.Invalidated += RefreshField;
                    field.Content.Closed += (s, e) => propertyIObject3D.Invalidated -= RefreshField;

                    topToBottom.AddChild(rowContainer);
                }

                return topToBottom;
            }

            return null;
        }

        public static void Register()
        {
            PropertyEditor.RegisterEditor(typeof(Vector3OrExpression), new Vector3OrExpressionPropertyEditor());
        }
    }
}