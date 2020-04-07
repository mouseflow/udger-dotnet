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

namespace Udger.Parser
{
    class DataReader
    {
        private SQLiteConnection db;
        public string DataSourcePath { get; set; }
        public bool Connected { get; private set; }
        public string DataDir { get; set; }

        public void Connect()
        {
            if (Connected)
                return;

            db = new SQLiteConnection(@"Data Source=" + DataSourcePath);
            Connected = true;
        }

        public DataTable SelectQuery(string query)
        {
            if (!Connected)
                return new DataTable();

            db.Open();

            var cmd = db.CreateCommand();
            cmd.CommandText = query;
            var dt = new DataTable();
            var ad = new SQLiteDataAdapter(cmd);
            ad.Fill(dt);

            db.Close();

            return dt;
        }
    }
}
