
using CloudflareProvisioner.Lib.Services;
using Microsoft.Extensions.Configuration;

internal class Program
{
    public static async Task Main(string[] args)
    {

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var cloudflareSection = config.GetSection("Cloudflare");
        var _apiToken = cloudflareSection["ApiToken"] ?? string.Empty;
        var _accountId = cloudflareSection["AccountId"] ?? string.Empty;
        var _zoneId = cloudflareSection["ZoneId"] ?? string.Empty;
        var _domain = cloudflareSection["Domain"] ?? string.Empty;
        var _defaultServiceAddress = cloudflareSection["DefaultServiceAddress"] ?? "http://localhost:5581";

        var api = new CloudflareApiService(_apiToken, _accountId, _zoneId, _domain);

        while (true)
        {
            try
            {
                Console.WriteLine("Enter the serial for provisioning (or type 'exit' to quit):");
                var serial = Console.ReadLine();
                if (string.Equals(serial, "exit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.WriteLine($"Enter the service address (default: {_defaultServiceAddress}):");
                var serviceAddress = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(serviceAddress))
                    serviceAddress = _defaultServiceAddress;

                Console.WriteLine($"Provisioning with service address: {serviceAddress}, please wait...");
                var provisionResult = await api.ProvisionClientAsync(serial, serviceAddress);
                Console.WriteLine("Provisioning completed.");
                Console.WriteLine($"Provisioning Result: Serial={provisionResult.Serial}, Hostname={provisionResult.Hostname}, TunnelId={provisionResult.TunnelId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}