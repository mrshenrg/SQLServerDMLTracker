using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SQLServerDMLTracker
{
    public partial class FormSQLServer : Form
    {
        public FormSQLServer()
        {
            InitializeComponent();
        }

        private void FormLoad_Load(object sender, EventArgs e)
        {
            if (this.comboBox2.SelectedText == "")
                this.comboBox2.SelectedIndex = this.comboBox2.FindString("Windows 身份验证");
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.comboBox2.SelectedIndex == this.comboBox2.FindString("Windows 身份验证"))
            {
                //this.comboBox3.Text = "";
                //this.textBox1.Text = "";
                this.comboBox3.Enabled = false;
                this.textBox1.Enabled = false;
                this.label3.Enabled = false;
                this.label4.Enabled = false;
            }
            else
            {
                this.comboBox3.Text = "sa";
                //this.textBox1.Text = "";
                this.comboBox3.Enabled = true;
                this.textBox1.Enabled = true;
                this.label3.Enabled = true;
                this.label4.Enabled = true;
                this.textBox1.Focus();
            }
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            this.button1.Enabled = this.comboBox1.Text != "";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            String connectionString = "";
            if (this.comboBox2.SelectedIndex == this.comboBox2.FindString("Windows 身份验证")) {
                connectionString = "Server=" + comboBox1.Text + ";Initial Catalog=master;Integrated Security=True";
            }
            else {
                connectionString = "Server=" + comboBox1.Text + ";Initial Catalog=master;User ID=" + comboBox3.Text + ";Password=" + textBox1.Text;
            }

            if (MSSQLHelper.ExecuteConnect(connectionString))
            {
                MSSQLHelper.UpdateConnectionString(connectionString);
                this.Hide();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}
