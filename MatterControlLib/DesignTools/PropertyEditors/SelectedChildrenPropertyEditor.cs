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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using static MatterHackers.Agg.UI.OnScreenKeyboard;

namespace Matter_CAD_Lib.DesignTools.PropertyEditors
{
    public class SelectedChildrenPropertyEditor : IPropertyEditorFactory
    {
        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is SelectedChildren childSelector)
            {
                var theme = propertyEditor.Theme;
                var undoBuffer = propertyEditor.UndoBuffer;
                var contextItem = context.Item;
                var contextObject3D = contextItem as IObject3D;
                var propertyIObject3D = property.Source as IObject3D;
                var propertyGridModifier = property.Source as IPropertyGridModifier;

                GuiWidget rowContainer;
                {
                    if (property.PropertyInfo.GetCustomAttributes(true).OfType<ShowAsListAttribute>().FirstOrDefault() is ShowAsListAttribute showAsList)
                    {
                        UIField field = new ChildrenSelectorListField(property, theme);

                        field.Initialize(ref tabIndex);
                        PropertyEditor.RegisterValueChanged(property, undoBuffer, context,
                            field,
                            (valueString) =>
                            {
                                var childrenSelector = new SelectedChildren();
                                foreach (var child in valueString.Split(','))
                                {
                                    childrenSelector.Add(child);
                                }

                                return childrenSelector;
                            });

                        rowContainer = propertyEditor.CreateSettingsRow(property, field.Content, theme);
                    }
                    else // show the subtract editor for boolean subtract and subtract and replace
                    {
                        rowContainer = PropertyEditor.CreateSettingsColumn(property);
                        if (property.Source is OperationSourceContainerObject3D sourceContainer)
                        {
                            Action selected = null;
                            var showUpdate = contextItem.GetType().GetCustomAttributes(typeof(ShowUpdateButtonAttribute), true).FirstOrDefault() as ShowUpdateButtonAttribute;
                            if (showUpdate == null
                                || !showUpdate.SuppressPropertyChangeUpdates)
                            {
                                selected = () =>
                                {
                                    propertyIObject3D?.Invalidate(new InvalidateArgs(contextObject3D, InvalidateType.Properties));
                                };
                            }

                            rowContainer.AddChild(CreateSourceChildSelector(childSelector, sourceContainer, theme, selected));
                        }
                        else
                        {
                            rowContainer.AddChild(CreateSelector(childSelector, propertyIObject3D, theme));
                        }
                    }
                }

                return rowContainer;
            }

            return null;
        }

        public static GuiWidget CreateSourceChildSelector(SelectedChildren childSelector, OperationSourceContainerObject3D sourceContainer, ThemeConfig theme, Action selectionChanged)
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

        public static void Register()
        {
            PropertyEditor.RegisterEditor(typeof(SelectedChildren), new SelectedChildrenPropertyEditor());
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
    }
}