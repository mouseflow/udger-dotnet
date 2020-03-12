/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System.Data;
using System.Data.SQLite;
using System.Collections.Concurrent;

namespace Mouseflow.Udger.Parser
{
    class DataReader
    {
        public string DataSourcePath { get; set; }
        public string DataDir { get; set; }
        public bool Connected { get; private set; }
        private SQLiteConnection sqlite;
        

        public void Connect()
        {
            if (Connected)
                return;        
            sqlite = new SQLiteConnection($"Data Source={DataSourcePath};");
            Connected = true;
        }

        public DataTable selectQuery(string query)
        {
            if (Connected)
            {
                SQLiteDataAdapter ad;
                DataTable dt = new DataTable();

                try
                {
                    SQLiteCommand cmd;
                    sqlite.Open();  //Initiate connection to the db
                    cmd = sqlite.CreateCommand();
                    cmd.CommandText = query;  //set the passed query
                    ad = new SQLiteDataAdapter(cmd);
                    ad.Fill(dt); //fill the datasource
                }
                catch (SQLiteException ex)
                {
                    throw ex;
                }
                sqlite.Close();
                return dt;
            }
            return new DataTable();
        }

        public ConcurrentDictionary<string, DataRow> SelectQuery(string query, int keyRowIndex)
        {
            if (Connected)
            {
                var conDir = new ConcurrentDictionary<string, DataRow>();
                try
                {
                    SQLiteCommand cmd = sqlite.OpenAndReturn().CreateCommand();
                    cmd.CommandText = query;
                    var dt = new DataTable();
                    var ad = new SQLiteDataAdapter(cmd).Fill(dt);

                    foreach (DataRow row in dt.Rows)
                        conDir.TryAdd(row[keyRowIndex].ToString(), row);
                }
                catch (SQLiteException ex)
                {
                    throw ex;
                }
                finally
                {
                    sqlite.Close();
                }               
                return conDir;
            }
            return null;
        }

    }
}