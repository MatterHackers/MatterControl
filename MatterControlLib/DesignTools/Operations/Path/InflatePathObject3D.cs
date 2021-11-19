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

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public enum ExpandStyles
	{
		Flat,
		Round,
		Sharp,
	}

	public class InflatePathObject3D : Object3D, IPathObject, IEditorDraw, IObject3DControlsProvider
	{
		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		public InflatePathObject3D()
		{
			Name = "Inflate Path".Localize();
		}

		[Description("The amount to expand the path lines.")]
		public DoubleOrExpression Inflate { get; set; } = 1;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ExpandStyles Style { get; set; } = ExpandStyles.Sharp;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

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
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			using (RebuildLock())
			{
				InsetPath();
				// set the mesh to show the path
				this.Mesh = this.VertexSource.Extrude(Constants.PathPolygonsHeight);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			return Task.CompletedTask;
		}

		private void InsetPath()
		{
			var path = this.Children.OfType<IPathObject>()?.FirstOrDefault();
			if (path == null)
			{
				// clear our existing data
				VertexSource = new VertexStorage();
				return;
			}

			VertexSource = path.VertexSource.Offset(Inflate.Value(this), GetJoinType(Style));
		}

		internal static JoinType GetJoinType(ExpandStyles style)
		{
			ClipperLib.JoinType joinType = ClipperLib.JoinType.jtMiter;
			switch (style)
			{
				case ExpandStyles.Flat:
					joinType = ClipperLib.JoinType.jtSquare;
					break;
				case ExpandStyles.Round:
					joinType = ClipperLib.JoinType.jtRound;
					break;
			}

			return joinType;
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			this.DrawPath();
		}
	}
}