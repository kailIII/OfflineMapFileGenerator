﻿namespace com.atgardner.OMFG
{
    using com.atgardner.OMFG.packagers;
    using com.atgardner.OMFG.sources;
    using com.atgardner.OMFG.tiles;
    using com.atgardner.OMFG.utils;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public partial class MainForm : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly string SourceFile = @"sources\sources.json";

        private readonly TilesManager manager;
        private MapSource[] sources;
        private bool zoomChangeFromCode;

        public MainForm()
        {
            InitializeComponent();
            manager = new TilesManager();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            CreateZoomCheckBoxes();
            try
            {
                sources = await MapSource.LoadSources(SourceFile);
                cmbMapSource.DataSource = sources;
            }
            catch (Exception ex)
            {
                logger.FatalException("Failed reading sources", ex);
                MessageBox.Show("Failed reading sources", "Missing sources", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (dlgOpenFile.ShowDialog() == DialogResult.OK)
            {
                var fileNames = from f in dlgOpenFile.FileNames select Path.GetFileName(f);
                txtBxInput.Text = string.Join("; ", fileNames);
                logger.Debug("Input files: {0}", txtBxInput.Text);
                if (string.IsNullOrWhiteSpace(txtBxOutput.Text))
                {
                    var outputFileName = Path.GetFileNameWithoutExtension(fileNames.First());
                    txtBxOutput.Text = outputFileName;
                    logger.Debug("Automatically selected output file name: {0}", outputFileName);
                }
            }
        }

        private async void btnRun_Click(object sender, EventArgs e)
        {
            var inputFiles = dlgOpenFile.FileNames;
            if (inputFiles.Length == 0)
            {
                logger.Warn("No input files selected");
                MessageBox.Show("Please specify an input file or files", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var zoomLevels = GetZoomLevels();
            if (zoomLevels.Length == 0)
            {
                logger.Warn("No zoom level selected");
                MessageBox.Show("Please chose at least one zoom level", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var source = cmbMapSource.SelectedItem as MapSource;
            string outputFile = txtBxOutput.Text;
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                logger.Warn("No output file name selected");
                MessageBox.Show("Please specify an output file name", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var formatType = rdBtnBCNav.Checked ? FormatType.BCNav : FormatType.OruxMaps;
            tlpContainer.Enabled = false;
            prgBar.Value = 0;
            await Task.Factory.StartNew(() => DownloadTiles(inputFiles, zoomLevels, source, outputFile.Replace("\"", string.Empty), formatType));
            tlpContainer.Enabled = true;
        }

        private void cmbMapSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mapSource = cmbMapSource.SelectedItem as MapSource;
            if (mapSource != null)
            {
                logger.Debug("Changed source to {0}", mapSource);
                ResetZoomCheckBoxes(mapSource.MinZoom, mapSource.MaxZoom);
                HtmlUtils.ConfigLinkLabel(lnk, mapSource.Attribution);
            }
        }

        private void CreateZoomCheckBoxes()
        {
            for (int i = 0; i <= 20; i++)
            {
                var chkBx = new CheckBox();
                chkBx.Width = 40;
                chkBx.Text = i.ToString();
                chkBx.CheckedChanged += chkBxZoom_CheckedChanged;
                flpZoomLevels.Controls.Add(chkBx);
            }

            flpZoomLevels.Controls.SetChildIndex(chkBxAll, 21);
        }

        private void chkBxZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (zoomChangeFromCode)
            {
                return;
            }

            var zoomLevels = GetZoomLevels();
            var mapSource = cmbMapSource.SelectedItem as MapSource;
            var totalLevels = mapSource.MaxZoom - mapSource.MinZoom + 1;
            var checkState = CheckState.Unchecked;
            if (zoomLevels.Length > 0)
            {
                checkState = CheckState.Indeterminate;
            }

            if (zoomLevels.Length == totalLevels)
            {
                checkState = CheckState.Checked;
            }

            zoomChangeFromCode = true;
            chkBxAll.CheckState = checkState;
            zoomChangeFromCode = false;
        }

        private void chkBxAll_CheckedChanged(object sender, EventArgs e)
        {
            if (zoomChangeFromCode)
            {
                return;
            }

            var chkBxAll = (CheckBox)sender;
            zoomChangeFromCode = true;
            foreach (CheckBox chkBx in flpZoomLevels.Controls)
            {
                if (chkBx.Enabled)
                {
                    chkBx.Checked = chkBxAll.Checked;
                }
            }

            zoomChangeFromCode = false;
        }

        private void lnk_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var lnk = (LinkLabel)sender;
            lnk.Links[lnk.Links.IndexOf(e.Link)].Visited = true;
            Process.Start(e.Link.LinkData.ToString());
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var about = new AboutBox())
            {
                about.ShowDialog(this);
            }
        }

        private int[] GetZoomLevels()
        {
            var levels = new List<int>();
            foreach (CheckBox chkBx in flpZoomLevels.Controls)
            {
                int zoomLevel;
                if (chkBx.Enabled && chkBx.Checked && int.TryParse(chkBx.Text, out zoomLevel))
                {
                    levels.Add(zoomLevel);
                }
            }

            return levels.ToArray();
        }

        private void ResetZoomCheckBoxes(int minZoom, int maxZoom)
        {
            CheckBox checkBox;
            for (int i = 0; i < minZoom; i++)
            {
                checkBox = (CheckBox)flpZoomLevels.Controls[i];
                DisableCheckBox(checkBox);
            }

            for (int i = minZoom; i <= maxZoom; i++)
            {
                checkBox = (CheckBox)flpZoomLevels.Controls[i];
                EnableCheckBox(checkBox);
            }

            for (int i = maxZoom + 1; i <= 20; i++)
            {
                checkBox = (CheckBox)flpZoomLevels.Controls[i];
                DisableCheckBox(checkBox);
            }
        }

        private void UpdateStatus(string status)
        {
            BeginInvoke((MethodInvoker)(() => lblStatus.Text = status));
        }

        private void UpdateProgBar(int value)
        {
            BeginInvoke((MethodInvoker)(() => prgBar.Value = value));
        }

        private bool PromptUser(int toDownload)
        {
            var text = string.Format("Are you sure you whish to download {0} tiles?", toDownload);
            DialogResult result = DialogResult.No;
            Invoke((MethodInvoker)(() =>
            {
                result = MessageBox.Show(this, text, "Offline Map File Generator", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }));
            return result == DialogResult.Yes;
        }

        private async void DownloadTiles(string[] inputFiles, int[] zoomLevels, MapSource source, string outputFile, FormatType formatType)
        {
            tlpContainer.Enabled = false;
            prgBar.Value = 0;
            logger.Debug("Getting tiles, inputFiles: {0}, outputFile: {1}, zoomLevels: {2}, source: {3}", inputFiles, outputFile, zoomLevels, source);
            UpdateStatus("Done Reading File");
            var coordinates = FileUtils.ExtractCoordinates(inputFiles);
            logger.Trace("Got coordinates stream from input files");
            var map = manager.GetTileDefinitions(coordinates, zoomLevels);
            logger.Trace("Got tile definition stream from coordinates");
            var toDownload = manager.CheckTileCache(source, map);
            var total = map.Count();
            if (toDownload != 0 && !PromptUser(toDownload))
            {
                return;
            }

            var tasks = manager.GetTileData(source, map).ToList();
            var prevPercentage = -1;
            var current = 0;
            var packager = SQLitePackager.GetPackager(formatType, outputFile, map);
            using (packager)
            {
                await packager.Init();
                while (tasks.Count > 0)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                    var tile = await task;
                    await packager.AddTile(tile);
                    current++;
                    var progressPercentage = 100 * current / total;
                    if (progressPercentage > prevPercentage)
                    {
                        prevPercentage = progressPercentage;
                        UpdateProgBar(progressPercentage);
                        UpdateStatus(string.Format("{0}/{1} Tiles Downloaded", current, total));
                    }
                }

                UpdateStatus(string.Format("Done Downloading {0} tiles", total));
            }
        }

        private static void DisableCheckBox(CheckBox checkBox)
        {
            checkBox.Tag = checkBox.CheckState;
            if (checkBox.Checked)
            {
                checkBox.CheckState = CheckState.Indeterminate;
            }

            checkBox.Enabled = false;
        }

        private static void EnableCheckBox(CheckBox checkBox)
        {
            if (checkBox.Tag is CheckState)
            {
                checkBox.CheckState = (CheckState)checkBox.Tag;
                checkBox.Tag = null;
            }

            checkBox.Enabled = true;
        }
    }
}
