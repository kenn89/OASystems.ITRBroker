using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OASystems.ITRBroker.Models;
using Microsoft.Extensions.Hosting;

namespace OASystems.ITRBroker.Services
{
    public interface IDatabaseService
    {
        Task<ITRJob> Authenticate(string username, string password);
        Task<ITRJob> GetITRJobFromID(Guid itrJobID);
        Task<List<ITRJob>> GetAllEnabledITRJobs();
        void UpdateITRJobCronSchedule(Guid itrJobID, string cronSchedule);
        void UpdateITRJobIsScheduled(Guid itrJobID, bool isScheduled);
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly SqlConnectionStringBuilder _sqlConnBuilder;

        public DatabaseService()
        {
            IConfigurationBuilder configBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

            // Use values from secret.json file for development. Use appsettings.json file for non-development
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development)
            {
                configBuilder.AddUserSecrets<Program>();
            }

            IConfigurationRoot configuration = configBuilder.Build();

            _sqlConnBuilder = new SqlConnectionStringBuilder
            {
                DataSource = configuration.GetSection("DatabaseSettings")["DataSource"],
                UserID = configuration.GetSection("DatabaseSettings")["UserID"],
                Password = configuration.GetSection("DatabaseSettings")["Password"],
                InitialCatalog = configuration.GetSection("DatabaseSettings")["InitialCatalog"]
            };
        }

        // Authenticate the username and password
        public async Task<ITRJob> Authenticate(string username, string password)
        {
            // Get ITR Job from Database
            ITRJob itrJob = new ITRJob();
            using (SqlConnection connection = new SqlConnection(_sqlConnBuilder.ConnectionString))
            {
                connection.Open();

                string sql = "EXECUTE [dbo].[GetITRJobFromApiUsernamePassword] @ApiUsername, @ApiPassword";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    SqlParameter param1 = new SqlParameter();
                    param1.ParameterName = "@ApiUsername";
                    param1.Value = username;

                    SqlParameter param2 = new SqlParameter();
                    param2.ParameterName = "@ApiPassword";
                    param2.Value = password;

                    command.Parameters.Add(param1);
                    command.Parameters.Add(param2);

                    await using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            itrJob.ID = reader.GetGuid(0);
                            itrJob.Name = reader.GetString(1);
                        }
                    }
                }
            }
            return itrJob;
        }

        // Use ITR Job ID to retrieve the ITR Job from the Database
        public async Task<ITRJob> GetITRJobFromID(Guid itrJobID)
        {
            // Get ITR Job from Database
            ITRJob itrJob = new ITRJob();
            using (SqlConnection connection = new SqlConnection(_sqlConnBuilder.ConnectionString))
            {
                connection.Open();

                string sql = "EXECUTE [dbo].[GetITRJobFromID] @itrJobID";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    SqlParameter param = new SqlParameter();
                    param.ParameterName = "@itrJobID";
                    param.Value = itrJobID;

                    command.Parameters.Add(param);

                    await using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            itrJob.ID = reader.GetGuid(0);
                            itrJob.Name = reader.GetString(1);
                            itrJob.CronSchedule = reader.IsDBNull(2) ? String.Empty : reader.GetString(2);
                            itrJob.IsScheduled = reader.GetBoolean(3);
                            itrJob.CrmUrl = reader.GetString(4);
                            itrJob.CrmClientID = reader.GetString(5);
                            itrJob.CrmSecret = reader.GetString(6);
                        }
                    }
                }
            }
            return itrJob;
        }

        // Get list of all enabled ITR Jobs from Database
        public async Task<List<ITRJob>> GetAllEnabledITRJobs()
        {
            List<ITRJob> itrJobs = new List<ITRJob>();

            using (SqlConnection connection = new SqlConnection(_sqlConnBuilder.ConnectionString))
            {
                connection.Open();

                string sql = String.Format("EXECUTE [dbo].[GetAllEnabledITRJobs]");
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    await using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ITRJob itrJob = new ITRJob()
                            {
                                ID = reader.GetGuid(0),
                                Name = reader.GetString(1),
                                CronSchedule = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                                IsScheduled = reader.GetBoolean(3),
                                CrmUrl = reader.GetString(4),
                                CrmClientID = reader.GetString(5),
                                CrmSecret = reader.GetString(6)
                            };
                            itrJobs.Add(itrJob);
                        }
                    }
                }
            }
            return itrJobs;
        }

        // Update the Cron Schedule for a given ITR Job in Database
        public void UpdateITRJobCronSchedule(Guid itrJobID, string cronSchedule)
        {
            using (SqlConnection connection = new SqlConnection(_sqlConnBuilder.ConnectionString))
            {
                connection.Open();

                string sql = "EXECUTE [dbo].[UpdateITRJobCronSchedule] @ITRJobID, @CronSchedule";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    SqlParameter param1 = new SqlParameter();
                    param1.ParameterName = "@ITRJobID";
                    param1.Value = itrJobID;

                    SqlParameter param2 = new SqlParameter();
                    param2.ParameterName = "@CronSchedule";
                    param2.Value = cronSchedule;

                    command.Parameters.Add(param1);
                    command.Parameters.Add(param2);

                    SqlDataReader reader = command.ExecuteReader();
                    return;
                }
            }
        }

        // Update the Cron Schedule for a given ITR Job in Database
        public void UpdateITRJobIsScheduled(Guid itrJobID, bool isScheduled)
        {
            using (SqlConnection connection = new SqlConnection(_sqlConnBuilder.ConnectionString))
            {
                connection.Open();

                string sql = "EXECUTE [dbo].[UpdateITRJobIsScheduled] @ITRJobID, @IsScheduled";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    SqlParameter param1 = new SqlParameter();
                    param1.ParameterName = "@ITRJobID";
                    param1.Value = itrJobID;

                    SqlParameter param2 = new SqlParameter();
                    param2.ParameterName = "@IsScheduled";
                    param2.Value = isScheduled;

                    command.Parameters.Add(param1);
                    command.Parameters.Add(param2);

                    SqlDataReader reader = command.ExecuteReader();
                    return;
                }
            }
        }
    }
}
