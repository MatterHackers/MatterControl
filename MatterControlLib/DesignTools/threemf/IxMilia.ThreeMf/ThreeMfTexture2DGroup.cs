using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IxMilia.ThreeMf.Collections;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public class ThreeMfTexture2DGroup : ThreeMfResource, IThreeMfPropertyResource
    {
        private const string TextureIdAttributeName = "texid";

        public IList<ThreeMfTexture2DCoordinate> Coordinates { get; } = new ListNonNull<ThreeMfTexture2DCoordinate>();
        public ThreeMfTexture2D Texture { get; set; }

        IEnumerable<IThreeMfPropertyItem> IThreeMfPropertyResource.PropertyItems => Coordinates;

        public ThreeMfTexture2DGroup(ThreeMfTexture2D texture)
        {
            Texture = texture;
        }

        internal override XElement ToXElement(Dictionary<ThreeMfResource, int> resourceMap)
        {
            return new XElement(Texture2DGroupName,
                new XAttribute(IdAttributeName, Id),
                new XAttribute(TextureIdAttributeName, resourceMap[Texture]),
                Coordinates.Select(c => c.ToXElement()));
        }

        internal static ThreeMfTexture2DGroup ParseTexture2DGroup(XElement element, Dictionary<int, ThreeMfResource> resourceMap)
        {
            var texture = resourceMap[element.AttributeIntValueOrThrow(TextureIdAttributeName)] as ThreeMfTexture2D;
            var textureGroup = new ThreeMfTexture2DGroup(texture);
            textureGroup.Id = element.AttributeIntValueOrThrow(IdAttributeName);
            foreach (var textureCoordinateElement in element.Elements(ThreeMfTexture2DCoordinate.Texture2DCoordinateName))
            {
                var coord = ThreeMfTexture2DCoordinate.ParseCoordinate(textureCoordinateElement);
                textureGroup.Coordinates.Add(coord);
            }

            return textureGroup;
        }
    }
}
