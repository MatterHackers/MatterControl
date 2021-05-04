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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class LinearExtrudeObject3D : Object3D
#if DEBUG
, IPropertyGridModifier
#endif
	{
		public double Height { get; set; } = 5;

#if DEBUG
		[Description("Bevel the top of the extrusion")]
		public bool BevelTop { get; set; } = false;

		[Description("The amount to inset the bevel")]
		public double BevelInset { get; set; } = 2;

		/// <summary>
		[Description("The height the bevel will start")]
		/// </summary>
		public double BevelStart { get; set; } = 4;

		public int BevelSteps { get; set; } = 1;
#endif

		public override bool CanFlatten => true;

		[JsonIgnore]
		private IVertexSource VertexSource
		{
			get
			{
				var item = this.Descendants().Where((d) => d is IPathObject).FirstOrDefault();
				if (item is IPathObject pathItem)
				{
					return pathItem.VertexSource;
				}

				return null;
			}
		}

		public override void Flatten(UndoBuffer undoBuffer)
		{
			if (Mesh == null)
			{
				Remove(undoBuffer);
			}
			else
			{
				// only keep the mesh and get rid of everything else
				using (RebuildLock())
				{
					var meshOnlyItem = new Object3D()
					{
						Mesh = this.Mesh.Copy(CancellationToken.None)
					};

					meshOnlyItem.CopyProperties(this, Object3DPropertyFlags.All);

					// and replace us with the children
					undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { meshOnlyItem }));
				}

				Invalidate(InvalidateType.Children);
			}
		}

		public LinearExtrudeObject3D()
		{
			Name = "Linear Extrude".Localize();
		}

		public override async void OnInvalidate(InvalidateArgs eventArgs)
		{
			if ((eventArgs.InvalidateType.HasFlag(InvalidateType.Path)
					|| eventArgs.InvalidateType.HasFlag(InvalidateType.Children))
				&& eventArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (eventArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& eventArgs.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(eventArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool valuesChanged = false;

#if DEBUG
			if (BevelTop)
			{
				BevelSteps = agg_basics.Clamp(BevelSteps, 1, 32, ref valuesChanged);
				BevelStart = agg_basics.Clamp(BevelStart, 0, Height, ref valuesChanged);
				var aabb = this.GetAxisAlignedBoundingBox();
				BevelInset = agg_basics.Clamp(BevelInset, 0, Math.Min(aabb.XSize /2, aabb.YSize / 2), ref valuesChanged);
			}
#endif

			var rebuildLock = RebuildLock();
			// now create a long running task to process the image
			return ApplicationController.Instance.Tasks.Execute(
				"Linear Extrude".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var vertexSource = this.VertexSource;
					List<(double height, double inset)> bevel = null;
#if DEBUG
					if (BevelTop)
					{
						bevel = new List<(double height, double inset)>();
						for (int i = 0; i < BevelSteps; i++)
						{
							var heightRatio = i / (double)BevelSteps;
							var height = heightRatio * (Height - BevelStart) + BevelStart;
							var insetRatio = (i + 1) / (double)BevelSteps;
							var inset = Easing.Sinusoidal.In(insetRatio) * -BevelInset;
							bevel.Add((height, inset));
						}
					}
#endif

					Mesh = VertexSourceToMesh.Extrude(this.VertexSource, Height, bevel);
					if (Mesh.Vertices.Count == 0)
					{
						Mesh = null;
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						if (valuesChanged)
						{
							Invalidate(InvalidateType.DisplayValues);
						}
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
					});
					return Task.CompletedTask;
				});
		}

#if DEBUG
		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(BevelStart), () => BevelTop);
			change.SetRowVisible(nameof(BevelInset), () => BevelTop);
			change.SetRowVisible(nameof(BevelSteps), () => BevelTop);
		}
#endif
	}
}