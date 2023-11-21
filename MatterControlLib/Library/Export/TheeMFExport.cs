/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library.Export
{
#if DEBUG
    // This is not working yet. So it is only enabled in debug mode
    public class TheeMFExport : IExportPlugin
    {
        public string ButtonText => "3MF File".Localize();
        public string DisabledReason => "";
        public bool Enabled => true;
        public string ExtensionFilter => "Save as 3MF|*.3mf";
        public string FileExtension => ".3mf";
        public ImageBuffer Icon { get; } = StaticData.Instance.LoadIcon(Path.Combine("filetypes", "3mf.png"));
        public int Priority => 3;

        public bool ExportPossible(ILibraryAsset libraryItem) => true;

        public async Task<List<ValidationError>> Generate(IEnumerable<ILibraryItem> libraryItems, string outputPath, Action<double, string> progress, CancellationToken cancellationToken)
        {
            var firstItem = libraryItems.OfType<ILibraryAsset>().FirstOrDefault();
            if (firstItem is ILibraryAsset libraryItem)
            {
                bool exportSuccessful = await MeshExport.ExportMesh(libraryItem, outputPath, true, progress);
                if (exportSuccessful)
                {
                    return null;
                }
            }

            return new List<ValidationError>()
            {
                new ValidationError(ValidationErrors.ItemTo3MFExportInvalid)
                {
                    Error = "Item cannot be exported as 3MF".Localize(),
                    Details = firstItem?.ToString() ?? ""
                }
            };
        }

        public void Initialize(PrinterConfig printer)
        {
        }
    }
#endif
}