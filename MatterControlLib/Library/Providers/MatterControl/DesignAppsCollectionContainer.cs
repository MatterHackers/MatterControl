/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library
{
    public class DesignAppsCollectionContainer : LibraryContainer
    {
        public DesignAppsCollectionContainer()
        {
            this.ChildContainers = new SafeList<ILibraryContainerLink>();
            this.Items = new SafeList<ILibraryItem>();
            this.Name = "Design Apps".Localize();

            this.ChildContainers.Add(
                new DynamicContainerLink(
                    "Calibration Parts".Localize(),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "calibration_library_icon.png")),
                    () => new CalibrationPartsContainer())
                {
                    IsReadOnly = true
                });

            this.ChildContainers.Add(
                new DynamicContainerLink(
                    "Scripting".Localize(),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "scripting_icon.png")),
                    () => new ScriptingPartsContainer())
                {
                    IsReadOnly = true
                });

            this.ChildContainers.Add(
                new DynamicContainerLink(
                    "Primitives".Localize(),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "primitives_library_icon.png")),
                    () => new PrimitivesContainer())
                {
                    IsReadOnly = true
                });

#if DEBUG
            int index = 0;

            this.ChildContainers.Add(
                new DynamicContainerLink(
                    "Experimental".Localize(),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
                    StaticData.Instance.LoadIcon(Path.Combine("Library", "experimental.png")),
                    () => new DynamicContainer()
                    {
                        Items = new SafeList<ILibraryItem>()
                        {
                            new GeneratorItem(
                                "Calibration Tab".Localize(),
                                async () => await XyCalibrationTabObject3D.Create())
                            { DateCreated = new System.DateTime(index++) },
                            new GeneratorItem(
                                "Calibration Face".Localize(),
                                async () => await XyCalibrationFaceObject3D.Create())
                            { DateCreated = new System.DateTime(index++) },
                            new GeneratorItem(
                                "Text2".Localize(),
                                async () => await TextPathObject3D.Create())
                            { DateCreated = new System.DateTime(index++) },
                            new GeneratorItem(
                                "Path".Localize(),
                                () =>
                                {
                                    var storage = new VertexStorage();
                                    storage.MoveTo(5, 5);
                                    storage.LineTo(10, 5);
                                    storage.LineTo(7.5, 10);
                                    storage.ClosePolygon();

                                    var path = new PathObject3D()
                                    {
                                        VertexSource = storage
                                    };

                                    return Task.FromResult<IObject3D>(path);
                                })
                                { DateCreated = new System.DateTime(index++) },
                        }
                    })
                {
                    IsReadOnly = true
                });
#endif
        }

        public override void Load()
        {
        }
    }
}