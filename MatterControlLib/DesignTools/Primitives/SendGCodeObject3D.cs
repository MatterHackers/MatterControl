/*
Copyright (c) 2019, Lars Brubaker
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
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    public class SendGCodeObject3D : Object3D, IObject3DControlsProvider, IGCodeTransformer, IEditorDrawControled
	{
		private bool hasBeenReached;
		private double accumulatedLayerHeight;

		public SendGCodeObject3D()
		{
			Name = "Send G-Code".Localize();
			Color = Color.White.WithAlpha(.4);
			Mesh = new RoundedRect(-20, -20, 20, 20, 3)
			{
				ResolutionScale = 10
			}.Extrude(.2);
		}

		public static async Task<SendGCodeObject3D> Create()
		{
			var item = new SendGCodeObject3D();
			await item.Rebuild();
			return item;
		}

		public string GCodeToSend { get; set; } = "";

		public override bool Printable => false;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.MoveInZ | ControlTypes.Shadow | ControlTypes.ScaleMatrixXY);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

        public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
				}
			}

			Invalidate(InvalidateType.DisplayValues);

			UpdateTexture();

			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		private (string gcode, double worldZ) displayInfo = ("", double.MinValue);

		private double WorldZ => default(Vector3).Transform(this.WorldMatrix()).Z;

		public IEnumerable<string> ProcessCGcode(string lineToWrite, PrinterConfig printer)
		{
			if (!hasBeenReached
				&& lineToWrite.StartsWith("; LAYER_HEIGHT:"))
			{
				double layerHeight = 0;
				// this gives us the layer height we will be at AFTER this layer is done printing
				if (GCodeFile.GetFirstNumberAfter("; LAYER_HEIGHT", lineToWrite, ref layerHeight, out _, stopCheckingString: ":"))
				{
					// check if we are above the accumulated at the start of the layer but before adding in this layer height
					if (accumulatedLayerHeight > WorldZ)
					{
						hasBeenReached = true;
						yield return $"{GCodeToSend} ; G-Code from Scene Object";
					}

					accumulatedLayerHeight += layerHeight;
				}
			}
		}

		public void Reset()
		{
			hasBeenReached = false;
			accumulatedLayerHeight = 0;
		}


		public void DrawEditor(Object3DControlsLayer object3DControlLayer, DrawEventArgs e)
		{
			if (displayInfo.worldZ != WorldZ
                || displayInfo.gcode != DisplayGCode)
            {
				UpdateTexture();
			}
		}

        private string DisplayGCode
        {
            get
            {
				var max40Chars = GCodeToSend;
				if (GCodeToSend.Length > 35)
                {
					max40Chars = GCodeToSend.Substring(0, 35) + "...";
				}
				EnglishTextWrapping wrapper = new EnglishTextWrapping(10);
				max40Chars = wrapper.InsertCRs(max40Chars, 120);
				return max40Chars;
            }
        }

		private void UpdateTexture()
		{
			Mesh.FaceTextures.Clear();
			displayInfo.worldZ = WorldZ;
			displayInfo.gcode = DisplayGCode;
			var theme = AppContext.Theme;
			var texture = new ImageBuffer(128, 128, 32);
			var graphics2D = texture.NewGraphics2D();
			graphics2D.Clear(theme.BackgroundColor);
			graphics2D.DrawString($"Height: {displayInfo.worldZ:0.##}",
				texture.Width / 2,
				texture.Height * .7,
				14,
				Agg.Font.Justification.Center,
				Agg.Font.Baseline.BoundsCenter,
				theme.TextColor);
			graphics2D.DrawString($"G-Code",
				texture.Width / 2,
				texture.Height * .45,
				14,
				Agg.Font.Justification.Center,
				Agg.Font.Baseline.BoundsCenter,
				theme.TextColor);
			var height = texture.Height * .37;
			graphics2D.Line(texture.Width / 5, height, texture.Width / 5 * 4, height, theme.TextColor);
			graphics2D.DrawString($"{displayInfo.gcode}",
				texture.Width / 2,
				texture.Height * .3,
				10,
				Agg.Font.Justification.Center,
				Agg.Font.Baseline.BoundsCenter,
				theme.TextColor);
			Mesh.PlaceTextureOnFaces(0, texture);
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return AxisAlignedBoundingBox.Empty();
		}

        public bool DoEditorDraw(bool isSelected)
        {
			return true;
        }
    }
}