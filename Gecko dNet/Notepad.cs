﻿
using AMS.Profile;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using System.Windows.Forms;

namespace GeckoApp
{
    public partial class NoteSheets : Form
    {
        private string gId;

        private TabPage PSelected;

        private TabPage SourceTab;

        public NoteSheets()
        {
            InitializeComponent();
        }

        public TabPage GetTabPageFromXY(int x, int y)
        {
            for (int i = 0; i < SheetSelection.TabPages.Count; i++)
                if (SheetSelection.GetTabRect(i).Contains(x, y))
                    return SheetSelection.TabPages[i];
            return null;
        }

        private void CreateSheet(Sheet data)
        {
            TabPage newTab = new TabPage();
            newTab.Tag = data;
            NotePage noteData = new NotePage(newTab, this);
            noteData.Location = new Point(0, 0);
            noteData.Size = newTab.Size;
            noteData.Anchor = AnchorStyles.Bottom | AnchorStyles.Top |
                              AnchorStyles.Right | AnchorStyles.Left;
            newTab.Controls.Add(noteData);
            UpdateTitle(newTab);
            SheetSelection.TabPages.Add(newTab);
        }

        private List<Sheet> ImportSheet(string filename)
        {
            List<Sheet> sheets = new List<Sheet>();
            Xml ImportFile = new Xml(filename);
            try
            {
                ImportFile.RootName = "notesheet";

                string[] sheetsecs = ImportFile.GetSectionNames();
                string name, content;
                foreach (string sheetname in sheetsecs)
                {
                    name = ImportFile.GetValue(sheetname, "name", "[Noname]");
                    content = ImportFile.GetValue(sheetname, "content", string.Empty);
                    sheets.Add(new Sheet(name, content));
                }
            }
            finally
            {
            }
            return sheets;
        }

        public void Show(string gameID)
        {
            char delim = Path.DirectorySeparatorChar;
            if (gameID.Length >= 3)
            {
                gId = gameID.Substring(0, 3);
            }
            else
            {
                gId = "SYSMENU";
            }
            Text = "Notepad (Game ID: " + gId + ")";
            if (!Directory.Exists("notes"))
                Directory.CreateDirectory("notes");

            PSelected = null;
            SheetSelection.TabPages.Clear();

            List<Sheet> sheets = null;

            if (File.Exists("notes" + delim + gId + ".xml"))
                try
                {
                    sheets = ImportSheet("notes" + delim + gId + ".xml");
                }
                catch
                {
                    sheets = new List<Sheet>();
                }
            else
                sheets = new List<Sheet>();

            if (sheets.Count == 0)
                sheets.Add(new Sheet("Default sheet"));

            for (int i = 0; i < sheets.Count; i++)
            {
                CreateSheet(sheets[i]);
            }
            SheetSelection.SelectedIndex = 0;

            Show();
        }

        public void UpdateTitle(TabPage page)
        {
            Sheet sheet = (Sheet)page.Tag;
            string title = sheet.title;
            page.ToolTipText = title;
            if (title.Length > 25)
                title = title.Substring(0, 22) + "...";
            page.Text = title;
        }

        private void Whatsthis_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This is a simple and straight forward notepad for all notes you can do while code searching.. store addresses here you found or store breakpoint traces here. These notes are stored per game (region independent - so if you have multiple regions of a game these lists will be identical)! When closing this window, the notes you made will be immediately stored to your harddisk!");
        }

        private void AddSheet_Click(object sender, EventArgs e)
        {
            int cnt = SheetSelection.TabPages.Count + 1;
            Sheet nsheet = new Sheet("New sheet " + cnt.ToString());
            CreateSheet(nsheet);
            PSelected = SheetSelection.TabPages[SheetSelection.TabPages.Count - 1];
            SheetSelection.SelectedTab = PSelected;
        }

