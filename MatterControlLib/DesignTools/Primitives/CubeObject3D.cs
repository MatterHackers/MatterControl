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

using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CubeObject3D : PrimitiveObject3D, IObject3DControlsProvider
	{
		public CubeObject3D()
		{
			Name = "Cube".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cube"];
		}

		public override string ThumbnailName => "Cube";

		/// <summary>
		/// This is the actual serialized with that can use expressions
		/// </summary>
		[MaxDecimalPlaces(2)]
		public DoubleConstantOrReference Width { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		public DoubleConstantOrReference Depth { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		public DoubleConstantOrReference Height { get; set; } = 20;

		public static async Task<CubeObject3D> Create()
		{
			var item = new CubeObject3D();
			await item.Rebuild();
			return item;
		}

		public static async Task<CubeObject3D> Create(double x, double y, double z)
		{
			var item = new CubeObject3D()
			{
				Width = x,
				Depth = y,
				Height = z,
			};

			await item.Rebuild();
			return item;
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			double getHeight() => Height.Value(this);
			void setHeight(double height) => Height = height;

			var controls = object3DControlsLayer.Object3DControls;

			controls.Add(new ScaleHeightControl(object3DControlsLayer, getHeight, setHeight));
			object3DControlsLayer.AddWidthDepthControls(getHeight, setHeight);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateType.Source == this)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.SheetUpdated))
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
					Mesh = PlatonicSolids.CreateCube(Width.Value(this), Depth.Value(this), Height.Value(this));
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}
	}

	public interface IObjectWithWidthAndDepth
	{
		double Width { get; set; }
		double Depth { get; set; }
	}
}