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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Linq;

namespace Matter_CAD_Lib.DesignTools.PropertyEditors
{
    public class Vector3OrExpressionPropertyEditor : IPropertyEditorFactory
    {
        public static void Register()
        {
            PropertyEditor.RegisterEditor(typeof(Vector3OrExpression), new Vector3OrExpressionPropertyEditor());
        }

        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is Vector3OrExpression vector3Expresion)
            {
                var theme = propertyEditor.Theme;
                var undoBuffer = propertyEditor.UndoBuffer;

                var propertyIObject3D = property.Source as IObject3D;

                var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
                {
                    HAnchor = HAnchor.Stretch,
                    VAnchor = VAnchor.Fit,
                    Margin = theme.DefaultContainerPadding
                };

                var axisLabels = new string[] { "X", "Y", "Z" };

                var components = Vector3OrExpression.ExtractComponents(vector3Expresion.Expression);

                for (int index = 0; index < 3; index++)
                {
                    var i = index;

                    var component = components.Length > i ? components[i] : "0";

                    // create a string editor
                    var field = new ExpressionField(theme)
                    {
                        Name = property.DisplayName + " " + axisLabels[i] + " Field"
                    };
                    field.Initialize(ref tabIndex);
                    field.SetValue(component, false);

                    field.ClearUndoHistory();
                    RegisterValueChanged(property, undoBuffer, context, field, i);

                    var content = PublicPropertySliderFunctions.GetFieldContentWithSlider(property, context, field, undoBuffer, (valueString) =>
                    {
                        if (property.Value is Vector3OrExpression vector3Expresion)
                        {
                            var newComponents = Vector3OrExpression.ExtractComponents(vector3Expresion.Expression);
                            newComponents[i] = valueString;
                            return new Vector3OrExpression($"[{string.Join(", ", newComponents)}]");
                        }

                        // This should never happen
                        return new Vector3OrExpression("[0, 0, 0]");
                    }, theme);
                    var rowContainer = new SettingsRow(axisLabels[i], property.Description, content, theme);

                    void RefreshField(object s, InvalidateArgs e)
                    {
                        // This code only executes when the in scene controls are updating the objects data and the display needs to tack them.
                        if (e.InvalidateType.HasFlag(InvalidateType.DisplayValues))
                        {
                            var newValue = (Vector3OrExpression)property.Value;
                            var newComponents = Vector3OrExpression.ExtractComponents(newValue.Expression);
                            if (newComponents.Length > i)
                            {
                                field.SetValue(newComponents[i], false);
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

        private void RegisterValueChanged(EditableProperty property, UndoBuffer undoBuffer, EditorContext context, UIField field, int index)
        {
            var i = index;

            field.ValueChanged += (s, e) =>
            {
                var source = property.Source;
                var propertySourceObject3D = source as IObject3D;
                var propertyGridModifier = source as IPropertyGridModifier;

                var newComponent = field.Value;
                var directOrExpression = property.Value as DirectOrExpression;
                var oldExpression = directOrExpression.Expression;
                // and replace the component
                var components = Vector3OrExpression.ExtractComponents(oldExpression);
                components[i] = newComponent;
                var newExpression = $"[{string.Join(", ", components)}]";

                if (newComponent == oldExpression)
                {
                    return;
                }

                // field.Content
                if (undoBuffer != null
                    && e.UserInitiated)
                {
                    undoBuffer.AddAndDo(new DoUndoActions("Value Change".Localize(), () =>
                    {
                        property.SetValue(new Vector3OrExpression(newExpression));
                        propertySourceObject3D?.Invalidate(new InvalidateArgs(propertySourceObject3D, InvalidateType.Properties));
                        propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
                    },
                    () =>
                    {
                        property.SetValue(new Vector3OrExpression(oldExpression));
                        propertySourceObject3D?.Invalidate(new InvalidateArgs(propertySourceObject3D, InvalidateType.Properties));
                        propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
                    }));
                }
                else
                {
                    property.SetValue(new Vector3OrExpression(newExpression));
                    propertySourceObject3D?.Invalidate(new InvalidateArgs(propertySourceObject3D, InvalidateType.Properties));
                    propertyGridModifier?.UpdateControls(new PublicPropertyChange(context, property.PropertyInfo.Name));
                }
            };
        }
    }
}