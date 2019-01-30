using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace GeckoApp.external
{
    public partial class HistoryTextBox : TextBox
    {

        [Browsable(true)]
        public bool AutoHistory { get; set; }

        public HistoryTextBox()
        {
            InitializeComponent();

            comboBoxHistory.Parent = this.Parent;
            comboBoxHistory.Location = this.Location;
            comboBoxHistory.Width = this.Width;
            comboBoxHistory.MaxLength = this.MaxLength;
            comboBoxHistory.Font = this.Font;
        }

        private void HistoryTextBox_Layout(object sender, LayoutEventArgs e)
        {
            comboBoxHistory.Parent = this.Parent;
            comboBoxHistory.Location = this.Location;
            comboBoxHistory.Width = this.Width;
            comboBoxHistory.DropDownWidth = comboBoxHistory.Width + 15;
        }

        private void HistoryTextBox_LocationChanged(object sender, EventArgs e)
        {
            comboBoxHistory.Location = this.Location;
        }

        private void HistoryTextBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!comboBoxHistory.DroppedDown)
            {
                comboBoxHistory.SelectedIndex = comboBoxHistory.Items.IndexOf(this.Text);
            }
            ShowHistory(true);
        }

        private void comboBoxHistory_DropDownClosed(object sender, EventArgs e)
        {
            ShowHistory(false);
        }

        private void comboBoxHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxHistory.SelectedItem != null)
            {
                this.Text = comboBoxHistory.SelectedItem.ToString();
            }
        }

        public void SendKeyCode(KeyEventArgs keyCode)
        {
            HistoryTextBox_KeyDown(null, keyCode);
        }

        private void HistoryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            bool HistoryShown = false, handled = false;

            if (e.KeyCode == Keys.Down)
            {
                if (comboBoxHistory.Items.Count > 0)
                {
                    int index;
                    if (!comboBoxHistory.DroppedDown)
                    {
                        index = comboBoxHistory.Items.IndexOf(this.Text);
                    }
                    else
                    {
                        index = comboBoxHistory.SelectedIndex + 1;
                    }

                    if (index == comboBoxHistory.Items.Count)
                    {
                        index = 0;
                    }


                    string oldItem = this.Text;
                    comboBoxHistory.SelectedIndex = index;
                    this.Text = oldItem;
                }
                handled = true;
                HistoryShown = true;
            }

            if (e.KeyCode == Keys.Up)
            {
                if (comboBoxHistory.Items.Count > 0)
                {
                    int index = comboBoxHistory.SelectedIndex;
                    if (index < 1)
                    {
                        index = comboBoxHistory.Items.Count;
                    }
                    if (!comboBoxHistory.DroppedDown)
                    {
                        index = comboBoxHistory.Items.IndexOf(this.Text);
                    }
                    else
                    {
                        index--;
                    }
                    string oldItem = this.Text;
                    comboBoxHistory.SelectedIndex = index;
                    this.Text = oldItem;
                }
                handled = true;
                HistoryShown = true;
            }

            if (e.KeyCode == Keys.Delete)
            {
                if (comboBoxHistory.Items.Count > 0)
                {
                    object selectedString = comboBoxHistory.SelectedItem;
                    int index = Math.Min(comboBoxHistory.SelectedIndex, comboBoxHistory.Items.Count - 2);
                    if (selectedString != null && comboBoxHistory.DroppedDown)
                    {
                        RemoveTextFromHistory(selectedString.ToString());
                        comboBoxHistory.SelectedIndex = index;
                    }
                }
                handled = true;
                HistoryShown = true;
            }

            if (e.KeyCode == Keys.Enter && comboBoxHistory.DroppedDown && comboBoxHistory.Items.Count > 0)
            {
                if (comboBoxHistory.SelectedItem != null)
                {
                    this.Text = comboBoxHistory.SelectedItem.ToString();
                }
            }

            if (e.Control)
            {
                if (e.Shift)
                {
                    if (e.KeyCode == Keys.C)
                    {
                        CopyHistoryToClipboard();
                        this.DeselectAll();
                        handled = true;
                        HistoryShown = true;
                    }
                    else if (e.KeyCode == Keys.X)
                    {
                        CopyHistoryToClipboard();
                        ClearHistory();
                        this.DeselectAll();
                        handled = true;
                        HistoryShown = true;
                    }
                    else if (e.KeyCode == Keys.V)
                    {
                        CopyClipboardToHistory();
                        handled = true;
                        HistoryShown = true;
                    }
                    else if (e.KeyCode == Keys.Delete)
                    {
                        ClearHistory();
                        handled = true;
                        HistoryShown = true;
                    }
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    AddTextToHistory();
                    handled = true;
                    HistoryShown = true;
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    RemoveTextFromHistory();
                    handled = true;
                    HistoryShown = true;
                }
            }

            if ((e.Control || e.Shift) && comboBoxHistory.DroppedDown)
            {
                HistoryShown = true;
            }

            ShowHistory(HistoryShown);
            e.Handled = handled;
        }

        private void HistoryTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\n')
            {
                e.Handled = true;
            }
        }

        public void AddTextToHistory(string addMe)
        {
            if (!comboBoxHistory.Items.Contains(addMe))
            {
                comboBoxHistory.Items.Add(addMe);
            }

            if (comboBoxHistory.Items.Contains(string.Empty))
            {
                comboBoxHistory.Items.Remove(string.Empty);
            }
        }

        public void AddTextToHistory()
        {
            AddTextToHistory(this.Text);
        }

        public void RemoveTextFromHistory(string removeMe)
        {
            if (comboBoxHistory.Items.Contains(removeMe))
            {
                comboBoxHistory.Items.Remove(removeMe);
            }
        }

        public void RemoveTextFromHistory()
        {
            RemoveTextFromHistory(this.Text);
        }

        public void ClearHistory()
        {
            comboBoxHistory.Items.Clear();
        }

        public int GetHistoryCount()
        {
            return comboBoxHistory.Items.Count;
        }

        public string GetHistoryString(int index)
        {
            return comboBoxHistory.Items[index].ToString();
        }

        public void CopyStringToHistory(string newHistory)
        {
            string[] sep = newHistory.Split(new char[] { '\r', '\n' });
            foreach (string entry in sep)
            {
                AddTextToHistory(entry);
            }
        }

        public string GetStringFromHistory()
        {
            string result = string.Empty;

            foreach (object entry in comboBoxHistory.Items)
            {
                result += entry.ToString();
                if (entry != comboBoxHistory.Items[comboBoxHistory.Items.Count - 1])
                {
                    result += "\r\n";
                }
            }
            return result;
        }

        public void CopyHistoryToClipboard()
        {
            Clipboard.SetText(GetStringFromHistory());
        }

        public void CopyClipboardToHistory()
        {
            CopyStringToHistory(Clipboard.GetText());
        }

        public void ShowHistory(bool shown)
        {
            comboBoxHistory.Visible = shown;
            if (comboBoxHistory.Items.Count == 0)
            {
                comboBoxHistory.Items.Add(string.Empty);
            }
            comboBoxHistory.DroppedDown = shown;
            if (shown)
            {
                comboBoxHistory.BringToFront();
                BringToFront();
            }
            else
            {
                comboBoxHistory.SendToBack();
            }
        }

        private void HistoryTextBox_Leave(object sender, EventArgs e)
        {
            if (AutoHistory)
            {
                AddTextToHistory();
            }
        }

        private void HistoryTextBox_ContextMenuStripChanged(object sender, EventArgs e)
        {
            comboBoxHistory.ContextMenuStrip = this.ContextMenuStrip;
        }
    }
}
