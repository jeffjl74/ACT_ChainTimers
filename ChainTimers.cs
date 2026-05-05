using Advanced_Combat_Tracker;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("Chain Timers Plugin")]
[assembly: AssemblyDescription("Spell timers with up to 2 different recast times")]
[assembly: AssemblyCompany("Mineeme")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace ACT_ChainTimers
{
    public partial class ChainTimers : UserControl, IActPluginV1
	{
        bool debugEnabled = false; // true for Debug.WriteLineIf printouts & test buttons

        const string helpUrl = "https://github.com/jeffjl74/ACT_ChainTimers#chained-timers-plugin";

        Label lblStatus;        // The status label that appears in ACT's Plugin tab
        TabPage myTab;          // the plugin's tab
        bool firstShow = true;  // plugin tab has never been shown

        // data and persistence
        DataTable spells = new DataTable();
        BindingSource bindingSource = new BindingSource();
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\ChainTimers.config.xml");

        System.Timers.Timer timer = new System.Timers.Timer();

        List<string> optionalColumns = new List<string>();  // normally hidden state-of-progress columns

        // progress bar column definitions
        Dictionary<int, string> progressBars = new Dictionary<int, string>();

        // filter panel support
        Dictionary<int, Control> filterBoxes = new Dictionary<int, Control>();
        bool filtering = false; // reduce unnecessary realignments of the filter boxes when true
        int firstVisibleFilter;
        int lastVisibleFilter;
        int viewOnlyCols = 2;   // the [Reset] and [Realign] buttons aren't in the DataTable
        // add the 'filter' ghost text to the filter boxes
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        // game zone, account for possible color decoration
        Regex reCleanActZone = new Regex(@"(?::.+?:)?(?<decoration>\\#[0-9A-F]{6})?(?<zone>.+?)(?: \d+)?$", RegexOptions.Compiled);
        string currentZone = string.Empty;

        // UI thread support
        WindowsFormsSynchronizationContext mUiContext = new WindowsFormsSynchronizationContext();
        private int _isProcessing = 0;
        bool importChecked; // make state accessible from non-UI thread
        // state change events
        ConcurrentQueue<EventDescription> eventDescriptions = new ConcurrentQueue<EventDescription>();

        // sharing
        static public string shareType = "Chain";
        public string usersPrefix = "g ";

        // context menu
        int contextRow = -1;
        int contextCol = -1;

        public ChainTimers()
		{
			InitializeComponent();
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
                spells.Columns["Spell"].DefaultValue = string.Empty;
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
                spells.Columns["Warn At"].DefaultValue = 6;
                spells.Columns.Add("Alert", typeof(string));
                spells.Columns["Alert"].DefaultValue = "joust in 5";

                // progress fields
                spells.Columns.Add("Mob Hit", typeof(bool));
                spells.Columns["Mob Hit"].DefaultValue = false;
                spells.Columns.Add("First Timer", typeof(int));
                spells.Columns.Add("First Active", typeof(bool));
                spells.Columns["First Active"].DefaultValue = false;
                spells.Columns.Add("First Late", typeof(bool));
                spells.Columns["First Late"].DefaultValue = false;
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
            spells.Columns["Mob"].AllowDBNull = true;
            spells.Columns["Zone"].AllowDBNull = true;

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

            // define hide-able columns
            optionalColumns.Add("Mob Hit");
            optionalColumns.Add("First Active");
            optionalColumns.Add("First Late");
            optionalColumns.Add("1 Active");
            optionalColumns.Add("1 Late");
            optionalColumns.Add("2 Active");
            optionalColumns.Add("2 Late");

            // and hide them
            foreach (string col in optionalColumns)
            {
                dataGridView1.Columns[col].Visible = false;
            }

            // tooltips
            dataGridView1.Columns["Spell"].ToolTipText = "Exact name of the spell";
            dataGridView1.Columns["Mob"].ToolTipText = "(Optioinal) Restricts the timers to that mob";
            dataGridView1.Columns["Zone"].ToolTipText = "(Optioinal) Restricts the timers to that zone";
            dataGridView1.Columns["First At"].ToolTipText = "(Optional - Requires Mob name)\nSeconds after the fight starts\nthat the first occurrance\nof the spell is expected";
            dataGridView1.Columns["Recast 1"].ToolTipText = "Spell recast time";
            dataGridView1.Columns["Recast 2"].ToolTipText = "(Optional) If the 2nd occurrance of the spell\nhas a different recast time";
            dataGridView1.Columns["Warn At"].ToolTipText = "Timer seconds remaining when the alert is sounded.\nAlso when to start watching for the next spell hit.\nAlso how long to wait after missing a spell hit.";
            dataGridView1.Columns["Fill Miss"].ToolTipText = "If the spell is not seen when expected,\nassume it happened anyway";
            dataGridView1.Columns["Mob Hit"].ToolTipText = "Combat has started on the named mob";
            dataGridView1.Columns["2 Late"].ToolTipText = "2nd occurance of the spell\ndid not happen within the recast time";
            dataGridView1.Columns["1 Late"].ToolTipText = "1st occurance of the spell\ndid not happen within the recast time";

            AutoSizeGridColumns();

            // define progress bar columns for _CellPainting
            progressBars.Add(dataGridView1.Columns["First Timer"].Index, "First At");
            progressBars.Add(dataGridView1.Columns["Timer 1"].Index, "Recast 1");
            progressBars.Add(dataGridView1.Columns["Timer 2"].Index, "Recast 2");

            // filter panel
            filterPanel.Height = dataGridView1.ColumnHeadersHeight + 10;
            // we only put filters above user columns
            firstVisibleFilter = spells.Columns.IndexOf("Enable") + viewOnlyCols;
            lastVisibleFilter = spells.Columns.IndexOf("Alert") + viewOnlyCols;
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (col.Index < firstVisibleFilter || col.Index > lastVisibleFilter)
                    continue;

                if (col.ValueType == typeof(string) || col.ValueType == typeof(int))
                {
                    TextBoxX tb = new TextBoxX();
                    tb.Tag = col.Index;
                    tb.Name = "textBox" + col.Name.Replace(" ", "_");
                    tb.BackColor = Color.GhostWhite;
                    tb.TextChanged += Tb_TextChanged;
                    tb.ClickX += Tb_ClickX;
                    // as long as we're looping, let's set the numeric alignment
                    if (col.ValueType == typeof(int))
                    {
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        tb.TextAlign = HorizontalAlignment.Center;
                    }
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
                row["First Timer"] = 0;
                row["First Active"] = false;
                row["First Late"] = false;
                row["1 Active"] = false;
                row["Timer 1"] = 0;
                row["2 Late"] = false;
                row["2 Active"] = false;
                row["Timer 2"] = 0;
                row["1 Late"] = false;
            }
            spells.WriteXml(settingsFile, XmlWriteMode.WriteSchema);
        }


        #region IActPluginV1 Members

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
		{
			lblStatus = pluginStatusText;	        // save the status label's reference to our local var
            myTab = pluginScreenSpace;
			pluginScreenSpace.Controls.Add(this);	// Add this UserControl to the tab ACT provides
			this.Dock = DockStyle.Fill;             // Expand the UserControl to fill the tab's client space
            myTab.VisibleChanged += Tab_VisibleChanged;

            // debugging
            checkBoxImport.Visible = debugEnabled;
            buttonTest.Visible = debugEnabled;

            LoadSettings();

            timer.Interval = 1000;
            timer.SynchronizingObject = this;
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Start();

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

        #region ACT hooks

        private void OFormActMain_OnCombatStart(bool isImport, CombatToggleEventArgs encounterInfo)
        {
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
            int pluginId = 109;

            try
            {
                Version localVersion = this.GetType().Assembly.GetName().Version;
                // Strip any leading 'v' from the string before passing to the Version constructor
                Version remoteVersion = new Version(ActGlobals.oFormActMain.PluginGetRemoteVersion(pluginId).TrimStart(new char[] { 'v' }));
                if (remoteVersion > localVersion)
                {
                    Rectangle screen = Screen.GetWorkingArea(ActGlobals.oFormActMain);
                    DialogResult result = SimpleMessageBox.Show(new Point(screen.Width / 2 - 100, screen.Height / 2 - 100),
                          @"There is an update for the Chain Timers plugin."
                        + @"\line\line Update it now?"
                        + @"\line If there is an update to ACT"
                        + @"\line you should click No and update ACT first."
                        + @"\line\line Release notes at project website:"
                        + @"{\line\ql " + helpUrl + "}"
                        , "Notes New Version", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        FileInfo updatedFile = ActGlobals.oFormActMain.PluginDownload(pluginId);
                        ActPluginData pluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
                        pluginData.pluginFile.Delete();
                        updatedFile.MoveTo(pluginData.pluginFile.FullName);
                        Application.DoEvents();
                        ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, false);
                        Application.DoEvents();
                        ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(ex, "Chained Timers Plugin Update Download failed");
            }
        }

        private void OFormActMain_XmlSnippetAdded(object sender, XmlSnippetEventArgs e)
        {
            if (e.ShareType == shareType)
            {
                mUiContext.Post(UiParseXml, e);
            }
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
                            row["Spell"] = XmlCopyForm.DecodeXml_ish(field.Value);
                            break;
                        case "M":
                            row["Mob"] = XmlCopyForm.DecodeXml_ish(field.Value);
                            break;
                        case "Z":
                            row["Zone"] = XmlCopyForm.DecodeXml_ish(field.Value);
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
                            row["Alert"] = XmlCopyForm.DecodeXml_ish(field.Value);
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
            // running on UI thread
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
            if (dataGridView1.IsCurrentCellInEditMode)
            {
                dataGridView1.EndEdit();
                bindingSource.EndEdit();
            }
            dataGridView1.ClearSelection();

            // if we have timers for this zone,
            // filter to show only those
            currentZone = args.encounter.ZoneName;
            Match match = reCleanActZone.Match(currentZone);
            if (match.Success)
                currentZone = match.Groups["zone"].Value.Trim();
            try
            {
                TextBox tb = filterPanel.Controls["textBoxZone"] as TextBox;
                CheckBox cb = filterPanel.Controls["checkBoxEnable"] as CheckBox;
                if (tb != null && cb != null)
                {
                    cb.Checked = true;
                    if (tb.Text != currentZone)
                        tb.Text = currentZone;
                    ApplyFilters(); //re-apply since CombatEnd clears the zone filter
                }
            }
            catch { }
                
            try
            {
                // look thru the filtered rows
                foreach (DataRowView rowView in bindingSource)
                {
                    DataRow row = rowView.Row;
                    Debug.WriteLineIf(debugEnabled, $"CombatStart: checking {row["Spell"]}");
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
                            Debug.WriteLineIf(debugEnabled, $"CombatStart: Start watching for {row["Mob"]}");
                            row["First Active"] = true; //indicator to watch for the mob in AfterCombatAction
                            row["1 Active"] = false;
                            row["2 Active"] = false;
                            row["First Late"] = false;
                            row["1 Late"] = false;
                            row["2 Late"] = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLineIf(debugEnabled,$"OnCombatStart: {ex.ToString()}");
            }
        }

        private void ProcessCombatEnd(CombatToggleEventArgs args)
        {
            if (!importChecked) //debug, to let timers run after combat stops when importing
            {
                Debug.WriteLineIf(debugEnabled,"Combat end stopping everything");
                foreach (DataRow row in spells.Rows)
                {
                    row["Mob Hit"] = false;
                    row["First Active"] = false;
                    row["First Late"] = false;
                    row["1 Active"] = false;
                    row["2 Late"] = false;
                    row["2 Active"] = false;
                    row["1 Late"] = false;
                    row["First Timer"] = 0;
                    row["Timer 1"] = 0;
                    row["Timer 2"] = 0;
                }
                TextBox tb = filterPanel.Controls["textBoxZone"] as TextBox;
                CheckBox cb = filterPanel.Controls["checkBoxEnable"] as CheckBox;
                if (tb != null && cb != null)
                {
                    cb.Checked = false;
                    tb.Text = string.Empty;
                    ApplyFilters();
                }
            }
        }

        private void ProcessCombatAction(CombatActionEventArgs actionInfo)
        {
            try
            {
                foreach (DataRowView rowView in bindingSource)
                {
                    DataRow row = rowView.Row;
                    if ((bool)row["Enable"])
                    {
                        string zone = row["Zone"].ToString();
                        if (!string.IsNullOrWhiteSpace(zone) && zone != this.currentZone)
                            continue;

                        // check for combat started and we have a first-cast timer
                        bool definedMob = !string.IsNullOrWhiteSpace(row["Mob"].ToString());
                        bool hittingMob = actionInfo.victim.Equals(row["Mob"].ToString());
                        bool watching = !string.IsNullOrWhiteSpace(row["First Timer"].ToString());
                        if (!(bool)row["Mob Hit"]
                            && hittingMob
                            && watching
                            && (bool)row["First Active"])
                        {
                            // have the right mob and a 1st timer, start it
                            row["Mob Hit"] = true;
                            row["First Timer"] = row["First At"];
                        }

                        // wait til we're fighting the specified mob
                        // in case we didn't start a first-cast timer
                        if (definedMob
                            && hittingMob
                            && !(bool)row["Mob Hit"])
                        {
                            row["Mob Hit"] = true;
                        }
                        if (definedMob && !(bool)row["Mob Hit"])
                            continue;


                        // watch for the spell
                        if (actionInfo.theAttackType.Equals(row["Spell"].ToString()))
                        {
                            int warning = (int)row["Warn At"];

                            if (!(bool)row["1 Active"] && !(bool)row["2 Active"])
                            {
                                if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()))
                                {
                                    Debug.WriteLineIf(debugEnabled,"combat start from scratch");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Late"] = false;
                                    row["1 Late"] = false;
                                    if ((bool)row["First Active"])
                                    {
                                        row["First Active"] = false;
                                        row["First Timer"] = 0;
                                    }
                                }
                            } // end neither-is-active

                            else if ((bool)row["1 Active"] && !string.IsNullOrWhiteSpace(row["Timer 1"].ToString()))
                            {
                                int remains = (int)row["Timer 1"];
                                if ((bool)row["1 Late"] || (bool)row["First Late"])
                                {
                                    // It was started by the timer.
                                    // If we are in the warning window...
                                    if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()) && remains > ((int)row["Recast 1"] - warning))
                                    {
                                        // restart it based on the hit
                                        Debug.WriteLineIf(debugEnabled,"combat re-init timer 1 from hit");
                                        row["Timer 1"] = row["Recast 1"];
                                        row["1 Late"] = false;
                                        row["First Late"] = false;
                                        if ((bool)row["First Active"])
                                        {
                                            row["First Active"] = false;
                                            row["First Timer"] = 0;
                                        }
                                        remains = (int)row["Timer 1"];
                                        // and stop any late counter on Recast 2
                                        row["Timer 2"] = 0;
                                        row["1 Late"] = false;
                                        row["2 Active"] = false;
                                    }
                                }
                                if (remains <= warning)
                                {
                                    //we got hit a little before we expected it
                                    if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                    {
                                        // start timer 2
                                        Debug.WriteLineIf(debugEnabled,"combat 1 start 2");
                                        row["Timer 1"] = 0;
                                        row["Timer 2"] = row["Recast 2"];
                                        row["1 Active"] = false;
                                        row["2 Late"] = false;
                                        row["2 Active"] = true;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()))
                                    {
                                        // re-start timer 1
                                        Debug.WriteLineIf(debugEnabled,"combat 1 restart 1");
                                        row["Timer 1"] = row["Recast 1"];
                                        row["1 Active"] = true;
                                        row["1 Late"] = false;
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
                                    // so if we are in the warning window...
                                    if(remains > ((int)row["Recast 2"] - warning))
                                    {
                                        // re-init the timer
                                        Debug.WriteLineIf(debugEnabled,"combat: re-init timer 2 from hit");
                                        row["Timer 2"] = row["Recast 2"];
                                        row["2 Late"] = false;
                                        remains = (int)row["Timer 2"];
                                    }
                                }
                                if (remains <= warning)
                                {
                                    //we got hit a little before we expected it
                                    // start timer 1
                                    Debug.WriteLineIf(debugEnabled,"combat 2 starting 1");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Active"] = false;
                                    row["Timer 2"] = 0;
                                }
                            }
                            else if ((bool)row["1 Late"] && !string.IsNullOrWhiteSpace(row["Timer 1"].ToString()))
                            {
                                int remains = (int)row["Timer 1"];
                                if (!string.IsNullOrWhiteSpace(row["Recast 1"].ToString()) && remains + warning >= (int)row["Recast 1"])
                                {
                                    //we got hit a little after we expected it
                                    // restart timer 1
                                    Debug.WriteLineIf(debugEnabled,"combat 2 start 1 late");
                                    row["Timer 1"] = row["Recast 1"];
                                    row["1 Active"] = true;
                                    row["2 Late"] = false;
                                    row["2 Active"] = false;
                                    row["1 Late"] = false;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLineIf(debugEnabled,$"AfterCombatAction: {e.ToString()}");
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
                foreach (DataRowView rowView in bindingSource)
                {
                    DataRow row = rowView.Row;
                    if (!(bool)row["Enable"])
                        continue;

                    string zone = row["Zone"].ToString();
                    if (!string.IsNullOrWhiteSpace(zone))
                        if (zone != this.currentZone) continue;

                    bool fillMissed = (bool)row["Fill Miss"];
                    int warning = (int)row["Warn At"];

                    // watch for very first alert
                    // triggered by start of the fight
                    if ((bool)row["Mob Hit"])
                    {
                        int remaining = (int)row["First Timer"];
                        if (remaining > 0)
                        {
                            remaining--;
                            row["First Timer"] = remaining;
                            if (remaining == warning && !(bool)row["1 Active"])
                            {
                                Debug.WriteLineIf(debugEnabled,"Timer: combat start alert");
                                if (!string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                                    ActGlobals.oFormActMain.TTS(row["Alert"].ToString());
                            }
                            if (remaining == 0 && !(bool)row["1 Active"] && fillMissed)
                            {
                                // didn't see the first hit but want to assume it happened
                                Debug.WriteLineIf(debugEnabled,"Timer: first fill-missing, starting recast 1");
                                row["First Late"] = true;
                                row["1 Active"] = true;
                                row["1 Late"] = false;
                                row["Timer 1"] = (int)row["Recast 1"] + 1; //+1 since about to subtract one, in "1 Active" processing
                            }
                        }
                        else if (remaining <= 0)
                        {
                            if (remaining == 0)
                            {
                                // progress only if we're still in the initial start of fight window
                                if ((bool)row["First Active"])
                                {
                                    // just hit the mob, not with the spell
                                    remaining--;
                                    row["First Timer"] = remaining;
                                    row["First Late"] = true;
                                }
                            }
                            else if (remaining > (-warning) && (bool)row["First Active"])
                            {
                                remaining--;
                                row["First Timer"] = remaining;
                            }
                            else
                            {
                                // timed out
                                remaining = 0;
                                if((int)row["First Timer"] != 0)  // avoid unnecessay re-paint
                                    row["First Timer"] = 0;
                                row["First Active"] = false;
                                row["First Late"] = false;
                            }
                        }
                    }

                    if ((bool)row["1 Active"] && !string.IsNullOrWhiteSpace(row["Timer 1"].ToString()))
                    {
                        int remains = (int)row["Timer 1"] - 1;
                        row["Timer 1"] = remains;

                        if (remains == warning && !string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                            ActGlobals.oFormActMain.TTS(row["Alert"].ToString());

                        if (remains <= 0)
                        {
                            Debug.WriteLineIf(debugEnabled,"Timer: expired 1");
                            if (fillMissed)
                            {
                                if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                {
                                    if (!(bool)row["2 Active"])
                                    {
                                        Debug.WriteLineIf(debugEnabled, "Timer: starting 2");
                                        row["2 Late"] = true;
                                        row["2 Active"] = true;
                                        row["1 Late"] = false;
                                        row["Timer 2"] = row["Recast 2"];
                                    }
                                }
                                else
                                {
                                    Debug.WriteLineIf(debugEnabled,"Timer: re-starting 1");
                                    row["1 Active"] = true;
                                    row["1 Late"] = true;
                                    row["Timer 1"] = row["Recast 1"];
                                }
                            }

                            if (remains <= (-warning))
                            {
                                Debug.WriteLineIf(debugEnabled,"Timer: done waiting for 1, going inactive");
                                row["Timer 1"] = 0;
                                row["1 Active"] = false;
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(row["Recast 2"].ToString()))
                                {
                                    Debug.WriteLineIf(debugEnabled,"Timer: missing 2, it's late");
                                    row["2 Late"] = true;
                                }
                                else
                                {
                                    Debug.WriteLineIf(debugEnabled,"Timer: missing another 1, it's late");
                                    row["1 Late"] = true;
                                }
                            }
                        }
                    } // end of 1 Active

                    if ((bool)row["2 Active"] && !string.IsNullOrWhiteSpace(row["Timer 2"].ToString()))
                    {
                        int remains = (int)row["Timer 2"] - 1;
                        row["Timer 2"] = remains;

                        if (remains == warning && !string.IsNullOrWhiteSpace(row["Alert"].ToString()))
                            ActGlobals.oFormActMain.TTS(row["Alert"].ToString());

                        if (remains <= 0)
                        {
                            Debug.WriteLineIf(debugEnabled,"Timer: expired 2");
                            row["1 Late"] = true;
                            if (fillMissed)
                            {
                                if (!(bool)row["1 Active"])
                                {
                                    Debug.WriteLineIf(debugEnabled, "Timer: starting 1");
                                    row["1 Active"] = true;
                                    //row["2 Active"] = false;
                                    row["2 Late"] = false;
                                    row["Timer 1"] = row["Recast 1"];
                                }
                            }
                            if (remains <= (-warning))
                            {
                                Debug.WriteLineIf(debugEnabled,"Timer: done waiting for 2, going inactive");
                                row["Timer 2"] = 0;
                                row["2 Active"] = false;
                            }
                            else
                            {
                                Debug.WriteLineIf(debugEnabled,"Timer: missing 1, it's late");
                                row["1 Late"] = true;
                            }
                        }
                    }
                }
            }
            catch (Exception tte)
            {
                Debug.WriteLineIf(debugEnabled,$"Timer: {tte}");
            }
        }

        #endregion Progress Processing

        #region Datagrid

        private void Tab_VisibleChanged(object sender, EventArgs e)
        {
            if (firstShow)
            {
                AutoSizeGridColumns();
                firstShow = false;
            }
        }

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
            var dgv = sender as DataGridView;
            Debug.WriteLineIf(debugEnabled,$"Data Error: {e.RowIndex},{e.ColumnIndex} {e.Exception.Message}");
            e.Cancel = true;
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
                Debug.WriteLineIf(debugEnabled,$"Reset button {e.RowIndex} clicked");
                if(e.RowIndex >= 0)
                {
                    if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                    {
                        DataRow row = drv.Row;
                        row["Mob Hit"] = false;
                        row["First Timer"] = 0;
                        row["First Active"] = false;
                        row["First Late"] = false;
                        row["1 Active"] = false;
                        row["Timer 1"] = 0;
                        row["2 Late"] = false;
                        row["2 Active"] = false;
                        row["Timer 2"] = 0;
                        row["1 Late"] = false;
                    }
                }
            }
            else if (e.ColumnIndex == dataGridView1.Columns["Realign"].Index)
            {
                Debug.WriteLineIf(debugEnabled,$"Realign button {e.RowIndex} clicked");
                if (e.RowIndex >= 0)
                {
                    if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                    {
                        DataRow row = drv.Row;
                        row["1 Active"] = false;
                        row["Timer 1"] = 0;
                        row["2 Late"] = false;
                        row["2 Active"] = false;
                        row["Timer 2"] = 0;
                        row["1 Late"] = false;
                    }
                }
            }
        }

        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            // progress bars
            string maxColName;
            if(progressBars.TryGetValue(e.ColumnIndex, out maxColName))
            {
                //Debug.WriteLineIf(debugEnabled, $"paint[{e.RowIndex}][{e.ColumnIndex}]={e.Value}");
                using (
                    Brush gridBrush = new SolidBrush(dataGridView1.GridColor),
                    backColorBrush = new SolidBrush(e.CellStyle.BackColor),
                    warningBrush = new SolidBrush(Color.MistyRose),
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
                                    int maxVal = (int)row[maxColName];
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
                            {
                                if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
                                {
                                    DataRow row = drv.Row;
                                    float maxVal = (float)((int)row["Warn At"]);
                                    float progress = (maxVal + (float)value) / maxVal;
                                    Rectangle progRect = new Rectangle(e.CellBounds.X + 1,
                                                        e.CellBounds.Y + 1, e.CellBounds.Width - 4,
                                                        e.CellBounds.Height - 4);
                                    int progWidth = (int)((float)progRect.Width * progress);
                                    progRect.Width = progWidth;
                                    e.Graphics.FillRectangle(warningBrush, progRect);
                                }

                                e.Graphics.DrawString(e.Value.ToString(), e.CellStyle.Font,
                                    Brushes.Red, e.CellBounds.X + 2,
                                    e.CellBounds.Y + 2, StringFormat.GenericDefault);
                            }
                        }
                        e.Handled = true;
                    }
                }
            }
        }

        #endregion Datagrid

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
                if (rect.Width == 0) continue; // hasn't been displayed / calculated yet

                if (col.ValueType == typeof(string) || col.ValueType == typeof(int))
                {
                    TextBox tb = filterBoxes[col.Index] as TextBox;
                    if (tb != null)
                    {
                        tb.Left = rect.Left;
                        tb.Width = rect.Width;
                        if (tb.Top != filterPanel.Height - tb.Height - 1)
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

                if (col.ValueType == typeof(string))
                {
                    TextBox tb = filterBoxes[col.Index] as TextBox;
                    string value = tb.Text;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string esc = EscapeFilterValue(value);
                        if(col.Name == "Zone")
                        {
                            // include rows with no zone name
                            filters.Add($"([{col.DataPropertyName}] LIKE '%{esc}%' OR [{col.DataPropertyName}]='')");
                        }
                        else
                            filters.Add($"[{col.DataPropertyName}] LIKE '%{esc}%'");
                    }
                }
                else if (col.ValueType == typeof(int))
                {
                    TextBox tb = filterBoxes[col.Index] as TextBox;
                    string value = tb.Text;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (value.Any(c => c == '<' || c == '>' || c == '='))
                        {
                            if (value.Any(char.IsDigit))
                                filters.Add($"[{col.DataPropertyName}] {value}");
                        }
                        else
                            filters.Add($"[{col.DataPropertyName}] = '{value}'");
                    }
                }
                else if (col.ValueType == typeof(bool))
                {
                    CheckBox cb = filterBoxes[col.Index] as CheckBox;
                    bool value = cb.Checked;
                    if (value)
                        filters.Add($"[{col.DataPropertyName}] = '{value}'");
                }
            }

            filtering = true; // "turn off" UI updates
            try
            {
                if (filters.Count > 0)
                    bindingSource.Filter = string.Join(" AND ", filters);
                else
                    bindingSource.Filter = string.Empty;
            }
            catch
            {
                SimpleMessageBox.ShowDialog(ActGlobals.oFormActMain, "Invalid filter: That filter syntax is not supported" , "Filter Error");
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

        #endregion Filters

        #region Buttons

        private void checkBoxImport_CheckedChanged(object sender, EventArgs e)
        {
            importChecked = checkBoxImport.Checked;
        }

        private void buttonShare_Click(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection rows = dataGridView1.SelectedRows;
            if (rows.Count == 0)
            {
                SimpleMessageBox.Show(ActGlobals.oFormActMain, "Select row(s) to share", "Missing selection");
                return;
            }

            List<string> timers = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;

                    StringBuilder sb = new StringBuilder();
                    string spell = XmlCopyForm.EncodeXml_ish(row["Spell"].ToString());
                    if (!string.IsNullOrWhiteSpace(spell))
                    {
                        sb.Append($"<Chain S='{spell}'");
                        string mob = XmlCopyForm.EncodeXml_ish(row["Mob"].ToString());
                        if (!string.IsNullOrWhiteSpace(mob))
                            sb.Append($" M='{mob}'");
                        string zone = XmlCopyForm.EncodeXml_ish(row["Zone"].ToString());
                        if (!string.IsNullOrWhiteSpace(zone))
                            sb.Append($" Z='{zone}'");
                        string first = row["First At"].ToString();
                        if (!string.IsNullOrWhiteSpace(first))
                            sb.Append($" F='{first}'");
                        string recast1 = row["Recast 1"].ToString();
                        if (!string.IsNullOrWhiteSpace(recast1))
                            sb.Append($" R1='{recast1}'");
                        string recast2 = row["Recast 2"].ToString();
                        if (!string.IsNullOrWhiteSpace(recast2))
                            sb.Append($" R2='{recast2}'");
                        string fill = string.IsNullOrWhiteSpace(row["Fill Miss"].ToString()) ? "F" : "T";
                        sb.Append($" L='{fill}'");
                        string warn = row["Warn At"].ToString();
                        sb.Append($" W='{warn}'");
                        string alert = XmlCopyForm.EncodeXml_ish(row["Alert"].ToString());
                        sb.Append($" A='{alert}'");

                        sb.Append(" />");
                        timers.Add( sb.ToString() );
                    }
                    else
                        SimpleMessageBox.Show(ActGlobals.oFormActMain, $"Must specify a spell name for row {i}", "Spell Error");
                }
            }
            if(timers.Count == 1)
            {
                try
                {
                    Clipboard.SetText(timers[0]);
                }
                catch (Exception ce)
                {
                    SimpleMessageBox.Show(ActGlobals.oFormActMain, ce.ToString(), "Clipboard Error");
                }
            }
            else
            {
                XmlCopyForm form = new XmlCopyForm(usersPrefix, timers);
                form.FormClosing += (s, ev) => { usersPrefix = form._prefix; };
                Point screenButton = buttonShare.PointToScreen(buttonShare.Location);
                Point loc = new Point(screenButton.X, screenButton.Y - form.Size.Height);
                form.Show();
                form.Location = loc;
            }
        }

        private void buttonTest_Click(object sender, EventArgs e)
        {
            string p = "S='Constant Glare Test' M='Oogiloi Eye' Z='Zon Zobboz!#58: The Outer Swarmyard [Raid]' F='164' R1='77' R2='37' L='T' W='7' A='fear in 5' />";
            var fieldPattern = @"(\w+)='([^']*)'";
            var fieldMatches = Regex.Matches(p, fieldPattern);
            var xmlFields = new Dictionary<string, string>();
            foreach (Match match in fieldMatches)
            {
                string fieldName = match.Groups[1].Value;
                string fieldValue = match.Groups[2].Value;
                xmlFields[fieldName] = fieldValue;
            }
            XmlSnippetEventArgs xe = new XmlSnippetEventArgs(shareType, xmlFields, p);
            UiParseXml(xe);
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            Process.Start(helpUrl);
        }

        private void checkBoxFit_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxFit.Checked)
            {
                int colCount = 0;
                for (int i =  0; i < dataGridView1.Columns.Count; i++)
                {
                    if (dataGridView1.Columns[i].Visible)
                        colCount++;
                }
                int wide = panelData.ClientRectangle.Width / (colCount + 1);
                if (wide < 50)
                    wide = 50;
                for (int i = 0; i <= dataGridView1.Columns.Count - 1; i++)
                {
                    dataGridView1.Columns[i].Width = dataGridView1.Columns[i].Width = wide;
                }
            }
            else
            {
                AutoSizeGridColumns();
            }
        }

        private void checkBoxShowStates_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxShowStates.Checked)
            {
                foreach (string col in optionalColumns)
                {
                    dataGridView1.Columns[col].Visible = true;
                }
            }
            else
            {
                foreach (string col in optionalColumns)
                {
                    dataGridView1.Columns[col].Visible = false;
                }
            }
            AlignFilterBoxes();
        }

        private void buttonShare_MouseHover(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection rows = dataGridView1.SelectedRows;
            if(rows.Count > 1)
                toolTip1.SetToolTip(buttonShare, "Open Macro dialog");
            else if(rows.Count == 1)
                toolTip1.SetToolTip(buttonShare, "Copy selected row to the clipboard");
            else if (rows.Count == 0)
                toolTip1.SetToolTip(buttonShare, "Must first select row(s) to share");
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
            if (!string.IsNullOrWhiteSpace(dataGridView1.Rows[contextRow].Cells[contextCol].Value.ToString()))
                setFilterToThisZoneToolStripMenuItem.Enabled = true;
            else
                setFilterToThisZoneToolStripMenuItem.Enabled = false;
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

        private void setFilterToThisZoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string zone = dataGridView1.Rows[contextRow].Cells[contextCol].Value.ToString();
                if (!string.IsNullOrWhiteSpace(zone))
                {
                    TextBox tb = filterPanel.Controls["textBoxZone"] as TextBox;
                    if (tb != null)
                    {
                        tb.Text = zone;
                    }
                }
            }
            catch { }
        }

        #endregion Context menus
    }
}
