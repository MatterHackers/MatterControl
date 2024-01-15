﻿/*
Copyright (c) 2017, Matt Moening, John Lewin
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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.Library.Export
{
    public static class MeshExport
    {
        /// <summary>
		/// Export the source item(s) to a single or multiple STL files
		/// </summary>
		/// <param name="source">The source item(s)</param>
		/// <param name="filePathToSave">The destination to save to (only the folder will be used in saveMultipleStls</param>
		/// <param name="mergeMeshes">Do a Combine on the individual meshes to ensure they are a single item</param>
		/// <param name="saveMultipleStls">Save multiple stls using the name of the objects in the scene</param>
		/// <param name="progress">Update the save progress</param>
		/// <returns>If the function succeded</returns>
		public static async Task<bool> ExportMesh(ILibraryItem source, string filePathToSave, bool mergeMeshes, Action<double, string> progress, bool saveMultipleStls = false)
        {
            try
            {
                if (source is ILibraryObject3D contentItem)
                {
                    // If the content is an IObject3D, then we need to load it and MeshFileIO save to the target path
                    var content = await contentItem.GetObject3D(null);
                    return Object3D.Save(content, filePathToSave, mergeMeshes, CancellationToken.None, reportProgress: (ratio, name) =>
                    {
                        progress?.Invoke(ratio, "Exporting".Localize());
                    }, saveMultipleStls: saveMultipleStls);
                }
                else if (source is ILibraryAssetStream streamContent)
                {
                    if (!string.IsNullOrEmpty(filePathToSave))
                    {
                        // If the file is already the target type, it just needs copied to the target path
                        if (Path.GetExtension(streamContent.FileName).ToUpper() == Path.GetExtension(filePathToSave).ToUpper())
                        {
                            using (var result = await streamContent.GetStream(null))
                            using (var fileStream = File.Create(filePathToSave))
                            {
                                result.Stream.CopyTo(fileStream);
                            }

                            return true;
                        }
                        else
                        {
                            // Otherwise we need to load the content and MeshFileIO save to the target path
                            using (var result = await streamContent.GetStream(null))
                            {
                                IObject3D item = Object3D.Load(result.Stream, Path.GetExtension(streamContent.FileName), CancellationToken.None);
                                if (item != null)
                                {
                                    return Object3D.Save(item, filePathToSave, mergeMeshes, CancellationToken.None, reportProgress: (ratio, name) =>
                                    {
                                        progress?.Invoke(ratio, null);
                                    }, saveMultipleStls: saveMultipleStls);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error exporting file: " + ex.Message);
            }

            return false;
        }
    }
}
