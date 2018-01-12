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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class ScaleControls : FlowLayoutWidget, IIgnoredPopupChild
	{
		private EditableNumberDisplay[] sizeDisplay = new EditableNumberDisplay[3];
		internal CheckBox uniformScale;
		internal CheckBox usePercents;
		internal Vector3 scaleRatios = Vector3.One;
		private ThemeConfig theme;
		private InteractiveScene scene;

		public ScaleControls(InteractiveScene scene, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.scene = scene;
			this.Padding = 15;

			List<GuiWidget> scaleControls = new List<GuiWidget>();

			// Put in the scale ratio edit field
			this.AddChild(new ScaleOptionsPanel(this, theme));

			// Going to use this in the scaling controls, create it early.
			usePercents = new CheckBox("Use Percents".Localize(), textColor: theme.Colors.PrimaryTextColor, textSize: theme.DefaultFontSize);

			// add in the dimensions
			this.AddChild(CreateAxisScalingControl("x".ToUpper(), 0));
			this.AddChild(CreateAxisScalingControl("y".ToUpper(), 1));
			this.AddChild(CreateAxisScalingControl("z".ToUpper(), 2));

			// lock ratio checkbox
			uniformScale = new CheckBox("Lock Ratio".Localize(), textColor: theme.Colors.PrimaryTextColor, textSize: theme.DefaultFontSize);
			uniformScale.Margin = new BorderDouble(5, 3);
			uniformScale.Checked = true;
			uniformScale.CheckedStateChanged += (s, e) =>
			{
				if (uniformScale.Checked)
				{
					scaleRatios = Vector3.One;
					UpdateSizeValues();
				}
			};
			this.AddChild(uniformScale);

			// percent checkbox
			usePercents.Margin = new BorderDouble(5, 3);
			usePercents.Checked = false;
			usePercents.CheckedStateChanged += (s, e) =>
			{
				UpdateSizeValues();
			};
			this.AddChild(usePercents);

			// put in the apply button
			var applyScaleButton = new TextButton("Apply Scale".Localize(), theme)
			{
				VAnchor = VAnchor.Absolute,
				HAnchor = HAnchor.Right,
				BackgroundColor = theme.SlightShade,
				Cursor = Cursors.Hand
			};
			this.AddChild(applyScaleButton);

			scaleControls.Add(applyScaleButton);
			applyScaleButton.Click += (s, e) =>
			{
				var selectedItem = scene.SelectedItem;
				if (selectedItem != null)
				{
					Matrix4X4 startingTransform = selectedItem.Matrix;

					ScaleAxis(selectedItem, scaleRatios[0], 0);
					ScaleAxis(selectedItem, scaleRatios[1], 1);
					ScaleAxis(selectedItem, scaleRatios[2], 2);

					scene.UndoBuffer.Add(new TransformCommand(selectedItem, startingTransform, selectedItem.Matrix));
					scaleRatios = Vector3.One;
					UpdateSizeValues();
				}
			};

			scene.SelectionChanged += (s, e) =>
			{
				UpdateSizeValues();
			};

			scene.Invalidated += (s, e) =>
			{
				UpdateSizeValues();
			};

			UpdateSizeValues();
		}

		private GuiWidget CreateAxisScalingControl(string axis, int axisIndex)
		{
			var leftToRight = new FlowLayoutWidget
			{
				Padding = new BorderDouble(5, 3)
			};

			var sizeDescription = new TextWidget("{0}:".FormatWith(axis), textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
			{
				VAnchor = VAnchor.Center
			};
			leftToRight.AddChild(sizeDescription);

			sizeDisplay[axisIndex] = new EditableNumberDisplay(100, "1000.00", theme.Colors.PrimaryTextColor);
			sizeDisplay[axisIndex].DisplayFormat = "{0:0.00}";
			sizeDisplay[axisIndex].ValueChanged += (sender, e) =>
			{
				var selectedItem = scene.SelectedItem;
				if (selectedItem != null)
				{
					Matrix4X4 startingTransform = selectedItem.Matrix;
					SetNewModelSize(axisIndex);
					UpdateSizeValues();
				}
				else
				{
					sizeDisplay[axisIndex].ValueDirect = 0;
				}
			};

			leftToRight.AddChild(sizeDisplay[axisIndex]);

			var units = new TextWidget("mm".FormatWith(axis), textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
			{
				VAnchor = VAnchor.Center
			};
			leftToRight.AddChild(units);

			usePercents.CheckedStateChanged += (s, e) =>
			{
				if (usePercents.Checked)
				{
					sizeDisplay[axisIndex].DisplayFormat = "{0:0.##}";
					units.Text = "%";
				}
				else
				{
					sizeDisplay[axisIndex].DisplayFormat = "{0:0.00}";
					units.Text = "mm";
				}
			};

			return leftToRight;
		}

		class DropDownMenuFloating : DropDownMenu, IIgnoredPopupChild
		{
			internal DropDownMenuFloating()
				: base("Scale ", Direction.Down)
			{

			}
		}

		public class ScaleOptionsPanel : FlowLayoutWidget, IIgnoredPopupChild
		{
			public ScaleOptionsPanel(ScaleControls scaleControls, ThemeConfig theme)
				: base(FlowDirection.TopToBottom)
			{
				this.HAnchor = HAnchor.Stretch;

				var scaleSettings = new Dictionary<double, string>()
				{
					{ 1, "Scale".Localize()},
					{ .0393, "mm to in (.0393)"},
					{ 25.4, "in to mm (25.4)"},
					{ .1, "mm to cm (.1)"},
					{ 10, "cm to mm (10)"},
				};

				var dropDownList = new DropDownList("Scale".Localize(), theme.Colors.PrimaryTextColor, Direction.Down)
				{
					HAnchor = HAnchor.Left
				};

				foreach (var scaleSetting in scaleSettings)
				{
					MenuItem newItem = dropDownList.AddItem(scaleSetting.Value);

					newItem.Selected += (sender, e) =>
					{
						scaleControls.uniformScale.Checked = true;
						var scale = scaleSetting.Key;
						scaleControls.scaleRatios = new Vector3(scale, scale, scale);
						scaleControls.UpdateSizeValues();

						dropDownList.SelectedLabel = "Scale".Localize();
					};
				}
				this.AddChild(dropDownList);
			}
		}

		private void ScaleAxis(IObject3D selectedItem, double scaleIn, int axis)
		{
			AxisAlignedBoundingBox originalMeshBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

			Vector3 newScale = Vector3.One;
			newScale[axis] = scaleIn;
			Matrix4X4 totalScale = Matrix4X4.CreateScale(newScale);

			selectedItem.Matrix = PlatingHelper.ApplyAtCenter(selectedItem, totalScale);

			// keep the bottom where it was
			AxisAlignedBoundingBox scaledBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			PlatingHelper.PlaceMeshAtHeight(selectedItem, originalMeshBounds.minXYZ.Z);

			Invalidate();
		}

		private void SetNewModelSize(int axis)
		{
			var selectedItem = scene.SelectedItem;
			if (selectedItem != null)
			{
				// because we remove any current scale before we change to a new one we only get the size of the base mesh data
				AxisAlignedBoundingBox originalMeshBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				double currentSize = originalMeshBounds.Size[axis];
				double desiredSize = sizeDisplay[axis].Value;
				if (usePercents.Checked)
				{
					desiredSize = originalMeshBounds.Size[axis] * sizeDisplay[axis].Value / 100.0;
				}

				double scaleFactor = 1;
				if (currentSize != 0)
				{
					scaleFactor = desiredSize / currentSize;
				}

				if (uniformScale.Checked)
				{
					scaleRatios = new Vector3(scaleFactor, scaleFactor, scaleFactor);
				}
				else
				{
					scaleRatios[axis] = scaleFactor;
				}
			}
		}

		internal void UpdateSizeValues()
		{
			var selectedItem = scene.SelectedItem;

			if (sizeDisplay[0] != null
				&& selectedItem != null)
			{
				AxisAlignedBoundingBox bounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				if (usePercents.Checked)
				{
					sizeDisplay[0].ValueDirect = scaleRatios[0] * 100;
					sizeDisplay[1].ValueDirect = scaleRatios[1] * 100;
					sizeDisplay[2].ValueDirect = scaleRatios[2] * 100;
				}
				else
				{
					sizeDisplay[0].ValueDirect = bounds.Size[0] * scaleRatios[0];
					sizeDisplay[1].ValueDirect = bounds.Size[1] * scaleRatios[1];
					sizeDisplay[2].ValueDirect = bounds.Size[2] * scaleRatios[2];
				}
			}
			else
			{
				sizeDisplay[0].ValueDirect = 0;
				sizeDisplay[1].ValueDirect = 0;
				sizeDisplay[2].ValueDirect = 0;
			}
		}

	}
}