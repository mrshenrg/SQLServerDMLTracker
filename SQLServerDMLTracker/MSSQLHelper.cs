using System;
using System.Collections.Generic;
using System.Configuration;
using System.Configuration.Install;
using System.Data.SqlClient;
using System.Data;

namespace SQLServerDMLTracker
{
    public class MSSQLHelper
    {
        public static string GetConnectionString()
        {
            try
            {
                return System.Configuration.ConfigurationManager.AppSettings["ConnStr"].ToString();
            }
            catch
            {
                return "";
            }
        }

        public static void UpdateConnectionString(string newConnStrValue)
        {
            bool isModified = false;
            foreach (string key in ConfigurationManager.AppSettings)
            {
                if (key == "ConnStr")
                {
                    isModified = true;
                }
            }
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            if (isModified)
            {
                config.AppSettings.Settings.Remove("ConnStr");
            }
            config.AppSettings.Settings.Add("ConnStr", newConnStrValue);

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        #region ExecuteConnect方法
        public static Boolean ExecuteConnect(string connectionString)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region ExecuteNonQuery方法
        public static int ExecuteNonQuery(string connectionString, string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 0;
                    cmd.CommandText = sql;
                    cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region ExecuteScalar方法
        public static object ExecuteScalar(string connectionString, string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteScalar();
                }
            }
        }
        #endregion

        #region ExecuteTable方法
        public static DataTable ExecuteDataTable(string connectionString, string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddRange(parameters);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        using (DataTable dt = new DataTable())
                        {
                            da.Fill(dt);
                            da.FillSchema(dt, SchemaType.Source);   //从数据源中检索架构
                            return dt;
                        }
                    }
                }
            }
        }
        #endregion

        #region ExecuteReader方法
        public static SqlDataReader ExecuteDataReader(string connectionString, string sql, params SqlParameter[] parameters)
        {

            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);
            SqlDataReader dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return dr;
        }

        #endregion

        public static string GetDataBaseString(string connectionString)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                return conn.Database;
            }
        }
    }
}
