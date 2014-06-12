using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Data.SQLite;


namespace MonitorDevice
{
    class MkDBHandler
    {
        SQLiteConnection m_Connection = null;
        string[] SchemaTypes = { "MetaDataCollections", "DataSourceInformation", "DataTypes", "ReservedWords", "Catalogs", "Columns", "Indexes", "IndexColumns", "Tables", "Views", "ViewColumns", "ForeignKeys", "Triggers" };
        public MkDBHandler()
        {
        }

        public bool Open(string fullPath)
        {
            try
            {
                if (IsOpen())
                {
                    m_Connection.Close();
                }
                if (!Directory.Exists(Directory.GetParent(fullPath).FullName))
                {
                    Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                }
                m_Connection = new SQLiteConnection(string.Format("Data source = {0}", fullPath));
                if (m_Connection == null)
                {
                    Console.WriteLine(string.Format("Exp:SQLiteConnection faile"));
                    return false;
                }
                m_Connection.Open();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(string.Format("Exp:{0}", ex.Message));
                return false;
            }
            return true;
        }

        public void Close()
        {
            if (m_Connection != null)
            {
                m_Connection.Close();
            }
        }

        public bool IsOpen()
        {
            if (m_Connection == null)
            {
                return false;
            }
            if (m_Connection.State == ConnectionState.Closed)
            {
                return false;
            }
            return true;
        }

        public DataTable ExcuteQuery(string command)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command, m_Connection))
                {
                    dataAdapter.Fill(dt);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(string.Format("Exp:{0}", ex.Message));
                throw ex;
            }
            return dt;
        }

        public int Excute(string command)
        {
            int effectRow = 0;
            try
            {
                using (SQLiteCommand sqliteCommand = new SQLiteCommand(m_Connection))
                {
                    sqliteCommand.CommandText = command;
                    effectRow = sqliteCommand.ExecuteNonQuery();
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(string.Format("Exp:{0}", ex.Message));
                throw ex;
            }
            return effectRow;
        }

        public DataTable GetSchema(SchemaType type)
        {
            return m_Connection.GetSchema(SchemaTypes[(int)type]);
        }

        public DataTable GetTableInfo(string tableName)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter("PRAGMA table_info(" + tableName + ");", m_Connection))
                {
                    dataAdapter.Fill(dt);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(string.Format("Exp:{0}", ex.Message));
            }
            return dt;
        }

        public bool IsTableExist(string tableName)
        {
            DataTable dt = m_Connection.GetSchema("Tables");
            return dt.Select("Table_Name = '" + tableName + "'").Length > 0;
        }
    }

    public enum SchemaType : int
    {
        MetaDataCollections,
        DataSourceInformation,
        DataTypes,
        ReservedWords,
        Catalogs,
        Columns,
        Indexes,
        IndexColumns,
        Tables,
        Views,
        ViewColumns,
        ForeignKeys,
        Triggers
    }
}