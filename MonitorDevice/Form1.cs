using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace MonitorDevice
{
    public partial class Form1 : Form
    {
        bool m_IsMonitor = false;
        Graphics m_DrawWavGraphic = null;
        float m_Threadshold = 0;
        int m_HalfHeight = 0;
        MkAudio m_Audio = null;
        MkDBHandler m_DBHandler = null;
        Queue<string> m_DataQueue = new Queue<string>();
        Thread m_SaveDataThread = null;
        string m_ST_NO = "";
        string m_U_ID = "";
        string m_Date_Format = "yyyy_MM_dd";
        string m_Time_Format = "HH:mm:ss";
        DataTable m_dtLocation = null;
        DataTable m_dtDataAnalysis = null;
        DataTable m_dtSelect = new DataTable();
        int m_AnalysisStart = -1;
        int m_AnalysisStop = -1;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Initial();
        }

        private void Initial()
        {
            m_Audio = new MkAudio();
            m_DrawWavGraphic = PN_DRAW_WAV.CreateGraphics();
            ShowThreadshold();
            m_HalfHeight = PN_DRAW_WAV.Height / 2;
            try
            {
                m_Audio.DataAvailable = DataAvailable;
                m_Audio.StartAudioIn();
            }
            catch (NAudio.MmException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }

            m_DBHandler = new MkDBHandler();
            if (!m_DBHandler.Open(@"database\database.db"))
            {
                MessageBox.Show("Create DB Fail.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
            CreateTable();

            RefreshUserList();
            RefreshLocationList();

            CB_USER.DisplayMember = "Show";
            CB_LOCATION.DisplayMember = "Show";
            CB_DATA_USER.DisplayMember = "Show";
            CB_DATA_LOCATION.DisplayMember = "Show";
            DGV_LOACTION.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            DGV_DATA_RECORDLIST.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            m_dtSelect.Columns.Add("TriggerTime", typeof(String));
            m_dtSelect.Columns.Add("TimeSpan(ms)", typeof(int));

            m_SaveDataThread = new Thread(new ThreadStart(SaveData));
            m_SaveDataThread.IsBackground = true;
            m_SaveDataThread.Start();
        }

        public void CreateTable()
        {
            if (!m_DBHandler.IsTableExist("USER"))
            {
                m_DBHandler.Excute("CREATE TABLE USER(U_ID INTEGER, U_NAME TEXT, ENABLE INT)");
            }

            if (!m_DBHandler.IsTableExist("LOCATION"))
            {
                m_DBHandler.Excute("CREATE TABLE LOCATION(ST_NO TEXT,ADDR_C TEXT, TRI_NO INTEGER, TM_X REAL, TM_Y REAL, OBS_SRC INTEGER)");
            }

            if (!m_DBHandler.IsTableExist("RECORD"))
            {
                m_DBHandler.Excute("CREATE TABLE RECORD(REC_ID INTEGER, U_ID INTEGER, ST_NO TEXT, TRIGGER_TIME TEXT, REC_TIME TEXT)");
            }
        }

        private List<Point> GetDrawPoints(double[] array)
        {
            List<Point> result = new List<Point>();
            for (int i = 0; i < array.Length; i++)
            {
                result.Add(new Point(i, m_HalfHeight + Convert.ToInt32(array[i] * (m_HalfHeight))));
            }
            return result;
        }

        private void DataAvailable(double[] samples)
        {
            double max = samples.Max();
            //Debug.WriteLine(string.Format("max:{0},min:{1}", max, min));

            List<double> sampleCapture = new List<double>();
            int step = (samples.Length / PN_DRAW_WAV.Width);
            for (int i = 0; i < samples.Length; i += step)
            {
                sampleCapture.Add(samples[i]);
            }

            //List<Point> drawPoints = GetDrawPoints(samples);
            List<Point> drawPoints = GetDrawPoints(sampleCapture.ToArray());
            try
            {
                m_DrawWavGraphic.Clear(SystemColors.Control);
                m_DrawWavGraphic.DrawLines(new Pen(new SolidBrush(Color.Black), 1), drawPoints.ToArray());

                m_DrawWavGraphic.DrawLine(new Pen(new SolidBrush(Color.Red), 1), new Point(0, Convert.ToInt32(m_HalfHeight + m_Threadshold * m_HalfHeight)), new Point(drawPoints.Count, Convert.ToInt32(m_HalfHeight + m_Threadshold * m_HalfHeight)));
                m_DrawWavGraphic.DrawLine(new Pen(new SolidBrush(Color.Red), 1), new Point(0, Convert.ToInt32(m_HalfHeight + (-m_Threadshold) * m_HalfHeight)), new Point(drawPoints.Count, Convert.ToInt32(m_HalfHeight + (-m_Threadshold) * m_HalfHeight)));
                if (max > m_Threadshold)
                {
                    PB_SIGNAL.Image = Properties.Resources.Green;
                    if (m_IsMonitor)
                    {
                        DateTime triggerTime = DateTime.Now;
                        string saveData = string.Format("{0} {1}:{2}", triggerTime.ToString(m_Date_Format), triggerTime.ToString(m_Time_Format), triggerTime.Millisecond.ToString("000"));
                        m_DataQueue.Enqueue(saveData);
                    }
                }
                else
                {
                    PB_SIGNAL.Image = Properties.Resources.Red;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private void BT_START_Click(object sender, EventArgs e)
        {
            if (CB_USER.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a user first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (CB_LOCATION.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a location first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            m_IsMonitor = true;
            CB_LOCATION.Enabled = false;
            CB_USER.Enabled = false;
        }

        private void BT_STOP_Click(object sender, EventArgs e)
        {
            m_IsMonitor = false;
            CB_LOCATION.Enabled = true;
            CB_USER.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_SaveDataThread != null)
            {
                m_SaveDataThread.Abort();
            }
            m_Audio.StopAudioIn();
            m_DrawWavGraphic.Dispose();
        }

        private void SCB_THREADSHOLD_Scroll(object sender, ScrollEventArgs e)
        {
            ShowThreadshold();
        }

        private void ShowThreadshold()
        {
            m_Threadshold = (SCB_THREADSHOLD.Value / 1000f);
            TB_THREADSHOLD.Text = m_Threadshold.ToString("0.000");
        }

        private void SaveData()
        {
            while (true)
            {
                if (m_DataQueue.Count > 0)
                {
                    string triggerTime = m_DataQueue.Dequeue();
                    DateTime curDateTime = DateTime.Now;
                    string recordTime = string.Format("{0} {1}:{2}", curDateTime.ToString(m_Date_Format), curDateTime.ToString(m_Time_Format), curDateTime.Millisecond.ToString("000"));
                    string cmd = string.Format("INSERT INTO RECORD(REC_ID,U_ID,ST_NO,TRIGGER_TIME,REC_TIME) VALUES ((SELECT IFNULL(MAX(REC_ID), 0) + 1 FROM RECORD), {0}, '{1}', '{2}', '{3}')", m_U_ID, m_ST_NO, triggerTime, recordTime);
                    int effect = m_DBHandler.Excute(cmd);
                }
                Thread.Sleep(10);
            }
        }

        private void BT_ADD_SINGLE_USER_Click(object sender, EventArgs e)
        {
            if (TB_USERNAME.Text == "")
            {
                return;
            }

            DataTable dt = m_DBHandler.ExcuteQuery(string.Format("SELECT * FROM USER WHERE U_NAME ='{0}'", TB_USERNAME.Text));
            if (dt.Rows.Count > 0)
            {
                MessageBox.Show("User have already exist. Please use another username.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (m_DBHandler.Excute(string.Format("INSERT INTO USER(U_ID, U_NAME, ENABLE) VALUES ((SELECT IFNULL(MAX(U_ID), 0) + 1 FROM USER),'{0}', 1)", TB_USERNAME.Text)) == 0)
            {
                MessageBox.Show("Add user fail. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show("Add user success.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            TB_USERNAME.Text = "";
            RefreshUserList();
        }

        private void RefreshUserList()
        {
            LT_USERLIST.Items.Clear();
            CB_USER.Items.Clear();
            CB_DATA_USER.Items.Clear();
            DataTable dt = m_DBHandler.ExcuteQuery("SELECT U_ID, U_NAME FROM USER WHERE ENABLE = 1");
            foreach (DataRow dr in dt.Rows)
            {
                MKItem item = new MKItem();
                item.Show = dr[1].ToString();
                item.Hide = dr[0].ToString();
                CB_USER.Items.Add(item);
                CB_DATA_USER.Items.Add(item);
                LT_USERLIST.Items.Add(dr[1]);
            }
        }

        private void BT_DISABLE_USER_Click(object sender, EventArgs e)
        {
            if (LT_USERLIST.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a user from list first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (m_DBHandler.Excute(string.Format("UPDATE USER SET ENABLE = 0 WHERE U_NAME = '{0}'", LT_USERLIST.SelectedItem.ToString())) == 0)
            {
                MessageBox.Show("Delete user fail. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RefreshUserList();
        }

        private void BT_ADD_MULTIPLE_USER_Click(object sender, EventArgs e)
        {
            if (RTB_USERDATALIST.Text == "")
            {
                return;
            }
            List<string> existList = new List<string>();
            List<string> failList = new List<string>();
            List<string> successList = new List<string>();
            string[] dataList = RTB_USERDATALIST.Text.Split(new char[] { '\r', '\n' });
            foreach (string username in dataList)
            {
                DataTable dt = m_DBHandler.ExcuteQuery(string.Format("SELECT * FROM USER WHERE U_NAME ='{0}'", username));
                if (dt.Rows.Count > 0)
                {
                    existList.Add(username);
                    continue;
                }

                if (m_DBHandler.Excute(string.Format("INSERT INTO USER(U_ID, U_NAME, ENABLE) VALUES ((SELECT IFNULL(MAX(U_ID), 0) + 1 FROM USER),'{0}', 1)", username)) == 0)
                {
                    failList.Add(username);
                    continue;
                }
                successList.Add(username);
            }

            RTB_ADD_USER_RESULT.Text = string.Format("Success : [{0}]\r\n", successList.Count);
            foreach (string username in successList)
            {
                RTB_ADD_USER_RESULT.Text += string.Format("{0}\r\n", username);
            }

            RTB_ADD_USER_RESULT.Text += string.Format("Already exist : [{0}]\r\n", existList.Count);
            foreach (string username in existList)
            {
                RTB_ADD_USER_RESULT.Text += string.Format("{0}\r\n", username);
            }

            RTB_ADD_USER_RESULT.Text += string.Format("Add fail : [{0}]\r\n", failList.Count);
            foreach (string username in failList)
            {
                RTB_ADD_USER_RESULT.Text += string.Format("{0}\r\n", username);
            }
            RefreshUserList();
        }

        private void BT_ADD_SINGLE_LOCATION_Click(object sender, EventArgs e)
        {
            if (TB_ST_NO.Text == "")
            {
                MessageBox.Show("ST_NO is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_ST_NO.Focus();
                return;
            }

            if (TB_ADDR_C.Text == "")
            {
                MessageBox.Show("ADDR_C is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_ADDR_C.Focus();
                return;
            }

            if (TB_TRI_NO.Text == "")
            {
                MessageBox.Show("TRI_NO is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_TRI_NO.Focus();
                return;
            }

            if (TB_TM_X.Text == "")
            {
                MessageBox.Show("TM_X is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_TM_X.Focus();
                return;
            }

            if (TB_TM_Y.Text == "")
            {
                MessageBox.Show("TM_Y is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_TM_Y.Focus();
                return;
            }

            if (TB_OBS_SRC.Text == "")
            {
                MessageBox.Show("OBS_SRC is empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TB_OBS_SRC.Focus();
                return;
            }

            DataTable dt = m_DBHandler.ExcuteQuery(string.Format("SELECT ST_NO FROM LOCATION WHERE ST_NO = '{0}'", TB_ST_NO.Text));
            if (dt.Rows.Count > 0)
            {
                MessageBox.Show("Location have already exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string cmd = string.Format("INSERT INTO LOCATION(ST_NO, ADDR_C, TRI_NO, TM_X, TM_Y, OBS_SRC) VALUES('{0}', '{1}', {2}, {3}, {4}, {5})", TB_ST_NO.Text, TB_ADDR_C.Text, TB_TRI_NO.Text, TB_TM_X.Text, TB_TM_Y.Text, TB_OBS_SRC.Text);
            if (m_DBHandler.Excute(cmd) == 0)
            {
                MessageBox.Show("Add location fail. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RefreshLocationList();
        }

        private void RefreshLocationList()
        {
            CB_LOCATION.Items.Clear();
            LT_USERLIST.Items.Clear();
            CB_DATA_LOCATION.Items.Clear();
            m_dtLocation = m_DBHandler.ExcuteQuery("SELECT * FROM LOCATION");
            DGV_LOACTION.DataSource = m_dtLocation;

            foreach (DataRow dr in m_dtLocation.Rows)
            {
                MKItem item = new MKItem();
                item.Show = string.Format("{0},{1},{2},{3},{4}", dr[1].ToString(), dr[2].ToString(), dr[3].ToString(), dr[4].ToString(), dr[5].ToString());
                item.Hide = dr[0].ToString();
                CB_LOCATION.Items.Add(item);
                CB_DATA_LOCATION.Items.Add(item);
            }
        }

        private void BT_ADD_MULTIPLE_LOCATION_Click(object sender, EventArgs e)
        {
            if (RTB_LOCATIONDATALIST.Text == "")
            {
                return;
            }

            List<string> existList = new List<string>();
            List<string> failList = new List<string>();
            List<string> successList = new List<string>();
            string[] dataList = RTB_LOCATIONDATALIST.Text.Split(new char[] { '\r', '\n' });
            foreach (string location in dataList)
            {
                if (location == "")
                {
                    continue;
                }

                string[] args = location.Split(',');
                if (args.Length < 6)
                {
                    failList.Add(location);
                    continue;
                }

                DataTable dt = m_DBHandler.ExcuteQuery(string.Format("SELECT * FROM LOCATION WHERE ST_NO ='{0}'", args[0]));
                if (dt.Rows.Count > 0)
                {
                    existList.Add(location);
                    continue;
                }

                string cmd = string.Format("INSERT INTO LOCATION(ST_NO, ADDR_C, TRI_NO, TM_X, TM_Y, OBS_SRC) VALUES('{0}', '{1}', {2}, {3}, {4}, {5})", args[0], args[1], args[2], args[3], args[4], args[5]);
                if (m_DBHandler.Excute(cmd) == 0)
                {
                    failList.Add(location);
                    continue;
                }
                successList.Add(location);
            }

            RTB_ADD_LOCATION_RESULT.Text = string.Format("Success : [{0}]\r\n", successList.Count);
            foreach (string location in successList)
            {
                RTB_ADD_LOCATION_RESULT.Text += string.Format("{0}\r\n", location);
            }

            RTB_ADD_LOCATION_RESULT.Text += string.Format("Already exist : [{0}]\r\n", existList.Count);
            foreach (string location in existList)
            {
                RTB_ADD_LOCATION_RESULT.Text += string.Format("{0}\r\n", location);
            }

            RTB_ADD_LOCATION_RESULT.Text += string.Format("Add fail : [{0}]\r\n", failList.Count);
            foreach (string location in failList)
            {
                RTB_ADD_LOCATION_RESULT.Text += string.Format("{0}\r\n", location);
            }
            RefreshLocationList();
        }

        private void BT_DELETE_LOCATION_Click(object sender, EventArgs e)
        {
            if (DGV_LOACTION.CurrentCell == null)
            {
                return;
            }
            int index = DGV_LOACTION.CurrentCell.RowIndex;
            string cmd = string.Format("DELETE FROM LOCATION WHERE ST_NO = '{0}'", m_dtLocation.Rows[index][0]);
            if (m_DBHandler.Excute(cmd) == 0)
            {
                MessageBox.Show("Delete location fail. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RefreshLocationList();
        }

        private void CB_USER_SelectedIndexChanged(object sender, EventArgs e)
        {
            MKItem item = CB_USER.SelectedItem as MKItem;
            m_U_ID = item.Hide;
        }

        private void CB_LOCATION_SelectedIndexChanged(object sender, EventArgs e)
        {
            MKItem item = CB_LOCATION.SelectedItem as MKItem;
            m_ST_NO = item.Hide;
        }

        private void BT_QUERY_Click(object sender, EventArgs e)
        {
            string cmd = "SELECT * FROM RECORD WHERE";
            if (CB_DATA_USER.SelectedIndex > -1)
            {
                MKItem item = CB_DATA_USER.SelectedItem as MKItem;
                cmd += string.Format(" U_ID = {0} AND", item.Hide);
            }

            if (CB_DATA_LOCATION.SelectedIndex != -1)
            {
                MKItem item = CB_DATA_LOCATION.SelectedItem as MKItem;
                cmd += string.Format(" ST_NO = '{0}' AND", item.Hide);
            }

            if (DTP_FROM.Value == DTP_TO.Value)
            {
                cmd += string.Format(" (TRIGGER_TIME LIKE '{0}%') ", DTP_FROM.Value.ToString(m_Date_Format));
            }
            else
            {
                cmd += string.Format(" (TRIGGER_TIME BETWEEN '{0}' AND '{1}') ", DTP_FROM.Value.ToString(m_Date_Format), DTP_TO.Value.ToString(m_Date_Format));
            }

            m_dtDataAnalysis = m_DBHandler.ExcuteQuery(cmd);

            GetSelectTable();
            DGV_DATA_RECORDLIST.DataSource = m_dtSelect;
        }

        private void TSM_START_Click(object sender, EventArgs e)
        {
            if (DGV_DATA_RECORDLIST.CurrentCell == null)
            {
                return;
            }

            m_AnalysisStart = DGV_DATA_RECORDLIST.CurrentCell.RowIndex;
            CalculateMS(m_AnalysisStart, m_dtSelect.Rows.Count);
        }

        private void TSM_STOP_Click(object sender, EventArgs e)
        {
            if (DGV_DATA_RECORDLIST.CurrentCell == null)
            {
                return;
            }

            if (m_AnalysisStart == -1)
            {
                MessageBox.Show("Please set start row first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            m_AnalysisStop = DGV_DATA_RECORDLIST.CurrentCell.RowIndex;
            CalculateMS(m_AnalysisStart, m_AnalysisStop);
            AnylysisData();
        }

        private void TSM_RESET_Click(object sender, EventArgs e)
        {
            m_AnalysisStart = -1;
            m_AnalysisStop = -1;

            GetSelectTable();
            DGV_DATA_RECORDLIST.DataSource = m_dtSelect;
        }

        private int GetMillisecond(string data)
        {
            string[] args = data.Split(':');
            int HH = Convert.ToInt32(args[0]) * 60 * 60 * 1000;
            int MM = Convert.ToInt32(args[1]) * 60 * 1000;
            int SS = Convert.ToInt32(args[2]) * 1000;
            int MS = Convert.ToInt32(args[3]);
            return HH + MM + SS + MS;
        }

        private void CalculateMS(int start, int stop)
        {
            for (int i = 1; i < m_dtSelect.Rows.Count; i++)
            {
                if (i < start)
                {
                    m_dtSelect.Rows[i][1] = 0;
                    continue;
                }

                if (i > stop)
                {
                    m_dtSelect.Rows[i][1] = 0;
                    continue;
                }
                int msBase = GetMillisecond(m_dtSelect.Rows[i - 1][0].ToString());
                int msCur = GetMillisecond(m_dtSelect.Rows[i][0].ToString());
                m_dtSelect.Rows[i][1] = (msCur - msBase);
            }
        }

        private void AnylysisData()
        {
            TB_VALID_RECORDS.Text = (m_AnalysisStop - m_AnalysisStart).ToString();
            int msStar = GetMillisecond(m_dtSelect.Rows[m_AnalysisStart][0].ToString());
            int msStop = GetMillisecond(m_dtSelect.Rows[m_AnalysisStop][0].ToString());
            int HH = (msStop - msStar) / (60 * 60 * 1000);
            int MM = ((msStop - msStar) - (HH * 60 * 60 * 1000)) / (60 * 1000);
            int SS = ((msStop - msStar) - (HH * 60 * 60 * 1000) - (MM * 60 * 1000)) / (1000);
            int MS = (msStop - msStar) % 1000;
            TB_USE_TIME.Text = string.Format("{0:00}:{1:00}:{2:00}.{3:000}", HH, MM, SS, MS);
            TB_AVERAE_MS.Text = string.Format("{0}", (msStop - msStar) / (m_AnalysisStop - m_AnalysisStart));
        }

        private void GetSelectTable()
        {
            m_dtSelect.Rows.Clear();
            foreach (DataRow dr in m_dtDataAnalysis.Rows)
            {
                DataRow newRow = m_dtSelect.NewRow();
                newRow["TriggerTime"] = dr["TRIGGER_TIME"].ToString().Split(' ')[1];
                newRow["TimeSpan(ms)"] = 0;
                m_dtSelect.Rows.Add(newRow);
            }
        }
    }

    public class MKItem
    {
        public string Show { get; set; }
        public string Hide { get; set; }
    }
}
