using System.Collections.Generic;

namespace IxMilia.ThreeMf
{
    public interface IThreeMfPropertyResource
    {
        IEnumerable<IThreeMfPropertyItem> PropertyItems { get; }
    }
}
