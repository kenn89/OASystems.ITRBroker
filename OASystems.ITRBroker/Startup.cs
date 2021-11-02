using KissLog;
using KissLog.AspNetCore;
using KissLog.CloudListeners.Auth;
using KissLog.CloudListeners.RequestLogsListener;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using OASystems.ITRBroker.Handler;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using Quartz;
using System;
using System.Diagnostics;
using System.Text;

namespace OASystems.ITRBroker
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));

            services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy
                options.FallbackPolicy = options.DefaultPolicy;
            });

            services.AddRazorPages()
                .AddMicrosoftIdentityUI();

            // Configure Kiss Log
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<ILogger>((context) =>
            {
                return Logger.Factory.Get();
            });
            services.AddLogging(logging =>
            {
                logging.AddKissLog();
            });

            // Configure database context
            var server = Configuration["DatabaseConnection:Server"];
            var database = Configuration["DatabaseConnection:Database"];
            var userId = Configuration["DatabaseConnection:UserId"];
            var password = Configuration["DatabaseConnection:Password"];
            services.AddDbContext<DatabaseContext>(
                options => options.UseSqlServer($"Server={server};Database={database};User Id={userId};Password={password}"));

            // Configure Quartz scheduler
            services.Configure<QuartzOptions>(Configuration.GetSection("Quartz"));
            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
            });
            services.AddQuartzServer(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            services.AddScoped<ISchedulerService, SchedulerService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ISchedulerService schedulerService)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=ITRJobMetadata}/{action=Index}/{id?}");
            });

            app.UseKissLogMiddleware(options => {
                ConfigureKissLog(options);
            });

            schedulerService.InitializeITRJobScheduler();
        }

        private void ConfigureKissLog(IOptionsBuilder options)
        {
            // optional KissLog configuration
            options.Options
                .AppendExceptionDetails((Exception ex) =>
                {
                    StringBuilder sb = new StringBuilder();

                    if (ex is System.NullReferenceException nullRefException)
                    {
                        sb.AppendLine("Important: check for null references");
                    }

                    return sb.ToString();
                });

            // KissLog internal logs
            options.InternalLog = (message) =>
            {
                Debug.WriteLine(message);
            };

            // register logs output
            RegisterKissLogListeners(options);
        }

        private void RegisterKissLogListeners(IOptionsBuilder options)
        {
            // multiple listeners can be registered using options.Listeners.Add() method

            // register KissLog.net cloud listener
            options.Listeners.Add(new RequestLogsApiListener(new Application(
                Configuration["KissLog.OrganizationId"],
                Configuration["KissLog.ApplicationId"])
            )
            {
                ApiUrl = Configuration["KissLog.ApiUrl"]
            });
        }
    }
}
