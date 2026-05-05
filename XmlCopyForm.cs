using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace ACT_ChainTimers
{

    public partial class XmlCopyForm : Form
    {
        const int maxChatLen = 240;
        bool _loading = true;
        bool _preIncremet = false;
        bool _autoIncrementing = false;
        int _validTriggers = 0;
        int _validTimers = 0;
        int _totalTriggers = 0;
        int _totalTimers = 0;
        public string _prefix;
        List<string> _timers;
        List<TimerData> _categoryTimers;
        List<CustomTrigger> _triggers;
        //bool _enableOnZoneIn;
        string _groupName;
        //public event EventHandler AltEncodeCheckChanged;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        List<IntPtr> _handles = new List<IntPtr>();

        // simple class to use as the listbox item
        enum ItemType { Trigger, Timer, Command }
        class ListItem
        {
            public string description;
            public string data;
            public ItemType type;
            public override string ToString()
            {
                return description;
            }
        }

        public XmlCopyForm(string prefix, List<string> timers)
        {
            InitializeComponent();

            _prefix = prefix;
            _timers = timers;
        }

        private void XmlCopyForm_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_prefix))
            {
                if (_prefix.StartsWith("g"))
                    radioButtonG.Checked = true;
                else if (_prefix.StartsWith("r"))
                    radioButtonR.Checked = true;
                else
                {
                    radioButtonCustom.Checked = true;
                    textBoxCustom.Text = _prefix;
                }
            }
            else
                _prefix = "g ";

            // look for game instances
            Process[] processes = Process.GetProcessesByName("EverQuest2");
            if (processes.Length > 0)
            {
                _handles = new List<IntPtr>();
                foreach (Process p in processes)
                {
                    // only want the main window
                    if (p.MainWindowTitle.StartsWith("EverQuest II ("))
                    {
                        if(!_handles.Contains(p.MainWindowHandle))
                            _handles.Add(p.MainWindowHandle);
                    }
                }
                if (_handles.Count > 0)
                {
                    foreach (IntPtr intPtr in _handles)
                    {
                        comboBoxGame.Items.Add(intPtr);
                    }
                    comboBoxGame.Items.Add(""); // item to allow user to de-select game activation
                    comboBoxGame.SelectedIndex = 0;
                }
            }

            // build macro list
            buttonMacro_Click(null, null);

            _loading = false;
            this.TopMost = true;
        }

        private void NextListItem(string prefix)
        {
            try
            {
                if (listBox1.SelectedIndex >= 0)
                {
                    if (_preIncremet)
                    {
                        // select the next item
                        _autoIncrementing = true;
                        if (listBox1.SelectedIndex < listBox1.Items.Count - 1)
                            listBox1.SelectedIndex++;
                        else
                        {
                            listBox1.SelectedIndex = -1;
                            toolStripStatusLabel1.Text = "No more items.";
                        }
                        _autoIncrementing = false;
                    }
                    else
                    {
                        // first time through, we use the selected item
                        // next time, we will go to the next item
                        _preIncremet = true;
                    }

                    if (listBox1.SelectedIndex >= 0)
                    {
                        int itemNum = listBox1.SelectedIndex + 1;
                        // copy to the clipboard
                        ListItem item = (ListItem)listBox1.Items[listBox1.SelectedIndex];
                        Clipboard.SetText(prefix + item.data);

                        bool gameActivated = false;
                        if (comboBoxGame.Items.Count > 0 && comboBoxGame.SelectedIndex >= 0)
                        {
                            // if we found an EQII game window, activate it
                            if (!string.IsNullOrEmpty(comboBoxGame.Items[comboBoxGame.SelectedIndex].ToString()))
                            {
                                toolStripStatusLabel1.Text = String.Format(@"<Enter><Ctrl-v> to paste item {0}. {1} for next.", itemNum, item.type == ItemType.Command ? "[Macro]" : "[Copy]");
                                IntPtr handle = (IntPtr)comboBoxGame.Items[comboBoxGame.SelectedIndex];
                                SetForegroundWindow(handle);
                                gameActivated = true;
                            }
                        }
                        if(!gameActivated)
                        {
                            toolStripStatusLabel1.Text = String.Format("Item {0} copied. Press {1} for next.", itemNum, item.type == ItemType.Command ? "[Macro]" : "[Copy]");
                        }
                    }
                }
                else
                {
                    SimpleMessageBox.Show(this, "Select an item to copy to the clipboard.", "Error");
                    toolStripStatusLabel1.Text = string.Empty;
                }
            }
            catch (Exception)
            {
                SimpleMessageBox.Show(this, "Clipboard copy failed. Try again.", "Failed");
                _preIncremet = false;
            }
        }

        // Encode with a scheme like HTML, but avoid characters that confuse or break
        // ACT XML handing, EQII chat pasting, and EQII macros.
        // Use ! to start and : to end a special charcter encode instead of & and ;
        public static string EncodeXml_ish(string text)
        {
            if (text == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            int len = text.Length;
            for (int i = 0; i < len; i++)
            {
                switch (text[i])
                {
                    case '<':
                        sb.Append("!lt:");
                        break;
                    case '>':
                        sb.Append("!gt:");
                        break;
                    case '"':
                        sb.Append("!quot:");
                        break;
                    case '&':
                        sb.Append("!amp:");
                        break;
                    case '\'':
                        sb.Append("!apos:");
                        break;
                    case '\\':
                        sb.Append("!#92:");
                        break;
                    case ':':
                        sb.Append("!#58:");
                        break;
                    case '!':
                        sb.Append("!#33:");
                        break;
                    case ';':
                        sb.Append("!#59:");
                        break;
                    default:
                        sb.Append(text[i]);
                        break;
                }
            }
            return sb.ToString();
        }

        public static string DecodeXml_ish(string text)
        {
            // convert incoming string to encoded HTML, then decode the HTML
            return System.Net.WebUtility.HtmlDecode(text.Replace(':', ';').Replace('!', '&'));
        }

        private void buttonDone_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void radioButtonCustom_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading)
            {
                if (radioButtonCustom.Checked)
                {
                    _prefix = textBoxCustom.Text;
                    // regenerate the macros
                    listBox1.Items.Clear();
                    buttonMacro_Click(null, null);
                }
            }
        }

        private void textBoxCustom_TextChanged(object sender, EventArgs e)
        {
            if (radioButtonCustom.Checked)
            {
                _prefix = textBoxCustom.Text;
                // regenerate the macros
                listBox1.Items.Clear();
                buttonMacro_Click(null, null);
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_loading)
            {
                if (listBox1.SelectedIndex >= 0)
                {
                    ListItem listItem = (ListItem)listBox1.Items[listBox1.SelectedIndex];
                    toolStripStatusLabel1.Text = String.Format("Press {0} to copy selection to clipboard", listItem.type == ItemType.Command ? "[Macro]" : "[Copy]");
                    if (!_autoIncrementing)
                        _preIncremet = false;
                }
            }
        }

        private void buttonMacro_Click(object sender, EventArgs e)
        {
            bool needLoad = true;
            if(listBox1.Items.Count > 0)
            {
                // select the next one
                NextListItem("");
                needLoad = false;
            }
            if(needLoad)
            {
                toolTip1.SetToolTip(buttonMacro, "Press to copy the selected item to the clipboard");
                if (radioButtonG.Checked)
                    _prefix = "g ";
                else if (radioButtonR.Checked)
                    _prefix = "r ";
                else if (!string.IsNullOrEmpty(textBoxCustom.Text))
                {
                    _prefix = textBoxCustom.Text;
                    _prefix = _prefix.TrimStart('/');
                    if (!_prefix.EndsWith(" "))
                        _prefix = _prefix + " ";
                }

                this.Text = String.Format($"Share {_timers.Count} timers");

                int count = WriteMacroFiles(_prefix);
                listBox1.Items.Clear();
                for (int i = 0; i < count; i++)
                {
                    ListItem listItem = new ListItem();
                    listItem.data = string.Format($"/do_file_commands chain-timers{i}.txt");
                    listItem.description = listItem.data;
                    listItem.type = ItemType.Command;
                    listBox1.Items.Add(listItem);
                }
                if (listBox1.Items.Count > 0)
                {
                    listBox1.SelectedIndex = 0;
                    toolStripStatusLabel1.Text = "Press [Macro] to copy selection to clipboard";
                }
            }
        }

        private int WriteMacroFiles(string prefix)
        {
            int count = 0;
            bool done = false;
            for (int j=0; !done; j++)
            {
                StringBuilder sb = new StringBuilder();
                string fileName = $"chain-timers{j}.txt";
                for(int i=0; i<16 && j*16+i<_timers.Count; i++)
                {
                    sb.Append(prefix);
                    sb.Append(_timers[j*16+i]);
                    sb.Append(Environment.NewLine);
                }
                ActGlobals.oFormActMain.SendToMacroFile(fileName, sb.ToString(), string.Empty);
                count++;
                done = (j+1) * 16 > _timers.Count;
            }
            return count;
        }

        private void radioButtonG_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading)
            {
                if (radioButtonG.Checked)
                {
                    // regenerate the macros
                    listBox1.Items.Clear();
                    buttonMacro_Click(null, null);
                }
            }
        }

        private void radioButtonR_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading)
            {
                if (radioButtonR.Checked)
                {
                    // regenerate the macros
                    listBox1.Items.Clear();
                    buttonMacro_Click(null, null);
                }
            }
        }
    }
}
