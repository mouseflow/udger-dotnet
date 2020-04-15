/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace Udger.Parser
{
    internal class DataReader
    {
        public string DataSourcePath { get; set; }

        public IEnumerable<DataRow> Select(string query)
        {
            using var connection = CreateConnection(DataSourcePath);
            using var command = CreateCommand(connection, query);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return new DataRow(reader);
            }
        }

        public DataRow SelectRow(string query)
        {
            return Select(query).FirstOrDefault();
        }

        private static IDbConnection CreateConnection(string dataSourcePath)
        {
            return new SQLiteConnection($"Data Source={dataSourcePath}");
        }

        private static IDbCommand CreateCommand(IDbConnection connection, string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            return command;
        }
    }
}
