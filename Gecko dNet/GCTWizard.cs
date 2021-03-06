﻿using System;
using System.Windows.Forms;

namespace GeckoApp
{
    public partial class GCTWizard : Form
    {
        private object[] RAMWriteCollection;
        private object[] IfThenCollection;
        private int indexToSelect;

        private CodeController GCTCodeContents;

        public int SelectedCodeNameIndex
        {
            get
            {
                return Math.Min(Math.Max(comboBoxCodeName.SelectedIndex, 0), GCTCodeContents.Count);
            }
            set
            {
                if (value <= GCTCodeContents.Count && value >= 0)
                    comboBoxCodeName.SelectedIndex = value;
            }
        }

        public GCTWizard(CodeController codeController)
        {
            InitializeComponent();
            RAMWriteCollection = new string[] {
                "Write",
                "Fill" };
            IfThenCollection = new string[] {
                "equal",
                "not equal",
                "greater",
                "lesser" };
            GCTCodeContents = codeController;
        }

        private void comboBoxCodeType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBoxCodeType.SelectedIndex)
            {
                case 0:
                    comboBoxCodeSubType.Items.Clear();
                    comboBoxCodeSubType.Items.AddRange(RAMWriteCollection);
                    break;
                case 1:
                    comboBoxCodeSubType.Items.Clear();
                    comboBoxCodeSubType.Items.AddRange(IfThenCollection);
                    break;
                default:
                    comboBoxCodeSubType.Items.Clear();
                    comboBoxCodeSubType.Items.AddRange(RAMWriteCollection);
                    break;
            }
            comboBoxCodeSubType.SelectedIndex = 0;
        }

        public void PrepareGCTWizard(int selectedCodeIndex)
        {
            comboBoxCodeType.SelectedIndex = 0;

            comboBoxCodeName.Items.Clear();
            for (int i = 0; i < GCTCodeContents.Count; i++)
            {
                comboBoxCodeName.Items.Add(GCTCodeContents.GetCodeName(i));
            }
            comboBoxCodeName.Items.Add("New Code");
            indexToSelect = selectedCodeIndex;
        }

        private void GCTWizard_Shown(object sender, EventArgs e)
        {
            SelectedCodeNameIndex = indexToSelect;
        }

        private void comboBoxCodeName_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = SelectedCodeNameIndex;
            if (comboBoxCodeName.SelectedIndex != index)
            {
                comboBoxCodeName.SelectedIndex = index;
            }

            if (comboBoxCodeName.SelectedIndex == comboBoxCodeName.Items.Count - 1)
            {
                textBoxCodeEntries.Text = string.Empty;
            }
            else
            {
                textBoxCodeEntries.Text = CodeController.CodeContentToCodeTextBox(GCTCodeContents[comboBoxCodeName.SelectedIndex]);
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Hide();
        }

        private bool ValidUserAddress(out uint address)
        {
            if (!GlobalFunctions.tryToHex(textBoxAddress.Text, out address) ||
                !ValidMemory.validAddress(address))
            {
                MessageBox.Show("Invalid address");
                textBoxAddress.Focus();
                textBoxAddress.SelectAll();
                return false;
            }

            if (radioButton16Bit.Checked && ((address & 1) != 0))
            {
                MessageBox.Show("address must be multiple of 2");
                textBoxAddress.Focus();
                textBoxAddress.SelectAll();
                return false;
            }
            else if (radioButton32Bit.Checked && ((address & 3) != 0))
            {
                MessageBox.Show("address must be multiple of 4");
                textBoxAddress.Focus();
                textBoxAddress.SelectAll();
                return false;
            }

            return true;
        }

        private bool ValidUserValue(out uint value)
        {
            if (!GlobalFunctions.tryToHex(textBoxValue.Text, out value))
            {
                MessageBox.Show("Invalid value");
                textBoxValue.Focus();
                textBoxValue.SelectAll();
                return false;
            }

            if (radioButton16Bit.Checked && value > 0xFFFF)
            {
                MessageBox.Show("value must be <= FFFF");
                textBoxValue.Focus();
                textBoxValue.SelectAll();
                return false;
            }
            else if (radioButton8Bit.Checked && value > 0xFF)
            {
                MessageBox.Show("value must be <= FF");
                textBoxValue.Focus();
                textBoxValue.SelectAll();
                return false;
            }

            return true;
        }

        private bool ValidUserMask(out uint mask)
        {
            if (radioButton32Bit.Checked)
            {
                mask = 0;
                return true;
            }

            if (!GlobalFunctions.tryToHex(textBoxMask.Text, out mask))
            {
                MessageBox.Show("Invalid mask");
                textBoxMask.Focus();
                textBoxMask.SelectAll();
                return false;
            }

            if (mask > 0xFFFF)
            {
                MessageBox.Show("mask must be <= FFFF");
                textBoxMask.Focus();
                textBoxMask.SelectAll();
                return false;
            }

            return true;
        }

        private bool ValidUserFill(out uint fill)
        {
            if (radioButton32Bit.Checked)
            {
                fill = 0;
                return true;
            }

            if (comboBoxCodeSubType.SelectedIndex == 0)
            {
                fill = 0;
                return true;
            }

            if (!GlobalFunctions.tryToHex(textBoxFill.Text, out fill))
            {
                MessageBox.Show("Invalid fill");
                textBoxFill.Focus();
                textBoxFill.SelectAll();
                return false;
            }

            if (fill > 0xFFFF)
            {
                MessageBox.Show("fill must be <= FFFF");
                textBoxFill.Focus();
                textBoxFill.SelectAll();
                return false;
            }

            return true;
        }

        private void AddCodeRAMWrite()
        {
            uint address, value, fill;

            bool addFill = comboBoxCodeSubType.SelectedIndex == 1;

            if (!ValidUserAddress(out address)) return;

            if (!ValidUserValue(out value)) return;

            if (!ValidUserFill(out fill)) return;

            uint add;

            if (radioButton8Bit.Checked)
            {
                value |= (fill << 16);

                add = 0x00000000;
            }
            else if (radioButton16Bit.Checked)
            {
                value |= (fill << 16);

                add = 0x02000000;
            }
            else
            {
                add = 0x04000000;
            }

            StandardCodeAddressStuff(address, value, add);
        }

        private void AddCodeIfThen()
        {
            uint address, value, mask;

            if (!ValidUserAddress(out address)) return;

            if (!ValidUserValue(out value)) return;

            if (!ValidUserMask(out mask)) return;

            uint add;

            if (radioButton8Bit.Checked)
            {
                MessageBox.Show("Can't do 8-bit if");
                return;
            }
            else if (radioButton16Bit.Checked)
            {
                add = 0x28000000;
                value = (mask << 16) | value;
            }
            else
            {
                add = 0x20000000;
            }

            if (comboBoxCodeSubType.SelectedIndex == 0)
            {
                add += 0;
            }
            else if (comboBoxCodeSubType.SelectedIndex == 1)
            {
                add += 0x02000000;
            }
            else if (comboBoxCodeSubType.SelectedIndex == 2)
            {
                add += 0x04000000;
            }
            else if (comboBoxCodeSubType.SelectedIndex == 3)
            {
                add += 0x06000000;
            }

            if (checkBoxEndIf.Checked)
            {
                add += 0x00000001;
            }

            StandardCodeAddressStuff(address, value, add);
        }

        private void StandardCodeAddressStuff(uint address, uint value, uint add)
        {
            CodeContent nCode = CodeController.CodeTextBoxToCodeContent(textBoxCodeEntries.Text);
            uint rAddressR;
            uint offset;

            if (radioButtonBA.Checked)
            {
                rAddressR = address & 0xFE000000;
            }
            else
            {
                rAddressR = address & 0xFE000000;
                add += 0x10000000;
            }

            bool changeBAorPO = false;
            if ((address & 0xFE000000) != 0x80000000)
            {
                changeBAorPO = true;
            }

            if (changeBAorPO)
            {
                if (radioButtonBA.Checked)
                {
                    nCode.addLine(0x42000000, rAddressR);
                }
                else
                {
                    nCode.addLine(0x4A000000, rAddressR);
                }
            }

            offset = address - rAddressR + add;
            nCode.addLine(offset, value);

            if (changeBAorPO)
            {
                nCode.addLine(0xE0000000, 0x80008000);
            }

            textBoxCodeEntries.Text = CodeController.CodeContentToCodeTextBox(nCode);
        }

        private void buttonAddCode_Click(object sender, EventArgs e)
        {
            switch (comboBoxCodeType.SelectedIndex)
            {
                case 0: AddCodeRAMWrite(); break;
                case 1: AddCodeIfThen(); break;
                default: AddCodeRAMWrite(); break;
            }
        }

        private void buttonStoreCode_Click(object sender, EventArgs e)
        {
            CheckNewCodeName();
            GCTCodeContents.UpdateCode(SelectedCodeNameIndex, textBoxCodeEntries.Text);
        }

        private void comboBoxCodeName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CheckNewCodeName();
            }
        }


        private void CheckNewCodeName()
        {
            if (comboBoxCodeName.FindStringExact(comboBoxCodeName.Text) == -1)
            {
                GCTCodeContents.AddCode(comboBoxCodeName.Text);
                comboBoxCodeName.Items.Remove("New Code");
                comboBoxCodeName.Items.Add(comboBoxCodeName.Text);
                string codeText = textBoxCodeEntries.Text;
                comboBoxCodeName.SelectedIndex = comboBoxCodeName.Items.Count - 1;
                comboBoxCodeName.Items.Add("New Code");
                textBoxCodeEntries.Text = codeText;
            }
        }

        private void buttonAddStoreClose_Click(object sender, EventArgs e)
        {
            buttonAddCode_Click(sender, e);
            buttonStoreCode_Click(sender, e);
            DialogResult = DialogResult.OK;
            Hide();
        }

    }
}
