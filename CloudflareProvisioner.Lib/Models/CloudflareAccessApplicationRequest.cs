namespace CloudflareProvisioner.Lib.Models
{
    public class CloudflareAccessApplicationRequest
    {
        public string name { get; set; }
        public string domain { get; set; }
        public string type { get; set; } = "self_hosted";
        public string session_duration { get; set; } = "24h";
    }
}