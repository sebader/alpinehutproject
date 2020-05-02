using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("de"),
                    new CultureInfo("en")
                };
                options.DefaultRequestCulture = new RequestCulture("de");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

            services.AddMvc().AddViewLocalization();
            services.AddLocalization(options => options.ResourcesPath = "Resources");


            services.AddDbContextPool<AlpinehutsDbContext>(options =>
            {
                options.UseSqlServer(Configuration["DatabaseConnectionString"]);
                options.UseAzureAccessToken();
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

            // Source: https://forums.asp.net/t/2151316.aspx?Why+DefaultRequestCulture+does+not+work+in+ASP+NET+Core
            var options = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            var cookieProvider = options.Value.RequestCultureProviders
                .OfType<CookieRequestCultureProvider>()
                .First();
            var urlProvider = options.Value.RequestCultureProviders
                .OfType<QueryStringRequestCultureProvider>().First();

            cookieProvider.Options.DefaultRequestCulture = new RequestCulture("de");
            urlProvider.Options.DefaultRequestCulture = new RequestCulture("de");

            cookieProvider.CookieName = "UserCulture";

            options.Value.RequestCultureProviders.Clear();
            options.Value.RequestCultureProviders.Add(cookieProvider);
            options.Value.RequestCultureProviders.Add(urlProvider);
            app.UseRequestLocalization(options.Value);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
