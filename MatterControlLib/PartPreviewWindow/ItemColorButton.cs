/*
Copyright (c) 2018, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.CustomWidgets.ColorPicker;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ItemColorButton : PopupButton
	{
		private ColorButton colorButton;
		private GuiWidget popupContent;

		public event EventHandler ColorChanged;

		public bool IsOpen => popupContent?.ContainsFocus == true;

		public ItemColorButton(ThemeConfig theme, Color selectedColor)
		{
			this.ToolTipText = "Color".Localize();
			var scaledButtonSize = 24 * GuiWidget.DeviceScale;
			this.PopupBorderColor = theme.PopupBorderColor;

			Width = 30 * GuiWidget.DeviceScale;
			Height = 30 * GuiWidget.DeviceScale;

			var menuTheme = AppContext.MenuTheme;

			MakeScrollable = false;

			PopupHAnchor = HAnchor.Fit;
			PopupVAnchor = VAnchor.Fit;

			colorButton = new ColorButton(selectedColor == Color.Transparent ? theme.SlightShade : selectedColor)
			{
				Width = scaledButtonSize,
				Height = scaledButtonSize,
				BackgroundRadius = scaledButtonSize / 2,
				BackgroundOutlineWidth = 1,
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				DisabledColor = theme.MinimalShade,
				Border = 1,
				BorderColor = theme.BorderColor20,
				Selectable = false
			};

			this.DynamicPopupContent = () =>
			{
#if true
				popupContent = NewColorSelector(theme, colorButton.BackgroundColor, menuTheme, (color) => colorButton.BackgroundColor = color);
#else
				return new ColorSwatchSelector(menuTheme,
					buttonSize: 16,
					buttonSpacing: new BorderDouble(1, 1, 0, 0),
					colorNotifier: (newColor) => colorButton.BackgroundColor = newColor)
				{
					Padding = theme.DefaultContainerPadding,
					BackgroundColor = menuTheme.BackgroundColor,
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit
				};
#endif
				return popupContent;
			};

			colorButton.BackgroundColorChanged += (s, e) =>
			{
				ColorChanged?.Invoke(this, null);
			};

			this.AddChild(colorButton);
		}

		public static GuiWidget NewColorSelector(ThemeConfig theme, Color selectedColor, ThemeConfig menuTheme, Action<Color> update)
		{
			var content = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Padding = new BorderDouble(5),
				BackgroundColor = menuTheme.BackgroundColor,
			};

			var htmlField = new TextField(theme);

			var pickerContainer = content.AddChild(new GuiWidget(128 * DeviceScale, 128 * DeviceScale));
			var picker = pickerContainer.AddChild(new RadialColorPicker()
			{
				SelectedColor = selectedColor.WithAlpha(255),
				BackgroundColor = Color.Transparent,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			}) as RadialColorPicker;
			picker.SelectedColorChanged += (s, newColor) =>
			{
				htmlField.SetValue(picker.SelectedColor.Html.Substring(1, 6), false);
				update?.Invoke(picker.SelectedColor);
			};

			var rightContent = content.AddChild(new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(5),
				VAnchor = VAnchor.Stretch,
			});

			// put in an html edit field
			htmlField.Initialize(0);
			htmlField.SetValue(selectedColor.Html.Substring(1, 6), false);
			htmlField.ClearUndoHistory();
			htmlField.ValueChanged += (s, e) =>
			{
				var colorString = htmlField.Value;
				if (!colorString.StartsWith("#"))
				{
					colorString = "#" + colorString;
				}

				var color = new Color(colorString);
				if (color == Color.Transparent)
				{
					// we did not understand the color
					// set it back to selectedColor
					htmlField.SetValue(selectedColor.Html.Substring(1, 6), false);
				}
				else // valid color set
				{
					picker.SelectedColor = color;
				}
			};

			var rowContainer = new SettingsRow("", "", htmlField.Content, theme)
			{
				HAnchor = HAnchor.Fit
			};
			var htmlColor = rightContent.AddChild(rowContainer);

			var colorContent = rightContent.AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			});

			var startColorSwatch = colorContent.AddChild(new GuiWidget(10, 10)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = picker.SelectedColor
			});

			var colorSwatch = colorContent.AddChild(new GuiWidget(10, 10)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = picker.SelectedColor
			});

			picker.IncrementalColorChanged += (s, newColor) =>
			{
				colorSwatch.BackgroundColor = picker.SelectedColor;
				htmlField.SetValue(picker.SelectedColor.Html.Substring(1, 6), false);
			};

			picker.SelectedColorChanged += (s, newColor) => colorSwatch.BackgroundColor = picker.SelectedColor;

			var resetButton = rightContent.AddChild(new TextIconButton("Clear".Localize(), StaticData.Instance.LoadIcon("transparent_grid.png", 16, 16), theme)
			{
				Margin = new BorderDouble(0, 0, 0, 3),
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Absolute,
				ToolTipText = "Clear any assigned color. This may allow component colors to be visible.".Localize(),
			});
			resetButton.Click += (s, e) =>
			{
				// The colorChanged action displays the given color - use .MinimalHighlight rather than no color
				update?.Invoke(Color.Transparent);
				picker.SetColorWithoutChangeEvent(Color.White);
			};

			var selectButton = rightContent.AddChild(new TextIconButton("Select".Localize(), StaticData.Instance.LoadIcon("eye_dropper.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				Margin = 0,
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Absolute
			});
			selectButton.Click += (s, e) =>
			{
				// change to an eye dropper mode in the design view to allow for color selection
				ApplicationController.Instance.GetTracedMouseRay((color) =>
				{
					update?.Invoke(color);
					picker.SelectedColor = color;
				});
			};
			selectButton.Visible = false;

			if (selectButton.Width < resetButton.Width)
			{
				selectButton.HAnchor = HAnchor.Stretch;
			}
			else
			{
				resetButton.HAnchor = HAnchor.Stretch;
			}

			return content;
		}

		public override void OnLoad(EventArgs args)
		{
			var firstBackgroundColor = this.Parents<GuiWidget>().Where(p => p.BackgroundColor.Alpha0To1 == 1).FirstOrDefault()?.BackgroundColor;
			if (firstBackgroundColor != null)
			{
				// Resolve alpha
				this.HoverColor = new BlenderRGBA().Blend(firstBackgroundColor.Value, this.HoverColor);
			}

			base.OnLoad(args);
		}

		public Color Color
		{
			get => colorButton.BackgroundColor;
			set => colorButton.BackgroundColor = value;
		}
	}
}
