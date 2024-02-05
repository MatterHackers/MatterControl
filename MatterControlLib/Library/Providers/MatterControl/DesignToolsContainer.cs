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

using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.Library
{
    public class DesignToolsContainer : LibraryContainer
    {
        public DesignToolsContainer()
        {
            Name = "Tools".Localize();
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
#if DEBUG
                new GeneratorItem(
                    "Dual Contouring".Localize(),
                    async () => await DualContouringObject3D.Create())
                    { DateCreated = new DateTime(index++) },
#endif
                new GeneratorItem(
                    "QR Code".Localize(),
                    async () => await QrCodeObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Measure Tool".Localize(),
                    async () => await MeasureToolObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Description".Localize(),
                    async () => await DescriptionObject3D.Create())
                    { DateCreated = new DateTime(index++) },
                new GeneratorItem(
                    "Variable Sheet".Localize(),
                    async () => await SheetObject3D.Create())
                    { DateCreated = new DateTime(index++) },
            };

            string title = "Primitive Shapes".Localize();

            foreach (var item in libraryItems)
            {
                item.Category = title;
                Items.Add(item);
            }
        }
    }
}