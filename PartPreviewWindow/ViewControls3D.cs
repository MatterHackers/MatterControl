/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.IO;
using MatterHackers.Localizations;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.ImageProcessing;
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public enum ViewControls3DButtons
	{
		Rotate,
		Scale,
		Translate,
		PartSelect
	}

	public class ViewControls3D : FlowLayoutWidget
	{
		private GuiWidget partSelectSeparator;
		private MeshViewerWidget meshViewerWidget;

		private Button resetViewButton;

		private RadioButton translateButton;
		private RadioButton rotateButton;
		private RadioButton scaleButton;
		private RadioButton partSelectButton;

		private EventHandler unregisterEvents;

		private int buttonHeight;

		public event EventHandler ResetView;

		bool overflowMenuActive = false;

		public bool PartSelectVisible
		{
			get { return partSelectSeparator.Visible; }
			set
			{
				partSelectSeparator.Visible = value;
				partSelectButton.Visible = value;
			}
		}

		private ViewControls3DButtons activeTransformState = ViewControls3DButtons.Rotate;

		public ViewControls3DButtons ActiveButton
		{
			get
			{
				return activeTransformState;
			}
			set
			{
				this.activeTransformState = value;

				switch(value)
				{
					case ViewControls3DButtons.Rotate:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
						rotateButton.Checked = true;
						break;

					case ViewControls3DButtons.Translate:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
						translateButton.Checked = true;
						break;

					case ViewControls3DButtons.Scale:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
						scaleButton.Checked = true;
						break;

					case ViewControls3DButtons.PartSelect:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
						partSelectButton.Checked = true;
						break;
				}
			}
		}

		public ViewControls3D(MeshViewerWidget meshViewerWidget)
		{
			if (UserSettings.Instance.IsTouchScreen)
			{
				buttonHeight = 40;
			}
			else
			{
				buttonHeight = 0;
			}

			this.meshViewerWidget = meshViewerWidget;
			var textImageButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedHeight = buttonHeight,
				FixedWidth = buttonHeight,
				AllowThemeToAdjustImage = false,
				checkedBorderColor = RGBA_Bytes.White
			};

			
			string iconPath;

			iconPath = Path.Combine("ViewTransformControls", "reset.png");
			resetViewButton = textImageButtonFactory.Generate("", StaticData.Instance.LoadIcon(iconPath,32,32).InvertLightness());
			resetViewButton.ToolTipText = "Reset View".Localize();
			AddChild(resetViewButton);
			resetViewButton.Click += (s, e) => ResetView?.Invoke(this, null);

			iconPath = Path.Combine("ViewTransformControls", "rotate.png");
			rotateButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			rotateButton.ToolTipText = "Rotate (Alt + Left Mouse)".Localize();
			rotateButton.Margin = new BorderDouble(3);
			AddChild(rotateButton);
			rotateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Rotate;

			iconPath = Path.Combine("ViewTransformControls", "translate.png");
			translateButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			translateButton.ToolTipText = "Move (Shift + Left Mouse)".Localize();
			translateButton.Margin = new BorderDouble(3);
			AddChild(translateButton);
			translateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Translate;

			iconPath = Path.Combine("ViewTransformControls", "scale.png");
			scaleButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			scaleButton.ToolTipText = "Zoom (Ctrl + Left Mouse)".Localize();
			scaleButton.Margin = 3;
			AddChild(scaleButton);
			scaleButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Scale;

			partSelectSeparator = new GuiWidget(2, 32);
			partSelectSeparator.BackgroundColor = RGBA_Bytes.White;
			partSelectSeparator.Margin = 3;
			AddChild(partSelectSeparator);

			iconPath = Path.Combine("ViewTransformControls", "partSelect.png");
			partSelectButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			partSelectButton.ToolTipText = "Select Part".Localize();
			partSelectButton.Margin = new BorderDouble(3);
			AddChild(partSelectButton);
			partSelectButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.PartSelect;

			var slightShade = new RGBA_Bytes(0, 0, 0, 40);

			var menuInitiallyActive = false;
			iconPath = Path.Combine("ViewTransformControls", "overflow.png");
			var overflowButton = new ImageWidget(StaticData.Instance.LoadIcon(iconPath, 32, 32));
			overflowButton.ToolTipText = "More...".Localize();
			overflowButton.Margin = 3;
			AddChild(overflowButton);
			overflowButton.MouseDown += (s, e) =>
			{
				menuInitiallyActive = overflowMenuActive;
			};
			overflowButton.Click += (s, e) =>
			{
				if (!menuInitiallyActive)
				{
					ShowOverflowMenu(overflowButton);
					overflowButton.BackgroundColor = slightShade;
				}
			};

			Margin = new BorderDouble(5);
			HAnchor |= Agg.UI.HAnchor.ParentLeft;
			VAnchor = Agg.UI.VAnchor.ParentTop;
			rotateButton.Checked = true;
			BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

			SetMeshViewerDisplayTheme();
			partSelectButton.CheckedStateChanged += SetMeshViewerDisplayTheme;

			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public void ShowOverflowMenu(GuiWidget parent)
		{
			overflowMenuActive = true;

			var menuItems = new List<GuiWidget>();

			menuItems.Add(
				AddCheckbox(
					"Show Print Bed".Localize(),
					"Show Help Checkbox",
					meshViewerWidget.RenderBed,
					5,
					(s, e) =>
					{
						var checkbox = s as CheckBox;
						if (checkbox != null)
						{
							meshViewerWidget.RenderBed = checkbox.Checked;
						}
					}));

			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);
			if (buildHeight > 0)
			{
				menuItems.Add(
					AddCheckbox(
						"Show Print Area".Localize(),
						"Show Help Checkbox",
						meshViewerWidget.RenderBuildVolume,
						5,
						(s, e) =>
						{
							var checkbox = s as CheckBox;
							if (checkbox != null)
							{
								meshViewerWidget.RenderBuildVolume = checkbox.Checked;
							}
						}));
			}

			var widget = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(5, 5, 5, 0)
			};

			menuItems.Add(new HorizontalLine());
			CreateRenderTypeRadioButtons(widget);

			menuItems.Add(widget);

			var popupWidget = new PopupWidget(menuItems, parent, Vector2.Zero, Direction.Down, 0, false)
			{
				BorderWidth = 1,
				BorderColor = RGBA_Bytes.Gray,
				BackgroundColor = RGBA_Bytes.White,
			};
			popupWidget.Closed += (s, e) =>
			{
				parent.BackgroundColor = RGBA_Bytes.Transparent;
				overflowMenuActive = false;
			};
			popupWidget.Focus();
		}

		private void CreateRenderTypeRadioButtons(GuiWidget parentContainer)
		{
			string renderTypeString = UserSettings.Instance.get(UserSettingsKey.defaultRenderSetting);
			if (renderTypeString == null)
			{
				if (UserSettings.Instance.IsTouchScreen)
				{
					renderTypeString = "Shaded";
				}
				else
				{
					renderTypeString = "Outlines";
				}
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderTypeString);
			}

			//var itemTextColor = ActiveTheme.Instance.PrimaryTextColor;
			var itemTextColor = RGBA_Bytes.Black;

			RenderTypes renderType;
			bool canParse = Enum.TryParse(renderTypeString, out renderType);
			if (canParse)
			{
				meshViewerWidget.RenderType = renderType;
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Shaded".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Shaded);

				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Shaded;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				parentContainer.AddChild(renderTypeCheckBox);
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Outlines".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Outlines);
				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Outlines;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				parentContainer.AddChild(renderTypeCheckBox);
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Polygons".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Polygons);
				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						meshViewerWidget.RenderType = RenderTypes.Polygons;
						UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, meshViewerWidget.RenderType.ToString());
					}
				};
				parentContainer.AddChild(renderTypeCheckBox);
			}

			{
				RadioButton renderTypeCheckBox = new RadioButton("Overhang".Localize(), textColor: itemTextColor);
				renderTypeCheckBox.Checked = (meshViewerWidget.RenderType == RenderTypes.Overhang);

				renderTypeCheckBox.CheckedStateChanged += (sender, e) =>
				{
					if (renderTypeCheckBox.Checked)
					{
						// TODO: Determine if Scene is available in scope
						var scene = MatterControlApplication.Instance.ActiveView3DWidget.Scene;

						meshViewerWidget.RenderType = RenderTypes.Overhang;
						UserSettings.Instance.set("defaultRenderSetting", meshViewerWidget.RenderType.ToString());
						foreach (var meshAndTransform in scene.VisibleMeshes(Matrix4X4.Identity))
						{
							meshAndTransform.MeshData.MarkAsChanged();
							// change the color to be the right thing
							GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshAndTransform.MeshData, (faceEdge) =>
							{
								Vector3 normal = faceEdge.containingFace.normal;
								normal = Vector3.TransformVector(normal, meshAndTransform.Matrix).GetNormal();
								VertexColorData colorData = new VertexColorData();

								double startColor = 223.0 / 360.0;
								double endColor = 5.0 / 360.0;
								double delta = endColor - startColor;

								RGBA_Bytes color = RGBA_Floats.FromHSL(startColor, .99, .49).GetAsRGBA_Bytes();
								if (normal.z < 0)
								{
									color = RGBA_Floats.FromHSL(startColor - delta * normal.z, .99, .49).GetAsRGBA_Bytes();
								}

								colorData.red = color.red;
								colorData.green = color.green;
								colorData.blue = color.blue;
								return colorData;
							});
						}
					}
					else
					{
						// TODO: Implement
						/*
						foreach (var meshTransform in Scene.VisibleMeshes(Matrix4X4.Identity))
						{
							// turn off the overhang colors
						} */
					}
				};

				parentContainer.AddChild(renderTypeCheckBox);
			}
		}

		private static MenuItem AddCheckbox(string text, string itemValue, bool itemChecked, BorderDouble padding, EventHandler eventHandler)
		{
			var checkbox = new CheckBox(text)
			{
				Checked = itemChecked
			};
			checkbox.CheckedStateChanged += eventHandler;

			return new MenuItem(checkbox, itemValue)
			{
				Padding = padding,
			};
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			SetMeshViewerDisplayTheme(null, null);
		}

		protected void SetMeshViewerDisplayTheme(object sender = null, EventArgs e = null)
		{
			meshViewerWidget.TrackballTumbleWidget.RotationHelperCircleColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//meshViewerWidget.MaterialColor = RGBA_Bytes.White;
			//meshViewerWidget.SelectedMaterialColor = ActiveTheme.Instance.PrimaryAccentColor;
			meshViewerWidget.BuildVolumeColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryAccentColor.Red0To255, ActiveTheme.Instance.PrimaryAccentColor.Green0To255, ActiveTheme.Instance.PrimaryAccentColor.Blue0To255, 50);
		}
	}
}