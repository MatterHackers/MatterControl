/*
Copyright (c) 2023, John Lewin, Lars Brubaker
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

using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Primitives;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.Library
{
    public class Primitives2DContainer : LibraryContainer
    {
        public Primitives2DContainer()
        {
            Name = "2D Primitives".Localize();
            DefaultSort = new LibrarySortBehavior()
            {
                SortKey = SortKey.ModifiedDate,
                Ascending = true,
            };
        }

        public override void Load()
        {
            var library = ApplicationController.Instance.Library;

            long index = DateTime.Now.Ticks;
            var libraryItems = new List<GeneratorItem>()
            {
                new GeneratorItem(
                    "Box".Localize(),
                    async () => await BoxPathObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Custom Path".Localize(),
                    async () => await CustomPathObject3D.Create())
                    { DateCreated = new DateTime(index++) },
#if DEBUG
                new GeneratorItem(
                    "Triangle".Localize(),
                    async () => await PyramidObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Trapezoid".Localize(),
                    async () => await WedgeObject3D_2.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Text".Localize(),
                    async () => await TextObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Oval".Localize(),
                    async () => await CylinderObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Star".Localize(),
                    async () => await ConeObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Ring".Localize(),
                    async () => await RingObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Circle".Localize(),
                    async () => await SphereObject3D.Create())
                    { DateCreated = new DateTime(index++) },
#endif
            };

            string title = "2D Shapes".Localize();

            foreach (var item in libraryItems)
            {
                item.Category = title;
                Items.Add(item);
            }
        }
    }
}