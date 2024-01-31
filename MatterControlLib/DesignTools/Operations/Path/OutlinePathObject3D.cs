﻿/*
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
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class OutlinePathObject3D : PathObject3DAbstract, IEditorDraw, IObject3DControlsProvider
    {
		public OutlinePathObject3D()
		{
			Name = "Outline Path".Localize();
		}

		[Description("The with of the outline.")]
		public DoubleOrExpression OutlineWidth { get; set; } = .5;

		[Description("The offset of the outside of the outline as a ratio of the width.")]
		public DoubleOrExpression Offset { get; set; } = .5;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ExpandStyles InnerStyle { get; set; } = ExpandStyles.Sharp;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ExpandStyles OuterStyle { get; set; } = ExpandStyles.Sharp;

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

        public override bool MeshIsSolidObject => false;
        
		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var outlineWidth = OutlineWidth.Value(this);
			if (outlineWidth < .01 || outlineWidth > 1000)
			{
				OutlineWidth = Math.Min(1000, Math.Max(.01, outlineWidth));
			}

			using (RebuildLock())
			{
				InsetPath();
				// set the mesh to show the path
				this.Mesh = VertexStorage.Extrude(Constants.PathPolygonsHeight);
			}

			Invalidate(InvalidateType.DisplayValues);

			this.DoRebuildComplete();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			return Task.CompletedTask;
		}

		private void InsetPath()
		{
			var path = this.CombinedVisibleChildrenPaths();
			if (path == null)
			{
				// clear our existing data
				VertexStorage = new VertexStorage();
				return;
			}

			var aPolys = path.CreatePolygons();

            aPolys = aPolys.GetCorrectedWinding();

			var offseter = new ClipperOffset();

			var outlineWidth = OutlineWidth.Value(this);
			var ratio = Offset.Value(this);

			offseter.AddPaths(aPolys, InflatePathObject3D.GetJoinType(OuterStyle), EndType.etClosedPolygon);
			var outerLoops = new List<List<IntPoint>>();
			offseter.Execute(ref outerLoops, outlineWidth * ratio * 1000);
			Clipper.CleanPolygons(outerLoops);

			offseter.AddPaths(aPolys, InflatePathObject3D.GetJoinType(InnerStyle), EndType.etClosedPolygon);
			var innerLoops = new List<List<IntPoint>>();
			offseter.Execute(ref innerLoops, -outlineWidth * (1 - ratio) * 1000);
			Clipper.CleanPolygons(innerLoops);

			var allLoops = outerLoops;
			allLoops.AddRange(innerLoops);

			VertexStorage = allLoops.CreateVertexStorage();

			VertexStorage.Add(0, 0, FlagsAndCommand.Stop);
		}
	}
}