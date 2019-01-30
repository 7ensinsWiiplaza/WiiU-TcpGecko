using System;
using System.Windows.Forms;

namespace GeckoApp
{
    public partial class ValueInput : Form
    {
        private uint inputValue;

        public ValueInput()
        {
            InitializeComponent();
        }

        public bool ShowDialog(uint address, ref uint value, int maxLength)
        {
            this.InstLab.Text = "Poking address " + GlobalFunctions.toHex(address) + ":";
            this.PValue.Text = GlobalFunctions.toHex(value, maxLength);
            this.PValue.MaxLength = maxLength;
            bool result = (this.ShowDialog() == DialogResult.OK);
            if (result)
                value = inputValue;
            return result;
        }

        private void ValueInput_Shown(object sender, EventArgs e)
        {
            PValue.Focus();
        }

        private void CheckInput_Click(object sender, EventArgs e)
        {
            uint tryHex;
            if (GlobalFunctions.tryToHex(PValue.Text, out tryHex))
            {
                inputValue = tryHex;
                DialogResult = DialogResult.OK;
            }
            else
                MessageBox.Show("Invalid value!");
        }
    }
}
