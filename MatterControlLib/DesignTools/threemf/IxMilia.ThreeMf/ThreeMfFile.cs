using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml.Linq;
using IxMilia.ThreeMf.Collections;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public class ThreeMfFile
    {
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ModelRelationshipType = "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel";
        private const string ModelContentType = "application/vnd.ms-package.3dmanufacturing-3dmodel+xml";
        private const string DefaultModelEntryName = "/3D/3dmodel";
        private const string ModelPathExtension = ".model";
        private const string TargetAttributeName = "Target";
        private const string IdAttributeName = "Id";
        private const string TypeAttributeName = "Type";

        internal static XName RelationshipsName = XName.Get("Relationships", RelationshipNamespace);
        private static XName RelationshipName = XName.Get("Relationship", RelationshipNamespace);

        public IList<ThreeMfModel> Models { get; } = new ListNonNull<ThreeMfModel>();

        public void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            {
                Save(stream);
            }
        }

        public void Save(Stream stream)
        {
            var currentModelSuffix = 0;
            string NextModelFileName()
            {
                var suffix = currentModelSuffix++ == 0 ? string.Empty : $"-{currentModelSuffix}";
                return string.Concat(DefaultModelEntryName, suffix, ModelPathExtension);
            }
            using (var package = Package.Open(stream, FileMode.Create))
            {
                var modelPaths = Enumerable.Range(0, Models.Count).Select(_ => NextModelFileName()).ToList();
                for (int i = 0; i < Models.Count; i++)
                {
                    var model = Models[i];
                    var modelXml = model.ToXElement(package);
                    var modelPath = modelPaths[i];
                    var modelPart = package.WriteXml(modelPath, ModelContentType, modelXml);
                    package.CreateRelationship(new Uri(modelPath, UriKind.RelativeOrAbsolute), TargetMode.Internal, ModelRelationshipType);
                    model.AfterPartAdded(package, modelPart);
                }
            }
        }

        internal static XElement GetRelationshipElement(string target, string id, string type)
        {
            return new XElement(RelationshipName,
                new XAttribute(TargetAttributeName, target),
                new XAttribute(IdAttributeName, id),
                new XAttribute(TypeAttributeName, type));
        }

        public static ThreeMfFile Load(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open))
            {
                return Load(stream);
            }
        }

        public static ThreeMfFile Load(Stream stream)
        {
            using (var package = Package.Open(stream))
            {
                return Load(package);
            }
        }

        public static ThreeMfFile Load(Package package)
        {
            var file = new ThreeMfFile();
            foreach (var modelRelationship in package.GetRelationshipsByType(ModelRelationshipType))
            {
                var modelUri = modelRelationship.TargetUri;
                var modelPart = package.GetPart(modelUri);
                using (var modelStream = modelPart.GetStream())
                {
                    var document = XDocument.Load(modelStream);
                    var model = ThreeMfModel.LoadXml(document.Root, package);
                    file.Models.Add(model);
                }
            }

            return file;
        }
    }
}
