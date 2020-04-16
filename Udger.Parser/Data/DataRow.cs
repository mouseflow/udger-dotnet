/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System;
using System.Collections.Generic;
using System.Data;

namespace Udger.Parser.Data
{
    internal class DataRow : Dictionary<string, object>
    {
        public DataRow(IDataReader reader)
        {
            for (var i = 0; i < reader.FieldCount; i++)
                this[reader.GetName(i)] = reader[i];
        }

        public T Read<T>(string key)
        {
            try
            {
                return ConvertTo<T>(this[key]);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Could not read key \"" + key + "\" into type " + typeof(T).FullName, ex);
            }
        }

        private static T ConvertTo<T>(object value)
        {
            var type = typeof(T);
            var defaultValue = default(T);

            if (value == null || value == DBNull.Value || value == (object)defaultValue)
                return defaultValue;

            if (value is T variable)
                return variable;

            if (type.IsEnum)
            {
                var valueString = value as string;
                return !string.IsNullOrEmpty(valueString) ? (T)Enum.Parse(type, valueString) : defaultValue;
            }

            return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type);
        }
    }
}
