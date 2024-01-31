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

using System;
using System.Collections.Generic;
using System.Data;
using DataConverters3D.Object3D.Nodes;
using System.IO;
using System.Linq;
using System.Reflection;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MatterHackers.DataConverters3D
{
    /// <summary>
    /// A custom contract resolver for IObject3D types.
    /// </summary>
    public class JsonIObject3DContractResolver : DefaultContractResolver
	{
		private static Type IObject3DType = typeof(IObject3D);

		private static Type ColorType = typeof(Color);

		// This function only effects the deserialization of properties. It is not called while serializing.
		protected override JsonObjectContract CreateObjectContract(Type objectType)
		{
			var result = base.CreateObjectContract(objectType);

			if (IObject3DType.IsAssignableFrom(objectType)
				&& result is JsonObjectContract contract)
			{
				// Add a post deserialization callback to set Parent
				contract.OnDeserializedCallbacks.Add((o, context) =>
				{
					if (o is IObject3D object3D)
					{
						foreach (var child in object3D.Children)
						{
							child.Parent = object3D;
						}

						object3D.Children.SetParent(object3D);
					}
				});
			}

			return result;
		}

		// This function only effects the serialization of properties. It is not called while deserializing.
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
		{
			var properties = base.CreateProperties(type, memberSerialization);

			// Custom sort order for properties, [ID] first, alpha by name, [Children] last
			return properties.OrderBy(property =>
			{
				switch (property.PropertyName)
				{
					case "ID":
						return 0;
					case "Children":
						return 2;
					default:
						return 1;
				}
			}).ThenBy(property => property.PropertyName).ToList();
		}

		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			JsonProperty jsonProperty = base.CreateProperty(member, memberSerialization);

			if (jsonProperty.PropertyName == "Children"
				&& IObject3DType.IsAssignableFrom(jsonProperty.DeclaringType))
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Serialize if has children and MeshPath is unset or is a relative path (i.e. no DirectoryName part), otherwise
					// we truncate the in memory Children property and fall back to the MeshPath value on reload
					return instance is IObject3D object3D
						&& object3D.Children.Count > 0
						&& (string.IsNullOrEmpty(object3D.MeshPath)
						|| string.IsNullOrEmpty(Path.GetDirectoryName(object3D.MeshPath)));
				};
			}

			if (jsonProperty.PropertyName == "Color"
				&& ColorType.IsAssignableFrom(jsonProperty.PropertyType))
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Serialize Color property as long as we're not the default value
					return instance is IObject3D object3D
						&& object3D.Color != Color.Transparent;
				};
			}

			if (jsonProperty.PropertyType == typeof(Matrix4X4)
				&& jsonProperty.PropertyName == "Matrix")
			{
				// We don't want to serialize the matrix if it is the identity matrix.
				// This keeps the json files smaller and easier to read.
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize Matrix values off by more than .001 from Matrix4X4.Identity
					return instance is Object3D object3D
						&& !object3D.Matrix.Equals(Matrix4X4.Identity, .001);
				};
			}

			if (jsonProperty.PropertyType == typeof(bool)
				&& jsonProperty.PropertyName == "NameOverriden")
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize non-default (false) value
					return instance is Object3D object3D
						&& object3D.NameOverriden == false;
				};
			}

			if (jsonProperty.PropertyType == typeof(bool)
				&& jsonProperty.PropertyName == "Persistable")
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize non-default (false) value
					return instance is IObject3D object3D
						&& !object3D.Persistable;
				};
			}

			if (jsonProperty.PropertyType == typeof(bool)
				&& jsonProperty.PropertyName == "Visible")
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize non-default (false) value
					return instance is IObject3D object3D
						&& !object3D.Visible;
				};
			}

			if (jsonProperty.PropertyType == typeof(bool)
				&& jsonProperty.PropertyName == "Expanded")
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize non-default (false) value
					return instance is IObject3D object3D
						&& object3D.Expanded;
				};
			}

			if (jsonProperty.PropertyType == typeof(PrintOutputTypes)
				&& jsonProperty.PropertyName == "OutputType")
			{
				jsonProperty.ShouldSerialize = (instance) =>
				{
					// Only serialize non-default (false) value
					return instance is IObject3D object3D
						&& object3D.OutputType != PrintOutputTypes.Default;
				};
			}

			return jsonProperty;
		}
	}
}