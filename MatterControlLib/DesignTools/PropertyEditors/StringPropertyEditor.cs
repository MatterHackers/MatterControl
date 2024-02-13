﻿/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web;

namespace Matter_CAD_Lib.DesignTools.PropertyEditors
{
    public class StringPropertyEditor : IPropertyEditorFactory
    {
        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is string stringValue)
            {
                var theme = propertyEditor.Theme;
                var undoBuffer = propertyEditor.UndoBuffer;

                var propertyIObject3D = property.Source as IObject3D;

                GuiWidget rowContainer;
                if (property.PropertyInfo.GetCustomAttributes(true).OfType<GoogleSearchAttribute>().FirstOrDefault() != null)
                {
                    rowContainer = NewImageSearchWidget(theme);
                }
                else if (property.PropertyInfo.GetCustomAttributes(true).OfType<FontSelectorAttribute>().FirstOrDefault() != null)
                {
                    rowContainer = InsertFontSelectorField(propertyEditor, property, context, ref tabIndex, stringValue, theme, undoBuffer);
                }
                else if (propertyIObject3D is AssetObject3D assetObject
                    && property.PropertyInfo.Name == "AssetPath")
                {
                    rowContainer = InsertEditFieldWithFileButton(property, theme, undoBuffer, assetObject);
                }
                else
                {
                    var readOnly = property.PropertyInfo.GetCustomAttributes(true).OfType<ReadOnlyAttribute>().FirstOrDefault() != null;

                    if (readOnly)
                    {
                        rowContainer = InsertReadOnlyTextDisplay(property, stringValue, theme, propertyIObject3D);
                    }
                    else // normal edit row
                    {
                        if (property.PropertyInfo.GetCustomAttributes(true).OfType<MultiLineEditAttribute>().FirstOrDefault() != null)
                        {
                            rowContainer = InsertMultiLineEdit(property, context, ref tabIndex, stringValue, theme, undoBuffer);
                        }
                        else
                        {
                            rowContainer = InsertSingleLineEdit(propertyEditor, property, context, ref tabIndex, stringValue, theme, undoBuffer);
                        }
                    }
                }

                return rowContainer;
            }

            return null;
        }

        private GuiWidget InsertFontSelectorField(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex, string stringValue, ThemeConfig theme, UndoBuffer undoBuffer)
        {
            GuiWidget rowContainer;
            var field = new FontSelectorField(property, theme);

            field.Initialize(ref tabIndex);
            field.SetValue(stringValue, false);
            field.ClearUndoHistory();
            field.Content.HAnchor = HAnchor.Stretch;
            PropertyEditor.RegisterValueChanged(property, undoBuffer, context, field, (valueString) => valueString);
            rowContainer = propertyEditor.CreateSettingsRow(property, field.Content, theme);

            return rowContainer;
        }

        private GuiWidget InsertSingleLineEdit(PropertyEditor propertyEditor,
            EditableProperty property,
            EditorContext context,
            ref int tabIndex,
            string stringValue,
            ThemeConfig theme,
            UndoBuffer undoBuffer)
        {
            GuiWidget rowContainer;
            // create a string editor
            var field = new TextField(theme);
            field.Initialize(ref tabIndex);
            field.SetValue(stringValue, false);
            field.ClearUndoHistory();
            field.Content.HAnchor = HAnchor.Stretch;
            PropertyEditor.RegisterValueChanged(property, undoBuffer, context, field, (valueString) => valueString);
            rowContainer = propertyEditor.CreateSettingsRow(property, field.Content, theme);

            // check for DirectoryPathAttribute
            var directoryPathAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<DirectoryPathAttribute>().FirstOrDefault();
            if (directoryPathAttribute != null)
            {
                // add a browse button
                var browseButton = new ThemedIconButton(StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png"), 16, 16).GrayToColor(theme.TextColor), theme)
                {
                    ToolTipText = "Select Folder".Localize(),
                };
                browseButton.Click += (s, e) =>
                {
                    UiThread.RunOnIdle(() =>
                    {
                        AggContext.FileDialogs.SelectFolderDialog(
                            new SelectFolderDialogParams(directoryPathAttribute.Message)
                            {
                                ActionButtonLabel = directoryPathAttribute.ActionLabel,
                                Title = ApplicationController.Instance.ProductName + " - " + "Select A Folder".Localize(),
                                RootFolder = SelectFolderDialogParams.RootFolderTypes.Specify,
                                FolderPath = stringValue
                            },
                            (openParams) =>
                            {
                                if (!string.IsNullOrEmpty(openParams.FolderPath))
                                {
                                    field.SetValue(openParams.FolderPath, true);
                                }
                            });
                    });
                };
                rowContainer.AddChild(browseButton);
            }

            return rowContainer;
        }

        private static GuiWidget InsertMultiLineEdit(EditableProperty property, EditorContext context, ref int tabIndex, string stringValue, ThemeConfig theme, UndoBuffer undoBuffer)
        {
            GuiWidget rowContainer;
            // create a a multi-line string editor
            var field = new MultilineStringField(theme, property.PropertyInfo.GetCustomAttributes(true).OfType<UpdateOnEveryKeystrokeAttribute>().FirstOrDefault() != null);
            field.Initialize(ref tabIndex);
            field.SetValue(stringValue, false);
            field.ClearUndoHistory();
            field.Content.HAnchor = HAnchor.Stretch;
            field.Content.Descendants<ScrollableWidget>().FirstOrDefault().MaximumSize = new Vector2(double.MaxValue, 200);
            field.Content.Descendants<ScrollingArea>().FirstOrDefault().Parent.VAnchor = VAnchor.Top;
            field.Content.MinimumSize = new Vector2(0, 100 * GuiWidget.DeviceScale);
            field.Content.Margin = new BorderDouble(0, 0, 0, 5);
            PropertyEditor.RegisterValueChanged(property, undoBuffer, context, field, (valueString) => valueString);
            rowContainer = PropertyEditor.CreateSettingsColumn(property, field, fullWidth: true);
            return rowContainer;
        }

        private static GuiWidget InsertReadOnlyTextDisplay(EditableProperty property, string stringValue, ThemeConfig theme, IObject3D propertyIObject3D)
        {
            GuiWidget rowContainer;
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

            if (propertyIObject3D != null)
            {
                propertyIObject3D.Invalidated += RefreshField;
                wrappedTextWidget.Closed += (s, e) => propertyIObject3D.Invalidated -= RefreshField;
            }

            return rowContainer;
        }

        private static GuiWidget InsertEditFieldWithFileButton(EditableProperty property, ThemeConfig theme, UndoBuffer undoBuffer, AssetObject3D assetObject)
        {
            GuiWidget rowContainer;
            // This is the AssetPath property of an asset object, add a button to set the AssetPath from a file
            // Change button
            var changeButton = new ThemedTextButton(property.Description, theme)
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
                    ImageObject3D.ShowOpenDialog(assetObject, undoBuffer);
                });
            };
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
            var searchButton = new ThemedIconButton(StaticData.Instance.LoadIcon("icon_search_24x24.png", 16, 16).GrayToColor(theme.TextColor), theme)
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

        public static void Register()
        {
            PropertyEditor.RegisterEditor(typeof(string), new StringPropertyEditor());
        }
    }
}