        public void DeleteSheet(TabPage selected)
        {
            if (selected == null)
                return;

            if (SheetSelection.TabPages.Count == 1)
            {
                MessageBox.Show("You must have at least one sheet!");
                return;
            }
            int index = SheetSelection.TabPages.IndexOf(selected);
            Sheet sheet = (Sheet)selected.Tag;

            if (sheet.content == string.Empty ||
                MessageBox.Show("Are you sure you want to delete the current sheet: " +
                sheet.title + " ?", "Warning", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                SheetSelection.TabPages.RemoveAt(index);
                if (index > 0)
                    index--;
                if (SheetSelection.TabPages.Count == 0)
                {
                    PSelected = null;
                }
                else
                {
                    PSelected = SheetSelection.TabPages[index];
                    SheetSelection.SelectedTab = PSelected;
                }
            }
        }

        private void NoteSheets_FormClosing(object sender, FormClosingEventArgs e)
        {
            char delim = Path.DirectorySeparatorChar;
            string filename = "notes" + delim + gId + ".xml";
            if (File.Exists(filename))
                File.Delete(filename);
            Xml ExportFile = new Xml(filename);
            try
            {
                ExportFile.RootName = "notesheet";

                string secname;
                int pagecount = SheetSelection.TabPages.Count;
                for (int i = 0; i < pagecount; i++)
                {
                    Sheet sheet = (Sheet)SheetSelection.TabPages[i].Tag;
                    secname = "sheet" + (i + 1).ToString();
                    ExportFile.SetValue(secname, "name", sheet.title);
                    ExportFile.SetValue(secname, "content", sheet.content);
                }
            }
            finally
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void NoteSheets_Load(object sender, EventArgs e)
        {
            this.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        private void SheetSelection_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && SourceTab != null)
            {
                SuspendLayout();
                TabPage currTabPage = GetTabPageFromXY(e.X, e.Y);
                if (currTabPage != null && !currTabPage.Equals(SourceTab))
                {
                    SourceTab.SuspendLayout();
                    if (SheetSelection.TabPages.IndexOf(currTabPage) < SheetSelection.TabPages.IndexOf(SourceTab))
                    {
                        SheetSelection.TabPages.Remove(SourceTab);
                        SheetSelection.TabPages.Insert(SheetSelection.TabPages.IndexOf(currTabPage), SourceTab);
                        SheetSelection.SelectedTab = SourceTab;
                    }
                    else if (SheetSelection.TabPages.IndexOf(currTabPage) > SheetSelection.TabPages.IndexOf(SourceTab))
                    {
                        SheetSelection.TabPages.Remove(SourceTab);
                        SheetSelection.TabPages.Insert(SheetSelection.TabPages.IndexOf(currTabPage) + 1, SourceTab);
                        SheetSelection.SelectedTab = SourceTab;
                    }
                    SourceTab.ResumeLayout();
                }
            }
            ResumeLayout();
            SourceTab = null;
            Cursor = Cursors.Default;
        }

        private void SheetSelection_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && SourceTab != null)
            {
                TabPage HoveredTabPage = GetTabPageFromXY(e.X, e.Y);
                if (HoveredTabPage != null)
                {
                    Rectangle Rect = SheetSelection.GetTabRect(SheetSelection.TabPages.IndexOf(HoveredTabPage));
                    if (SheetSelection.TabPages.IndexOf(HoveredTabPage) < SheetSelection.TabPages.IndexOf(SourceTab))
                    {
                        Cursor = Cursors.Hand;
                        if (HoveredTabPage == SheetSelection.TabPages[0])
                        { }
                        else
                        { }
                    }
                    else if (SheetSelection.TabPages.IndexOf(HoveredTabPage) > SheetSelection.TabPages.IndexOf(SourceTab))
                    {
                        Cursor = Cursors.Hand;
                    }
                    else
                        Cursor = Cursors.Default;
                }
                else
                    Cursor = Cursors.Default;
            }
        }

        private void SheetSelection_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && SheetSelection.SelectedTab != null && !SheetSelection.GetTabRect(SheetSelection.SelectedIndex).IsEmpty)
                SourceTab = SheetSelection.SelectedTab;
        }
    }
}
