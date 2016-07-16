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
//#define DoBooleanTest

using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class ScaleControls : FlowLayoutWidget
	{
		private Button applyScaleButton;
		private CheckBox expandScaleOptions;
		private FlowLayoutWidget scaleOptionContainer;
		private MHNumberEdit scaleRatioControl;
		private EditableNumberDisplay[] sizeDisplay = new EditableNumberDisplay[3];
		private CheckBox uniformScale;
		private View3DWidget view3DWidget;

		public ScaleControls(View3DWidget view3DWidget)
			: base(FlowDirection.TopToBottom)
		{
			this.view3DWidget = view3DWidget;
			{
				expandScaleOptions = view3DWidget.ExpandMenuOptionFactory.GenerateCheckBoxButton("Scale".Localize().ToUpper(), StaticData.Instance.LoadIcon("icon_arrow_right_no_border_32x32.png", 32, 32).InvertLightness());
				expandScaleOptions.Margin = new BorderDouble(bottom: 2);
				this.AddChild(expandScaleOptions);

				scaleOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				scaleOptionContainer.HAnchor = HAnchor.ParentLeftRight;
				scaleOptionContainer.Visible = false;
				this.AddChild(scaleOptionContainer);

				AddScaleControls(scaleOptionContainer);
			}

			expandScaleOptions.CheckedStateChanged += expandScaleOptions_CheckedStateChanged;

			view3DWidget.SelectedTransformChanged += OnSelectedTransformChanged;
		}

		private void AddScaleControls(FlowLayoutWidget buttonPanel)
		{
			List<GuiWidget> scaleControls = new List<GuiWidget>();

			// Put in the scale ratio edit field
			{
				FlowLayoutWidget scaleRatioContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
				scaleRatioContainer.HAnchor = HAnchor.ParentLeftRight;
				scaleRatioContainer.Padding = new BorderDouble(5);

				string scaleRatioLabelText = "Ratio".Localize();
				string scaleRatioLabelTextFull = "{0}:".FormatWith(scaleRatioLabelText);
				TextWidget scaleRatioLabel = new TextWidget(scaleRatioLabelTextFull, textColor: ActiveTheme.Instance.PrimaryTextColor);
				scaleRatioLabel.Margin = new BorderDouble(0, 0, 3, 0);
				scaleRatioLabel.VAnchor = VAnchor.ParentCenter;
				scaleRatioContainer.AddChild(scaleRatioLabel);

				scaleRatioContainer.AddChild(new HorizontalSpacer());

				scaleRatioControl = new MHNumberEdit(1, pixelWidth: 50 * GuiWidget.DeviceScale, allowDecimals: true, increment: .05);
				scaleRatioControl.SelectAllOnFocus = true;
				scaleRatioControl.VAnchor = VAnchor.ParentCenter;
				scaleRatioContainer.AddChild(scaleRatioControl);
				scaleRatioControl.ActuallNumberEdit.KeyPressed += (sender, e) =>
				{
					OnSelectedTransformChanged(this, null);
				};

				scaleRatioControl.ActuallNumberEdit.KeyDown += (sender, e) =>
				{
					OnSelectedTransformChanged(this, null);
				};

				scaleRatioControl.ActuallNumberEdit.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
				{
					ApplyScaleFromEditField();
				};

				scaleRatioContainer.AddChild(CreateScaleDropDownMenu());

				buttonPanel.AddChild(scaleRatioContainer);

				scaleControls.Add(scaleRatioControl);
			}

			applyScaleButton = view3DWidget.WhiteButtonFactory.Generate("Apply Scale".Localize(), centerText: true);
			applyScaleButton.Cursor = Cursors.Hand;
			buttonPanel.AddChild(applyScaleButton);

			scaleControls.Add(applyScaleButton);
			applyScaleButton.Click += (object sender, EventArgs mouseEvent) =>
			{
				ApplyScaleFromEditField();
			};

			// add in the dimensions
			{
				buttonPanel.AddChild(CreateAxisScalingControl("x".ToUpper(), 0));
				buttonPanel.AddChild(CreateAxisScalingControl("y".ToUpper(), 1));
				buttonPanel.AddChild(CreateAxisScalingControl("z".ToUpper(), 2));

				uniformScale = new CheckBox("Lock Ratio".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				uniformScale.Checked = true;

				FlowLayoutWidget leftToRight = new FlowLayoutWidget();
				leftToRight.Padding = new BorderDouble(5, 3);

				leftToRight.AddChild(uniformScale);
				buttonPanel.AddChild(leftToRight);
			}

			buttonPanel.AddChild(view3DWidget.GenerateHorizontalRule());
		}

		private void ApplyScaleFromEditField()
		{
			if (view3DWidget.HaveSelection)
			{
				Matrix4X4 startingTransform = view3DWidget.SelectedMeshGroupTransform;

				double scale = scaleRatioControl.ActuallNumberEdit.Value;
				if (scale > 0)
				{
					ScaleAxis(scale, 0);
					ScaleAxis(scale, 1);
					ScaleAxis(scale, 2);
				}

				view3DWidget.AddUndoForSelectedMeshGroupTransform(startingTransform);
			}
		}

		private GuiWidget CreateAxisScalingControl(string axis, int axisIndex)
		{
			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			leftToRight.Padding = new BorderDouble(5, 3);

			TextWidget sizeDescription = new TextWidget("{0}:".FormatWith(axis), textColor: ActiveTheme.Instance.PrimaryTextColor);
			sizeDescription.VAnchor = Agg.UI.VAnchor.ParentCenter;
			leftToRight.AddChild(sizeDescription);

			sizeDisplay[axisIndex] = new EditableNumberDisplay(view3DWidget.textImageButtonFactory, "100", "1000.00");
			sizeDisplay[axisIndex].EditComplete += (sender, e) =>
			{
				if (view3DWidget.HaveSelection)
				{
					Matrix4X4 startingTransform = view3DWidget.SelectedMeshGroupTransform;
					SetNewModelSize(sizeDisplay[axisIndex].GetValue(), axisIndex);
					sizeDisplay[axisIndex].SetDisplayString("{0:0.00}".FormatWith(view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox().Size[axisIndex]));
					OnSelectedTransformChanged(null, null);
					view3DWidget.AddUndoForSelectedMeshGroupTransform(startingTransform);
				}
				else
				{
					sizeDisplay[axisIndex].SetDisplayString("---");
				}
			};

			leftToRight.AddChild(sizeDisplay[axisIndex]);

			return leftToRight;
		}

		private DropDownMenu CreateScaleDropDownMenu()
		{
			DropDownMenu presetScaleMenu = new DropDownMenu("", Direction.Down);
			presetScaleMenu.NormalArrowColor = ActiveTheme.Instance.PrimaryTextColor;
			presetScaleMenu.HoverArrowColor = ActiveTheme.Instance.PrimaryTextColor;
			presetScaleMenu.MenuAsWideAsItems = false;
			presetScaleMenu.AlignToRightEdge = true;
			//presetScaleMenu.OpenOffset = new Vector2(-50, 0);
			presetScaleMenu.HAnchor = HAnchor.AbsolutePosition;
			presetScaleMenu.VAnchor = VAnchor.AbsolutePosition;
			presetScaleMenu.Width = 25;
			presetScaleMenu.Height = scaleRatioControl.Height + 2;

			presetScaleMenu.AddItem("mm to in (.0393)");
			presetScaleMenu.AddItem("in to mm (25.4)");
			presetScaleMenu.AddItem("mm to cm (.1)");
			presetScaleMenu.AddItem("cm to mm (10)");
			string resetLable = "reset".Localize();
			string resetLableFull = "{0} (1)".FormatWith(resetLable);
			presetScaleMenu.AddItem(resetLableFull);

			presetScaleMenu.SelectionChanged += (sender, e) =>
			{
				double scale = 1;
				switch (presetScaleMenu.SelectedIndex)
				{
					case 0:
						scale = 1.0 / 25.4;
						break;

					case 1:
						scale = 25.4;
						break;

					case 2:
						scale = .1;
						break;

					case 3:
						scale = 10;
						break;

					case 4:
						scale = 1;
						break;
				}

				scaleRatioControl.ActuallNumberEdit.Value = scale;
				ApplyScaleFromEditField();
			};

			return presetScaleMenu;
		}

		private void expandScaleOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (scaleOptionContainer.Visible != expandScaleOptions.Checked)
			{
				scaleOptionContainer.Visible = expandScaleOptions.Checked;
			}
		}

		private void ScaleAxis(double scaleIn, int axis)
		{
			AxisAlignedBoundingBox originalMeshBounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox();

			Vector3 newScale = Vector3.One;
			newScale[axis] = scaleIn;
			Matrix4X4 totalScale = Matrix4X4.CreateScale(newScale);

			view3DWidget.SelectedMeshGroupTransform = PlatingHelper.ApplyAtCenter(view3DWidget.SelectedMeshGroup, view3DWidget.SelectedMeshGroupTransform, totalScale);

			// keep the bottom where it was
			AxisAlignedBoundingBox scaledBounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox(view3DWidget.SelectedMeshGroupTransform);
			PlatingHelper.PlaceMeshAtHeight(view3DWidget.MeshGroups, view3DWidget.MeshGroupTransforms, view3DWidget.SelectedMeshGroupIndex, scaledBounds.minXYZ.z);

			PlatingHelper.PlaceMeshGroupOnBed(view3DWidget.MeshGroups, view3DWidget.MeshGroupTransforms, view3DWidget.SelectedMeshGroupIndex);

			view3DWidget.PartHasBeenChanged();
			Invalidate();
			OnSelectedTransformChanged(this, null);
		}

		private void SetNewModelSize(double sizeInMm, int axis)
		{
			if (view3DWidget.HaveSelection)
			{
				// because we remove any current scale before we change to a new one we only get the size of the base mesh data
				AxisAlignedBoundingBox originalMeshBounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox(view3DWidget.SelectedMeshGroupTransform);

				double currentSize = originalMeshBounds.Size[axis];
				double desiredSize = sizeDisplay[axis].GetValue();
				double scaleFactor = 1;
				if (currentSize != 0)
				{
					scaleFactor = desiredSize / currentSize;
				}

				if (uniformScale.Checked)
				{
					scaleRatioControl.ActuallNumberEdit.Value = scaleFactor;
					ApplyScaleFromEditField();
				}
				else
				{
					ScaleAxis(scaleFactor, axis);
				}
			}
		}

		private void OnSelectedTransformChanged(object sender, EventArgs e)
		{
			if (sizeDisplay[0] != null
				&& view3DWidget.SelectedMeshGroup != null)
			{
				AxisAlignedBoundingBox bounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox(view3DWidget.SelectedMeshGroupTransform);
				sizeDisplay[0].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[0]));
				sizeDisplay[1].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[1]));
				sizeDisplay[2].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[2]));

				// set the scaling to be this new size
				AxisAlignedBoundingBox originalMeshBounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox scaledBounds = view3DWidget.SelectedMeshGroup.GetAxisAlignedBoundingBox(view3DWidget.SelectedMeshGroupTransform);
				Vector3 currentScale = new Vector3();
				currentScale.x = scaledBounds.XSize / originalMeshBounds.XSize;
				currentScale.y = scaledBounds.YSize / originalMeshBounds.YSize;
				currentScale.z = scaledBounds.ZSize / originalMeshBounds.ZSize;
			}
			else
			{
				sizeDisplay[0].SetDisplayString("---");
				sizeDisplay[1].SetDisplayString("---");
				sizeDisplay[2].SetDisplayString("---");
			}
		}
	}
}