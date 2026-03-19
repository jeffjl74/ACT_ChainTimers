using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace ACT_ChainTimers
{
    public partial class AddLoot : Form
    {
        public bool dataChanged = false;

        Looter _looter;
        int _col;

        public AddLoot()
        {
            InitializeComponent();
        }

        public AddLoot(Looter looter, int col)
        {
            InitializeComponent();
            _looter = looter;
            _col = col;
        }

        private void AddLoot_Load(object sender, EventArgs e)
        {
            labelHeader.Text = $"Add a T{_col/2} parcel for {_looter.Player} by adding a unique loot time:";
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            Debug.WriteLine($"{dateTimePicker1.Value}");
            List<DateTime> list = null;
            switch(_col)
            {
                case 2:
                    list = _looter.lootDates;
                    break;
                case 4:
                    list = _looter.T2LootDates;
                    break;
            }
            if(list != null)
            {
                if (list.Contains(dateTimePicker1.Value.ToUniversalTime()))
                    SimpleMessageBox.Show(this, "Each timestamp must be unique", "Not added", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    list.Add(dateTimePicker1.Value.ToUniversalTime());
                    dataChanged = true;
                }
            }
        }

        private void buttonDone_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
