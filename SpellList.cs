using System.Collections.Generic;
using System.Xml.Serialization;

namespace ACT_ChainTimers
{
    // just need this class to allow specification of a binding list at design time
    [XmlRoot]
    public class SpellList
    {
        public List<Spell> Spells { get; set; } = new List<Spell>();
    }
}
