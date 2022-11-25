/*
Copyright (c) 2018, John Lewin
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

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Plugins.Lithophane
{
	[ShowUpdateButton(SuppressPropertyChangeUpdates = true)]
	public class LithophaneObject3D : Object3D
	{
		public LithophaneObject3D()
		{
			this.Name = "Lithophane".Localize();
		}

		[JsonIgnore]
		public IImageProvider ImageChild => this.Children.OfType<IImageProvider>().FirstOrDefault();

		[DisplayName("Pixels Per mm"), Range(0.5, 3, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		public double PixelsPerMM { get; set; } = 1.5;

		[Slider(0.5, 3)]
		public double Height { get; set; } = 2.5;

		public int Width { get; set; } = 150;

		public bool Invert { get; set; } = true;

		public Vector3 ImageOffset { get; private set; } = Vector3.Zero;

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			var invalidateType = invalidateArgs.InvalidateType;
			if ((invalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateArgs.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		private CancellationTokenSource cancellationToken;

		public bool IsBuilding => this.cancellationToken != null;

		public void CancelBuild()
		{
			var threadSafe = this.cancellationToken;
			if (threadSafe != null)
			{
				threadSafe.Cancel();
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			var activeImage = this.ImageChild.Image;

			if (activeImage == null
				|| activeImage.Width == 0
				|| activeImage.Height == 0)
			{
				return Task.CompletedTask;
			}

			activeImage.GetVisibleBounds(out RectangleInt visibleBounds);

			if (visibleBounds.Width == 0
				|| visibleBounds.Height == 0)
			{
				return Task.CompletedTask;
			}

			var rebuildLocks = this.RebuilLockAll();

			ApplicationController.Instance.Tasks.Execute("Generating Lithophane".Localize(), null, (reporter, cancellationTokenSource) =>
			{
				this.cancellationToken = cancellationTokenSource as CancellationTokenSource;
				var generatedMesh = Lithophane.Generate(
					new Lithophane.ImageBufferImageData(activeImage, this.Width),
					this.Height,
					0.4,
					this.PixelsPerMM,
					this.Invert,
					reporter);

				this.Mesh = generatedMesh;

				// Remove old offset
				this.Matrix *= Matrix4X4.CreateTranslation(this.ImageOffset);

				// Set and store new offset
				var imageBounds = generatedMesh.GetAxisAlignedBoundingBox();
				this.ImageOffset = imageBounds.Center + new Vector3(0, 0, -imageBounds.Center.Z);

				// Apply offset
				this.Matrix *= Matrix4X4.CreateTranslation(-this.ImageOffset);

				this.cancellationToken = null;
				UiThread.RunOnIdle(() =>
				{
					rebuildLocks.Dispose();
					this.CancelAllParentBuilding();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
				});

				return Task.CompletedTask;
			});

			return base.Rebuild();
		}
	}
}
