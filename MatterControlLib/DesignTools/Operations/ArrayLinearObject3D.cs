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

using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ArrayLinearObject3D : OperationSourceContainerObject3D
	{
		public ArrayLinearObject3D()
		{
			Name = "Linear Array".Localize();
		}

		public override bool CanFlatten => true;
		public int Count { get; set; } = 3;
		public DirectionVector Direction { get; set; } = new DirectionVector { Normal = new Vector3(1, 0, 0) };
		public double Distance { get; set; } = 30;
		
		public override async Task Rebuild()
		{
			var rebuildLock = this.RebuildLock();
			SourceContainer.Visible = true;

			await ApplicationController.Instance.Tasks.Execute(
				"Linear Array".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					this.DebugDepth("Rebuild");

					using (new CenterAndHeightMantainer(this))
					{
						var newChildren = new List<IObject3D>();

						newChildren.Add(SourceContainer);

						var arrayItem = SourceContainer.Children.First();

						// add in all the array items
						for (int i = 0; i < Math.Max(Count, 1); i++)
						{
							var next = arrayItem.Clone();
							next.Matrix = arrayItem.Matrix * Matrix4X4.CreateTranslation(Direction.Normal.GetNormal() * Distance * i);
							newChildren.Add(next);
						}

						Children.Modify(list =>
						{
							list.Clear();
							list.AddRange(newChildren);
						});
					}
					SourceContainer.Visible = false;
					rebuildLock.Dispose();
					Invalidate(InvalidateType.Children);
					return Task.CompletedTask;
				});
		}
	}
}