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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SetTemperatureObject3D : Object3D, IObject3DControlsProvider, IGCodeTransformer, IEditorDraw
	{
		private bool hasBeenReached;
		private double accumulatedLayerHeight;

		public SetTemperatureObject3D()
		{
			Name = "Set Temperature".Localize();
			Color = Color.White.WithAlpha(.2);
			Mesh = new RoundedRect(-20, -20, 20, 20, 3)
			{
				ResolutionScale = 10
			}.Extrude(.2);
		}

		public static async Task<SetTemperatureObject3D> Create()
		{
			var item = new SetTemperatureObject3D();
			await item.Rebuild();
			return item;
		}

		public double Temperature { get; set; } = 210;

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
			bool valuesChanged = false;

			using (RebuildLock())
			{
				Temperature = agg_basics.Clamp(Temperature, 140, 400, ref valuesChanged);

				using (new CenterAndHeightMaintainer(this))
				{
				}
			}

			Invalidate(InvalidateType.DisplayValues);

			UpdateTexture();

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		private double WorldZ => default(Vector3).Transform(this.WorldMatrix()).Z;

		private (double temp, double worldZ) displayInfo = (double.MinValue, double.MinValue);

		public IEnumerable<string> ProcessCGcode(string lineToWrite, PrinterConfig printer)
		{
			if (!hasBeenReached
				&& lineToWrite.StartsWith("; LAYER_HEIGHT:"))
			{
				double layerHeight = 0;
				if (GCodeFile.GetFirstNumberAfter("; LAYER_HEIGHT", lineToWrite, ref layerHeight, out _, stopCheckingString: ":"))
				{
					accumulatedLayerHeight += layerHeight;
					if (accumulatedLayerHeight > WorldZ)
					{
						hasBeenReached = true;
						yield return $"M104 S{Temperature} ; Change Layer Temperature";
					}
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
			if (displayInfo.temp == double.MinValue
				|| displayInfo.temp != Temperature
				|| displayInfo.worldZ != WorldZ)
			{
				UpdateTexture();
			}
		}

		private void UpdateTexture()
		{
			Mesh.FaceTextures.Clear();
			displayInfo.temp = Temperature;
			displayInfo.worldZ = WorldZ;
			var theme = AppContext.Theme;
			var texture = new ImageBuffer(128, 128, 32);
			var graphics2D = texture.NewGraphics2D();
			graphics2D.Clear(theme.BackgroundColor);
			graphics2D.DrawString($"Height: {displayInfo.worldZ:0.##}",
				texture.Width / 2,
				texture.Height / 5 * 3,
				14,
				Agg.Font.Justification.Center,
				Agg.Font.Baseline.BoundsCenter,
				theme.TextColor);
			graphics2D.DrawString($"Temp: {displayInfo.temp:0.##}",
				texture.Width / 2,
				texture.Height / 5 * 2,
				14,
				Agg.Font.Justification.Center,
				Agg.Font.Baseline.BoundsCenter,
				theme.TextColor);
			Mesh.PlaceTextureOnFaces(0, texture);
		}
	}
}