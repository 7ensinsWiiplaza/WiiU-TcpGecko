﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace GeckoApp.external
{
    public partial class AddressTextBox : TextBox
    {
        private Color colorAddressGood, colorAddressBad;

        [Browsable(true)]
        public bool AutoHistory { get; set; }

        [Browsable(true)]
        public bool EndingAddress { get; set; }

        [Browsable(true)]
        public bool MultiPokeAddress { get; set; }

        public AddressTextBox()
        {
            InitializeComponent();
            this.Width = 62;
            this.MaxLength = 8;
            CharacterCasing = CharacterCasing.Upper;
            this.Font = new Font("Courier New", (float)8.25);
            comboBoxHistory.Parent = this.Parent;
            comboBoxHistory.Location = this.Location;
            comboBoxHistory.Width = this.Width;
            comboBoxHistory.MaxLength = this.MaxLength;
            comboBoxHistory.Font = this.Font;

            colorAddressBad = Color.FromArgb(255, 200, 200);
            colorAddressGood = this.BackColor;
        }

        private void AddressTextBox_Layout(object sender, LayoutEventArgs e)
        {
            comboBoxHistory.Parent = this.Parent;
            comboBoxHistory.Location = this.Location;
            comboBoxHistory.Width = this.Width;
            comboBoxHistory.DropDownWidth = comboBoxHistory.Width + 15;
        }

        private void AddressTextBox_LocationChanged(object sender, EventArgs e)
        {
            comboBoxHistory.Location = this.Location;
        }

        private void AddressTextBox_MouseDoubleClick(object sender, MouseEventArgs e)
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
            AddressTextBox_KeyDown(null, keyCode);
        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
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
                        RemoveAddressFromHistory(selectedString.ToString());
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
                    AddAddressToHistory();
                    handled = true;
                    HistoryShown = true;
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    RemoveAddressFromHistory();
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

        private void AddressTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\n')
            {
                e.Handled = true;
            }
        }

        private void AddressTextBox_TextChanged(object sender, EventArgs e)
        {
            string text = this.Text;
            if (MultiPokeAddress)
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, "[^A-FMP0-9]", string.Empty);
            }
            else
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, "[^A-F0-9]", string.Empty);
            }
            this.Text = text;

            if ((MultiPokeAddress && this.Text.Equals("MP")) || IsValid())
            {
                this.BackColor = colorAddressGood;
            }
            else
            {
                this.BackColor = colorAddressBad;
            }
        }

        public void AddAddressToHistory(string addMe)
        {
            if (IsValid(addMe) && !comboBoxHistory.Items.Contains(addMe))
            {
                comboBoxHistory.Items.Add(addMe);
            }

            if (comboBoxHistory.Items.Contains(string.Empty))
            {
                comboBoxHistory.Items.Remove(string.Empty);
            }
        }

        public void AddAddressToHistory(uint address)
        {
            AddAddressToHistory(GlobalFunctions.toHex(address));
        }

        public void AddAddressToHistory()
        {
            AddAddressToHistory(this.Text);
        }

        public void RemoveAddressFromHistory(string removeMe)
        {
            if (comboBoxHistory.Items.Contains(removeMe))
            {
                comboBoxHistory.Items.Remove(removeMe);
            }
        }

        public void RemoveAddressFromHistory()
        {
            RemoveAddressFromHistory(this.Text);
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

        public uint GetHistoryuint(int index)
        {
            uint foo = 0x80000000;
            GlobalFunctions.tryToHex(comboBoxHistory.Items[index].ToString(), out foo);
            return foo;
        }

        public void CopyStringToHistory(string newHistory)
        {
            string[] sep = newHistory.Split(new char[] { '\r', '\n' });
            foreach (string entry in sep)
            {
                AddAddressToHistory(entry);
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

        public bool IsValidGet(string checkMe, bool showMessages, out uint value)
        {
            uint newValue;
            if (GlobalFunctions.tryToHex(checkMe, out newValue))
            {
                if (ValidMemory.validAddress(newValue))
                {
                    value = newValue;
                    return true;
                }
                else if (EndingAddress && ValidMemory.validAddress(newValue - 1))
                {
                    value = newValue;
                    return true;
                }
                else
                {
                    if (showMessages)
                    {
                        MessageBox.Show("Address is not a valid 32-bit hex string");
                    }
                }
            }
            else
            {
                if (showMessages)
                {
                    MessageBox.Show("Address is not in valid range of Wii memory");
                }
            }

            value = 0x80000000;
            return false;
        }

        public bool IsValidGet(bool showErrorMessages, out uint value)
        {
            return IsValidGet(this.Text, showErrorMessages, out value);
        }

        public bool IsValidGet(out uint value)
        {
            return IsValidGet(this.Text, false, out value);
        }

        public bool IsValid(string checkMe, bool showErrorMessages)
        {
            uint newValue;
            return IsValidGet(checkMe, showErrorMessages, out newValue);
        }

        public bool IsValid(string checkMe)
        {
            return IsValid(checkMe, false);
        }

        public bool IsValid()
        {
            return IsValid(this.Text);
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

        private void AddressTextBox_Leave(object sender, EventArgs e)
        {
            if (AutoHistory)
            {
                AddAddressToHistory();
            }
        }

        private void AddressTextBox_ContextMenuStripChanged(object sender, EventArgs e)
        {
            comboBoxHistory.ContextMenuStrip = this.ContextMenuStrip;
        }

        public void AddOffsetToAddress(string offset)
        {
            try
            {
                bool negative = false;
                if (offset.Contains("-"))
                {
                    offset = offset.Replace("-", string.Empty);
                    negative = true;
                }
                int casted = Convert.ToInt32(offset, 16);
                if (negative) casted *= -1;
                AddOffsetToAddress(casted);
            }
            catch (FormatException)
            {
            }
        }

        public void AddOffsetToAddress(int offset)
        {
            uint address;
            IsValidGet(out address);
            address = (uint)(address + offset);
            this.Text = string.Format("{0:X}", address);
        }
    }
}
