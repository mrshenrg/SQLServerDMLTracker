using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using Microsoft.VisualBasic;

namespace SQLServerDMLTracker
{
    public partial class FormMain : Form
    {
        private String connStr = "";
        
        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMian_Load(object sender, EventArgs e)
        {
            connStr = MSSQLHelper.GetConnectionString();
            if (connStr != "")
            {
                if (MSSQLHelper.ExecuteConnect(connStr))
                {
                    InitToolStripComboBox1(connStr);
                }
                else
                {
                    connStr = "";
                    MSSQLHelper.UpdateConnectionString(connStr);
                }
            }
        }

        private void FormMian_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            resetConn();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            resetConn();
        }

        private void resetConn()
        {
            FormSQLServer frmSqlServer = new FormSQLServer();
            frmSqlServer.ShowDialog();

            if (connStr != MSSQLHelper.GetConnectionString())
            {
                connStr = MSSQLHelper.GetConnectionString();
                InitToolStripComboBox1(connStr);
            }
        }

        private void InitToolStripComboBox1(string connectionString)
        {
            this.toolStripComboBox1.Items.Clear();
            SqlDataReader dr = MSSQLHelper.ExecuteDataReader(connectionString, "Select * from sys.databases where state_desc = 'ONLINE' order by name");
            if (dr.HasRows)
            {
                while (dr.Read())
                {
                    this.toolStripComboBox1.Items.Add(dr[0].ToString());
                }
            }

            toolStripStatusLabel3.Text = "SQLServer服务器[" + MSSQLHelper.GetDataSource(connStr) + "]";
            this.toolStripComboBox1.SelectedIndex = this.toolStripComboBox1.FindString(MSSQLHelper.GetDataBaseString(connectionString).ToString());

            toolStripButton1.Enabled = false;
            toolStripButton2.Enabled = true;
            toolStripComboBox1.Enabled = true;
            toolStripButton3.Enabled = true;
            toolStripButton4.Enabled = true;
            toolStripButton5.Enabled = true;
            toolStripButton6.Enabled = true;
            toolStripButton7.Enabled = true;
            toolStripButton8.Enabled = true;
            toolStripButton9.Enabled = true;

            //dataGridView1.Rows.Clear();
            queryTraceTrig("", false);
            queryTraceLog("");
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            connStr = connStr.Replace(MSSQLHelper.GetDataBaseString(connStr).ToString(), toolStripComboBox1.Text.ToString());
            MSSQLHelper.UpdateConnectionString(connStr);

            queryTraceTrig("", false);
            queryTraceLog("");
            //button1.Focus();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            if (this.tabControl1.SelectedIndex == 0)
            {
                List<String> tblList = new List<string>();
                String tbls = "";
                tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
                if (tblList.Count == 0)
                {
                    tbls = Microsoft.VisualBasic.Interaction.InputBox("请输入跟踪表范围，例如：TableA,TableB,..\n不输入则跟踪全部表！", "创建跟踪", "", -1, -1);
                    if (tbls == "" && MessageBox.Show("是否全部新建或重建跟踪触发器？", "创建跟踪", MessageBoxButtons.OKCancel) != DialogResult.OK)
                        return;
                    else if (tbls != "")
                    {
                        String[] tblArr = tbls.Split(new char[] { ',' });
                        tblList = new List<string>(tblArr);
                    }
                    else
                        tblList = queryTraceTrigList(true);
                }

                if (tblList.Count == 0)
                {
                    return;
                }
                else
                {
                    toolStripStatusLabel1.Text = "正在创建跟踪...";
                    this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                    toolStripProgressBar1.Value = 0;
                    toolStripProgressBar1.Minimum = 0;
                    toolStripProgressBar1.Maximum = tblList.Count;
                    Application.DoEvents();
                    toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                    int count = 0;
                    tbls = "";

                    for (int i = 0; i < tblList.Count; i++)
                    {
                        toolStripProgressBar1.Value = i + 1;
                        count = count + 1;
                        toolStripStatusLabel2.Text = (i+1).ToString() + "/" + tblList.Count.ToString();
                        Application.DoEvents();
                        tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                        if (count == 100)
                        {
                            DMLTrace.InitTraceTrig(connStr, tbls);
                            count = 0;
                            tbls = "";
                        }
                    }

                    if (tbls!="")
                        DMLTrace.InitTraceTrig(connStr, tbls);

                    this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                    toolStripStatusLabel1.Text = "创建跟踪完成";
                }

                Application.DoEvents();
                tbls = (tblList.Count>0 && tblList.Count <= 100 ?String.Join(",", tblList.ToArray()):"");
                queryTraceTrigList(tbls, false);
            }
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
            if (tblList.Count == 0)
            {
                if (MessageBox.Show("是否删除全部跟踪信息(触发器、日志)？", "删除跟踪", MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
                else
                    tblList = queryTraceTrigList(false);
            }

            if (tblList.Count == 0)
            {
                return;
            }
            else
            {
                toolStripStatusLabel1.Text = "正在删除跟踪...";
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = tblList.Count;
                Application.DoEvents();
                toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                int count = 0;
                tbls = "";

                for (int i = 0; i < tblList.Count; i++)
                {
                    toolStripProgressBar1.Value = i + 1;
                    count = count + 1;
                    toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                    Application.DoEvents();
                    tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                    if (count == 100)
                    {
                        DMLTrace.DeleteTraceTrig(connStr, tbls, true);
                        count = 0;
                        tbls = "";
                    }
                }

                if (tbls != "")
                    DMLTrace.DeleteTraceTrig(connStr, tbls, true);

                this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                toolStripStatusLabel1.Text = "删除跟踪完成";
            }

            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls, true);
            queryTraceLog("");
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
            if (tblList.Count == 0)
            {
                if (MessageBox.Show("是否禁用全部跟踪触发器？", "禁用跟踪", MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
                else
                    tblList = queryTraceTrigList(false);
            }

            if (tblList.Count == 0)
            {
                return;
            }
            else
            {
                toolStripStatusLabel1.Text = "正在禁用跟踪...";
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = tblList.Count;
                Application.DoEvents();
                toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                int count = 0;
                tbls = "";

                for (int i = 0; i < tblList.Count; i++)
                {
                    toolStripProgressBar1.Value = i + 1;
                    count = count + 1;
                    toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                    Application.DoEvents();
                    tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                    if (count == 100)
                    {
                        DMLTrace.DisableTraceTrig(connStr, tbls);
                        count = 0;
                        tbls = "";
                    }
                }

                if (tbls != "")
                    DMLTrace.DisableTraceTrig(connStr, tbls);

                this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                toolStripStatusLabel1.Text = "禁用跟踪完成";
            }

            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls, true);
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
            if (tblList.Count == 0)
            {
                if (MessageBox.Show("是否启用全部跟踪触发器？", "启用跟踪", MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
                else
                    tblList = queryTraceTrigList(false);
            }

            if (tblList.Count == 0)
            {
                return;
            }
            else
            {
                toolStripStatusLabel1.Text = "正在启用跟踪...";
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = tblList.Count;
                Application.DoEvents();
                toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                int count = 0;
                tbls = "";

                for (int i = 0; i < tblList.Count; i++)
                {
                    toolStripProgressBar1.Value = i + 1;
                    count = count + 1;
                    toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                    Application.DoEvents();
                    tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                    if (count == 100)
                    {
                        DMLTrace.EnableTraceTrig(connStr, tbls);
                        count = 0;
                        tbls = "";
                    }
                }

                if (tbls != "")
                    DMLTrace.EnableTraceTrig(connStr, tbls);

                this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                toolStripStatusLabel1.Text = "启用跟踪完成";
            }

            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls, true);
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);

            if (tblList.Count == 0)
            {
                if (MessageBox.Show("是否清除所有日志？", "清除日志", MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
                else
                {
                    DMLTrace.TruncateTraceLog(connStr, "");
                    queryTraceTrigList("", true);
                    queryTraceLog("");
                    return;
                }
            }

            toolStripStatusLabel1.Text = "正在清除日志...";
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
            toolStripProgressBar1.Value = 0;
            toolStripProgressBar1.Minimum = 0;
            toolStripProgressBar1.Maximum = tblList.Count;
            Application.DoEvents();
            toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
            int count = 0;
            tbls = "";

            for (int i = 0; i < tblList.Count; i++)
            {
                toolStripProgressBar1.Value = i + 1;
                count = count + 1;
                toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                Application.DoEvents();
                tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                if (count == 100)
                {
                    DMLTrace.TruncateTraceLog(connStr, tbls);
                    count = 0;
                    tbls = "";
                }
            }

            if (tbls != "")
                DMLTrace.TruncateTraceLog(connStr, tbls);

            this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
            toolStripStatusLabel1.Text = "清除日志完成";
            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls, true);
            queryTraceLog("");
        }

        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
            if (tblList.Count == 0)
            {
                MessageBox.Show("请选择需要启用跟踪记录的表？", "启用跟踪记录", MessageBoxButtons.OK);
                return;
            }

            if (tblList.Count == 0)
            {
                return;
            }
            else
            {
                toolStripStatusLabel1.Text = "正在启用跟踪记录...";
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = tblList.Count;
                Application.DoEvents();
                toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                int count = 0;
                tbls = "";

                for (int i = 0; i < tblList.Count; i++)
                {
                    toolStripProgressBar1.Value = i + 1;
                    count = count + 1;
                    toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                    Application.DoEvents();
                    tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                    if (count == 100)
                    {
                        DMLTrace.EnableTraceTrigRecord(connStr, tbls);
                        count = 0;
                        tbls = "";
                    }
                }

                if (tbls != "")
                    DMLTrace.EnableTraceTrigRecord(connStr, tbls);

                this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                toolStripStatusLabel1.Text = "启用跟踪记录完成";
            }

            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls,true);
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            List<String> tblList = new List<string>();
            String tbls = "";
            tblList = getSelectedTraceTrig(this.tabControl1.SelectedIndex);
            if (tblList.Count == 0)
            {
                if (MessageBox.Show("是否禁用全部跟踪记录？", "禁用跟踪记录", MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
                else
                    tblList = queryTraceTrigList(false);
            }

            if (tblList.Count == 0)
            {
                return;
            }
            else
            {
                toolStripStatusLabel1.Text = "正在禁用跟踪记录...";
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;//鼠标为忙碌状态
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = tblList.Count;
                Application.DoEvents();
                toolStripStatusLabel2.Text = "0/" + tblList.Count.ToString();
                int count = 0;
                tbls = "";

                for (int i = 0; i < tblList.Count; i++)
                {
                    toolStripProgressBar1.Value = i + 1;
                    count = count + 1;
                    toolStripStatusLabel2.Text = (i + 1).ToString() + "/" + tblList.Count.ToString();
                    Application.DoEvents();
                    tbls = tbls + (tbls == "" ? "" : ",") + tblList[i].ToString();

                    if (count == 100)
                    {
                        DMLTrace.DisableTraceTrigRecord(connStr, tbls);
                        count = 0;
                        tbls = "";
                    }
                }

                if (tbls != "")
                    DMLTrace.DisableTraceTrigRecord(connStr, tbls);

                this.Cursor = System.Windows.Forms.Cursors.Arrow;//设置鼠标为正常状态
                toolStripStatusLabel1.Text = "禁用跟踪记录完成";
            }

            Application.DoEvents();
            tbls = (tblList.Count > 0 && tblList.Count <= 100 ? String.Join(",", tblList.ToArray()) : "");
            queryTraceTrigList(tbls, true);
        }

        private void toolStripButton10_Click(object sender, EventArgs e)
        {
            int findStartRow = 0;
            if (toolStripTextBox1.Text == "")
                return;

            if (this.tabControl1.SelectedIndex == 0)
            {
                findStartRow = dataGridView1.CurrentRow.Index;
                REFIND0:
                for (int i = findStartRow; i < this.dataGridView1.Rows.Count; i++)
                {
                    for (int j = 1; j < 3; j++)
                    {
                        if (dataGridView1.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                        {
                            dataGridView1.CurrentCell = dataGridView1[j, i];
                            if (MessageBox.Show("已查找到数据,是否继续？", "定位", MessageBoxButtons.OKCancel) != DialogResult.OK)
                                return;
                        }
                    }
                }
                if (MessageBox.Show("已查找到表尾,是否从头查找!", "定位", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    findStartRow = 0;
                    goto REFIND0;
                }
            }
            else if (this.tabControl1.SelectedIndex == 1)
            {
                findStartRow = dataGridView2.CurrentRow.Index;
                REFIND1:
                for (int i = findStartRow; i < this.dataGridView2.Rows.Count; i++)
                {
                    for (int j = 1; j < this.dataGridView2.Columns.Count; j++)
                    {
                        if (dataGridView2.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                        {
                            dataGridView2.CurrentCell = dataGridView2[j, i];
                            if (MessageBox.Show("已查找到数据,是否继续？", "定位", MessageBoxButtons.OKCancel) != DialogResult.OK)
                                return;
                        }
                    }
                }
                if (MessageBox.Show("已查找到表尾,是否从头查找!", "定位", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    findStartRow = 0;
                    goto REFIND1;
                }
            }
            else if (this.tabControl1.SelectedIndex == 2)
            {
                findStartRow = dataGridView3.CurrentRow.Index;
                REFIND2:
                for (int i = findStartRow; i < this.dataGridView3.Rows.Count; i++)
                {
                    for (int j = 0; j < this.dataGridView3.Columns.Count; j++)
                    {
                        if (this.dataGridView3.Rows[i].Cells[j].Value != null)
                        {
                            if (dataGridView3.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                            {
                                dataGridView3.CurrentCell = dataGridView3[j, i];
                                if (MessageBox.Show("已查找到数据,是否继续？", "定位", MessageBoxButtons.OKCancel) != DialogResult.OK)
                                    return;
                            }
                        }
                    }
                }
                if (MessageBox.Show("已查找到表尾,是否从头查找!", "定位", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    findStartRow = 0;
                    goto REFIND2;
                }
            }
        }

        private void toolStripButton12_Click(object sender, EventArgs e)
        {
            int findNum = 0;
            if (toolStripTextBox1.Text == "")
                return;

            if (this.tabControl1.SelectedIndex == 0)
            {
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    for (int j = 1; j < 3; j++)
                    {
                        if (dataGridView1.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                        {
                            findNum = findNum + 1;
                            this.dataGridView1.Rows[i].Cells[j].Style.BackColor =  Color.Red;
                        }
                        else
                            this.dataGridView1.Rows[i].Cells[j].Style.BackColor =  Color.White;
                    }
                }
                MessageBox.Show("标记完成，共查找到[" + findNum.ToString() + "]处!", "标记", MessageBoxButtons.OK);
            }
            else if (this.tabControl1.SelectedIndex == 1)
            {
                for (int i = 0; i < this.dataGridView2.Rows.Count; i++)
                {
                    for (int j = 1; j < this.dataGridView2.Columns.Count; j++)
                    {
                        if (dataGridView2.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                        {
                            findNum = findNum + 1;
                            this.dataGridView2.Rows[i].Cells[j].Style.BackColor = Color.Red;
                        }
                        else
                            this.dataGridView2.Rows[i].Cells[j].Style.BackColor = Color.White;
                    }
                }
                MessageBox.Show("标记完成，共查找到[" + findNum.ToString() + "]处!", "标记", MessageBoxButtons.OK);
            }
            else if (this.tabControl1.SelectedIndex == 2)
            {
                for (int i = 0; i < this.dataGridView3.Rows.Count; i++)
                {
                    for (int j = 1; j < this.dataGridView3.Columns.Count; j++)
                    {
                        if (dataGridView3.Rows[i].Cells[j].Value.ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                        {
                            findNum = findNum + 1;
                            this.dataGridView3.Rows[i].Cells[j].Style.BackColor = Color.Red;
                        }
                        else
                            this.dataGridView3.Rows[i].Cells[j].Style.BackColor = Color.White;
                    }
                }
                MessageBox.Show("标记完成，共查找到[" + findNum.ToString() + "]处!", "标记", MessageBoxButtons.OK);
            }
        }

        private void toolStripButton11_Click(object sender, EventArgs e)
        {
            List<int> rowList = new List<int>();
            if (this.tabControl1.SelectedIndex == 0)
            {
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells[0].Value) == true)
                    {
                        rowList.Add(i);
                    }
                }
                if (rowList.Count < 2 || rowList.Count>10)
                    MessageBox.Show("请至少选择2行且最多10行对比!", "对比", MessageBoxButtons.OK);
                else
                {
                    for (int j = 1; j < this.dataGridView1.Columns.Count; j++)
                    {
                        bool b = false;
                        string s = "";
                        for (int i = 0; i < rowList.Count; i++)
                        {
                            if (i == 0)
                                s = dataGridView1.Rows[rowList[i]].Cells[j].Value.ToString();
                            else if (s != dataGridView1.Rows[rowList[i]].Cells[j].Value.ToString())
                            {
                                b = true;
                                break;
                            }
                        }
                        for (int i = 0; i < rowList.Count; i++)
                        {
                            this.dataGridView1.Rows[rowList[i]].Cells[j].Style.BackColor = (b == true ? Color.Red : Color.White);
                        }
                    }
                }
            } 
            else if (this.tabControl1.SelectedIndex == 1)
            {
                for (int i = 0; i < this.dataGridView2.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView2.Rows[i].Cells[0].Value) == true)
                    {
                        rowList.Add(i);
                    }
                }
                if (rowList.Count < 2 || rowList.Count > 10)
                    MessageBox.Show("请至少选择2行且最多10行对比!", "对比", MessageBoxButtons.OK);
                else
                {
                    for (int j = 1; j < this.dataGridView2.Columns.Count; j++)
                    {
                        bool b = false;
                        string s = "";
                        for (int i = 0; i < rowList.Count; i++)
                        {
                            if (i == 0)
                                s = dataGridView2.Rows[rowList[i]].Cells[j].Value.ToString();
                            else if (s != dataGridView2.Rows[rowList[i]].Cells[j].Value.ToString())
                            {
                                b = true;
                                break;
                            }
                        }
                        for (int i = 0; i < rowList.Count; i++)
                        {
                            this.dataGridView2.Rows[rowList[i]].Cells[j].Style.BackColor = (b == true ? Color.Red : Color.White);
                        }
                    }
                }
            }
            else if (this.tabControl1.SelectedIndex == 2)
            {
                for (int i = 0; i < this.dataGridView3.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView3.Rows[i].Cells[0].Value) == true)
                    {
                        rowList.Add(i);
                    }
                }
                if (rowList.Count < 2 || rowList.Count > 10)
                    MessageBox.Show("请至少选择2行且最多10行对比!", "对比", MessageBoxButtons.OK);
                else
                {
                    for (int j = 1; j < this.dataGridView3.Columns.Count; j++)
                    {
                        bool b = false;
                        string s = "";
                        for (int i = 0; i < rowList.Count; i++)
                        {
                            if (i == 0)
                                s = dataGridView3.Rows[rowList[i]].Cells[j].Value.ToString();
                            else if (s != dataGridView3.Rows[rowList[i]].Cells[j].Value.ToString())
                            {
                                b = true;
                                break;
                            }
                        }

                        for (int i = 0; i < rowList.Count; i++)
                        {
                            this.dataGridView3.Rows[rowList[i]].Cells[j].Style.BackColor = (b == true ? Color.Red : Color.White);
                        }
                    }
                }
            }
        }

        private void toolStripSelectAll_Click(object sender, EventArgs e)
        {
            dataGridViewSelect(this.tabControl1.SelectedIndex, 1);
        }

        private void toolStripNoSelectAll_Click(object sender, EventArgs e)
        {
            dataGridViewSelect(this.tabControl1.SelectedIndex, 0);
        }

        private void toolStripUnSelectALL_Click(object sender, EventArgs e)
        {
            dataGridViewSelect(this.tabControl1.SelectedIndex, 2);
        }

        private void dataGridViewSelect(int index, int selectType)
        {
            if (index == 0)
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    switch (selectType)
                    {
                        case 0:
                            this.dataGridView1.Rows[i].Cells["选择"].Value = 0;
                            break;
                        case 1:
                            this.dataGridView1.Rows[i].Cells["选择"].Value = 1;
                            break;
                        case 2:
                            if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells[0].Value) == true)
                                this.dataGridView1.Rows[i].Cells["选择"].Value = 0;
                            else
                                this.dataGridView1.Rows[i].Cells["选择"].Value = 1;
                            break;
                    }
                }
            else if (index == 1)
                for (int i = 0; i < this.dataGridView2.Rows.Count; i++)
                {
                    switch (selectType)
                    {
                        case 0:
                            this.dataGridView2.Rows[i].Cells["选择1"].Value = 0;
                            break;
                        case 1:
                            this.dataGridView2.Rows[i].Cells["选择1"].Value = 1;
                            break;
                        case 2:
                            if (Convert.ToBoolean(this.dataGridView2.Rows[i].Cells[0].Value) == true)
                                this.dataGridView2.Rows[i].Cells["选择1"].Value = 0;
                            else
                                this.dataGridView2.Rows[i].Cells["选择1"].Value = 1;
                            break;
                    }
                }
            else if (index == 2)
                for (int i = 0; i < this.dataGridView3.Rows.Count; i++)
                {
                    switch (selectType)
                    {
                        case 0:
                            this.dataGridView3.Rows[i].Cells["选择2"].Value = 0;
                            break;
                        case 1:
                            this.dataGridView3.Rows[i].Cells["选择2"].Value = 1;
                            break;
                        case 2:
                            if (Convert.ToBoolean(this.dataGridView3.Rows[i].Cells[0].Value) == true)
                                this.dataGridView3.Rows[i].Cells["选择2"].Value = 0;
                            else
                                this.dataGridView3.Rows[i].Cells["选择2"].Value = 1;
                            break;
                    }
                }
        }

        private List<String> getSelectedTraceTrig(int index)
        {
            List<String> tblList = new List<string>();
            String tbl = "";
            if (index == 0)
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells[0].Value) == true)
                    {
                        tbl = this.dataGridView1.Rows[i].Cells[1].Value.ToString();
                        if (! tblList.Exists(t => t == tbl))
                        {
                            tblList.Add(tbl);
                        }
                    }
                }
            else if (index == 1)
                for (int i = 0; i < this.dataGridView2.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView2.Rows[i].Cells[0].Value) == true)
                    {
                        tbl = this.dataGridView2.Rows[i].Cells[1].Value.ToString();
                        if (! tblList.Exists(t => t == tbl))
                        {
                            tblList.Add(tbl);
                        }
                    }
                }

            return tblList;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            queryTraceTrig(this.textBox1.Text, this.checkBox1.Checked);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            queryTraceTrig("", this.checkBox1.Checked);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            queryTraceLog(this.textBox2.Text);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            queryTraceLog("");
        }

        private List<String> queryTraceTrigList(bool bAllTbl)
        {
            List<String> tblList = new List<string>();
            SqlDataReader dr = DMLTrace.QueryTraceTrigList(connStr, "", bAllTbl);

            DataTable table = new DataTable();
            table.Load(dr);
            //dataGridView1.DataSource = table;
            for (int i = 0; i < table.Rows.Count; i++) // 遍历行
            {
                tblList.Add(table.Rows[i][0].ToString());
            }

            return tblList;
        }

        private void queryTraceTrigList(string tblList, bool bAllTbl)
        {
            SqlDataReader dr = DMLTrace.QueryTraceTrigList(connStr, tblList, bAllTbl);

            DataTable table = new DataTable();
            table.Load(dr);
            dataGridView1.DataSource = table;
        }

        private void queryTraceTrig(string tbl, bool bAllTbl)
        {
            SqlDataReader dr = DMLTrace.QueryTraceTrig(connStr, tbl, bAllTbl);

            DataTable table = new DataTable();
            table.Load(dr);
            dataGridView1.DataSource = table;
        }

        private void queryTraceLog(string tbl)
        {
            queryTraceLog(tbl,true);
        }

        private void queryTraceLog(string tbl, bool bUseLike)
        {
            string dtEnd = (this.checkEndDateTime.Checked?this.dateTimeEnd.Value.ToString():"");
            SqlDataReader dr = DMLTrace.QueryTraceLog(connStr, tbl, bUseLike, this.checkBox2.Checked, dtEnd, this.numericUpDown1.Value.ToString());

            DataTable table = new DataTable();
            table.Load(dr);
            dataGridView2.DataSource = table;
        }

        private void dataGridView1_DoubleClick(object sender, EventArgs e)
        {
            this.textBox2.Text = this.dataGridView1.Rows[this.dataGridView1.CurrentRow.Index].Cells[1].Value.ToString();
            this.tabControl1.SelectedIndex = 1;
            queryTraceLog(this.textBox2.Text,false);
        }

        private void dataGridView2_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                String logTbl = this.dataGridView2.Rows[this.dataGridView2.CurrentRow.Index].Cells[1].Value.ToString();
                String logGuid = this.dataGridView2.Rows[this.dataGridView2.CurrentRow.Index].Cells[10].Value.ToString();
                if (logGuid != "")
                {
                    this.tabControl1.SelectedIndex = 2;
                    this.textBox3.Text = this.dataGridView2.Rows[this.dataGridView2.CurrentRow.Index].Cells[1].Value.ToString(); ;
                    this.richTextBox2.Text = this.dataGridView2.Rows[this.dataGridView2.CurrentRow.Index].Cells[2].Value.ToString();
                    SqlDataReader dr_d = DMLTrace.QueryTraceRecordLog(connStr, logTbl, logGuid);
                    DataTable table_d = new DataTable();
                    table_d.Load(dr_d);
                    dataGridView3.DataSource = table_d;
                }
            }
            catch
            {
                this.textBox3.Text = "";
                this.richTextBox2.Text = "";
                DataTable table_d = new DataTable();
                dataGridView3.DataSource = table_d;
            }
        }

        private void dataGridView2_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                this.richTextBox1.Text = this.dataGridView2.Rows[this.dataGridView2.CurrentRow.Index].Cells[2].Value.ToString();
            }
            catch
            {
            }
        }

        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            Rectangle rect = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y,
                dataGridView1.RowHeadersWidth - 4, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dataGridView1.RowHeadersDefaultCellStyle.Font, rect,
                dataGridView1.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void dataGridView2_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            Rectangle rect = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y,
                dataGridView2.RowHeadersWidth - 4, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dataGridView2.RowHeadersDefaultCellStyle.Font, rect,
                dataGridView2.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void dataGridView3_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            Rectangle rect = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y,
                dataGridView3.RowHeadersWidth - 4, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dataGridView3.RowHeadersDefaultCellStyle.Font, rect,
                dataGridView3.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }
}
