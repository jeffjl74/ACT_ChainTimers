using Advanced_Combat_Tracker;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: AssemblyTitle("Chain Timers Plugin")]
[assembly: AssemblyDescription("Tracks a spell with 2 different recast times")]
[assembly: AssemblyCompany("Mineeme")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace ACT_ChainTimers
{
    public partial class ChainTimers : UserControl, IActPluginV1
	{

        Label lblStatus;        // The status label that appears in ACT's Plugin tab
        TabPage myTab;          // the plugin's tab
        bool firstShow = true;  // plugin tab has never been shown

        // data and persistence
        DataTable spells = new DataTable();
        BindingSource bindingSource = new BindingSource();
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\ChainTimers.config.xml");

        System.Timers.Timer timer = new System.Timers.Timer();

        // filter panel support
        List<string> progressColumns = new List<string>();
        List<string> progressMaxColumns = new List<string>();
        Dictionary<int, Control> filterBoxes = new Dictionary<int, Control>();
        bool filtering = false;
        int firstVisibleFilter;
        int lastVisibleFilter;
        int viewOnlyCols = 2;
        // add the 'filter' ghost text to the filter boxes
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        // game zone
        Regex reCleanActZone = new Regex(@"(?::.+?:)?(?<decoration>\\#[0-9A-F]{6})?(?<zone>.+?)(?: \d+)?$", RegexOptions.Compiled);
        string currentZone = string.Empty;
        string filteredZone = string.Empty;

        // UI thread support
        WindowsFormsSynchronizationContext mUiContext = new WindowsFormsSynchronizationContext();
        bool importChecked;

        // state control
        ConcurrentQueue<EventDescription> eventDescriptions = new ConcurrentQueue<EventDescription>();
        private int _isProcessing = 0;

        // context menu
        int contextRow = -1;
        int contextCol = -1;

        public ChainTimers()
		{
			InitializeComponent();
		}


		#region IActPluginV1 Members
		
		public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
		{
			lblStatus = pluginStatusText;	        // save the status label's reference to our local var
            myTab = pluginScreenSpace;
			pluginScreenSpace.Controls.Add(this);	// Add this UserControl to the tab ACT provides
			this.Dock = DockStyle.Fill;             // Expand the UserControl to fill the tab's client space
            myTab.VisibleChanged += Tab_VisibleChanged;

            LoadSettings();

            timer.Interval = 1000;
            timer.SynchronizingObject = this;
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;

            // Create some sort of parsing event handler.  After the "+=" hit TAB twice and the code will be generated for you.
            ActGlobals.oFormActMain.XmlSnippetAdded += OFormActMain_XmlSnippetAdded;
            ActGlobals.oFormActMain.AfterCombatAction += OFormActMain_AfterCombatAction;
            ActGlobals.oFormActMain.OnCombatStart += OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd += OFormActMain_OnCombatEnd;

            if (ActGlobals.oFormActMain.GetAutomaticUpdatesAllowed())
            {
                // If ACT is set to automatically check for updates, check for updates to the plugin
                // If we don't put this on a separate thread, web latency will delay the plugin init phase
                //new Thread(new ThreadStart(oFormActMain_UpdateCheckClicked)).Start();
            }

            lblStatus.Text = "Plugin Started";
		}

        public void DeInitPlugin()
		{
			// Unsubscribe from any events you listen to when exiting!
			//ActGlobals.oFormActMain.OnLogLineRead -= OFormActMain_OnLogLineRead;
            ActGlobals.oFormActMain.XmlSnippetAdded -= OFormActMain_XmlSnippetAdded;
            ActGlobals.oFormActMain.AfterCombatAction -= OFormActMain_AfterCombatAction;
            ActGlobals.oFormActMain.OnCombatStart -= OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd -= OFormActMain_OnCombatEnd;
            myTab.VisibleChanged -= Tab_VisibleChanged;

            SaveSettings();
			lblStatus.Text = "Plugin Exited";
		}

        #endregion

        private void Tab_VisibleChanged(object sender, EventArgs e)
        {
            if (firstShow)
            {
                AutoSizeGridColumns();
                firstShow = false;
            }
        }

        void LoadSettings()
        {
            bool fileFailed = true;
            if (File.Exists(settingsFile))
            {
                try
                {
                    spells.ReadXml(settingsFile);
                    fileFailed = false;
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Error loading settings: " + ex.Message;
                }
            }
            if (fileFailed)
            {
                // create the datatable
                spells.TableName = "Spells";
                // user fields
                spells.Columns.Add("Enable", typeof(bool));
                spells.Columns["Enable"].DefaultValue = false;
                spells.Columns.Add("Spell", typeof(string));
                spells.Columns.Add("Mob", typeof(string));
                spells.Columns["Mob"].DefaultValue = string.Empty;
                spells.Columns.Add("Zone", typeof(string));
                spells.Columns["Zone"].DefaultValue = string.Empty;
                spells.Columns.Add("First At", typeof(int));
                spells.Columns.Add("Recast 1", typeof(int));
                spells.Columns.Add("Recast 2", typeof(int));
                spells.Columns.Add("Fill Miss", typeof(bool));
                spells.Columns["Fill Miss"].DefaultValue = true;
                spells.Columns.Add("Warn At", typeof(int));
                spells.Columns["Warn At"].DefaultValue = 7;
                spells.Columns.Add("Alert", typeof(string));
                spells.Columns["Alert"].DefaultValue = "joust in 5";

                // progress fields
                spells.Columns.Add("Mob Hit", typeof(bool));
                spells.Columns["Mob Hit"].DefaultValue = false;
                spells.Columns.Add("First Timer", typeof(int));
                spells.Columns.Add("1 Active", typeof(bool));
                spells.Columns["1 Active"].DefaultValue = false;
                spells.Columns.Add("Timer 1", typeof(int));
                spells.Columns.Add("2 Late", typeof(bool));
                spells.Columns["2 Late"].DefaultValue = false;
                spells.Columns.Add("2 Active", typeof(bool));
                spells.Columns["2 Active"].DefaultValue = false;
                spells.Columns.Add("Timer 2", typeof(int));
                spells.Columns.Add("1 Late", typeof(bool));
                spells.Columns["1 Late"].DefaultValue = false;

                // key
                var keys = new DataColumn[3];
                keys[0] = spells.Columns["Spell"];
                keys[1] = spells.Columns["Mob"];
                keys[2] = spells.Columns["Zone"];
                spells.PrimaryKey = keys;
            }
            bindingSource.DataSource = spells;
            dataGridView1.DataSource = bindingSource;

            // add [Reset] button
            DataGridViewButtonColumn btn = new DataGridViewButtonColumn
            {
                UseColumnTextForButtonValue = true,
            };
            btn.Name = btn.HeaderText = btn.Text = "Reset";
            btn.ToolTipText = "Stop and reset all timers";
            dataGridView1.Columns.Insert(0, btn);

            // add [Realign] button
            DataGridViewButtonColumn btn2 = new DataGridViewButtonColumn
            {
                UseColumnTextForButtonValue = true,
            };
            btn2.Name = btn2.HeaderText = btn2.Text = "Realign";
            btn2.ToolTipText = "Stop and wait for a new Timer 1 start";
            dataGridView1.Columns.Insert(1, btn2);

            // tooltips
            dataGridView1.Columns["Spell"].ToolTipText = "Exact name of the spell";
            dataGridView1.Columns["Mob"].ToolTipText = "(Optioinal) Restricts the timers to that mob";
            dataGridView1.Columns["Zone"].ToolTipText = "(Optioinal) Restricts the timers to that zone";
            dataGridView1.Columns["First At"].ToolTipText = "(Optional - Requires Mob name)\nSeconds after the fight starts\nthat the first occurrance\nof the spell is expected";
            dataGridView1.Columns["Recast 1"].ToolTipText = "Spell recast time";
            dataGridView1.Columns["Recast 2"].ToolTipText = "(Optional) If the 2nd occurrance of the spell\nhas a different recast time";
            dataGridView1.Columns["Warn At"].ToolTipText = "Timer seconds remaining when the alert is sounded.\nAlso begins watching for the next spell hit.";
            dataGridView1.Columns["Fill Miss"].ToolTipText = "If the spell is not seen when expected,\nassume it happened anyway";
            dataGridView1.Columns["Mob Hit"].ToolTipText = "Combat has started on the named mob";
            dataGridView1.Columns["2 Late"].ToolTipText = "2nd occurance of the spell\ndid not happen within the recast time";
            dataGridView1.Columns["1 Late"].ToolTipText = "1st occurance of the spell\ndid not happen within the recast time";

            AutoSizeGridColumns();

            // define progress columns for _CellPainting
            progressColumns.Add("First Timer");
            progressColumns.Add("Timer 1");
            progressColumns.Add("Timer 2");
            progressMaxColumns.Add("First At");
            progressMaxColumns.Add("Recast 1");
            progressMaxColumns.Add("Recast 2");

            // filter panel
            filterPanel.Height = dataGridView1.ColumnHeadersHeight + 10;
            // we only put filters above user columns
            firstVisibleFilter = spells.Columns.IndexOf("Enable") + viewOnlyCols;
            lastVisibleFilter = spells.Columns.IndexOf("Alert") + viewOnlyCols;
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if(col.Index < firstVisibleFilter || col.Index > lastVisibleFilter)
                    continue;

                if (col.ValueType == typeof(string) || col.ValueType == typeof(int))
                {
                    TextBoxX tb = new TextBoxX();
                    tb.Tag = col.Index;
                    tb.Name = "textBox" + col.Name.Replace(" ", "_");
                    tb.BackColor = Color.GhostWhite;
                    tb.TextChanged += Tb_TextChanged;
                    tb.ClickX += Tb_ClickX;
                    SetFilterPlaceholder(tb);

                    filterPanel.Controls.Add(tb);
                    filterBoxes[col.Index] = tb;
                }
                else if (col.ValueType == typeof(bool))
                {
                    CheckBox cb = new CheckBox();
                    cb.Tag = col.Index;
                    cb.BackColor = Color.GhostWhite;
                    cb.Name = "checkBox" + col.Name.Replace(" ", "_");
                    cb.CheckedChanged += Cb_CheckedChanged;

                    filterPanel.Controls.Add(cb);
                    filterBoxes[col.Index] = cb;
                }
            }
            dataGridView1.Scroll += (s, e) => AlignFilterBoxes();
            dataGridView1.ColumnWidthChanged += (s, e) => AlignFilterBoxes();
            dataGridView1.ColumnDisplayIndexChanged += (s, e) => AlignFilterBoxes();
            dataGridView1.SizeChanged += (s, e) => AlignFilterBoxes();
        }

        void SaveSettings()
        {
            foreach (DataRow row in spells.Rows)
            {
                row["Mob Hit"] = false;
                row["1 Active"] = false;
                row["2 Late"] = false;
                row["2 Active"] = false;
                row["1 Late"] = false;
                row["First Timer"] = 0;
                row["Timer 1"] = 0;
                row["Timer 2"] = 0;
            }
            spells.WriteXml(settingsFile, XmlWriteMode.WriteSchema);
        }

        #region Filters

        private void Tb_ClickX(object sender, EventArgs e)
        {
            TextBoxX tb = sender as TextBoxX;
            if (tb != null)
                tb.Text = string.Empty;
        }

        private void Cb_CheckedChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void Tb_TextChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        void SetFilterPlaceholder(TextBox tb)
        {
            SendMessage(tb.Handle, EM_SETCUEBANNER, IntPtr.Zero, "Filter");
        }

        void AlignFilterBoxes()
        {
            if (filtering)
            {
                return; // let the view settle before we re-size
            }

            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (!filterBoxes.ContainsKey(col.Index)) continue;

                if (col.Index < firstVisibleFilter || col.Index > lastVisibleFilter)
                    continue;

                Rectangle rect = dataGridView1.GetCellDisplayRectangle(col.Index, -1, true);
                if(rect.Width == 0) continue; // hasn't been displayed / calculated yet

                if (col.ValueType == typeof(string) || col.ValueType == typeof(int))
                {
                    TextBox tb = filterBoxes[col.Index] as TextBox;
                    if(tb != null)
                    {
                        tb.Left = rect.Left;
                        tb.Width = rect.Width;
                        if(tb.Top != filterPanel.Height - tb.Height - 1)
                            tb.Top = filterPanel.Height - tb.Height - 1;
                    }
                }
                else if (col.ValueType == typeof(bool))
                {
                    CheckBox cb = filterBoxes[col.Index] as CheckBox;
                    if (cb != null)
                    {
                        cb.Left = rect.Left;
                        cb.Width = rect.Width;
                        cb.Height = 18;
                        cb.Top = filterPanel.Height - cb.Height - 2;
                        cb.CheckAlign = ContentAlignment.MiddleCenter;
                    }
                }
            }
        }

        void ApplyFilters()
        {
            var filters = new List<string>();

            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (!filterBoxes.ContainsKey(col.Index)) continue;
                if (col.Index < firstVisibleFilter || col.Index > lastVisibleFilter) continue;

                if (col.ValueType == typeof(string) || col.ValueType == typeof(int))
                {
                    TextBox tb = filterBoxes[col.Index] as TextBox;
                    var value = tb.Text;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string esc = EscapeFilterValue(value);
                        filters.Add($"[{col.DataPropertyName}] LIKE '%{esc}%'");
                    }
                }
                else if (col.ValueType == typeof(bool))
                {
                    CheckBox cb = filterBoxes[col.Index] as CheckBox;
                    bool value = cb.Checked;
                    if(value)
                        filters.Add($"[{col.DataPropertyName}] = '{value}'");
                }
            }

            filtering = true;
            try
            {
                if (filters.Count > 0)
                    bindingSource.Filter = string.Join(" AND ", filters);
                else
                    bindingSource.Filter = string.Empty;
            }
            catch (Exception ex)
            {
                SimpleMessageBox.ShowDialog(ActGlobals.oFormActMain, "Invalid filter: " + ex.ToString(), "Filter Error");
            }
            finally
            {
                filtering = false;
            }
            AlignFilterBoxes();
        }

        string EscapeFilterValue(string value)
        {
            var sb = new StringBuilder(value.Length * 2);

            foreach (char c in value)
            {
                switch (c)
                {
                    case '[':
                        sb.Append("[[]");
                        break;
                    case ']':
                        sb.Append("[]]");
                        break;
                    case '\'':
                        sb.Append("''");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private void CheckZoneFilter(object o)
        {
            try
            {
                DataRow[] foundRows = spells.Select($"Zone='{EscapeFilterValue(currentZone)}'");
                if (foundRows.Length > 0)
                {
                    filteredZone = currentZone;
                    TextBox tb = filterPanel.Controls["textBoxZone"] as TextBox;
                    if (tb != null)
                        tb.Text = filteredZone;
                }
            }
            catch { }
        }

        #endregion Filters

        #region ACT hooks

        private void OFormActMain_OnCombatStart(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            currentZone = ActGlobals.oFormActMain.CurrentZone;
            Match match = reCleanActZone.Match(currentZone);
            if (match.Success)
                currentZone = match.Groups["zone"].Value.Trim();

            if(currentZone != filteredZone)
            {
                mUiContext.Post(CheckZoneFilter, null);
            }

            Enqueue(new EventDescription { CombatStart=true, CombatToggleArgs=encounterInfo });
        }

        private void OFormActMain_OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            Enqueue(new EventDescription { CombatEnd=true, CombatToggleArgs=encounterInfo });
        }

        private void OFormActMain_AfterCombatAction(bool isImport, CombatActionEventArgs actionInfo)
        {
            Enqueue(new EventDescription { CombatAction = true, combatActionArgs = actionInfo });
        }

        void oFormActMain_UpdateCheckClicked()
        {
            try
            {
                Version localVersion = this.GetType().Assembly.GetName().Version;
                Task<Version> vtask = Task.Run(() => { return GetRemoteVersionAsync(); });
                vtask.Wait();
                if (vtask.Result > localVersion)
                {
                    DialogResult result = MessageBox.Show("There is an updated version of the Parcels Plugin.\n\nSee the changes by clicking the About link in the plugin.\n\nUpdate it now?\n\n(If there is an update to ACT, you should click No and update ACT first.)",
                        "New Version", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        Task<FileInfo> ftask = Task.Run(() => { return GetRemoteFileAsync(); });
                        ftask.Wait();
                        if (ftask.Result != null)
                        {
                            ActPluginData pluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
                            pluginData.pluginFile.Delete();
                            File.Move(ftask.Result.FullName, pluginData.pluginFile.FullName);
                            Application.DoEvents();
                            ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, false);
                            Application.DoEvents();
                            ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(ex, "RoR Parcels Plugin Update Download:" + ex.Message);
            }
        }

        private void OFormActMain_XmlSnippetAdded(object sender, XmlSnippetEventArgs e)
        {
            if (e.ShareType == "Chain")
            {
                mUiContext.Post(UiParseXml, e);
            }
        }

        private async Task<Version> GetRemoteVersionAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    ProductInfoHeaderValue hdr = new ProductInfoHeaderValue("ACT_Ror_Parcels", "1");
                    client.DefaultRequestHeaders.UserAgent.Add(hdr);
                    HttpResponseMessage response = await client.GetAsync(@"https://api.github.com/repos/jeffjl74/ACT_RoR_Parcels/releases/latest");
                    if (response.IsSuccessStatusCode)
                    {
                        //response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Regex reVer = new Regex(@".tag_name.:.v([^""]+)""");
                        Match match = reVer.Match(responseBody);
                        if (match.Success)
                            return new Version(match.Groups[1].Value);
                    }
                    return new Version("0.0.0");
                }
            }
            catch { return new Version("0.0.0"); }
        }

        private async Task<FileInfo> GetRemoteFileAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    ProductInfoHeaderValue hdr = new ProductInfoHeaderValue("ACT_RoR_Parcels", "1");
                    client.DefaultRequestHeaders.UserAgent.Add(hdr);
                    HttpResponseMessage response = await client.GetAsync(@"https://github.com/jeffjl74/ACT_RoR_Parcels/releases/latest/download/Parcels.dll");
                    if (response.IsSuccessStatusCode)
                    {
                        string tmp = Path.GetTempFileName();
                        var stream = await response.Content.ReadAsStreamAsync();
                        var fileStream = new FileStream(tmp, FileMode.Create);
                        await stream.CopyToAsync(fileStream);
                        fileStream.Close();
                        Application.DoEvents();
                        FileInfo fi = new FileInfo(tmp);
                        return fi;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private void UiParseXml(object o)
        {
            XmlSnippetEventArgs data = o as XmlSnippetEventArgs;
            if (data != null)
            {
                if (data.XmlAttributes == null)
                    return;
                if (data.XmlAttributes.Count < 2)
                    return;

                // Create a new row through the binding layer
                var newRowView = (DataRowView)bindingSource.AddNew();

                // Access the underlying DataRow
                DataRow row = newRowView.Row;
                row["Enable"] = true;

                foreach (var field in data.XmlAttributes)
                {
                    switch (field.Key)
                    {
                        case "S":
                            row["Spell"] = field.Value;
                            break;
                        case "M":
                            row["Mob"] = field.Value;
                            break;
                        case "Z":
                            row["Zone"] = field.Value;
                            break;
                        case "F":
                            row["First At"] = Int32.Parse(field.Value);
                            break;
                        case "R1":
                            row["Recast 1"] = Int32.Parse(field.Value);
                            break;
                        case "R2":
                            row["Recast 2"] = Int32.Parse(field.Value);
                            break;
                        case "L":
                            row["Fill Miss"] = field.Value == "T" ? true : false;
                            break;
                        case "W":
                            row["Warn At"] = Int32.Parse(field.Value);
                            break;
                        case "A":
                            row["Alert"] = field.Value;
                            break;
                    }
                }
                // see if we have this item
                var keys = new Object[3];
                keys[0] = row["Spell"];
                keys[1] = row["Mob"];
                keys[2] = row["Zone"];
                DataRow found = spells.Rows.Find(keys);
                if (found == null)
                {
                    // Commit the new row
                    newRowView.EndEdit();
                }
                else
                {
                    // update the old row
                    found["Spell"] = row["Spell"];
                    found["Mob"] = row["Mob"];
                    found["Zone"] = row["Zone"];
                    found["First At"] = row["First At"];
                    found["Recast 1"] = row["Recast 1"];
                    found["Recast 2"] = row["Recast 2"];
                    found["Fill Miss"] = row["Fill Miss"];
                    found["Warn At"] = row["Warn At"];
                    found["Alert"] = row["Alert"];
                    newRowView.CancelEdit();
                }

                AutoSizeGridColumns();
            }
        }

        #endregion ACT hooks

        #region Progress Processing

        private void Enqueue(EventDescription item)
        {
            eventDescriptions.Enqueue(item);
            StartProcessing();
        }

        private void StartProcessing()
        {
            // Ensure only one UI processing loop is scheduled
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
                return;

            mUiContext.Post(_ => ProcessQueue(), null);
        }

        private void ProcessQueue()
        {
            // runs on UI thread
            try
            {
                while (eventDescriptions.TryDequeue(out EventDescription item))
                {
                    if (item.CombatAction)
                        ProcessCombatAction(item.combatActionArgs);
                    else if (item.CombatStart)
                        ProcessCombatStart(item.CombatToggleArgs);
                    else if (item.CombatEnd)
                        ProcessCombatEnd(item.CombatToggleArgs);
                    else if (item.TimerTick)
                        ProcessTimerTick();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);

                // If new items arrived while we were finishing, restart
                if (!eventDescriptions.IsEmpty)
                    StartProcessing();
            }
        }


        private void ProcessCombatStart(CombatToggleEventArgs args)
        {
            try
            {
                foreach (DataRow row in spells.Rows)
                {
                    if ((bool)row["Enable"]
                        && !string.IsNullOrWhiteSpace(row["Mob"].ToString())
                        && !string.IsNullOrWhiteSpace(row["First At"].ToString()))
                    {
                        bool zoneOK = true;
                        string spellZone = row["Zone"].ToString();
                        if (!string.IsNullOrWhiteSpace(spellZone))
                        {
                            if (spellZone != currentZone)
                                zoneOK = false;
                        }
                        if (zoneOK)
                        {
                            Debug.WriteLine("Combat Start");
                            row["First Timer"] = -1; //combat start indicator to watch for the mob
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCombatStart: {ex.ToString()}");
            }
        }

        private void ProcessCombatEnd(CombatToggleEventArgs args)
        {
            if (!importChecked) //debug, to let timers run after combat stops
            {
                Debug.WriteLine("Combat end stopping everything");
                foreach (DataRow row in spells.Rows)
                {
                    row["Mob Hit"] = false;
                    row["1 Active"] = false;
                    row["2 Late"] = false;
                    row["2 Active"] = false;
                    row["1 Late"] = false;
                    row["First Timer"] = 0;
                    row["Timer 1"] = 0;
                    row["Timer 2"] = 0;
                    timer.Stop();
                }
            }
        }

        private void ProcessCombatAction(CombatActionEventArgs actionInfo)
        {
            try
            {
                foreach (DataRow row in spells.Rows)
                {
                    if ((bool)row["Enable"])
                    {
                        string zone = row["Zone"].ToString();
                        if (!string.IsNullOrWhiteSpace(zone) && zone != this.currentZone)
                            continue;

                        // check for combat started but we haven't seen the mob yet
                        bool watching = !string.IsNullOrWhiteSpace(row["First Timer"].ToString());
                        if (watching && (int)row["First Timer"] < 0 && !(bool)row["Mob Hit"])
                        {
                            if (actionInfo.victim.Equals(row["Mob"].ToString()))
                            {
                                row["Mob Hit"] = true;
                                row["First Timer"] = row["First At"];
                                timer.Start();
                            }
                        }

                        // wait til we're fighting the specified mob
                        if (!string.IsNullOrWhiteSpace(row["Mob"].ToString())
                            && actionInfo.victim.Equals(row["Mob"].ToString())
                            && !(bool)row["Mob Hit"])
                        {
                            row["Mob Hit"] = true;
                        }
                        if (!string.IsNullOrWhiteSpace(row["Mob"].ToString()) && !(bool)row["Mob Hit"])
                            continue;


                        // watch for the spell
                        if (actionInfo.theAttackType.Equals(row["Spell"].ToString()))
                        {
                            int warning = (int)row["Warn At"];

                            if (!(bool)row["1 Active"] && !(bool)row["2 Active"])
                            {
                                if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()))
                                {
                                    timer.Stop();
                                    Debug.WriteLine("combat start from scratch");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Late"] = false;
                                    row["1 Late"] = false;
                                    row["First Timer"] = 0;
                                    timer.Start();
                                }
                            }
                            else if ((bool)row["1 Active"])
                            {
                                int remains = (int)row["Timer 1"];
                                if ((bool)row["1 Late"])
                                {
                                    // it was started by the timer
                                    // restart it based on the hit
                                    Debug.WriteLine("combat re-init timer 1 from hit");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Late"] = false;
                                    remains = (int)row["Timer 1"];
                                }
                                if (remains <= warning)
                                {
                                    //we got hit a little before we expected it
                                    if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                    {
                                        // start timer 2
                                        timer.Stop();
                                        Debug.WriteLine("combat 1 start 2");
                                        row["Timer 1"] = 0;
                                        row["Timer 2"] = row["Recast 2"];
                                        row["1 Active"] = false;
                                        row["2 Late"] = false;
                                        row["2 Active"] = true;
                                        timer.Start();
                                    }
                                    else if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()))
                                    {
                                        // re-start timer 1
                                        timer.Stop();
                                        Debug.WriteLine("combat 1 restart 1");
                                        row["Timer 1"] = row["Recast 1"];
                                        row["1 Active"] = true;
                                        row["1 Late"] = false;
                                        timer.Start();
                                    }
                                }

                            } // end 1 Active

                            else if ((bool)row["2 Active"])
                            {
                                int remains = (int)row["Timer 2"];
                                if ((bool)row["2 Late"])
                                {
                                    // the timer says we are late for the 2nd recast
                                    // so it started the 2nd timer
                                    // but now we got hit
                                    // so re-init the timer
                                    Debug.WriteLine("combat: re-init timer 2 from hit");
                                    row["Timer 2"] = row["Recast 2"];
                                    row["2 Late"] = false;
                                    remains = (int)row["Timer 2"];
                                }
                                if (remains <= warning)
                                {
                                    //we got hit a little before we expected it
                                    // start timer 1
                                    timer.Stop();
                                    Debug.WriteLine("combat 2 starting 1");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Active"] = false;
                                    row["Timer 2"] = 0;
                                    timer.Start();
                                }
                            }
                            else if ((bool)row["1 Late"])
                            {
                                int remains = (int)row["Timer 1"];
                                if (remains + warning >= (int)row["Recast 1"])
                                {
                                    //we got hit a little after we expected it
                                    // restart timer 1
                                    timer.Stop();
                                    Debug.WriteLine("combat 2 start 1 late");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Late"] = false;
                                    row["2 Active"] = false;
                                    row["1 Late"] = false;
                                    timer.Start();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"AfterCombatAction: {e.ToString()}");
            }
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Enqueue(new EventDescription { TimerTick = true });
        }

        private void ProcessTimerTick()
        {
            try
            {
                foreach (DataRow row in spells.Rows)
                {
                    if (!(bool)row["Enable"])
                        continue;

                    string zone = row["Zone"].ToString();
                    if (!string.IsNullOrWhiteSpace(zone))
                        if (zone != this.currentZone) continue;

                    bool fillMissed = (bool)row["Fill Miss"];
                    int warning = (int)row["Warn At"];

                    // watch for very first alert
                    // triggered by start of the fight rather than spell hit
                    if ((bool)row["Mob Hit"])
                    {
                        int remaining = (int)row["First Timer"];
                        if (remaining > 0)
                        {
                            remaining--;
                            row["First Timer"] = remaining;
                            if (remaining == warning && !(bool)row["1 Active"])
                            {
                                Debug.WriteLine("Timer: combat start alert");
                                if (!string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                                    ActGlobals.oFormActMain.TTS(row["Alert"].ToString());
                            }
                            if (remaining == 0 && !(bool)row["1 Active"] && fillMissed)
                            {
                                // didn't see the first hit but want to assume it happened
                                Debug.WriteLine("Timer: first fill-missing, starting recast 1");
                                row["1 Active"] = true;
                                row["1 Late"] = true;
                                row["Timer 1"] = (int)row["Recast 1"] + 1; //+1 since about to subtract one, in "1 Active" processing
                            }
                        }
                    }

                    if ((bool)row["1 Active"])
                    {
                        int remains = (int)row["Timer 1"] - 1;
                        row["Timer 1"] = remains;

                        if (remains == warning && !string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                            ActGlobals.oFormActMain.TTS(row["Alert"].ToString());

                        if (remains <= 0)
                        {
                            Debug.WriteLine("Timer: expired 1");
                            if (fillMissed)
                            {
                                if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                {
                                    Debug.WriteLine("Timer: starting 2");
                                    row["2 Late"] = true;
                                    row["2 Active"] = true;
                                    row["1 Late"] = false;
                                    row["1 Active"] = false;
                                    row["Timer 2"] = row["Recast 2"];
                                }
                                else
                                {
                                    Debug.WriteLine("Timer: re-starting 1");
                                    row["1 Active"] = true;
                                    row["1 Late"] = true;
                                    row["Timer 1"] = row["Recast 1"];
                                }
                            }
                            else
                            {
                                if (remains <= (-1 * warning))
                                {
                                    Debug.WriteLine("Timer: done waiting for 1, going inactive");
                                    row["Timer 1"] = 0;
                                    row["1 Active"] = false;
                                }
                                else
                                {
                                    if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                    {
                                        Debug.WriteLine("Timer: missing 2, it's late");
                                        row["2 Late"] = true;
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Timer: missing another 1, it's late");
                                        row["1 Late"] = true;
                                    }
                                }
                            }
                        }
                    }
                    else if ((bool)row["2 Active"])
                    {
                        int remains = (int)row["Timer 2"] - 1;
                        row["Timer 2"] = remains;

                        if (remains == warning && !string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                            ActGlobals.oFormActMain.TTS(row["Alert"].ToString());

                        if (remains <= 0)
                        {
                            Debug.WriteLine("Timer: expired 2");
                            row["1 Late"] = true;
                            if (fillMissed)
                            {
                                Debug.WriteLine("Timer: starting 1");
                                row["1 Active"] = true;
                                row["2 Late"] = false;
                                row["Timer 1"] = row["Recast 1"];
                            }
                            else
                            {
                                if (remains <= -warning)
                                {
                                    Debug.WriteLine("Timer: done waiting for 2, going inactive");
                                    row["Timer 2"] = 0;
                                    row["2 Active"] = false;
                                }
                                else
                                {
                                    Debug.WriteLine("Timer: missing 1, it's late");
                                    row["1 Late"] = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception tte)
            {
                Debug.WriteLine($"Timer: {tte}");
            }
        }

        #endregion Progress Processing

        #region Datagrid

        private void AutoSizeGridColumns()
        {
            // set autosize to fill cell contents
            for (int i = 0; i <= dataGridView1.Columns.Count - 1; i++)
            {
                dataGridView1.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            // collect and set the calculated sizes (the screen has possibly not been updated yet)
            for (int i = 0; i <= dataGridView1.Columns.Count - 1; i++)
            {
                dataGridView1.Columns[i].Width = dataGridView1.Columns[i].Width;
            }
            // change the columns to user-sizeable
            for (int i = 0; i <= dataGridView1.Columns.Count - 1; i++)
            {
                dataGridView1.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // just don't crash
            Debug.WriteLine($"Data Error: {e.RowIndex},{e.ColumnIndex} {e.Exception.Message}");
            e.Cancel = true;
        }

        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            // add row number to the row headers
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();

            var centerFormat = new StringFormat()
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };

            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth - 2, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, grid.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        private void dataGridView1_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (dataGridView1.Columns[e.ColumnIndex].Name.Equals("Spell")
                    || dataGridView1.Columns[e.ColumnIndex].Name.Equals("Mob")
                    || dataGridView1.Columns[e.ColumnIndex].Name.Equals("Zone")
                        )
                    e.ToolTipText = "Right-click for options";
            }
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dataGridView1.Columns["Reset"].Index)
            {
                Debug.WriteLine($"Reset button {e.RowIndex} clicked");
                if(e.RowIndex >= 0)
                {
                    if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                    {
                        DataRow row = drv.Row;
                        row["Mob Hit"] = false;
                        row["1 Active"] = false;
                        row["1 Late"] = false;
                        row["2 Active"] = false;
                        row["2 Late"] = false;
                        row["First Timer"] = 0;
                        row["Timer 1"] = 0;
                        row["Timer 2"] = 0;
                    }
                }
            }
            else if (e.ColumnIndex == dataGridView1.Columns["Realign"].Index)
            {
                Debug.WriteLine($"Realign button {e.RowIndex} clicked");
                if (e.RowIndex >= 0)
                {
                    if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                    {
                        DataRow row = drv.Row;
                        row["1 Active"] = false;
                        row["1 Late"] = false;
                        row["2 Active"] = false;
                        row["2 Late"] = false;
                        row["First Timer"] = 0;
                        row["Timer 1"] = 0;
                        row["Timer 2"] = 0;
                    }
                }
            }
        }

        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // progress bars
            for (int i = 0; i < progressColumns.Count; i++)
            {
                if (this.dataGridView1.Columns[progressColumns[i]].Index == e.ColumnIndex && e.RowIndex >= 0)
                {
                    using (
                        Brush gridBrush = new SolidBrush(this.dataGridView1.GridColor),
                        backColorBrush = new SolidBrush(e.CellStyle.BackColor),
                        progressBrush = new SolidBrush(Color.LightGreen))
                    {
                        using (Pen gridLinePen = new Pen(gridBrush))
                        {
                            // Erase the cell.
                            e.Graphics.FillRectangle(backColorBrush, e.CellBounds);

                            // Draw the grid lines (only the right and bottom lines;
                            // DataGridView takes care of the others).
                            e.Graphics.DrawLine(gridLinePen, e.CellBounds.Left,
                                e.CellBounds.Bottom - 1, e.CellBounds.Right - 1,
                                e.CellBounds.Bottom - 1);
                            e.Graphics.DrawLine(gridLinePen, e.CellBounds.Right - 1,
                                e.CellBounds.Top, e.CellBounds.Right - 1,
                                e.CellBounds.Bottom);

                            // Draw the text content of the cell, ignoring alignment.
                            int value;
                            if (e.Value != null && Int32.TryParse(e.Value.ToString(), out value))
                            {
                                if (value > 0)
                                {
                                    if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                                    {
                                        DataRow row = drv.Row;
                                        int maxVal = (int)row[progressMaxColumns[i]];
                                        float progress = (float)value / (float)maxVal;
                                        Rectangle progRect = new Rectangle(e.CellBounds.X + 1,
                                                            e.CellBounds.Y + 1, e.CellBounds.Width - 4,
                                                            e.CellBounds.Height - 4);
                                        int progWidth = (int)((float)progRect.Width * progress);
                                        progRect.Width = progWidth;
                                        e.Graphics.FillRectangle(progressBrush, progRect);
                                    }


                                    e.Graphics.DrawString(e.Value.ToString(), e.CellStyle.Font,
                                        Brushes.Black, e.CellBounds.X + 2,
                                        e.CellBounds.Y + 2, StringFormat.GenericDefault);
                                }
                                else if (value < 0)
                                    e.Graphics.DrawString(e.Value.ToString(), e.CellStyle.Font,
                                        Brushes.Red, e.CellBounds.X + 2,
                                        e.CellBounds.Y + 2, StringFormat.GenericDefault);
                            }
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        #endregion Datagrid

        #region Buttons

        private void checkBoxImport_CheckedChanged(object sender, EventArgs e)
        {
            importChecked = checkBoxImport.Checked;
        }

        private void buttonShare_Click(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection rows = dataGridView1.SelectedRows;
            if (rows.Count > 0)
            {
                if (rows[0].DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;

                    StringBuilder sb = new StringBuilder();
                    string spell = row["Spell"].ToString();
                    if (!string.IsNullOrWhiteSpace(spell))
                    {
                        sb.Append($"<Chain S=\"{spell}\"");
                        string mob = row["Mob"].ToString();
                        if (!string.IsNullOrWhiteSpace(mob))
                            sb.Append($" M=\"{mob}\"");
                        string zone = row["Zone"].ToString();
                        if (!string.IsNullOrWhiteSpace(zone))
                            sb.Append($" Z=\"{zone}\"");
                        string first = row["First At"].ToString();
                        if (!string.IsNullOrWhiteSpace(first))
                            sb.Append($" F=\"{first}\"");
                        string recast1 = row["Recast 1"].ToString();
                        if (!string.IsNullOrWhiteSpace(recast1))
                            sb.Append($" R1=\"{recast1}\"");
                        string recast2 = row["Recast 2"].ToString();
                        if (!string.IsNullOrWhiteSpace(recast2))
                            sb.Append($" R2=\"{recast2}\"");
                        string fill = string.IsNullOrWhiteSpace(row["Fill Miss"].ToString()) ? "F" : "T";
                        sb.Append($" L=\"{fill}\"");
                        string warn = row["Warn At"].ToString();
                        sb.Append($" W=\"{warn}\"");
                        string alert = row["Alert"].ToString();
                        sb.Append($" A=\"{alert}\"");

                        sb.Append(" />");

                        try
                        {
                            Clipboard.SetText(sb.ToString());
                        }
                        catch (Exception ce)
                        {
                            SimpleMessageBox.Show(ActGlobals.oFormActMain, ce.ToString(), "Clipboard Error");
                        }
                    }
                    else
                        SimpleMessageBox.Show(ActGlobals.oFormActMain, "Must specify a spell", "Spell Error");
                }
                else
                    SimpleMessageBox.Show(ActGlobals.oFormActMain, "Select a row to share", "Missing selection");
            }
            else
                SimpleMessageBox.Show(ActGlobals.oFormActMain, "Select a row to share", "Missing selection");
        }

        private void buttonTest_Click(object sender, EventArgs e)
        {
            //string p = "S=\"Constant Glare\" M=\"Oogiloi Eye\" Z=\"Zon Zobboz: The Outer Swarmyard [Raid]\" F=\"164\" R1=\"77\" R2=\"37\" L=\"T\" W=\"7\" A=\"fear in 5\" />";
            //var fieldPattern = @"(\w+)=""([^""]*)""";
            //var fieldMatches = Regex.Matches(p, fieldPattern);
            //var xmlFields = new Dictionary<string, string>();
            //foreach (Match match in fieldMatches)
            //{
            //    string fieldName = match.Groups[1].Value;
            //    string fieldValue = match.Groups[2].Value;
            //    xmlFields[fieldName] = fieldValue;
            //}
            //XmlSnippetEventArgs xe = new XmlSnippetEventArgs("Chain", xmlFields, p);
            //UiParseXml(xe);

        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/jeffjl74/ACT_RoR_Parcels#Locked-Parcel-Plugin-for-Advanced-Combat-Tracker");
        }

        private void checkBoxFit_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxFit.Checked)
            {
                int wide = panelData.ClientRectangle.Width / (dataGridView1.Columns.Count + 1);
                if (wide < 50)
                    wide = 50;
                for (int i = 0; i <= dataGridView1.Columns.Count - 1; i++)
                {
                    dataGridView1.Columns[i].Width = dataGridView1.Columns[i].Width = wide;
                }
                checkBoxFit.Text = "Fit columns to contents";
            }
            else
            {
                AutoSizeGridColumns();
                checkBoxFit.Text = "Fit columns to window";
            }
        }

        #endregion Buttons

        #region Context menus

        private void dataGridView1_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (dataGridView1.Columns[e.ColumnIndex].Name.Equals("Mob"))
                {
                    e.ContextMenuStrip = contextMenuStripMob;
                    contextRow = e.RowIndex;
                    contextCol = e.ColumnIndex;
                }
                else if (dataGridView1.Columns[e.ColumnIndex].Name == "Zone")
                {
                    e.ContextMenuStrip = contextMenuStripZone;
                    contextRow = e.RowIndex;
                    contextCol = e.ColumnIndex;
                }
                else if (dataGridView1.Columns[e.ColumnIndex].Name == "Spell")
                {
                    e.ContextMenuStrip = contextMenuStripSpell;
                    contextRow = e.RowIndex;
                    contextCol = e.ColumnIndex;
                }
            }
        }

        private string GetMainTabItem(int level, bool hasDash, string exclude)
        {
            string retVal = string.Empty;

            // if there is a selection on the ACT Main tab, grab it
            TreeNode actNode = ActGlobals.oFormActMain.MainTreeView.SelectedNode;
            if (actNode != null)
            {
                // step up to passed level if the selection is below it
                while (actNode.Level > level && actNode.Parent != null)
                    actNode = actNode.Parent;

                if (actNode.Level == level)
                {
                    string fight = actNode.Text;
                    if (hasDash)
                    {
                        int dash = fight.IndexOf(" - ");
                        fight = fight.Substring(0, dash);
                    }
                    Match match = reCleanActZone.Match(fight);
                    if (match.Success)
                        fight = match.Groups["zone"].Value.Trim();
                    if (fight != exclude)
                        retVal = fight;
                }
            }
            return retVal;
        }

        private void contextMenuStripSpell_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(GetMainTabItem(4, false, "All")))
                copySpellFromMainTabToolStripMenuItem.Enabled = true;
            else
                copySpellFromMainTabToolStripMenuItem.Enabled = false;
        }

        private void copySpellFromMainTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if there is a spell name selection on the ACT Main tab, grab it
            string spell = GetMainTabItem(4, false, "All");
            if (!string.IsNullOrEmpty(spell))
            {
                if (dataGridView1.Rows[contextRow].DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;
                    string colName = dataGridView1.Columns[contextCol].Name;
                    row[colName] = spell;
                    AutoSizeGridColumns();
                    dataGridView1.Refresh();
                }
            }
        }

        private void contextMenuStripMob_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(GetMainTabItem(1, true, "All")))
                copyMobFromMainTabToolStripMenuItem.Enabled = true;
            else
                copyMobFromMainTabToolStripMenuItem.Enabled = false;
        }

        private void copyMobFromMainTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if there is a mob name selection on the ACT Main tab, grab it
            string mob = GetMainTabItem(1, true, "All");
            if (!string.IsNullOrEmpty(mob))
            {
                if (dataGridView1.Rows[contextRow].DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;
                    string colName = dataGridView1.Columns[contextCol].Name;
                    row[colName] = mob;
                    AutoSizeGridColumns();
                    dataGridView1.Refresh();
                }
            }
        }

        private void contextMenuStripZone_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(GetMainTabItem(0, true, "Import/Merge")))
                copyZoneFromMainTabToolStripMenuItem.Enabled = true;
            else
                copyZoneFromMainTabToolStripMenuItem.Enabled = false;
        }

        private void copyCurrentZoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows[contextRow].DataBoundItem is DataRowView drv)
            {
                currentZone = ActGlobals.oFormActMain.CurrentZone;
                Match match = reCleanActZone.Match(currentZone);
                if (match.Success)
                    currentZone = match.Groups["zone"].Value.Trim();

                DataRow row = drv.Row;
                row["Zone"] = currentZone;
                AutoSizeGridColumns();
                dataGridView1.Refresh();
            }
        }

        private void copyZoneFromMainTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if there is a zone name selection on the ACT Main tab, grab it
            string zone = GetMainTabItem(0, true, "Import/Merge");
            if (!string.IsNullOrEmpty(zone))
            {
                Match match = reCleanActZone.Match(zone);
                if (match.Success)
                    zone = match.Groups["zone"].Value.Trim();

                if (dataGridView1.Rows[contextRow].DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;
                    string colName = dataGridView1.Columns[contextCol].Name;
                    row[colName] = zone;
                    AutoSizeGridColumns();
                    dataGridView1.Refresh();
                }
            }
        }

        #endregion Context menus
    }
}
