using Newtonsoft.Json;
using CloudflareProvisioner.Lib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CloudflareProvisioner.Lib.Services
{
    public class CloudflareApiService
    {
        private const string _baseApiUrl = "https://api.cloudflare.com/client/v4/";
        private const string _dateFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly string _cloudflaredFilename = "cloudflared";

        private readonly string _apiToken;
        private readonly string _accountId;
        private readonly string _zoneId;
        private readonly string _domain;
        private readonly HttpClient _httpClient;

        public CloudflareApiService(string apiToken, string accountId, string zoneId, string domain)
        {
            _apiToken = apiToken;
            _accountId = accountId;
            _zoneId = zoneId;
            _domain = domain;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            //_httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        }

        /// <summary>
        /// Gets the tunnel CNAME for a given tunnel ID. This is the hostname that should be used in the DNS record to point to the tunnel. The CNAME follows the pattern: #tunnel-id#.cfargotunnel.com
        /// </summary>
        /// <param name="tunnelId">The ID of the tunnel for which to get the CNAME</param>
        /// <returns>The CNAME that should be used in the DNS record to point to the tunnel</returns>
        public static string GetTunnelCname(string tunnelId)
        {
            // O CNAME do tunnel segue o padrão: <tunnel-id>.cfargotunnel.com
            return $"{tunnelId}.cfargotunnel.com";
        }

        /// <summary>
        /// Installs the Cloudflared service using the specified tunnel token, replacing any existing service
        /// installation.
        /// </summary>
        /// <remarks>This method first attempts to uninstall any existing Cloudflared service before
        /// installing a new one with the provided tunnel token. Errors encountered during the uninstallation process
        /// are ignored, as the service may not be present.</remarks>
        /// <param name="tunnelId">The ID of the tunnel for which to install the Cloudflared service.</param>
        /// <param name="tunnelToken">The token used to authenticate and configure the Cloudflared service with the desired Cloudflare tunnel.
        /// Cannot be null or empty.</param>
        /// <returns>true if the Cloudflared service is successfully installed; otherwise, an exception is thrown.</returns>
        /// <exception cref="Exception">Thrown if the installation of the Cloudflared service fails, such as when the cloudflared executable returns
        /// a non-zero exit code.</exception>
        public static bool InstallCloudflaredService(string tunnelId, string tunnelToken)
        {
            var exeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cloudflare");
            var tunnelExePath = Path.Combine(exeDir, $"{_cloudflaredFilename}{tunnelId}.exe");

            // Ensure the directory exists
            if (!Directory.Exists(exeDir))
                Directory.CreateDirectory(exeDir);

            // Extract the embedded cloudflared.exe if it doesn't exist or always overwrite
            File.WriteAllBytes(tunnelExePath, Properties.Resources.cloudflared);

            // 1. Uninstall any existing service (ignore errors)
            var uninstallProcess = new System.Diagnostics.Process();
            uninstallProcess.StartInfo.FileName = tunnelExePath;
            uninstallProcess.StartInfo.Arguments = "service uninstall";
            uninstallProcess.StartInfo.WorkingDirectory = exeDir;
            uninstallProcess.StartInfo.CreateNoWindow = true;
            uninstallProcess.StartInfo.UseShellExecute = false;
            uninstallProcess.StartInfo.RedirectStandardOutput = true;
            uninstallProcess.StartInfo.RedirectStandardError = true;
            uninstallProcess.StartInfo.Verb = "runas";

            try
            {
                uninstallProcess.Start();
                uninstallProcess.WaitForExit();
            }
            catch
            {
                // Ignore errors from uninstall (service may not exist)
            }

            // 2. Install the service with the new token
            var installProcess = new System.Diagnostics.Process();
            installProcess.StartInfo.FileName = tunnelExePath;
            installProcess.StartInfo.Arguments = $"service install {tunnelToken}";
            installProcess.StartInfo.WorkingDirectory = exeDir;
            installProcess.StartInfo.CreateNoWindow = true;
            installProcess.StartInfo.UseShellExecute = false;
            installProcess.StartInfo.RedirectStandardOutput = true;
            installProcess.StartInfo.RedirectStandardError = true;
            installProcess.StartInfo.Verb = "runas";

            installProcess.Start();
            string output = installProcess.StandardOutput.ReadToEnd();
            string error = installProcess.StandardError.ReadToEnd();
            installProcess.WaitForExit();

            if (installProcess.ExitCode != 0)
            {
                throw new Exception($"{tunnelId}.exe failed: {error}");
            }

            return true;
        }

        /// <summary>
        /// Creates a new Cloudflare tunnel with the specified name and optional hostname, configuring it to route
        /// traffic to a local service.
        /// </summary>
        /// <remarks>The method sends a request to the Cloudflare API to create a tunnel and configures it
        /// to forward traffic to a local service. If the hostname parameter is not provided, the method derives it from
        /// the tunnel name using the configured domain. The returned TunnelResult contains information about the newly
        /// created tunnel, including its identifiers and configuration.</remarks>
        /// <param name="tunnelName">The name of the tunnel to create. This value is used to identify the tunnel in Cloudflare and, if no
        /// hostname is provided, to derive the default hostname.</param>
        /// <param name="hostname">The optional hostname to associate with the tunnel. If not specified, the hostname is derived from the
        /// tunnel name and the configured domain.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a TunnelResult object with
        /// details about the created tunnel.</returns>
        /// <exception cref="Exception">Thrown if the tunnel creation fails or the Cloudflare API returns an error response.</exception>
        public async Task<TunnelResult> CreateTunnelAsync(string tunnelName, string hostname = null)
        {
            // Se hostname não fornecido, extrair do tunnelName (formato: tunnel-<serial>)
            if (string.IsNullOrEmpty(hostname))
            {
                var parts = tunnelName.Split('-');
                if (parts.Length > 1)
                {
                    hostname = $"{parts[1]}.{_domain}";
                }
                else
                {
                    hostname = $"{tunnelName}.{_domain}";
                }
            }

            var request = new CloudflareTunnelRequest
            {
                Name = tunnelName,
                Config = new TunnelConfig
                {
                    Ingress = new List<IngressRule>
                    {
                        new IngressRule
                        {
                            Hostname = hostname,
                            Service = "http://127.0.0.1:5580"
                        },
                        new IngressRule
                        {
                            Service = "http_status:404"
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] POST {_baseApiUrl}accounts/{_accountId}/cfd_tunnel");
            Console.WriteLine($"[{now}] [HTTP] Request Body: {json}");
            var response = await _httpClient.PostAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel",
                content
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            var result = JsonConvert.DeserializeObject<CloudflareTunnelResponse>(responseContent);

            if (!result.Success || result.Result == null)
            {
                throw new Exception($"Erro ao criar tunnel: {responseContent}");
            }

            return result.Result;
        }

        /// <summary>
        /// Asynchronously retrieves the connection token for a specified Cloudflare named tunnel.
        /// </summary>
        /// <remarks>This method sends an HTTP GET request to the Cloudflare API to obtain a tunnel token.
        /// Ensure that the provided tunnel ID is valid and that the account has the necessary permissions to access the
        /// tunnel.</remarks>
        /// <param name="tunnelId">The unique identifier of the tunnel for which to obtain the connection token. Cannot be null or empty.</param>
        /// <returns>A string containing the connection token for the specified tunnel.</returns>
        /// <exception cref="Exception">Thrown if the request to the Cloudflare API fails or if the response indicates an error.</exception>
        public async Task<string> GetTunnelTokenAsync(string tunnelId)
        {
            // Endpoint para obter o connection token de um Named Tunnel
            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] GET {_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/token");
            var response = await _httpClient.GetAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/token"
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao obter token do tunnel. Status: {response.StatusCode}, Resposta: {responseContent}");
            }
            var result = JsonConvert.DeserializeObject<TunnelTokenResponse>(responseContent);
            if (!result.Success || string.IsNullOrEmpty(result.Result))
            {
                throw new Exception($"Erro ao obter token do tunnel: {responseContent}");
            }
            return result.Result;
        }

        /// <summary>
        /// Asynchronously retrieves the DNS records for the configured Cloudflare zone.
        /// </summary>
        /// <remarks>Ensure that the zone identifier is set before calling this method. The returned JSON
        /// string can be deserialized into a model for further processing.</remarks>
        /// <returns>A string containing the JSON response from the Cloudflare API, which includes the DNS records for the zone.</returns>
        /// <exception cref="Exception">Thrown if the HTTP request to the Cloudflare API does not succeed. The exception message includes the HTTP
        /// status code and response content.</exception>
        public async Task<string> GetDnsRecordsAsync()
        {
            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] GET {_baseApiUrl}zones/{_zoneId}/dns_records");
            var response = await _httpClient.GetAsync(
                $"{_baseApiUrl}zones/{_zoneId}/dns_records");
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao obter DNS records. Status: {response.StatusCode}, Resposta: {responseContent}");
            }
            return responseContent; // Optionally deserialize to a model
        }

        /// <summary>
        /// Creates a CNAME DNS record for the specified hostname in the configured Cloudflare zone.
        /// </summary>
        /// <remarks>This method sends a request to the Cloudflare API using the configured zone ID. The
        /// created DNS record is proxied and uses automatic TTL. Ensure that the zone ID and API credentials are valid
        /// before calling this method.</remarks>
        /// <param name="hostname">The hostname for which to create the CNAME DNS record. This value cannot be null or empty.</param>
        /// <param name="tunnelCname">The CNAME target that the DNS record will point to, typically in the format '<UUID>.cfargotunnel.com'. This
        /// value cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a DnsRecordResult object
        /// representing the created DNS record.</returns>
        /// <exception cref="Exception">Thrown if the DNS record creation fails or if the Cloudflare API returns an error response.</exception>
        public async Task<DnsRecordResult> CreateDnsRecordAsync(string hostname, string tunnelCname)
        {
            var request = new DnsRecordRequest
            {
                Type = "CNAME",
                Name = hostname,
                Content = tunnelCname, // <UUID>.cfargotunnel.com
                Ttl = 1, // Auto
                Proxied = true
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] POST {_baseApiUrl}zones/{_zoneId}/dns_records");
            Console.WriteLine($"[{now}] [HTTP] Request Body: {json}");
            var response = await _httpClient.PostAsync(
                $"{_baseApiUrl}zones/{_zoneId}/dns_records",
                content
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            var result = JsonConvert.DeserializeObject<DnsRecordResponse>(responseContent);

            if (!result.Success || result.Result == null)
            {
                throw new Exception($"Erro ao criar DNS record: {responseContent}");
            }

            return result.Result;
        }

        public async Task<string> GetPublishedApplicationRoutesAsync(string tunnelId)
        {
            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] GET {_baseApiUrl}accounts/{_accountId}/cfd_tunnel/routes?tunnel_id={tunnelId}");
            var response = await _httpClient.GetAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/routes?tunnel_id={tunnelId}"
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao obter published application routes. Status: {response.StatusCode}, Resposta: {responseContent}");
            }
            return responseContent; // You can deserialize to a model if you want
        }

        //public async Task<string> GetPublishedApplicationRoutesAsync(string tunnelId)
        //{
        //    var response = await _httpClient.GetAsync(
        //        $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/routes"
        //    );

        //    var responseContent = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception($"Erro ao obter published application routes. Status: {response.StatusCode}, Resposta: {responseContent}");
        //    }

        //    return responseContent; // You can deserialize to a model if you want
        //}

        /// <summary>
        /// Creates a published application route for the specified tunnel with the given hostname, service, and path.
        /// </summary>
        /// <remarks>Ensure that the provided hostname and service are valid and that the tunnel ID refers
        /// to an existing tunnel in your Cloudflare account. The method sends a request to the Cloudflare API and
        /// requires appropriate authentication and permissions.</remarks>
        /// <param name="tunnelId">The unique identifier of the tunnel for which the application route is to be created. This value must
        /// correspond to an existing tunnel.</param>
        /// <param name="hostname">The hostname to associate with the published application route. This value must be a valid DNS hostname.</param>
        /// <param name="service">The service to which the route will direct traffic. Specify the internal service endpoint in the format
        /// required by Cloudflare.</param>
        /// <param name="path">The path pattern for the route. Defaults to "*" to match all paths. Specify a custom path to restrict the
        /// route to a subset of requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
        /// published application route is successfully created; otherwise, an exception is thrown.</returns>
        /// <exception cref="Exception">Thrown if the route creation fails due to an unsuccessful HTTP response. The exception message includes the
        /// status code and response content returned by the server.</exception>
        public async Task<bool> CreatePublishedApplicationRouteAsync(string tunnelId, string hostname, string service, string path = "*")
        {
            var requestBody = new
            {
                hostname = hostname,
                tunnel_id = tunnelId,
                path = path,
                service = service
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] POST {_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations");
            Console.WriteLine($"[{now}] [HTTP] Request Body: {json}");
            var response = await _httpClient.PostAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations",
                content
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao criar published application route. Status: {response.StatusCode}, Resposta: {responseContent}");
            }
            // Optionally, parse and check the response for success
            return true;
        }

        /// <summary>
        /// Asynchronously retrieves the configuration for the specified tunnel identified by its tunnel ID.
        /// </summary>
        /// <remarks>This method makes an HTTP GET request to the Cloudflare API to fetch the tunnel
        /// configuration. Ensure that the tunnel ID provided is valid and that the account has the necessary
        /// permissions to access the configuration.</remarks>
        /// <param name="tunnelId">The unique identifier of the tunnel for which the configuration is being retrieved. This parameter cannot be
        /// null or empty.</param>
        /// <returns>A string containing the configuration details of the specified tunnel. The content is in JSON format.</returns>
        /// <exception cref="Exception">Thrown if the request to retrieve the tunnel configuration fails, indicating the status code and response
        /// content for debugging.</exception>
        public async Task<string> GetTunnelConfigAsync(string tunnelId)
        {
            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] GET {_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations");
            var response = await _httpClient.GetAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations"
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao obter configuração do tunnel. Status: {response.StatusCode}, Resposta: {responseContent}");
            }
            return responseContent; // Optionally deserialize to a model
        }

        /// <summary>
        /// Asynchronously updates the configuration of a specified Cloudflare tunnel.
        /// </summary>
        /// <remarks>This method sends a PUT request to the Cloudflare API to update the configuration of
        /// the specified tunnel. Ensure that the tunnel identifier and configuration request are valid before calling
        /// this method.</remarks>
        /// <param name="tunnelId">The unique identifier of the tunnel to update. Cannot be null or empty.</param>
        /// <param name="tunnelConfigRequest">An object containing the configuration settings to apply to the tunnel. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a TunnelConfigResponse with the
        /// outcome of the update, including success status and updated configuration details.</returns>
        /// <exception cref="Exception">Thrown if the update operation fails, such as when the API returns an unsuccessful response or the tunnel
        /// configuration cannot be updated.</exception>
        public async Task<TunnelConfigResponse> UpdateTunnelConfigAsync(string tunnelId, TunnelConfigRequest tunnelConfigRequest)
        {
            var json = JsonConvert.SerializeObject(tunnelConfigRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] PUT {_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations");
            Console.WriteLine($"[{now}] [HTTP] Request Body: {json}");
            var response = await _httpClient.PutAsync(
                $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations",
                content
            );
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            var result = JsonConvert.DeserializeObject<TunnelConfigResponse>(responseContent);

            if (!result.Success || result.Result == null)
            {
                throw new Exception($"Erro ao atualizar configuração do tunnel. Status: {response.StatusCode}, Resposta: {responseContent}");
            }

            return result;
        }

        /// <summary>
        /// Asynchronously creates a new Access Application in Cloudflare Access with the specified application name, domain, and session duration.
        /// </summary>
        /// <param name="appName">The name of the Access Application to be created. This value is used to identify the application within Cloudflare Access and should be unique within the account.</param>
        /// <param name="domain">The domain associated with the Access Application. This should match the hostname used in the tunnel configuration to ensure proper routing and access control.</param>
        /// <param name="sessionDuration">The duration for which a user's session will remain active after authentication. This value should be specified in a format recognized by Cloudflare Access, such as "24h" for 24 hours. The default value is "24h".</param>
        /// <returns>The response content from the Cloudflare API, which may include details about the created Access Application. You may want to parse this response to extract specific information such as the application ID.</returns>
        /// <exception cref="Exception">Thrown if the request to create the Access Application fails, indicating the status code and response content for debugging purposes.</exception>
        public async Task<string> CreateAccessApplicationAsync(string appName, string domain)
        {
            var request = new CloudflareAccessApplicationRequest
            {
                name = appName,
                domain = domain
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_baseApiUrl}accounts/{_accountId}/access/apps";
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to create Access Application: {responseContent}");
            // You may want to parse and return the application ID from the response
            return responseContent;
        }

        /// <summary>
        /// Creates a new access policy for the specified application asynchronously.
        /// </summary>
        /// <remarks>The method sends a request to the Cloudflare API to create an access policy using a
        /// predefined service token. The returned JSON string contains details about the created policy. Ensure that
        /// the application ID is valid and that the caller has appropriate permissions to create policies.</remarks>
        /// <param name="appId">The unique identifier of the application for which the access policy is to be created. Cannot be null or
        /// empty.</param>
        /// <returns>A JSON string containing the response from the Cloudflare API after creating the access policy.</returns>
        /// <exception cref="Exception">Thrown if the access policy creation fails or the Cloudflare API returns an unsuccessful status code.</exception>
        public async Task<string> CreateAccessPolicyAsync(string appId, string serviceTokenId)
        {
            // Build the request to match the provided JSON structure for a Service Token Policy
            var request = new CloudflareAccessPolicyRequest
            {
                include = new object[]
                {
                    new {
                        service_token = new {
                            token_id = serviceTokenId
                        }
                    }
                }
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_baseApiUrl}accounts/{_accountId}/access/apps/{appId}/policies";
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to create Access Policy: {responseContent}");
            return responseContent;
        }

        /// <summary>
        /// Retrieves the access application details for the specified application identifier asynchronously.
        /// </summary>
        /// <param name="appId">The unique identifier of the access application to retrieve. Cannot be null or empty.</param>
        /// <returns>A string containing the access application details in the response body. The format of the string depends on
        /// the API response.</returns>
        /// <exception cref="Exception">Thrown if the request to retrieve the access application fails or returns a non-success status code.</exception>
        public async Task<string> GetAccessApplicationAsync(string appId)
        {
            var url = $"{_baseApiUrl}accounts/{_accountId}/access/apps/{appId}";
            var now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] GET {url}");
            var response = await _httpClient.GetAsync(url);
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            now = DateTime.Now.ToString(_dateFormat);
            Console.WriteLine($"[{now}] [HTTP] Response Body: {responseContent}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get Access Application: {responseContent}");
            }
            return responseContent;
        }

        //public async Task<string> GetTunnelConfigAsync(string tunnelId)
        //{
        //    var response = await _httpClient.GetAsync(
        //        $"{_baseApiUrl}accounts/{_accountId}/cfd_tunnel/{tunnelId}/configurations"
        //    );

        //    var responseContent = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception($"Erro ao obter configuração do tunnel. Status: {response.StatusCode}, Resposta: {responseContent}");
        //    }

        //    return responseContent; // Optionally deserialize to a model
        //}

        ///// <summary>
        ///// Provisions a new client by creating a secure tunnel, generating a tunnel token, configuring DNS records, and
        ///// installing the required cloudflared service.
        ///// </summary>
        ///// <remarks>This method performs multiple asynchronous operations to fully set up the client,
        ///// including tunnel creation, DNS configuration, and service installation. Ensure that the provided serial is
        ///// valid and that the necessary permissions are in place for tunnel and DNS operations. The method is intended
        ///// for scenarios where automated provisioning and configuration of secure tunnels are required.</remarks>
        ///// <param name="serial">The unique serial identifier for the client to be provisioned. This value is used to generate the tunnel
        ///// name and associated hostname. Cannot be null or empty.</param>
        ///// <returns>A ProvisioningStatus object containing details about the provisioned client, including the serial, hostname,
        ///// tunnel ID, tunnel token, DNS record result, tunnel configuration, creation timestamp, and active status.</returns>
        //public async Task<ProvisioningStatus> ProvisionClientAsync(string serial)
        //{
        //    return await ProvisionClientAsync(serial, "http://localhost:5581");
        //}

        /// <summary>
        /// Provisions a client by creating a secure tunnel, DNS record, and access application for the specified device
        /// serial and service address.
        /// </summary>
        /// <remarks>This method performs multiple asynchronous operations to set up secure access for a
        /// client device, including tunnel creation, DNS configuration, and access policy setup. The returned
        /// ProvisioningStatus provides all relevant identifiers and configuration details for the provisioned client.
        /// The method is not thread-safe; concurrent calls with the same serial may result in conflicts.</remarks>
        /// <param name="serial">The unique serial number of the client device to provision. Used to generate tunnel and hostname
        /// identifiers.</param>
        /// <param name="serviceAddress">The network address of the service to expose through the tunnel. Must be a valid address accessible by the
        /// provisioning system.</param>
        /// <returns>A ProvisioningStatus object containing details about the created tunnel, DNS record, access application, and
        /// provisioning state.</returns>
        public async Task<ProvisioningStatus> ProvisionClientAsync(string serial, string serviceAddress,
            string serviceTokenId)
        {
            var tunnelName = $"{serial}";
            var hostname = $"{serial}.{_domain}";

            Console.WriteLine("[Cloudflare] Creating tunnel...");
            var tunnel = await CreateTunnelAsync(tunnelName, hostname);
            Console.WriteLine($"[Cloudflare] Tunnel created: {tunnel.Id}");

            Console.WriteLine("[Cloudflare] Getting tunnel token...");
            var tunnelToken = await GetTunnelTokenAsync(tunnel.Id);
            Console.WriteLine("[Cloudflare] Tunnel token received.");

            Console.WriteLine("[Cloudflare] Getting tunnel CNAME...");
            var tunnelCname = GetTunnelCname(tunnel.Id);
            Console.WriteLine($"[Cloudflare] Tunnel CNAME: {tunnelCname}");

            Console.WriteLine("[Cloudflare] Creating DNS record...");
            var dnsRecordResult = await CreateDnsRecordAsync(hostname, tunnelCname);
            Console.WriteLine($"[Cloudflare] DNS record created: {dnsRecordResult.Id}");

            Console.WriteLine("[Cloudflare] Updating tunnel config...");
            var tunnelConfig = await UpdateTunnelConfigAsync(tunnel.Id,
                new TunnelConfigRequest 
                {
                    TunnelId = tunnel.Id,
                    Config = new Config
                    {
                        Ingress = new List<Ingress>
                        {
                            new Ingress
                            {
                                Hostname = hostname,
                                Service = serviceAddress
                            },
                            new Ingress
                            {
                                Service = "http_status:404"
                            }
                        }
                    }
                }
            );
            Console.WriteLine("[Cloudflare] Tunnel config updated.");

            Console.WriteLine("[Cloudflare] Creating Access Application...");
            var accessApplication = await CreateAccessApplicationAsync(tunnelName, hostname);
            Console.WriteLine("[Cloudflare] Access Application created.");

            Console.WriteLine("[Cloudflare] Creating Access Policy...");
            var appId = JObject.Parse(accessApplication)["result"]["id"].ToString();
            var accessPolicy = await CreateAccessPolicyAsync(appId, serviceTokenId);
            Console.WriteLine("[Cloudflare] Access Policy created.");

            Console.WriteLine("[Cloudflare] Installing cloudflared service...");
            InstallCloudflaredService(tunnel.Id, tunnelToken);
            Console.WriteLine("[Cloudflare] cloudflared service installed.");

            return new ProvisioningStatus
            {
                Serial = serial,
                Hostname = hostname,
                TunnelId = tunnel.Id,
                TunnelToken = tunnelToken,
                DnsRecord = dnsRecordResult,
                TunnelConfig = tunnelConfig.Result,
                AccessApplication = accessApplication,
                AccessPolicy = accessPolicy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }
    }
}
