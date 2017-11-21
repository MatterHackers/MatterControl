/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public enum ViewControls3DButtons
	{
		Rotate,
		Scale,
		Translate,
		PartSelect
	}

	public enum PartViewMode
	{
		Layers2D,
		Layers3D,
		Model
	}

	public class ViewModeChangedEventArgs : EventArgs
	{
		public PartViewMode ViewMode { get; set; }
	}

	public class TransformStateChangedEventArgs : EventArgs
	{
		public ViewControls3DButtons TransformMode { get; set; }
	}

	public class ViewControls3D : FlowLayoutWidget
	{
		public event EventHandler ResetView;

		public event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

		public event EventHandler<TransformStateChangedEventArgs> TransformStateChanged;

		internal OverflowMenu OverflowMenu;

		private GuiWidget partSelectSeparator;

		private RadioIconButton translateButton;
		private RadioIconButton rotateButton;
		private RadioIconButton scaleButton;
		private RadioIconButton partSelectButton;

		public RadioIconButton Layers2DButton;

		private EventHandler unregisterEvents;

		private ViewControls3DButtons activeTransformState = ViewControls3DButtons.Rotate;

		public bool PartSelectVisible
		{
			get { return partSelectSeparator.Visible; }
			set
			{
				partSelectSeparator.Visible = false;
				partSelectButton.Visible = false;
			}
		}

		public ViewControls3DButtons ActiveButton
		{
			get
			{
				return activeTransformState;
			}
			set
			{
				this.activeTransformState = value;
				switch (this.activeTransformState)
				{
					case ViewControls3DButtons.Rotate:
						if (rotateButton != null)
						{
							rotateButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.Translate:
						if (translateButton != null)
						{
							translateButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.Scale:
						if (scaleButton != null)
						{
							scaleButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.PartSelect:
						partSelectButton.Checked = true;
						break;
				}

				TransformStateChanged?.Invoke(this, new TransformStateChangedEventArgs()
				{
					TransformMode = activeTransformState
				});
			}
		}

		public ViewControls3D(BedConfig sceneContext, ThemeConfig theme, UndoBuffer undoBuffer)
		{
			string iconPath;

			var commonMargin = theme.ButtonSpacing;

			var buttonFactory = theme.RadioButtons;

			double height = theme.ButtonFactory.Options.FixedHeight;

			var homeButton = new IconButton(AggContext.StaticData.LoadIcon("fa-home_16.png", IconColor.Theme), theme)
			{
				ToolTipText = "Reset View".Localize(),
				Margin = commonMargin
			};
			homeButton.Click += (s, e) => ResetView?.Invoke(this, null);
			AddChild(homeButton);

			var undoButton = new IconButton(AggContext.StaticData.LoadIcon("Undo_grey_16x.png", 16, 16, IconColor.Theme), theme)
			{
				Name = "3D View Undo",
				ToolTipText = "Undo",
				Enabled = false,
				MinimumSize = new Vector2(height, height),
				Margin = commonMargin,
				VAnchor = VAnchor.Center
			};
			undoButton.Click += (sender, e) =>
			{
				undoBuffer.Undo();
			};
			this.AddChild(undoButton);

			var redoButton = new IconButton(AggContext.StaticData.LoadIcon("Redo_grey_16x.png", 16, 16, IconColor.Theme), theme)
			{
				Name = "3D View Redo",
				Margin = commonMargin,
				MinimumSize = new Vector2(height, height),
				ToolTipText = "Redo",
				Enabled = false,
				VAnchor = VAnchor.Center
			};
			redoButton.Click += (sender, e) =>
			{
				undoBuffer.Redo();
			};
			this.AddChild(redoButton);

			this.AddChild(new VerticalLine(50)
			{
				Margin = 4
			});

			undoBuffer.Changed += (sender, e) =>
			{
				undoButton.Enabled = undoBuffer.UndoCount > 0;
				redoButton.Enabled = undoBuffer.RedoCount > 0;
			};

			var buttonGroupA = new ObservableCollection<GuiWidget>();

			if (UserSettings.Instance.IsTouchScreen)
			{
				iconPath = Path.Combine("ViewTransformControls", "rotate.png");
				rotateButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, IconColor.Theme), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Rotate (Alt + Left Mouse)".Localize(),
					Margin = commonMargin
				};
				rotateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Rotate;
				buttonGroupA.Add(rotateButton);
				AddChild(rotateButton);

				iconPath = Path.Combine("ViewTransformControls", "translate.png");
				translateButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, IconColor.Theme), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Move (Shift + Left Mouse)".Localize(),
					Margin = commonMargin
				};
				translateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Translate;
				buttonGroupA.Add(translateButton);
				AddChild(translateButton);

				iconPath = Path.Combine("ViewTransformControls", "scale.png");
				scaleButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, IconColor.Theme), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Zoom (Ctrl + Left Mouse)".Localize(),
					Margin = commonMargin
				};
				scaleButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Scale;
				buttonGroupA.Add(scaleButton);
				AddChild(scaleButton);

				rotateButton.Checked = true;
			}

			partSelectSeparator = new VerticalLine(50)
			{
				Margin = 3
			};

			this.AddChild(partSelectSeparator);

			iconPath = Path.Combine("ViewTransformControls", "partSelect.png");
			partSelectButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroupA,
				ToolTipText = "Select Part".Localize(),
				Visible = false,
				Margin = commonMargin
			};
			partSelectButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.PartSelect;
			buttonGroupA.Add(partSelectButton);
			AddChild(partSelectButton);

			var buttonGroupB = new ObservableCollection<GuiWidget>();

			iconPath = Path.Combine("ViewTransformControls", "model.png");
			var modelViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Model View Button",
				Checked = true,
				ToolTipText = "Model".Localize(),
				Margin = commonMargin
			};
			modelViewButton.Click += SwitchModes_Click;
			buttonGroupB.Add(modelViewButton);
			AddChild(modelViewButton);

			iconPath = Path.Combine("ViewTransformControls", "3d.png");
			var layers3DButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Layers3D Button",
				ToolTipText = "3D Layers".Localize(),
				Margin = commonMargin
			};
			layers3DButton.Click += SwitchModes_Click;
			buttonGroupB.Add(layers3DButton);

			if (!UserSettings.Instance.IsTouchScreen)
			{
				this.AddChild(layers3DButton);
			}

			iconPath = Path.Combine("ViewTransformControls", "2d.png");
			Layers2DButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Layers2D Button",
				ToolTipText = "2D Layers".Localize(),
				Enabled = false,
				Margin = commonMargin
			};
			Layers2DButton.Click += SwitchModes_Click;
			buttonGroupB.Add(Layers2DButton);
			this.AddChild(Layers2DButton);

			this.AddChild(new VerticalLine(50)
			{
				Margin = 3,
				Border = new BorderDouble(right: 5),
				BorderColor = new Color(theme.ButtonFactory.Options.NormalTextColor, 100)
			});

			foreach (var namedAction in ApplicationController.Instance.RegisteredSceneOperations())
			{
				var button = new TextButton(namedAction.Title.Localize(), theme)
				{
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					BackgroundColor = theme.MinimalShade
				};
				button.Click += (s, e) =>
				{
					namedAction.Action.Invoke(sceneContext.Scene);
				};
				this.AddChild(button);
			}

			this.AddChild(new HorizontalSpacer());

			this.AddChild(this.OverflowMenu = new OverflowMenu()
			{
				Name = "View3D Overflow Menu",
				AlignToRightEdge = true,
				Margin = 3
			});

			this.ViewMode = PartViewMode.Model;
		}

		private void SwitchModes_Click(object sender, MouseEventArgs e)
		{
			if (sender is GuiWidget widget)
			{
				if (widget.Name == "Layers2D Button")
				{
					this.ViewMode = PartViewMode.Layers2D;
				}
				else if (widget.Name == "Layers3D Button")
				{
					this.ViewMode = PartViewMode.Layers3D;
				}
				else
				{
					this.ViewMode = PartViewMode.Model;
				}

				ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs()
				{
					ViewMode = this.ViewMode
				});
			}
		}

		private PartViewMode viewMode;
		public PartViewMode ViewMode
		{
			get => viewMode;
			set
			{
				if (viewMode != value)
				{
					viewMode = value;

					string controlName;

					if (viewMode == PartViewMode.Layers2D)
					{
						controlName = "Layers2D Button";
					}
					else if (viewMode == PartViewMode.Layers3D)
					{
						controlName = "Layers3D Button";
					}
					else
					{
						controlName = "Model View Button";
					}

					var targetChild = Children.Where(c => c.Name == controlName).FirstOrDefault();
					if (targetChild != null && targetChild is RadioButton button)
					{
						button.Checked = true;
					}
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}