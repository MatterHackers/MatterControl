/*
Copyright (c) 2018, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.DataConverters3D
{
    public interface IAssetManager
    {
        Task AcquireAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress);

        Task<Stream> LoadAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress);

        Task PublishAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress);

        Task StoreAsset(IAssetObject assetObject, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

        /// <summary>
        /// Ensures the given file is stored in the asset system
        /// </summary>
        /// <param name="filePath">The full path to the source file</param>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns>The new asset file name</returns>
        Task<string> StoreFile(string filePath, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

        /// <summary>
        /// Computes and writes the MCX file to the assets system
        /// </summary>
        /// <param name="object3D">The source MCX file</param>
        /// <returns></returns>
        Task<string> StoreMcx(IObject3D object3D, bool publishAfterSave);

        Task StoreMesh(IObject3D object3D, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

        Task<string> StoreStream(Stream stream, string extension, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);
    }

    public interface IAssetObject
    {
        string AssetID { get; set; }
        string AssetPath { get; set; }

        Task<Stream> LoadAsset(CancellationToken cancellationToken, Action<double, string> progress);
    }

    public static class StaticDataExtensions
    {
        /// <summary>
        /// Dynamically computes the SHA1 for the content, storing to Assets if needed and returning the given AssetPath
        /// </summary>
        /// <param name="staticData">The StaticData instance to act on</param>
        /// <param name="relativePath">The relative path of the Asset content</param>
        /// <returns></returns>
        public static string ToAssetPath(this StaticData staticData, string relativePath)
        {
            using (var sourceStream = staticData.OpenStream(relativePath))
            {
                return AssetObject3D.AssetManager.StoreStream(sourceStream, Path.GetExtension(relativePath), false, CancellationToken.None, null).Result;
            }
        }
    }

    public class AssetManager : IAssetManager
    {
        public virtual Task AcquireAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress)
        {
            return Task.CompletedTask;
        }

        public static Dictionary<string, object> assetLocks = new Dictionary<string, object>();

        public Task<Stream> LoadAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress)
        {
            // Natural path
            string filePath = assetObject.AssetPath;

            // Is relative asset path only, no directory
            if (Path.GetDirectoryName(filePath) == "")
            {
                filePath = Path.Combine(Object3D.AssetsPath, filePath);

                if (!assetLocks.ContainsKey(filePath))
                {
                    assetLocks[filePath] = new object();
                }

                // make sure we are only loading a give asset one at a time (in case we need to aquire it before getting)
                lock (assetLocks[filePath])
                {
                    // Prime cache
                    if (!File.Exists(filePath))
                    {
                        AcquireAsset(assetObject.AssetPath, cancellationToken, progress);
                    }
                }
            }

            if (!File.Exists(filePath))
            {
                // Not at natural path, not in local assets, not in remote assets
                return Task.FromResult<Stream>(null);
            }

            return Task.FromResult<Stream>(File.OpenRead(filePath));
        }

        public virtual Task PublishAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress)
        {
            return Task.CompletedTask;
        }

        public async Task StoreAsset(IAssetObject assetObject, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress)
        {
            // Natural path
            string filePath = assetObject.AssetPath;

            // Is full path and file exists, import as Asset
            if (Path.GetDirectoryName(filePath) != ""
                && File.Exists(filePath))
            {
                using (var sourceStream = File.OpenRead(assetObject.AssetPath))
                {
                    // ComputeSha1 -> Save asset
                    string sha1PlusExtension = await this.StoreStream(sourceStream, Path.GetExtension(assetObject.AssetPath), publishAfterSave, cancellationToken, progress);
                    string sha1PlusExtension2 = await this.StoreFile(assetObject.AssetPath, publishAfterSave, cancellationToken, progress);

                    // Update AssetID
                    assetObject.AssetID = Path.GetFileNameWithoutExtension(sha1PlusExtension);
                    assetObject.AssetPath = sha1PlusExtension;
                }
            }

            if (publishAfterSave)
            {
                await Publish(assetObject.AssetPath, cancellationToken, progress);
            }
        }

        public async Task<string> StoreFile(string filePath, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress)
        {
            // Compute SHA1
            string sha1 = HashGenerator.ComputeFileSHA1(filePath);
            string sha1PlusExtension = sha1 + Path.GetExtension(filePath).ToLower();
            string assetPath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

            // Load cache
            if (!File.Exists(assetPath))
            {
                File.Copy(filePath, assetPath);
            }

            if (publishAfterSave)
            {
                await Publish(sha1PlusExtension, cancellationToken, progress);
            }

            return sha1PlusExtension;
        }

        public async Task<string> StoreMcx(IObject3D object3D, bool publishAfterSave)
        {
            // TODO: Track SHA1 of persisted asset
            // TODO: Skip if cached sha1 exists in assets

            // Serialize object3D to in memory mcx/json stream
            using (var memoryStream = new MemoryStream())
            {
                // Write JSON
                object3D.SaveTo(memoryStream);

                // Reposition
                memoryStream.Position = 0;

                Directory.CreateDirectory(Object3D.AssetsPath);

                // Calculate
                string sha1 = HashGenerator.ComputeSHA1(memoryStream);
                string sha1PlusExtension = sha1 + ".mcx";
                string assetPath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

                if (!File.Exists(assetPath))
                {
                    memoryStream.Position = 0;

                    using (var outStream = File.Create(assetPath))
                    {
                        memoryStream.CopyTo(outStream);
                    }
                }

                if (publishAfterSave)
                {
                    await Publish(sha1PlusExtension, CancellationToken.None, null);
                }

                return assetPath;
            }
        }

        public async Task StoreMesh(IObject3D object3D, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress = null)
        {
            if (object3D.Mesh == Object3D.FileMissingMesh)
            {
                return;
            }

            var memoryStream = new MemoryStream();

            // Save the embedded asset to a memory stream
            bool savedSuccessfully = StlProcessing.Save(
                object3D.Mesh,
                memoryStream,
                CancellationToken.None,
                new MeshOutputSettings(MeshOutputSettings.OutputType.Binary),
                true);

            if (savedSuccessfully)
            {
                // reset the memory stream to the start
                memoryStream.Position = 0;
                // save the asset to the asset store
                string assetPath = await this.StoreStream(memoryStream, ".stl", publishAfterSave, cancellationToken, progress);

                // Update MeshPath with Assets relative filename
                object3D.MeshPath = Path.GetFileName(assetPath);
            }

            memoryStream.Close();
        }

        public async Task<string> StoreStream(Stream stream, string extension, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress)
        {
            // Compute SHA1
            string sha1PlusExtension = $"{HashGenerator.ComputeSHA1(stream)}{extension}";
            string assetPath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

            // Load cache
            if (!File.Exists(assetPath))
            {
                stream.Position = 0;

                using (var outstream = File.OpenWrite(assetPath))
                {
                    stream.CopyTo(outstream);
                }
            }

            if (publishAfterSave)
            {
                await Publish(sha1PlusExtension, cancellationToken, progress);
            }

            return sha1PlusExtension;
        }

        /// <summary>
        /// Creates a new non-colliding library file path to write library contents to
        /// </summary>
        /// <param name="extension">The file extension to use</param>
        /// <returns>A new unique library path</returns>
        private static string CreateNewLibraryPath(string extension)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(Object3D.AssetsPath, Path.ChangeExtension(Path.GetRandomFileName(), extension));
            } while (File.Exists(filePath));

            return filePath;
        }

        private async Task Publish(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress)
        {
            string assetPath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

            // If the local asset store contains the item, ensure it's copied to the remote
            if (File.Exists(assetPath))
            {
                await this.PublishAsset(sha1PlusExtension, cancellationToken, progress);
            }
        }
    }

    public abstract class AssetObject3D : Object3D, IAssetObject
    {
        // Collector
        public static IAssetManager AssetManager { get; set; }

        public string AssetID { get; set; }
        public abstract string AssetPath { get; set; }

        // Load
        public Task<Stream> LoadAsset(CancellationToken cancellationToken, Action<double, string> progress)
        {
            return AssetManager.LoadAsset(this, cancellationToken, progress);
        }
    }
}