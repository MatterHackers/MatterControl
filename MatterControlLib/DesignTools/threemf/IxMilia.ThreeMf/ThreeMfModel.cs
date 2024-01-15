using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Xml.Linq;
using IxMilia.ThreeMf.Collections;

namespace IxMilia.ThreeMf
{
    public class ThreeMfModel
    {
        internal const string ModelNamespace = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
        internal const string MaterialNamespace = "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
        internal const string ProductionNamespace = "http://schemas.microsoft.com/3dmanufacturing/production/2015/06";
        private const string UnitAttributeName = "unit";
        private const string NameAttributeName = "name";
        private const string RequiredExtensionsAttributeName = "requiredextensions";
        private const string DefaultLanguage = "en-US";

        private static XName ModelName = XName.Get("model", ModelNamespace);
        private static XName BuildName = XName.Get("build", ModelNamespace);
        internal static XName ResourcesName = XName.Get("resources", ModelNamespace);
        private static XName MetadataName = XName.Get("metadata", ModelNamespace);
        
        private static XName XmlLanguageAttributeName = XNamespace.Xml + "lang";

        private static HashSet<string> KnownExtensionNamespaces = new HashSet<string>()
        {
            ModelNamespace,
            MaterialNamespace,
            ProductionNamespace
        };

        public ThreeMfModelUnits ModelUnits { get; set; }
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public IList<ThreeMfResource> Resources { get; } = new ListNonNull<ThreeMfResource>();
        public IList<ThreeMfModelItem> Items { get; } = new ListNonNull<ThreeMfModelItem>();

        public ThreeMfModel()
        {
            ModelUnits = ThreeMfModelUnits.Millimeter;
        }

        private void ParseModelUnits(string value)
        {
            switch (value)
            {
                case "micron":
                    ModelUnits = ThreeMfModelUnits.Micron;
                    break;
                case "millimeter":
                    ModelUnits = ThreeMfModelUnits.Millimeter;
                    break;
                case "centimeter":
                    ModelUnits = ThreeMfModelUnits.Centimeter;
                    break;
                case "inch":
                    ModelUnits = ThreeMfModelUnits.Inch;
                    break;
                case "foot":
                    ModelUnits = ThreeMfModelUnits.Foot;
                    break;
                case "meter":
                    ModelUnits = ThreeMfModelUnits.Meter;
                    break;
                case null:
                    ModelUnits = ThreeMfModelUnits.Millimeter;
                    break;
                default:
                    throw new ThreeMfParseException($"Unsupported model unit '{value}'");
            }
        }

