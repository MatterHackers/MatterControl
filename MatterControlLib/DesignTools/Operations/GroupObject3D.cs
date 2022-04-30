/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class GroupObject3D : Object3D
	{
		public override bool CanApply => true;

		public GroupObject3D()
		{
			Name = "Group".Localize();
		}
	}

	[ShowUpdateButton(Show = false)]
	public class GroupHolesAppliedObject3D : SubtractObject3D_2
	{
		public GroupHolesAppliedObject3D()
		{
			Name = "Group".Localize();
		}

		// We can't use Subtracts Apply as it will leave a group if there are multiple object results
		// and we want to always leave the individual results after the ungroup.
        public override void Apply(UndoBuffer undoBuffer)
        {
			using (RebuildLock())
			{
				var newChildren = new List<IObject3D>();
				// push our matrix into a copy of our children
				foreach (var child in this.SourceContainer.Children)
				{
					var newChild = child.Clone();
					newChildren.Add(newChild);
					newChild.Matrix *= this.Matrix;
				}

				// and replace us with the children
				var replaceCommand = new ReplaceCommand(new[] { this }, newChildren, maintainCenterAndZHeight: false);

				if (undoBuffer != null)
				{
					undoBuffer.AddAndDo(replaceCommand);
				}
				else
				{
					replaceCommand.Do();
				}
			}

			Invalidate(InvalidateType.Children);
		}

		[HideFromEditor]
		public override SelectedChildren SelectedChildren 
		{
			get
            {
				var selections = new SelectedChildren();

				foreach(var child in SourceContainer.FirstWithMultipleChildrenDescendantsAndSelf().Children.Where(i => i.WorldOutputType(this) == PrintOutputTypes.Hole && !(i is OperationSourceObject3D) ))
                {
					selections.Add(child.ID);
                }

				return selections;
			}

			set
            {

            }
		}
	}
}