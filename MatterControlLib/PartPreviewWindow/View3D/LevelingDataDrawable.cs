/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class LevelingDataDrawable : IDrawable
	{
		private Mesh levelingDataMesh;
		private ISceneContext sceneContext;
		private Color darkWireframe = new Color("#3334");

		public LevelingDataDrawable(ISceneContext sceneContext)
		{
			this.sceneContext = sceneContext;

			if (sceneContext.Printer is PrinterConfig printer)
			{
				try
				{
					levelingDataMesh = LevelingMeshVisualizer.BuildMeshFromLevelingData(printer);
				}
				catch
				{
					// Create empty mesh if exception thrown building leveling mesh
					levelingDataMesh = new Mesh();
				}
			}
		}

		public string Title => "Leveling Data Visualizer".Localize();

		public string Description => "Render Leveling Data on the bed".Localize();

		public bool Enabled { get; set; }

		public DrawStage DrawStage => DrawStage.OpaqueContent;

		public void Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (levelingDataMesh != null)
			{
				GLHelper.Render(levelingDataMesh,
					Color.Blue,
					Matrix4X4.Identity,
					sceneContext.ViewState.RenderType,
					Matrix4X4.Identity * world.ModelviewMatrix,
					darkWireframe);
			}
		}
	}
}
