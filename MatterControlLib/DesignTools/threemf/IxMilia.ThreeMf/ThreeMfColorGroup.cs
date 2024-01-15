using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IxMilia.ThreeMf.Collections;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public class ThreeMfColorGroup : ThreeMfResource, IThreeMfPropertyResource
    {
        public IList<ThreeMfColor> Colors { get; } = new ListNonNull<ThreeMfColor>();

        IEnumerable<IThreeMfPropertyItem> IThreeMfPropertyResource.PropertyItems => Colors;

        internal override XElement ToXElement(Dictionary<ThreeMfResource, int> resourceMap)
        {
            return new XElement(ColorGroupName,
                new XAttribute(IdAttributeName, Id),
                Colors.Select(c => c.ToXElement()));
        }

        internal static ThreeMfColorGroup ParseColorGroup(XElement element)
        {
            var colorGroup = new ThreeMfColorGroup();
            colorGroup.Id = element.AttributeIntValueOrThrow(IdAttributeName);
            foreach (var colorElement in element.Elements(ThreeMfColor.ColorName))
            {
                var color = ThreeMfColor.ParseColor(colorElement);
                colorGroup.Colors.Add(color);
            }

            return colorGroup;
        }
    }
}
