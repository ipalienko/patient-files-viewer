using System;
using System.Configuration;
using System.Data.SqlClient;

namespace PatientFiles
{
    internal static class Db
    {
        public static SqlConnection Open()
        {
            return OpenByName("MisDb");
        }

        public static SqlConnection OpenWrite()
        {
            return OpenByName("MisWriteDb");
        }

        private static SqlConnection OpenByName(string name)
        {
            var cs = ConfigurationManager.ConnectionStrings[name];
            if (cs == null || String.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new InvalidOperationException("Не настроена строка подключения " + name + " в web.config.");

            var cn = new SqlConnection(cs.ConnectionString);
            cn.Open();
            return cn;
        }
    }
}