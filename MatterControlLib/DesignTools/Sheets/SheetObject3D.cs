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

using System;
using System.IO;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.DesignTools
{
    [HideChildrenFromTreeView]
    [HideMeterialAndColor]
    [WebPageLink("Documentation", "Open", "https://www.matterhackers.com/support/mattercontrol-variable-support")]
    [MarkDownDescription("[BETA] - Experimental support for variables and equations with a sheets like interface.")]
    public class SheetObject3D : Object3D, IStaticThumbnail, IVariableResolver
    {
        private SheetData _sheetData;
        public SheetData SheetData
        {
            get => _sheetData;

            set
            {
                if (_sheetData != value)
                {
                    if (_sheetData != null)
                    {
                        _sheetData.Recalculated -= SendInvalidateToAll;
                    }

                    _sheetData = value;
                    _sheetData.Recalculated += SendInvalidateToAll;
                }
            }
        }

        public static async Task<SheetObject3D> Create()
        {
            var item = new SheetObject3D
            {
                SheetData = new SheetData(5, 5)
            };
            await item.Rebuild();
            return item;
        }

        public string ThumbnailName => "Sheet";


        private static object loadLock = new object();
        private static IObject3D sheetObject;

        public override Mesh Mesh
        {
            get
            {
                if (this.Children.Count == 0)
                {
                    lock (loadLock)
                    {
                        if (sheetObject == null)
                        {
                            sheetObject = MeshContentProvider.LoadMCX(StaticData.Instance.OpenStream(Path.Combine("Stls", "sheet_icon.mcx")));
                        }

                        this.Children.Modify((list) =>
                        {
                            list.Clear();

                            list.Add(sheetObject.DeepCopy());
                        });
                    }
                }

                return null;
            }

            set => base.Mesh = value;
        }

        public SheetObject3D()
        {
        }

        public override bool Printable => false;

        public class UpdateItem
        {
            internal int depth;
            internal IObject3D item;
            internal RebuildLock rebuildLock;

            public override string ToString()
            {
                var state = rebuildLock == null ? "unlocked" : "locked";
                return $"{depth} {state} - {item}";
            }
        }

        private void SendInvalidateToAll(object s, EventArgs e)
        {
            var updateItems = Expressions.SortAndLockUpdateItems(this.Parent, (item) =>
            {
                if (item == this || item.Parent == this)
                {
                    // don't process this
                    return false;
                }
                else if (item.Parent is ArrayObject3D arrayObject3D
                    && arrayObject3D.SourceContainer != item)
                {
                    // don't process the copied children of an array object
                    return false;
                }

                return true;
            }, true);

            Expressions.SendInvalidateInRebuildOrder(updateItems, InvalidateType.SheetUpdated, this);
        }

        public string EvaluateExpression(string inputExpression)
        {
            return SheetData.EvaluateExpression(inputExpression);
        }
    }
}