using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Shared;

namespace AlpinHutsDashboard
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IWebHostEnvironment Environment { get; }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();

            /*
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            */

            services.AddMvc().AddViewLocalization();
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryMSI, new AzureIdentitySqlAuthenticationProvider());
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, new AzureIdentitySqlAuthenticationProvider());

            services.AddDbContextPool<AlpinehutsDbContext>(options =>
            {
                options.UseSqlServer(Configuration["DatabaseConnectionString"]);
            });

            IMvcBuilder builder = services.AddRazorPages();

            if (Environment.EnvironmentName == "Development")
            {
                builder.AddRazorRuntimeCompilation();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.EnvironmentName == "Development")
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
            app.UseCookiePolicy();

            app.UseRouting();

            #region Localization
            // REMARK: you may refactor this into a separate method as it's better to avoid long methods with regions
            var supportedCultures = new[]
            {
            new CultureInfo("de"),
            new CultureInfo("en")
            };
            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("de", "de"),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures,
                // you can change the list of providers, if you don't want the default behavior
                // e.g. the following line enables to pick up culture ONLY from cookies
                RequestCultureProviders = new[] { new CookieRequestCultureProvider() }
            };
            app.UseRequestLocalization(localizationOptions);
            #endregion

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
