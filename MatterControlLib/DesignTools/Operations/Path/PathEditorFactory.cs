﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterControlLib.DesignTools.Operations.Path
{
    public class PathEditorFactory : IPropertyEditorFactory
    {
        public class EditableVertexStorage : VertexStorage
        {
            public Vector2 UnscaledOffset { get; set; } = Vector2.Zero;
            public double Scale { get; set; } = 1;
        }

        private Object3D object3D;

        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Value is EditableVertexStorage vertexStorage)
            {
                var pathEditorWidget = new PathEditorWidget(vertexStorage,
                    property,
                    propertyEditor.UndoBuffer,
                    propertyEditor.Theme,
                    VertexBufferChanged,
                    ref tabIndex,
                    vertexStorage.UnscaledOffset,
                    vertexStorage.Scale,
                    (unscaledOffset, scale) =>
                    {
                        vertexStorage.UnscaledOffset = unscaledOffset;
                        vertexStorage.Scale = scale;
                    });

                if (property.Source is Object3D object3D)
                {
                    this.object3D = object3D;
                }

                return pathEditorWidget;
            }

            return null;
        }

        private void VertexBufferChanged()
        {
            object3D.Invalidate(new InvalidateArgs(null, InvalidateType.Path));
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class ShowOriginAttribute : Attribute
        {
        }
    }
}