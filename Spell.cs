using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ACT_ChainTimers
{
    public class Spell : IComparable<Spell>
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Mob { get; set; } = string.Empty;
        [XmlAttribute]
        public int Recast1 { get; set; } = 0;
        [XmlAttribute]
        public int Recast2 { get; set; } = 0;

        public List<DateTime> lootDates { get; set; } = new List<DateTime>();

        [XmlIgnore]
        public int WaitingFor { get; set; } = 1;

        public int CompareTo(Spell other)
        {
            return this.Name.CompareTo(other.Name);
        }

        public override string ToString()
        {
            return Name;
        }

        public void RestartIterator()
        {
            WaitingFor = 1;
        }


        //public (int tier, DateTime time) GetNextTime()
        //{
        //    if (t1Index+1 < T1Count)
        //    {
        //        t1Index++;
        //        return (1, lootDates[t1Index]);
        //    }
        //    else
        //        return (0, DateTime.MinValue);
        //}

        public bool AddTime(int tier, DateTime time)
        {
            bool added = false;
            {
                if (!lootDates.Contains(time))
                {
                    lootDates.Add(time);
                    added = true;
                }
            }
            return added;
        }

        public bool DeleteTime(int tier, DateTime time)
        {
            bool removed = false;
            {
                if (lootDates.Contains(time))
                {
                    lootDates.Remove(time);
                    removed = true;
                }
            }
            return removed;
        }
    }
}
