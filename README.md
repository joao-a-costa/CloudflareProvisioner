
# CloudflareProvisioner

CloudflareProvisioner is a .NET library and toolset for automating the provisioning and management of Cloudflare resources, such as DNS records and tunnels, from your .NET applications or scripts. It is designed for extensibility, automation, and integration into modern DevOps workflows.

## Table of Contents
- [Features](#features)
- [Architecture](#architecture)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Supported .NET Versions](#supported-net-versions)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Features
- Automate Cloudflare DNS record creation and management
- Provision and manage Cloudflare tunnels programmatically
- Integrate with Cloudflare's API using secure tokens
- Extensible models for DNS, tunnels, and enrollment
- Console-based provisioning workflow for interactive or automated use

## Architecture
CloudflareProvisioner is organized into:
- **Lib**: Core library for Cloudflare API integration, models, and services
- **Test**: Example/test project demonstrating usage and configuration

Key classes:
- `CloudflareApiService`: Handles API calls for DNS and tunnel management
- `CloudflareTunnelRequest`, `TunnelConfig`, `IngressRule`: Models for tunnel configuration
- `DnsRecordResult`, `EnrollmentRequest`, `EnrollmentResponse`: Models for DNS and enrollment

## Installation
1. Clone the repository:
   ```sh
   git clone https://github.com/joao-a-costa/CloudflareProvisioner.git
   ```
2. Open the solution in Visual Studio 2022 or later.
3. Build the solution to restore dependencies.

## Configuration
Create an `appsettings.json` file in your test or application project with the following structure:

```json
{
  "Cloudflare": {
    "ApiToken": "<your-api-token>",
    "AccountId": "<your-account-id>",
    "ZoneId": "<your-zone-id>",
    "Domain": "example.com",
    "DefaultServiceAddress": "http://localhost:5581"
  }
}
```

## Usage
Here is an example of how to use the library in a console application:

```csharp
using CloudflareProvisioner.Lib.Services;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var cloudflareSection = config.GetSection("Cloudflare");
var api = new CloudflareApiService(
    cloudflareSection["ApiToken"],
    cloudflareSection["AccountId"],
    cloudflareSection["ZoneId"],
    cloudflareSection["Domain"]);

Console.WriteLine("Enter the serial for provisioning:");
var serial = Console.ReadLine();
var provisionResult = await api.ProvisionClientAsync(serial, cloudflareSection["DefaultServiceAddress"]);
Console.WriteLine($"Provisioned: Serial={provisionResult.Serial}, Hostname={provisionResult.Hostname}, TunnelId={provisionResult.TunnelId}");
```

### Main API Capabilities
- **ProvisionClientAsync**: Provisions a new client, creates a tunnel, and sets up DNS
- **GetTunnelCname**: Retrieves the CNAME for a tunnel
- **Manage DNS Records**: Create, update, or delete DNS records via the API

## Supported .NET Versions
- .NET Standard 2.0
- .NET 10

## Testing
The `CloudflareProvisioner.Test` project demonstrates usage and can be used for integration testing. Run the test project after configuring your `appsettings.json`.

## Contributing
Contributions are welcome! Please open issues or submit pull requests for improvements, bug fixes, or new features.

## License
This project is licensed under the MIT License.

## Contact
For questions or support, open an issue on [GitHub](https://github.com/joao-a-costa/CloudflareProvisioner).
