/*
Copyright (c) 2018, John Lewin, Lars Brubaker
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

using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.DataConverters3D
{
    public class SelectionMaintainer : IDisposable
	{
        private InteractiveScene scene;
        private List<IObject3D> selectedObjects = new List<IObject3D>();

        public SelectionMaintainer(InteractiveScene interactiveScene)
        {
            this.scene = interactiveScene;

            // remember any selected objects we have
            var selection = interactiveScene.SelectedItem;
            if (selection != null)
            {
                if (selection is SelectionGroupObject3D selectionGroup)
                {
                    selectedObjects.AddRange(selectionGroup.Children);
                }
                else
                {
                    selectedObjects.Add(selection);
                }
            }

            // clear the selection
            interactiveScene.SelectedItem = null;
        }

        public void Dispose()
        {
            if (selectedObjects.Count == 1)
            {
                var item = selectedObjects[0];
                var rootItem = item.Parents().Where(i => scene.Children.Contains(i)).FirstOrDefault();
                if (rootItem != null)
                {
                    scene.SelectedItem = rootItem;
                    scene.SelectedItem = item;
                }
                else if (scene.Children.Contains(item))
                {
                    scene.SelectedItem = item;
                }
            }
            else
            {
                // restore the selcetion
                foreach (var item in selectedObjects)
                {
                    if (!(item is SelectionGroupObject3D)
                        && scene.Children.Contains(item))
                    {
                        scene.AddToSelection(item);
                    }
                }
            }
        }
    }
}
