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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[Obsolete("Use CombineObject3D_2 instead", false)]
	public class CombineObject3D : MeshWrapperObject3D
	{
		public CombineObject3D()
		{
			Name = "Combine";
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
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

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			// spin up a task to remove holes from the objects in the group
			return ApplicationController.Instance.Tasks.Execute(
				"Combine".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Combine(cancellationToken, reporter);
					}
					catch
					{
					}

					rebuildLocks.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					return Task.CompletedTask;
				});
		}

		public void Combine()
		{
			Combine(CancellationToken.None, null);
		}

		public void Combine(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			ResetMeshWrapperMeshes(Object3DPropertyFlags.All, cancellationToken);

			var participants = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
			if (participants.Count() < 2)
			{
				return;
			}

			var first = participants.First();

			var totalOperations = participants.Count() - 1;
			double amountPerOperation = 1.0 / totalOperations;
			double percentCompleted = 0;

			ProgressStatus progressStatus = new ProgressStatus();
			foreach (var item in participants)
			{
				if (item != first)
				{
					var result = BooleanProcessing.Do(item.Mesh, item.WorldMatrix(),
						first.Mesh,  first.WorldMatrix(),
						0, 
						reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);

					var inverse = first.WorldMatrix();
					inverse.Invert();
					result.Transform(inverse);
					using (first.RebuildLock())
					{
						first.Mesh = result;
					}

					percentCompleted += amountPerOperation;
					progressStatus.Progress0To1 = percentCompleted;
					reporter?.Report(progressStatus);
				}
			}
		}
	}
}
