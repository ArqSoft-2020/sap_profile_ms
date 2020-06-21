using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace sap_profile_ms
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();


        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:8000;http://localhost:8001;http://hostname:8002");

                });
    }
}
