/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System;
using System.Data;
using System.Data.SQLite;

namespace Udger.Parser
{
    class DataReader
    {
        public string DataSourcePath { get; set; }
        public bool Connected { get; private set; }
        private SQLiteConnection sqlite;
        public string data_dir { get; set; }

        public void connect(UdgerParser _udger)
        {
            try
            { 
                if (!this.Connected)
                {
                    sqlite = new SQLiteConnection(@"Data Source=" + DataSourcePath);
                    Connected = true;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public DataTable selectQuery(string query)
        {
            if (!Connected)
                return new DataTable();

            sqlite.Open();

            var cmd = sqlite.CreateCommand();
            cmd.CommandText = query;
            var dt = new DataTable();
            var ad = new SQLiteDataAdapter(cmd);
            ad.Fill(dt);

            sqlite.Close();

            return dt;
        }
    }
}
