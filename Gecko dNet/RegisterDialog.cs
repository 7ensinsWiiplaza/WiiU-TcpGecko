using System;
using System.Windows.Forms;

namespace GeckoApp
{
    public partial class RegisterDialog : Form
    {
        private uint setValue;

        public RegisterDialog()
        {
            InitializeComponent();
        }

        private void RegisterDialog_Load(object sender, EventArgs e)
        {

        }

        public bool SetRegister(string name, ref uint value)
        {
            InstLab.Text = "You are about to change the value stored in the register " + name +
                           ". Please type in the new value and click OK to set it or Cancel to abort.";
            RegVal.Text = "Value of register " + name + ":";
            RValue.Text = GlobalFunctions.toHex(value);
            setValue = value;

            if (this.ShowDialog() == DialogResult.OK)
            {
                value = setValue;
                return true;
            }

            return false;
        }

        private void CheckInput_Click(object sender, EventArgs e)
        {
            uint tryHex;
            if (GlobalFunctions.tryToHex(RValue.Text, out tryHex))
            {
                setValue = tryHex;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
                MessageBox.Show("Invalid value!");
        }

        private void RegisterDialog_Shown(object sender, EventArgs e)
        {
            RValue.Focus();
        }
    }
}
