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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library.Export
{
    public class ExportToBambuStudio : ExportStlToExecutable, IExportPlugin
    {
        public ExportToBambuStudio()
        {
            
        }

        protected override string regExKeyName => @" Bambu.Studio.1\Shell\Open\Command";

        public override string ButtonText => "Bambu Studio".Localize();

        public override string DisabledReason => "Bambu Studio not installed";

        public void Initialize(PrinterConfig printer)
        {
        }
    }

    public abstract class ExportStlToExecutable
    {
        private string pathToExe;

        public int Priority => 2;

        protected abstract string regExKeyName { get; }

        abstract public string ButtonText { get; }

        public string FileExtension => ".stl";

        public string ExtensionFilter => "Save as STL|*.stl";

        public ImageBuffer Icon { get; } = StaticData.Instance.LoadIcon(Path.Combine("filetypes", "stl.png"));

        public bool Enabled
        {
            get
            {
                // get data from the registry for: Computer\HKEY_CLASSES_ROOT\ Bambu.Studio.1\Shell\Open\Command
                RegistryKey key = Registry.ClassesRoot.OpenSubKey(regExKeyName);

                if (key != null)
                {
                    pathToExe = key.GetValue("").ToString();
                    
                    var regex = "C:.+.exe";
                    var match = System.Text.RegularExpressions.Regex.Match(pathToExe, regex);
                    pathToExe = match.Value;

                    key.Close();
                    return true;
                }

                return false;
            }
        }

        public abstract string DisabledReason { get; }

        public bool ExportPossible(ILibraryAsset libraryItem) => true;

        public async Task<List<ValidationError>> Generate(IEnumerable<ILibraryItem> libraryItems,
            string noPath,
            Action<double, string> progress,
            CancellationToken cancellationToken)
        {
            string exportTempFileFolder = Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "ExportToExe");
            Directory.CreateDirectory(exportTempFileFolder);
            var exportFilename = Path.ChangeExtension(Path.Combine(exportTempFileFolder, "TempExport"), ".stl");

            List<string> bedExports = new List<string>();
            foreach (var item in libraryItems.OfType<ILibraryAsset>())
            {
                if (!await MeshExport.ExportMesh(item, exportFilename, false, progress))
                {
                    bedExports.Add(item.Name);
                }
            }

            if (bedExports.Count == 0)
            {
                // open the file with the specified exe
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = $"\"{pathToExe}\"";
                startInfo.Arguments = $"\"{exportFilename}\"";

                Process.Start(startInfo);

                return null;
            }

            return new List<ValidationError>()
            {
                new ValidationError(ValidationErrors.ItemToSTLExportInvalid)
                {
                    Error = "One or more items cannot be exported as STL".Localize(),
                    Details = String.Join("\n", bedExports.ToArray())
                }
            };
        }
    }
}