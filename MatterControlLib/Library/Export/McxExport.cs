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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library.Export
{
    public class McxExport : IExportPlugin
    {
        public string ButtonText => "MCX File".Localize();
        public string DisabledReason => "";
        public bool Enabled => true;
        public string ExtensionFilter => "Save as MCX|*.mcx";
        public string FileExtension => ".mcx";
        public ImageBuffer Icon { get; } = StaticData.Instance.LoadIcon(Path.Combine("filetypes", "mcx.png"));
        public int Priority => 1;

        public bool ExportPossible(ILibraryAsset libraryItem) => true;

        public async Task<List<ValidationError>> Generate(IEnumerable<ILibraryItem> libraryItems, string outputPath, IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
        {
            try
            {
                var bedToSave = new BedConfig(null);
                var inMemoryLibraryItem = libraryItems.FirstOrDefault() as InMemoryLibraryItem;
                if (inMemoryLibraryItem.Object3D is SelectionGroupObject3D selectionGroupObject3D)
                {
                    foreach(var child in selectionGroupObject3D.Children)
                    {
                        bedToSave.Scene.Children.Add(child);
                    }
                }
                else
                {
                    bedToSave.Scene.Children.Add(inMemoryLibraryItem.Object3D);
                }
                bedToSave.Scene.Name = Path.GetFileName(outputPath);

                bedToSave.EditContext = new EditContext()
                {
                    ContentStore = ApplicationController.Instance.Library.PlatingHistory,
                    SourceItem = libraryItems.FirstOrDefault(),
                };
                var fileSystemContainer = new FileSystemContainer(outputPath)
                {
                    Items = new SafeList<ILibraryItem>(libraryItems)
                };
                bedToSave.SaveAs(fileSystemContainer, outputPath);
                return null;
            }
            catch
            {
                var firstItem = libraryItems.OfType<ILibraryAsset>().FirstOrDefault();
                return new List<ValidationError>()
                {
                    new ValidationError(ValidationErrors.ItemToAMFExportInvalid)
                    {
                        Error = "Item cannot be exported as MCX".Localize(),
                        Details = firstItem?.ToString() ?? ""
                    }
                };
            }
        }

        public void Initialize(PrinterConfig printer)
        {
        }
    }
}