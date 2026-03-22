using System.Windows.Forms;

namespace ACT_ChainTimers
{
    partial class ChainTimers
    {
        #region Designer Created Code (Avoid editing)
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.panelData = new System.Windows.Forms.Panel();
            this.panelControls = new System.Windows.Forms.Panel();
            this.buttonHelp = new System.Windows.Forms.Button();
            this.buttonTest = new System.Windows.Forms.Button();
            this.buttonShare = new System.Windows.Forms.Button();
            this.checkBoxImport = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStripMob = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyMobFromMainTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.filterPanel = new System.Windows.Forms.Panel();
            this.contextMenuStripZone = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyCurrentZoneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyZoneFromMainTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripSpell = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copySpellFromMainTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkBoxFit = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panelData.SuspendLayout();
            this.panelControls.SuspendLayout();
            this.contextMenuStripMob.SuspendLayout();
            this.contextMenuStripZone.SuspendLayout();
            this.contextMenuStripSpell.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.MultiSelect = false;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(686, 325);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            this.dataGridView1.CellContextMenuStripNeeded += new System.Windows.Forms.DataGridViewCellContextMenuStripNeededEventHandler(this.dataGridView1_CellContextMenuStripNeeded);
            this.dataGridView1.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.dataGridView1_CellPainting);
            this.dataGridView1.CellToolTipTextNeeded += new System.Windows.Forms.DataGridViewCellToolTipTextNeededEventHandler(this.dataGridView1_CellToolTipTextNeeded);
            this.dataGridView1.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridView1_DataError);
            this.dataGridView1.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.dataGridView1_RowPostPaint);
            // 
            // panelData
            // 
            this.panelData.Controls.Add(this.dataGridView1);
            this.panelData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelData.Location = new System.Drawing.Point(0, 24);
            this.panelData.Name = "panelData";
            this.panelData.Size = new System.Drawing.Size(686, 325);
            this.panelData.TabIndex = 3;
            // 
            // panelControls
            // 
            this.panelControls.Controls.Add(this.checkBoxFit);
            this.panelControls.Controls.Add(this.buttonHelp);
            this.panelControls.Controls.Add(this.buttonTest);
            this.panelControls.Controls.Add(this.buttonShare);
            this.panelControls.Controls.Add(this.checkBoxImport);
            this.panelControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelControls.Location = new System.Drawing.Point(0, 349);
            this.panelControls.Name = "panelControls";
            this.panelControls.Size = new System.Drawing.Size(686, 35);
            this.panelControls.TabIndex = 4;
            // 
            // buttonHelp
            // 
            this.buttonHelp.AutoSize = true;
            this.buttonHelp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonHelp.Location = new System.Drawing.Point(3, 7);
            this.buttonHelp.Name = "buttonHelp";
            this.buttonHelp.Size = new System.Drawing.Size(23, 23);
            this.buttonHelp.TabIndex = 7;
            this.buttonHelp.Text = "?";
            this.toolTip1.SetToolTip(this.buttonHelp, "Visit the help on the project web page");
            this.buttonHelp.UseVisualStyleBackColor = true;
            this.buttonHelp.Click += new System.EventHandler(this.buttonHelp_Click);
            // 
            // buttonTest
            // 
            this.buttonTest.Location = new System.Drawing.Point(506, 5);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(75, 23);
            this.buttonTest.TabIndex = 6;
            this.buttonTest.Text = "Test";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Visible = false;
            this.buttonTest.Click += new System.EventHandler(this.buttonTest_Click);
            // 
            // buttonShare
            // 
            this.buttonShare.Location = new System.Drawing.Point(32, 7);
            this.buttonShare.Name = "buttonShare";
            this.buttonShare.Size = new System.Drawing.Size(75, 23);
            this.buttonShare.TabIndex = 5;
            this.buttonShare.Text = "Copy XML";
            this.toolTip1.SetToolTip(this.buttonShare, "Copy selected item to the clipboard");
            this.buttonShare.UseVisualStyleBackColor = true;
            this.buttonShare.Click += new System.EventHandler(this.buttonShare_Click);
            // 
            // checkBoxImport
            // 
            this.checkBoxImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxImport.AutoSize = true;
            this.checkBoxImport.Location = new System.Drawing.Point(588, 11);
            this.checkBoxImport.Name = "checkBoxImport";
            this.checkBoxImport.Size = new System.Drawing.Size(90, 17);
            this.checkBoxImport.TabIndex = 3;
            this.checkBoxImport.Text = "Parse Imports";
            this.toolTip1.SetToolTip(this.checkBoxImport, "Check to have the plugin process imported files");
            this.checkBoxImport.UseVisualStyleBackColor = true;
            this.checkBoxImport.CheckedChanged += new System.EventHandler(this.checkBoxImport_CheckedChanged);
            // 
            // contextMenuStripMob
            // 
            this.contextMenuStripMob.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyMobFromMainTabToolStripMenuItem});
            this.contextMenuStripMob.Name = "contextMenuStrip1";
            this.contextMenuStripMob.Size = new System.Drawing.Size(210, 26);
            this.contextMenuStripMob.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripMob_Opening);
            // 
            // copyMobFromMainTabToolStripMenuItem
            // 
            this.copyMobFromMainTabToolStripMenuItem.Name = "copyMobFromMainTabToolStripMenuItem";
            this.copyMobFromMainTabToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.copyMobFromMainTabToolStripMenuItem.Text = "Copy mob from Main tab";
            this.copyMobFromMainTabToolStripMenuItem.Click += new System.EventHandler(this.copyMobFromMainTabToolStripMenuItem_Click);
            // 
            // filterPanel
            // 
            this.filterPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.filterPanel.Location = new System.Drawing.Point(0, 0);
            this.filterPanel.Name = "filterPanel";
            this.filterPanel.Size = new System.Drawing.Size(686, 24);
            this.filterPanel.TabIndex = 2;
            // 
            // contextMenuStripZone
            // 
            this.contextMenuStripZone.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyCurrentZoneToolStripMenuItem,
            this.copyZoneFromMainTabToolStripMenuItem});
            this.contextMenuStripZone.Name = "contextMenuStripZone";
            this.contextMenuStripZone.Size = new System.Drawing.Size(212, 48);
            this.contextMenuStripZone.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripZone_Opening);
            // 
            // copyCurrentZoneToolStripMenuItem
            // 
            this.copyCurrentZoneToolStripMenuItem.Name = "copyCurrentZoneToolStripMenuItem";
            this.copyCurrentZoneToolStripMenuItem.Size = new System.Drawing.Size(211, 22);
            this.copyCurrentZoneToolStripMenuItem.Text = "Copy current zone";
            this.copyCurrentZoneToolStripMenuItem.Click += new System.EventHandler(this.copyCurrentZoneToolStripMenuItem_Click);
            // 
            // copyZoneFromMainTabToolStripMenuItem
            // 
            this.copyZoneFromMainTabToolStripMenuItem.Name = "copyZoneFromMainTabToolStripMenuItem";
            this.copyZoneFromMainTabToolStripMenuItem.Size = new System.Drawing.Size(211, 22);
            this.copyZoneFromMainTabToolStripMenuItem.Text = "Copy Zone from Main tab";
            this.copyZoneFromMainTabToolStripMenuItem.Click += new System.EventHandler(this.copyZoneFromMainTabToolStripMenuItem_Click);
            // 
            // contextMenuStripSpell
            // 
            this.contextMenuStripSpell.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copySpellFromMainTabToolStripMenuItem});
            this.contextMenuStripSpell.Name = "contextMenuStripSpell";
            this.contextMenuStripSpell.Size = new System.Drawing.Size(210, 26);
            this.contextMenuStripSpell.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripSpell_Opening);
            // 
            // copySpellFromMainTabToolStripMenuItem
            // 
            this.copySpellFromMainTabToolStripMenuItem.Name = "copySpellFromMainTabToolStripMenuItem";
            this.copySpellFromMainTabToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.copySpellFromMainTabToolStripMenuItem.Text = "Copy Spell from Main tab";
            this.copySpellFromMainTabToolStripMenuItem.Click += new System.EventHandler(this.copySpellFromMainTabToolStripMenuItem_Click);
            // 
            // checkBoxFit
            // 
            this.checkBoxFit.AutoSize = true;
            this.checkBoxFit.Location = new System.Drawing.Point(126, 9);
            this.checkBoxFit.Name = "checkBoxFit";
            this.checkBoxFit.Size = new System.Drawing.Size(131, 17);
            this.checkBoxFit.TabIndex = 8;
            this.checkBoxFit.Text = "Fit Columns to window";
            this.checkBoxFit.UseVisualStyleBackColor = true;
            this.checkBoxFit.CheckedChanged += new System.EventHandler(this.checkBoxFit_CheckedChanged);
            // 
            // ChainTimers
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelData);
            this.Controls.Add(this.filterPanel);
            this.Controls.Add(this.panelControls);
            this.Name = "ChainTimers";
            this.Size = new System.Drawing.Size(686, 384);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panelData.ResumeLayout(false);
            this.panelControls.ResumeLayout(false);
            this.panelControls.PerformLayout();
            this.contextMenuStripMob.ResumeLayout(false);
            this.contextMenuStripZone.ResumeLayout(false);
            this.contextMenuStripSpell.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private DataGridView dataGridView1;
        private Panel panelData;
        private Panel panelControls;
        private CheckBox checkBoxImport;
        private Button buttonShare;
        private Button buttonTest;
        private ToolTip toolTip1;
        private Button buttonHelp;

        #endregion
        private ContextMenuStrip contextMenuStripMob;
        private Panel filterPanel;
        private ToolStripMenuItem copyMobFromMainTabToolStripMenuItem;
        private ContextMenuStrip contextMenuStripZone;
        private ContextMenuStrip contextMenuStripSpell;
        private ToolStripMenuItem copyCurrentZoneToolStripMenuItem;
        private ToolStripMenuItem copyZoneFromMainTabToolStripMenuItem;
        private ToolStripMenuItem copySpellFromMainTabToolStripMenuItem;
        private CheckBox checkBoxFit;
    }
}
