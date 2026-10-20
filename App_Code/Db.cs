using System;
using System.Configuration;
using System.Data.SqlClient;

namespace PatientFiles
{
    internal static class Db
    {
        public static SqlConnection Open()
        {
            var cs = ConfigurationManager.ConnectionStrings["MisDb"];
            if (cs == null || String.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new InvalidOperationException("Не настроена строка подключения MisDb в web.config.");
            var cn = new SqlConnection(cs.ConnectionString);
            cn.Open();
            return cn;
        }
    }
}