        internal static ThreeMfModel LoadXml(XElement root, Package package)
        {
            var model = new ThreeMfModel();
            model.ParseModelUnits(root.Attribute(UnitAttributeName)?.Value);
            var requiredNamespaces = (root.Attribute(RequiredExtensionsAttributeName)?.Value ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(prefix => root.GetNamespaceOfPrefix(prefix).NamespaceName);

            foreach (var rns in requiredNamespaces)
            {
                if (!KnownExtensionNamespaces.Contains(rns))
                {
                    throw new ThreeMfParseException($"The required namespace '{rns}' is not supported.");
                }
            }

            // metadata
            foreach (var metadataElementGroup in root.Elements(MetadataName).Where(e => e.Attribute(NameAttributeName) != null).GroupBy(e => e.Attribute(NameAttributeName).Value))
            {
                var metadataName = metadataElementGroup.Key;
                var metadataValues = metadataElementGroup.Select(e => e.Value);
                var metadataValue = string.Join("\r\n", metadataValues);
                model.Metadata[metadataName] = metadataValue;
            }

            var resourceMap = model.ParseResources(root.Element(ResourcesName), package);
            model.ParseBuild(root.Element(BuildName), resourceMap);

            return model;
        }

        internal XElement ToXElement(Package package)
        {
            // ensure build items are included
            var resourcesHash = new HashSet<ThreeMfResource>(Resources);
            foreach (var item in Items)
            {
                if (resourcesHash.Add(item.Object))
                {
                    Resources.Add(item.Object);
                }
            }

            // ensure components and property resources are included
            foreach (var resource in Resources.ToList())
            {
                if (resource is ThreeMfObject obj)
                {
                    foreach (var component in obj.Components)
                    {
                        if (resourcesHash.Add(component.Object))
                        {
                            // components must be defined ahead of their reference
                            Resources.Insert(0, component.Object);
                        }
                    }

                    if (obj.PropertyResource != null && resourcesHash.Add((ThreeMfResource)obj.PropertyResource))
                    {
                        // property resources must be defined ahead of their reference
                        Resources.Insert(0, (ThreeMfResource)obj.PropertyResource);
                    }

                    foreach (var triangle in obj.Mesh.Triangles)
                    {
                        if (triangle.PropertyResource != null && resourcesHash.Add((ThreeMfResource)triangle.PropertyResource))
                        {
                            // property resources must be defined ahead of their reference
                            Resources.Insert(0, (ThreeMfResource)triangle.PropertyResource);
                        }
                    }
                }
                else if (resource is ThreeMfTexture2DGroup textureGroup)
                {
                    if (resourcesHash.Add(textureGroup.Texture))
                    {
                        // textures must be defined ahead of their reference
                        Resources.Insert(0, textureGroup.Texture);
                    }
                }
            }

            var resourceMap = new Dictionary<ThreeMfResource, int>();
            for (int i = 0; i < Resources.Count; i++)
            {
                Resources[i].Id = i + 1;
                resourceMap.Add(Resources[i], Resources[i].Id);
            }

            var modelXml = new XElement(ModelName);

            // ensure all appropriate namespaces are included
            var extensionNamespaces = new List<Tuple<string, string>>();
            if (Resources.Any(r => r is ThreeMfColorGroup || r is ThreeMfTexture2D || r is ThreeMfTexture2DGroup))
            {
                extensionNamespaces.Add(Tuple.Create(MaterialNamespace, "m"));
            }

            modelXml.Add(
                new XAttribute(UnitAttributeName, ModelUnits.ToString().ToLowerInvariant()),
                new XAttribute(XmlLanguageAttributeName, DefaultLanguage),
                extensionNamespaces.Select(rns => new XAttribute(XNamespace.Xmlns + rns.Item2, rns.Item1)),
                Metadata.Select(mdkvp => mdkvp.Value.Split('\n').Select(v => v.TrimEnd('\r')).Select(v => new XElement(MetadataName, new XAttribute(NameAttributeName, mdkvp.Key), v))),
                new XElement(ResourcesName,
                    Resources.Select(r => r.ToXElement(resourceMap))),
                new XElement(BuildName,
                    Items.Select(i => i.ToXElement(resourceMap))));
            return modelXml;
        }

        internal void AfterPartAdded(Package package, PackagePart packagePart)
        {
            foreach (var resource in Resources)
            {
                resource.AfterPartAdded(package, packagePart);
            }
        }

        private Dictionary<int, ThreeMfResource> ParseResources(XElement resources, Package package)
        {
            var resourceMap = new Dictionary<int, ThreeMfResource>();
            if (resources == null)
            {
                return resourceMap;
            }

            foreach (var element in resources.Elements())
            {
                var resource = ThreeMfResource.ParseResource(element, resourceMap, package);
                if (resource != null)
                {
                    Resources.Add(resource);
                    resourceMap.Add(resource.Id, resource);
                }
            }

            return resourceMap;
        }

        private void ParseBuild(XElement build, Dictionary<int, ThreeMfResource> resourceMap)
        {
            if (build == null)
            {
                // no build items specified
                return;
            }

            foreach (var element in build.Elements())
            {
                var item = ThreeMfModelItem.ParseItem(element, resourceMap);
                if (item != null)
                {
                    Items.Add(item);
                }
            }
        }
    }
}
