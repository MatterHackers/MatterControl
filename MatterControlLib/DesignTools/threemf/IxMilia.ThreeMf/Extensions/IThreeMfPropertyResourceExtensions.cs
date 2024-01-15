using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace IxMilia.ThreeMf.Extensions
{
    internal static class IThreeMfPropertyResourceExtensions
    {
        public static bool TryGetPropertyResource(this Dictionary<int, ThreeMfResource> resourceMap, XElement element, string attributeName, out IThreeMfPropertyResource propertyResource)
        {
            propertyResource = null;
            var propertyReferenceAttribute = element.Attribute(attributeName);
            if (propertyReferenceAttribute != null)
            {
                if (!int.TryParse(propertyReferenceAttribute.Value, out var propertyIndex))
                {
                    throw new ThreeMfParseException($"Property reference index '{propertyReferenceAttribute.Value}' is not an int.");
                }

                if (resourceMap.ContainsKey(propertyIndex))
                {
                    propertyResource = resourceMap[propertyIndex] as IThreeMfPropertyResource;
                    if (propertyResource == null)
                    {
                        throw new ThreeMfParseException($"Property resource was expected to be of type {nameof(IThreeMfPropertyResource)}.");
                    }

                    return true;
                }
            }

            return false;
        }

        public static int ParseAndValidateRequiredResourceIndex(this IThreeMfPropertyResource propertyResource, XElement element, string attributeName)
        {
            var index = element.AttributeIntValueOrThrow(attributeName);
            propertyResource.ValidatePropertyIndex(index);
            return index;
        }

        public static int? ParseAndValidateOptionalResourceIndex(this IThreeMfPropertyResource propertyResource, XElement element, string attributeName)
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
            {
                return null;
            }

            if (!int.TryParse(attribute.Value, out var index))
            {
                throw new ThreeMfParseException($"Property index '{attribute.Value}' is not an int.");
            }

            propertyResource.ValidatePropertyIndex(index);

            return index;
        }

        private static void ValidatePropertyIndex(this IThreeMfPropertyResource propertyResource, int index)
        {
            var propertyCount =
                (propertyResource.PropertyItems as IList<ThreeMfBase>)?.Count ??
                (propertyResource.PropertyItems as IList<ThreeMfColor>)?.Count ??
                (propertyResource.PropertyItems as IList<ThreeMfTexture2DCoordinate>)?.Count ??
                -1;
            if (propertyCount == -1)
            {
                Debug.Assert(false, "Unknown property item type.  Falling back to much slower .Count().");
                propertyCount = propertyResource.PropertyItems.Count();
            }

            if (index < 0 || index >= propertyCount)
            {
                throw new ThreeMfParseException($"Property index is out of range.  Value must be [0, {propertyCount}).");
            }
        }
    }
}
