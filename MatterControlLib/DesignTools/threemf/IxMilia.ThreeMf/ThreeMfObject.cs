using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Xml.Linq;
using IxMilia.ThreeMf.Collections;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public class ThreeMfObject : ThreeMfResource
    {
        private const string NameAttributeName = "name";
        private const string PartNumberAttributeName = "partnumber";
        private const string PropertyReferenceAttributeName = "pid";
        private const string PropertyIndexAttributeName = "pindex";
        private const string ThumbnailAttributeName = "thumbnail";
        private const string TypeAttributeName = "type";
        private const string ThumbnailRelationshipType = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail";

        internal const string ThumbnailPathPrefix = "/3D/Thumbnails/";

        internal static XName MeshName = XName.Get("mesh", ThreeMfModel.ModelNamespace);
        internal static XName ComponentsName = XName.Get("components", ThreeMfModel.ModelNamespace);

        public ThreeMfObjectType Type { get; set; }
        public IThreeMfPropertyResource PropertyResource { get; set; }
        public int PropertyIndex { get; set; }
        public string PartNumber { get; set; }
        public string Name { get; set; }
        public ThreeMfImageContentType ThumbnailContentType { get; set; }
        public byte[] ThumbnailData { get; set; }

        private ThreeMfMesh _mesh;
        private Uri _thumbnailUri;

        public ThreeMfMesh Mesh
        {
            get => _mesh;
            set => _mesh = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IList<ThreeMfComponent> Components { get; } = new ListNonNull<ThreeMfComponent>();

        public ThreeMfObject()
        {
            Type = ThreeMfObjectType.Model;
            Mesh = new ThreeMfMesh();
        }

        internal override XElement ToXElement(Dictionary<ThreeMfResource, int> resourceMap)
        {
            string thumbnailPath = null;
            if (ThumbnailData != null)
            {
                thumbnailPath = string.Concat(ThumbnailPathPrefix, Guid.NewGuid().ToString("N"), ThumbnailContentType.ToExtensionString());
                _thumbnailUri = new Uri(thumbnailPath, UriKind.RelativeOrAbsolute);
            }
            else
            {
                _thumbnailUri = null;
            }

            return new XElement(ObjectName,
                new XAttribute(IdAttributeName, Id),
                new XAttribute(TypeAttributeName, Type.ToString().ToLowerInvariant()),
                PropertyResource == null
                    ? null
                    : new[]
                    {
                        new XAttribute(PropertyReferenceAttributeName, resourceMap[(ThreeMfResource)PropertyResource]),
                        new XAttribute(PropertyIndexAttributeName, PropertyIndex)
                    },
                thumbnailPath == null ? null : new XAttribute(ThumbnailAttributeName, thumbnailPath),
                PartNumber == null ? null : new XAttribute(PartNumberAttributeName, PartNumber),
                Name == null ? null : new XAttribute(NameAttributeName, Name),
                Mesh.ToXElement(resourceMap),
                Components.Count == 0 ? null : new XElement(ComponentsName, Components.Select(c => c.ToXElement(resourceMap))));
        }

        internal override void AfterPartAdded(Package package, PackagePart packagePart)
        {
            if (_thumbnailUri != null)
            {
                package.WriteBinary(_thumbnailUri.ToString(), ThumbnailContentType.ToContentTypeString(), ThumbnailData);
                packagePart.CreateRelationship(_thumbnailUri, TargetMode.Internal, ThumbnailRelationshipType);
            }
        }

        internal static ThreeMfObject ParseObject(XElement element, Dictionary<int, ThreeMfResource> resourceMap, Package package)
        {
            var obj = new ThreeMfObject();
            obj.Id = element.AttributeIntValueOrThrow(IdAttributeName);
            obj.Type = ParseObjectType(element.Attribute(TypeAttributeName)?.Value);
            obj.PartNumber = element.Attribute(PartNumberAttributeName)?.Value;
            obj.Name = element.Attribute(NameAttributeName)?.Value;

            var meshElement = element.Element(MeshName);
            if (meshElement != null)
            {
                obj.Mesh = ThreeMfMesh.ParseMesh(meshElement, resourceMap);
            }

            var thumbnailPath = element.Attribute(ThumbnailAttributeName)?.Value;
            if (thumbnailPath != null)
            {
                obj.ThumbnailData = package.GetPartBytes(thumbnailPath);
            }

            var components = element.Element(ComponentsName);
            if (components != null)
            {
                foreach (var componentElement in components.Elements())
                {
                    var component = ThreeMfComponent.ParseComponent(componentElement, resourceMap);
                    obj.Components.Add(component);
                }
            }

            if (resourceMap.TryGetPropertyResource(element, PropertyReferenceAttributeName, out var propertyResource))
            {
                obj.PropertyResource = propertyResource;
                obj.PropertyIndex = propertyResource.ParseAndValidateRequiredResourceIndex(element, PropertyIndexAttributeName);
            }
            else if (element.Attribute(PropertyReferenceAttributeName) == null && element.Attribute(PropertyIndexAttributeName) != null)
            {
                throw new ThreeMfParseException($"Attribute '{PropertyIndexAttributeName}' is only valid if '{PropertyReferenceAttributeName}' is also specified.");
            }

            return obj;
        }

        internal static ThreeMfObjectType ParseObjectType(string value)
        {
            switch (value)
            {
                case "model":
                case null:
                    return ThreeMfObjectType.Model;
                case "support":
                    return ThreeMfObjectType.Support;
                case "other":
                    return ThreeMfObjectType.Other;
                default:
                    throw new ThreeMfParseException($"Invalid object type '{value}'.");
            }
        }
    }
}
