namespace RedirectOldUi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.StatusCode = 301;
                    context.Response.Headers["Location"] = "https://alpinehuts.silenced.eu";
                });
            });
            app.Run();
        }
    }
}