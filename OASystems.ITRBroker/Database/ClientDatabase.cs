using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using OASystems.ITRBroker.Model;

namespace OASystems.ITRBroker.Database
{
    public class ClientDatabase
    {
        private readonly SqlConnectionStringBuilder _builder;

        public ClientDatabase(DatabaseSettings databaseSettings)
        {
            _builder = new SqlConnectionStringBuilder();

            // Set these values in the secret.json file for development. For Production/UAT apply these into the appsettings.json file
            _builder.DataSource = databaseSettings.DataSource;
            _builder.UserID = databaseSettings.UserID;
            _builder.Password = databaseSettings.Password;
            _builder.InitialCatalog = databaseSettings.InitialCatalog;
        }

        public bool AuthorizeUser(string apiKey)
        {
            bool isAuthorized = false;

            using (SqlConnection connection = new SqlConnection(_builder.ConnectionString))
            {
                connection.Open();

                String sql = String.Format("EXECUTE [dbo].[AuthorizeUser] @ApiKey = '{0}'", apiKey);

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            isAuthorized = reader.GetBoolean(0);
                        }
                    }
                }
            }

            return isAuthorized;
        }
    }
}
