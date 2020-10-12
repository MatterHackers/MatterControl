/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.DesignTools.Operations
{

	public class OutlinePathObject3D : Object3D, IPathObject, IEditorDraw, IObject3DControlsProvider
	{
		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		public OutlinePathObject3D()
		{
			Name = "Outline Path".Localize();
		}

		[Description("The with of the outline.")]
		public double OutlineWidth { get; set; } = 3;

		public double Ratio { get; set; } = .5;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ExpandStyles InnerStyle { get; set; } = ExpandStyles.Sharp;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ExpandStyles OuterStyle { get; set; } = ExpandStyles.Sharp;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Path))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

			if (OutlineWidth < .01 || OutlineWidth > 1000)
			{
				OutlineWidth = Math.Min(1000, Math.Max(.01, OutlineWidth));
				valuesChanged = true;
			}

			using (RebuildLock())
			{
				InsetPath();
			}

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			return Task.CompletedTask;
		}

		private void InsetPath()
		{
			var path = this.Children.OfType<IPathObject>().FirstOrDefault();
			if (path == null)
			{
				// clear our existing data
				VertexSource = new VertexStorage();
				return;
			}

			var aPolys = path.VertexSource.CreatePolygons();

			var offseter = new ClipperOffset();

			offseter.AddPaths(aPolys, InflatePathObject3D.GetJoinType(OuterStyle), EndType.etClosedPolygon);
			var outerLoops = new List<List<IntPoint>>();
			offseter.Execute(ref outerLoops, OutlineWidth * Ratio * 1000);
			Clipper.CleanPolygons(outerLoops);

			offseter.AddPaths(aPolys, InflatePathObject3D.GetJoinType(InnerStyle), EndType.etClosedPolygon);
			var innerLoops = new List<List<IntPoint>>();
			offseter.Execute(ref innerLoops, -OutlineWidth * (1 - Ratio) * 1000);
			Clipper.CleanPolygons(innerLoops);

			var allLoops = outerLoops;
			allLoops.AddRange(innerLoops);

			VertexSource = allLoops.CreateVertexStorage();

			(VertexSource as VertexStorage).Add(0, 0, ShapePath.FlagsAndCommand.Stop);
		}

		public void DrawEditor(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			this.DrawPath();
		}
	}
}