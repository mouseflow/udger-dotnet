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
using System.Collections.Concurrent;
using Mouseflow.Udger.Parser.Extensions;

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
            return null;
        }

        private DataTable GetDataTable(string query)
        {
            if (Connected)
            {
                try
                {
                    using (SQLiteCommand cmd = sqlite.OpenAndReturn().CreateCommand())
                    {
                        cmd.CommandText = query;
                        using (var dt = new DataTable())
                        {
                            var ad = new SQLiteDataAdapter(cmd);
                            ad.Fill(dt);
                            return dt;
                        }
                    }
                }
                catch (SQLiteException ex)
                {
                    throw ex;
                }
                finally
                {
                    sqlite.Close();
                }
            }
            return null;
        }

        public ConcurrentDictionary<string, DataRow> SelectQuery(string query, int keyRowIndex)
        {
            try
            {
                var conDir = new ConcurrentDictionary<string, DataRow>();
                foreach (DataRow row in GetDataTable(query).Rows)
                    conDir.TryAdd(row[keyRowIndex].ToString(), row);
                return conDir;
            }
            catch (SQLiteException ex)
            {
                return null;
            }             
        }

        public ConcurrentBag<T> SelectQuery<T>(string query) where T: new ()
        {        
            try
            {
                var conBag = new ConcurrentBag<T>();
                foreach (DataRow row in GetDataTable(query).Rows)
                {
                    T rowObj = row.ToObject<T>();
                    conBag.Add(rowObj);
                }
                return conBag;
            }
            catch (SQLiteException ex)
            {
                return null;
            }
        }

        public ConcurrentDictionary<string, T> SelectQuery<T>(string query, int keyIndex = 0) where T : new()
        {
            try
            {
                var conDir = new ConcurrentDictionary<string, T>();
                foreach (DataRow row in GetDataTable(query).Rows)
                {
                    T rowObj = row.ToObject<T>();
                    conDir.TryAdd(row[keyIndex].ToString(), rowObj);
                }
                return conDir;
            }
            catch (SQLiteException ex)
            {
                return null;
            }
                    
        }
    }
}