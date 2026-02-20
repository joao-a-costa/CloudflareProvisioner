using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CloudflareProvisioner.Lib.Models
{
    public class CloudflareTunnelRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("config")]
        public TunnelConfig Config { get; set; }
    }

    public class TunnelConfig
    {
        [JsonProperty("ingress")]
        public List<IngressRule> Ingress { get; set; }
    }

    public class IngressRule
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("service")]
        public string Service { get; set; }
    }

    public class CloudflareTunnelResponse
    {
        [JsonProperty("result")]
        public TunnelResult Result { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class TunnelResult
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("config")]
        public TunnelConfig Config { get; set; }
    }

    public class TunnelTokenResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class DnsRecordRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("ttl")]
        public int Ttl { get; set; } = 1; // Auto

        [JsonProperty("proxied")]
        public bool Proxied { get; set; } = true; // True
    }

    public class DnsRecordResponse
    {
        [JsonProperty("result")]
        public DnsRecordResult Result { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class DnsRecordResult
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    public class EnrollmentRequest
    {
        public string Serial { get; set; }
        public string EnrollmentCode { get; set; }
        public string Fingerprint { get; set; }
    }

    public class EnrollmentResponse
    {
        public string Hostname { get; set; }
        public string TunnelToken { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class ProvisioningStatus
    {
        public string Serial { get; set; }
        public string Hostname { get; set; }
        public string TunnelId { get; set; }
        public string TunnelToken { get; set; }
        public string AccessApplication { get; set; }
        public string AccessPolicy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public DnsRecordResult DnsRecord { get; set; }
        public TunnelConfigResponseResult TunnelConfig { get; set; }
    }

    public class Config
    {
        [JsonProperty("ingress")]
        public List<Ingress> Ingress { get; set; }

        [JsonProperty("warp-routing")]
        public WarpRouting WarpRouting { get; set; }
    }

    public class Ingress
    {
        [JsonProperty("service")]
        public string Service { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("originRequest")]
        public OriginRequest OriginRequest { get; set; }
    }

    public class OriginRequest
    {
        [JsonProperty("tlsTimeout")]
        public int? TlsTimeout { get; set; } = 0;

        [JsonProperty("proxyAddress")]
        public string ProxyAddress { get; set; } = "";

        [JsonProperty("tcpKeepAlive")]
        public int? TcpKeepAlive { get; set; } = 0;

        [JsonProperty("connectTimeout")]
        public int? ConnectTimeout { get; set; } = 0;

        [JsonProperty("keepAliveTimeout")]
        public int? KeepAliveTimeout { get; set; } = 0;

        [JsonProperty("keepAliveConnections")]
        public int? KeepAliveConnections { get; set; } = 0;
    }

    public class TunnelConfigRequest
    {
        [JsonProperty("tunnel_id")]
        public string TunnelId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("config")]
        public Config Config { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = "cloudflare";

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class WarpRouting
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    public class TunnelConfigResponseResult
    {
        [JsonProperty("tunnel_id")]
        public string TunnelId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("config")]
        public Config Config { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class TunnelConfigResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("errors")]
        public List<object> Errors { get; set; }

        [JsonProperty("messages")]
        public List<object> Messages { get; set; }

        [JsonProperty("result")]
        public TunnelConfigResponseResult Result { get; set; }
    }
}
