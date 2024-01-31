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
using System.Collections.Generic;
using DataConverters3D.Object3D.Nodes;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MatterHackers.DataConverters3D
{
    /// <summary>
    /// Convert a list of IObject3D to JSON. This is the class for reading IObject3D from JSON.
    /// Specifically, this converter is used to deserialize the Children property of IObject3D
    /// </summary>
    public class JsonIObject3DConverter : JsonConverter
	{
        // Register type mappings to support deserializing to the IObject3D concrete type - long term hopefully via configuration mapping, short term via IObject3D inheritance
        private static Dictionary<string, string> _mappingTypesCache;
        private static Dictionary<string, string> Object3DMappingTypesCache
        {
            get
            {
                if (_mappingTypesCache == null)
                {
                    var newCache = new Dictionary<string, string>();

                    foreach (var type in PluginFinder.FindTypes<IObject3D>())
                    {
                        newCache.Add(type.Name, type.AssemblyQualifiedName);
                    }

                    _mappingTypesCache = newCache;
                }

                return _mappingTypesCache;
            }
        }

        // Register type mappings to support deserializing to the INodeObject concrete type - long term hopefully via configuration mapping, short term via INodeObject inheritance
        private Dictionary<string, string> INodeObjectMappingTypesCache;

        private Dictionary<string, string> INodeObjectMappingTypes
        {
            get
            {
                if (INodeObjectMappingTypesCache == null)
                {
                    INodeObjectMappingTypesCache = new Dictionary<string, string>();

                    foreach (var type in PluginFinder.FindTypes<INodeObject>())
                    {
                        INodeObjectMappingTypesCache.Add(type.Name, type.AssemblyQualifiedName);
                    }
                }

                return INodeObjectMappingTypesCache;
            }
        }

        public override bool CanConvert(Type objectType)
        {
            var isObjectType = typeof(AscendableSafeList<IObject3D>).IsAssignableFrom(objectType);
            var isINodeObjectType = typeof(SafeList<INodeObject>).IsAssignableFrom(objectType);

            return isObjectType || isINodeObjectType;
        }

        public override bool CanRead { get; } = true;
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var isObjectType = typeof(AscendableSafeList<IObject3D>).IsAssignableFrom(objectType);
            var isINodeObjectType = typeof(SafeList<INodeObject>).IsAssignableFrom(objectType);

            if (isObjectType)
            {
                return IObject3DReadJson(reader, objectType, existingValue, serializer);
            }
            else if (isINodeObjectType)
            {
                return INodeObjectReadJson(reader, objectType, existingValue, serializer);
            }

            return null;
        }

        public object IObject3DReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.MaxDepth = Object3D.MaxJsonDepth;
            var parentItem = existingValue as IObject3D;

            var items = new List<IObject3D>();

            var jArray = JArray.Load(reader);
            foreach (var item in jArray)
            {
                string typeName = item[nameof(IObject3D.TypeName)]?.ToString();

                IObject3D childItem;

                if (string.IsNullOrEmpty(typeName) || typeName == "Object3D" || !Object3DMappingTypesCache.TryGetValue(typeName, out string fullTypeName))
                {
                    // Use a normal Object3D type if the TypeName field is missing, invalid or has no mapping entry
                    childItem = item.ToObject<Object3D>(serializer);
                }
                else
                {
                    // If a mapping entry exists, try to find the type for the given entry falling back to Object3D if that fails
                    Type type = Type.GetType(fullTypeName) ?? typeof(Object3D);
                    childItem = (IObject3D)item.ToObject(type, serializer);
                }

                childItem.Parent = parentItem;

                items.Add(childItem);
            }

            return new AscendableSafeList<IObject3D>(items, null);
        }

        public object INodeObjectReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.MaxDepth = Object3D.MaxJsonDepth;

            var items = new List<INodeObject>();

            var jArray = JArray.Load(reader);
            foreach (var item in jArray)
            {
                string typeName = item[nameof(INodeObject.TypeName)]?.ToString();

                INodeObject childItem;

                if (string.IsNullOrEmpty(typeName)
                    || typeName == "NodeObject"
                    || !INodeObjectMappingTypes.TryGetValue(typeName, out string fullTypeName))
                {
                    // Use a normal NodeObject type if the TypeName field is missing, invalid or has no mapping entry
                    childItem = item.ToObject<NodeObject>(serializer);
                }
                else
                {
                    // If a mapping entry exists, try to find the type for the given entry falling back to NodeObject if that fails
                    Type type = Type.GetType(fullTypeName) ?? typeof(NodeObject);
                    childItem = (INodeObject)item.ToObject(type, serializer);
                }

                items.Add(childItem);
            }

            return new SafeList<INodeObject>(items);
        }
        public override bool CanWrite { get; } = false;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
		}
	}
}