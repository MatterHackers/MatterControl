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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class HoldChildProportional : Object3D
	{
		public AxisAlignedBoundingBox InitialChildBounds = AxisAlignedBoundingBox.Zero;

		public HoldChildProportional()
		{

		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix, bool requirePrecision = false)
		{
			// We return our calculated bounds not those of our children
			return InitialChildBounds.NewTransformed(this.Matrix * matrix);
		}

		bool initiatingChange = false;
		protected override void OnInvalidate()
		{
			if (!initiatingChange
				&& InitialChildBounds.XSize != 0)
			{
				initiatingChange = true;
				var currentBounds = this.GetAxisAlignedBoundingBox();
				// keep the bounds proportional
				double minScale = double.MaxValue;
				for (int i = 0; i < 3; i++)
				{
					minScale = Math.Min(minScale, currentBounds.Size[i] / InitialChildBounds.Size[i]);
				}

				Vector3 innerScale = Vector3.One;
				for (int i = 0; i < 3; i++)
				{
					innerScale[i] = InitialChildBounds.Size[i] / currentBounds.Size[i] * minScale;
				}

				Children.First().Matrix = Matrix4X4.CreateScale(innerScale);
				initiatingChange = false;
			}

			base.OnInvalidate();
		}

		public static void AddSelectionAsChildren(InteractiveScene scene, IObject3D newParent, string classDescriptor, string editorName)
		{
			if (scene.HasSelection)
			{
				IObject3D itemToHoldProportional;

				List<IObject3D> itemsToReplace;

				if (scene.SelectedItem is SelectionGroup)
				{
					Object3D container = new Object3D();
					itemsToReplace = scene.SelectedItem.Children.ToList();
					foreach (var child in itemsToReplace)
					{
						container.Children.Add(child.Clone());
					}
					itemToHoldProportional = container;
				}
				else
				{
					itemsToReplace = new List<IObject3D> { scene.SelectedItem };
					itemToHoldProportional = scene.SelectedItem.Clone();
				}

				scene.SelectedItem = null;

				newParent.Children.Add(itemToHoldProportional);

				newParent.MakeNameNonColliding();

				scene.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						itemsToReplace,
						new List<IObject3D> { newParent }));

				// Make the object have an identity matrix and keep its position in our new object
				newParent.Matrix = itemToHoldProportional.Matrix;
				itemToHoldProportional.Matrix = Matrix4X4.Identity;

				if (newParent is HoldChildProportional pe)
				{
					pe.InitialChildBounds = itemToHoldProportional.GetAxisAlignedBoundingBox();
				}

				scene.SelectedItem = newParent;
			}
		}
	}

	public class ProportionalEditor : IObject3DEditor
	{
		private MeshWrapperOperation group;
		private View3DWidget view3DWidget;
		public string Name => "Proportional Scale";

		public bool Unlocked { get; } = true;

		public GuiWidget Create(IObject3D group, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.group = group as MeshWrapperOperation;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			return mainContainer;
		}

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(HoldChildProportional),
		};
	}
}