using System;
using System.Collections.Generic;
using System.Xml.Linq;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public class ThreeMfModelItem
    {
        private const string ObjectIdAttributeName = "objectid";
        private const string PartNumberAttributeName = "partnumber";

        private static XName ItemName = XName.Get("item", ThreeMfModel.ModelNamespace);

        private ThreeMfResource _obj;

        public ThreeMfResource Object
        {
            get => _obj;
            set => _obj = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ThreeMfMatrix Transform { get; set; }
        public string PartNumber { get; set; }

        public ThreeMfModelItem(ThreeMfResource obj)
        {
            Object = obj;
            Transform = ThreeMfMatrix.Identity;
        }

        internal XElement ToXElement(Dictionary<ThreeMfResource, int> resourceMap)
        {
            var objectId = resourceMap[Object];
            return new XElement(ItemName,
                new XAttribute(ObjectIdAttributeName, objectId),
                Transform.ToXAttribute(),
                string.IsNullOrEmpty(PartNumber) ? null : new XAttribute(PartNumberAttributeName, PartNumber));
        }

        internal static ThreeMfModelItem ParseItem(XElement element, Dictionary<int, ThreeMfResource> resourceMap)
        {
            if (!int.TryParse(element.AttributeValueOrThrow(ObjectIdAttributeName), out var objectId) &&
                !resourceMap.ContainsKey(objectId))
            {
                throw new ThreeMfParseException($"Invalid object id {objectId}.");
            }

            var modelItem = new ThreeMfModelItem(resourceMap[objectId]);
            modelItem.Transform = ThreeMfMatrix.ParseMatrix(element.Attribute(ThreeMfMatrix.TransformAttributeName));
            modelItem.PartNumber = element.Attribute(PartNumberAttributeName)?.Value;
            return modelItem;
        }
    }
}
