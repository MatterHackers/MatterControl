/*
Copyright (c) 2022, Lars Brubaker
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
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Linq;
using static MatterHackers.DataConverters3D.Object3DExtensions;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class SliceSettingsObject3D : Object3D
    {
        public SliceSettingsObject3D()
        {
            Name = "Slice Settings".Localize();
        }

        public PrinterSettingsLayer Overrides { get; set; }

        public void WrapSelectedItemAndSelect(InteractiveScene scene)
        {
            var items = scene.GetSelectedItems();

            var parent = items.First().Parent;
            RebuildLocks parentLock = (parent == null) ? null : parent.RebuilLockAll();

            var firstChild = new Object3D();
            this.Children.Add(firstChild);

            // if the items we are replacing are already in a list
            if (parent != null)
            {
                if (scene.UndoBuffer != null)
                {
                    foreach (var item in items)
                    {
                        firstChild.Children.Add(item.Clone());
                    }

                    var replace = new ReplaceCommand(items, new[] { this });
                    scene.UndoBuffer.AddAndDo(replace);
                }
                else
                {
                    parent.Children.Modify(list =>
                    {
                        foreach (var item in items)
                        {
                            list.Remove(item);
                            firstChild.Children.Add(item);
                        }

                        list.Add(this);
                    });
                }
            }
            else // just add them
            {
                firstChild.Children.Modify(list =>
                {
                    list.AddRange(items);
                });
            }

            parentLock?.Dispose();

            // and select this
            var rootItem = this.Parents().Where(i => scene.Children.Contains(i)).FirstOrDefault();
            if (rootItem != null)
            {
                scene.SelectedItem = rootItem;
            }

            scene.SelectedItem = this;

            this.CancelAllParentBuilding();
            parent?.Invalidate(new InvalidateArgs(parent, InvalidateType.Children));
        }
    }
}