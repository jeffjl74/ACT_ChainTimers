using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Advanced_Combat_Tracker;

namespace ACT_ChainTimers
{
    internal class EventDescription
    {
        public bool CombatStart { get; set; } = false;
        public bool CombatEnd { get; set; } = false;
        public bool CombatAction { get; set; } = false;
        public bool TimerTick { get; set; } = false;
        public CombatToggleEventArgs CombatToggleArgs { get; set; } = null;
        public CombatActionEventArgs combatActionArgs { get; set; } = null;
        public EventDescription() { }

    }
}
