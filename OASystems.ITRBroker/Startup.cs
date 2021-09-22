using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OASystems.ITRBroker.Handler;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using System.Data.SqlClient;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

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

            services.AddControllersWithViews();

            // Add Authentication
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));

            services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy
                options.FallbackPolicy = options.DefaultPolicy;
            });

            services.AddRazorPages()
                .AddMvcOptions(options => { })
                .AddMicrosoftIdentityUI();

            // Configure scheduler
            services.AddScoped<ISchedulerService, SchedulerService>();

            // Configure database context
            var sqlConnBuilder = new SqlConnectionStringBuilder
            {
                DataSource = Configuration["DatabaseSettings:DataSource"],
                UserID = Configuration["DatabaseSettings:UserID"],
                Password = Configuration["DatabaseSettings:Password"],
                InitialCatalog = Configuration["DatabaseSettings:InitialCatalog"]
            };
            services.AddDbContext<DatabaseContext>(
                options => options.UseSqlServer(sqlConnBuilder.ConnectionString));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, DatabaseContext context, ISchedulerService schedulerService)
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
                    pattern: "{controller=ITRJobs}/{action=Index}/{id?}");
            });

            schedulerService.InitializeITRJobScheduler(context);
        }
    }
}
