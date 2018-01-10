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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class SelectedObjectPanel
	{
		public class AlignControls : IgnoredPopupWidget
		{
			private InteractiveScene scene;
			private ThemeConfig theme;

			public AlignControls(InteractiveScene scene, ThemeConfig theme)
			{
				this.scene = scene;
				this.theme = theme;
				this.HAnchor = HAnchor.Fit;
				this.VAnchor = VAnchor.Fit;
				this.Padding = new BorderDouble(5, 5, 5, 0);

				FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.Fit,
					HAnchor = HAnchor.Fit,
				};
				this.AddChild(buttonPanel);

				string[] axisNames = new string[] { "X", "Y", "Z" };
				for (int axisIndex = 0; axisIndex < 3; axisIndex++)
				{
					var alignButtons = new FlowLayoutWidget(FlowDirection.LeftToRight)
					{
						HAnchor = HAnchor.Fit,
						Padding = new BorderDouble(5)
					};
					buttonPanel.AddChild(alignButtons);

					alignButtons.AddChild(new TextWidget(axisNames[axisIndex])
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(0, 0, 3, 0)
					});

					alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Min, "Min"));
					alignButtons.AddChild(new HorizontalSpacer());
					alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Center, "Center"));
					alignButtons.AddChild(new HorizontalSpacer());
					alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Max, "Max"));
					alignButtons.AddChild(new HorizontalSpacer());
				}

				var dualExtrusionAlignButton = theme.MenuButtonFactory.Generate("Align for Dual Extrusion".Localize());
				dualExtrusionAlignButton.Margin = new BorderDouble(21, 0);
				dualExtrusionAlignButton.HAnchor = HAnchor.Left;
				buttonPanel.AddChild(dualExtrusionAlignButton);

				AddAlignDelegates(0, AxisAlignment.SourceCoordinateSystem, dualExtrusionAlignButton);
			}

			private GuiWidget CreateAlignButton(int axisIndex, AxisAlignment alignment, string lable)
			{
				var smallMarginButtonFactory = theme.MenuButtonFactory;
				var alignButton = smallMarginButtonFactory.Generate(lable);
				alignButton.Margin = new BorderDouble(3, 0);

				AddAlignDelegates(axisIndex, alignment, alignButton);

				return alignButton;
			}

			private void AddAlignDelegates(int axisIndex, AxisAlignment alignment, Button alignButton)
			{
				alignButton.Click += (sender, e) =>
				{
					if (scene.HasSelection)
					{
						var transformDatas = GetTransforms(axisIndex, alignment);
						scene.UndoBuffer.AddAndDo(new TransformCommand(transformDatas));

						//scene.SelectedItem.MaterialIndex = extruderIndexCanPassToClick;
						scene.Invalidate();
					}
				};

				alignButton.MouseEnter += (s2, e2) =>
				{
					if (scene.HasSelection)
					{
						// make a preview of the new positions
						var transformDatas = GetTransforms(axisIndex, alignment);
						scene.Children.Modify((list) =>
						{
							foreach (var transform in transformDatas)
							{
								var copy = transform.TransformedObject.Clone();
								copy.Matrix = transform.RedoTransform;
								copy.Color = new Color(Color.Gray, 126);
								list.Add(copy);
							}
						});
					}
				};

				alignButton.MouseLeave += (s3, e3) =>
				{
					if (scene.HasSelection)
					{
						// clear the preview of the new positions
						scene.Children.Modify((list) =>
						{
							for (int i = list.Count - 1; i >= 0; i--)
							{
								if (list[i].Color.Alpha0To255 == 126)
								{
									list.RemoveAt(i);
								}
							}
						});
					}
				};
			}

			private List<TransformData> GetTransforms(int axisIndex, AxisAlignment alignment)
			{
				var transformDatas = new List<TransformData>();
				var totalAABB = scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				Vector3 firstSourceOrigin = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);

				// move the objects to the right place
				foreach (var child in scene.SelectedItem.Children)
				{
					var childAABB = child.GetAxisAlignedBoundingBox(scene.SelectedItem.Matrix);
					var offset = new Vector3();
					switch (alignment)
					{
						case AxisAlignment.Min:
							offset[axisIndex] = totalAABB.minXYZ[axisIndex] - childAABB.minXYZ[axisIndex];
							break;

						case AxisAlignment.Center:
							offset[axisIndex] = totalAABB.Center[axisIndex] - childAABB.Center[axisIndex];
							break;

						case AxisAlignment.Max:
							offset[axisIndex] = totalAABB.maxXYZ[axisIndex] - childAABB.maxXYZ[axisIndex];
							break;

						case AxisAlignment.SourceCoordinateSystem:
							{
								// move the object back to the origin
								offset = -Vector3.Transform(Vector3.Zero, child.Matrix);

								// figure out how to move it back to the start center
								if (firstSourceOrigin.X == double.MaxValue)
								{
									firstSourceOrigin = -offset;
								}

								offset += firstSourceOrigin;
							}
							break;
					}
					transformDatas.Add(new TransformData()
					{
						TransformedObject = child,
						RedoTransform = child.Matrix * Matrix4X4.CreateTranslation(offset),
						UndoTransform = child.Matrix,
					});
				}

				return transformDatas;
			}
		}
	}
}