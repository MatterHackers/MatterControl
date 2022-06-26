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

using System.Threading;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public abstract class ArrayObject3D : OperationSourceContainerObject3D
	{
		public abstract IntOrExpression Count { get; set; }

        public override void Apply(Agg.UI.UndoBuffer undoBuffer)
        {
			var indexExpansions = new (string key, int index)[] 
			{
				("[index]", 0),
				("[index0]", 0),
				("[index1]", 1),
				("[index2]", 2),
			};

			// convert [index] expressions to their constant values
			foreach (var item in this.Descendants((item) => !(item is ArrayObject3D)))
			{
				foreach (var expansion in indexExpansions)
				{
					foreach (var expression in SheetObject3D.GetActiveExpressions(item, expansion.key, false))
					{
						expression.Expression = expression.Expression.Replace(expansion.key, SheetObject3D.RetrieveArrayIndex(item, expansion.index).ToString());
					}

					// Also convert index expressions in ComponentObjects to their constants
					if (item is ComponentObject3D component)
					{
						for (int i = 0; i < component.SurfacedEditors.Count; i++)
						{
							var (cellId, cellData) = component.DecodeContent(i);

							if (cellId != null)
							{
								var newValue = cellData.Replace(expansion.key, SheetObject3D.RetrieveArrayIndex(component, expansion.index).ToString());
								component.SurfacedEditors[i] = "!" + cellId + "," + newValue;
							}
						}
					}
				}
			}

			// then call base apply
			base.Apply(undoBuffer);
        }

        internal void ProcessIndexExpressions()
        {
			var updateItems = SheetObject3D.SortAndLockUpdateItems(this, (item) =>
			{
				if (!SheetObject3D.HasExpressionWithString(item, "=", true))
				{
					return false;
				}

				// WIP
				if (item.Parent == this)
				{
					// only process our children that are not the source object
					return !(item is OperationSourceObject3D);
				}
				else if (item.Parent is OperationSourceContainerObject3D)
				{
					// If we find another source container
					// Only process its children that are the source container (they will be replicated and modified correctly by the source container)
					return item is OperationSourceObject3D;
				}
				else if (item.Parent is OperationSourceObject3D operationSourceObject3D
					&& operationSourceObject3D.Parent == this)
				{
					// we don't need to rebuild our source object
					return false;
				}
				else if (item.Parent is ComponentObject3D)
                {
					return false;
                }

				// process everything else
				return true;
			}, true);

			var runningInterval = SheetObject3D.SendInvalidateInRebuildOrder(updateItems, InvalidateType.Properties);

			while (runningInterval.Active)
			{
				Thread.Sleep(10);
			}
		}
	}
}