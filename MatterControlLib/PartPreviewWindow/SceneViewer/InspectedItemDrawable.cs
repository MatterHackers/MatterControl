/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.DataConverters3D;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InspectedItemDrawable : IDrawableItem
	{
		private InteractiveScene scene;
		private Color debugBorderColor = Color.Green;

		public InspectedItemDrawable(ISceneContext sceneContext)
		{
			this.scene = sceneContext.Scene;
		}

		public bool Enabled { get; set; } = true;

		public string Title { get; } = "Inspected Item Debugger";

		public string Description { get; } = "Render inspector selection";

		public DrawStage DrawStage { get; } = DrawStage.TransparentContent;

		public void Draw(GuiWidget sender, IObject3D item, bool isSelected, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (item == scene.DebugItem)
			{
				var aabb = item.GetAxisAlignedBoundingBox();

				world.RenderAabb(aabb, Matrix4X4.Identity, debugBorderColor, 1);

				if (item.Mesh != null)
				{
					GLHelper.Render(
						item.Mesh,
						debugBorderColor,
						item.WorldMatrix(),
						RenderTypes.Wireframe,
						item.WorldMatrix() * world.ModelviewMatrix);
				}
			}
		}
	}
}