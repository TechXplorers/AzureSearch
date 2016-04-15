//Copyright 2014 Microsoft

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace CatalogIndexer
{
    class ChangeEnumeratorSql
    {
        private static string _connectionString;
        private static string _query;
        private static string _versionColumnName;

        public ChangeEnumeratorSql(string connectionString, string query, string versionColumnName)
        {
            _connectionString = connectionString;
            _query = query;
            _versionColumnName = versionColumnName;
        }

        public ChangeSet ComputeChangeSet(byte[] lastVersion)
        {
            SqlConnection con = new SqlConnection(_connectionString);
            con.Open();

            try
            {
                // Compute a new version first to make sure we don't lose any updates
                byte[] version = GetChangeSetVersion(con);

                IEnumerable<Dictionary<string, object>> changes = EnumerateUpdatedDocuments(con, lastVersion);

                return new ChangeSet { Version = version, Changes = changes };
            }
            catch
            {
                // In non-error paths the data reader will auto-close the connection, but if 
                // we find an error let's close it here so we don't leak it
                con.Close();
                throw;
            }
        }

        private byte[] GetChangeSetVersion(SqlConnection con)
        {
            SqlCommand cmd = new SqlCommand("SELECT MIN_ACTIVE_ROWVERSION()", con);
            return (byte[])cmd.ExecuteScalar();
        }

        private IEnumerable<Dictionary<string, object>> EnumerateUpdatedDocuments(SqlConnection con, byte[] lastVersion)
        {
            SqlCommand cmd;
            if (lastVersion == null)
            {
                cmd = new SqlCommand(_query, con);
            }
            else
            {
                cmd = new SqlCommand("SELECT * FROM (" + _query + ") WHERE " + _versionColumnName + " >= @v", con);
                cmd.Parameters.Add("v", System.Data.SqlDbType.Timestamp).Value = lastVersion;
            }

            using (SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection))
            {
                while (reader.Read())
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();

                    for (int i = 0; i < reader.VisibleFieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        row[reader.GetName(i)] = value is DBNull ? null : value;
                    }

                    // Yield rows as we get them and avoid buffering them so we can easily handle
                    // large datasets without memory issues
                    yield return row;
                }
            }
        }

        static ChangeEnumeratorSql()
        {
            // We use this so that |DataDirectory| in connection strings maps to the local directory
            // if the environment didn't set a data directory yet
            if (AppDomain.CurrentDomain.GetData("DataDirectory") == null)
            {
                AppDomain.CurrentDomain.SetData("DataDirectory", AppDomain.CurrentDomain.BaseDirectory);
            }
        }
    }
}
