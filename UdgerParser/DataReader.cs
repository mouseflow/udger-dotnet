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
        public string DataSourcePath { get; set; }

        public DataTable SelectQuery(string query)
        {
            using (var connection = CreateConnection(DataSourcePath))
            using (var command = CreateCommand(connection, query))
            {
                var dataTable = new DataTable();
                var adapter = new SQLiteDataAdapter(command);
                adapter.Fill(dataTable);

                return dataTable;
            }
        }

        private static SQLiteConnection CreateConnection(string dataSourcePath)
        {
            return new SQLiteConnection($"Data Source={dataSourcePath}");
        }

        private static SQLiteCommand CreateCommand(SQLiteConnection connection, string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            return command;
        }
    }
}
