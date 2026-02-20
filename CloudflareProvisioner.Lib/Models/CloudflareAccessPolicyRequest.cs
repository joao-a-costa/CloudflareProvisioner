namespace CloudflareProvisioner.Lib.Models
{
    public class CloudflareAccessPolicyRequest
    {
        public string name { get; set; } = "Service Token Policy";
        public int precedence { get; set; } = 1; // <-- int, not string
        public string decision { get; set; } = "non_identity";
        public object[] include { get; set; } = new object[] { new { everyone = true } };
    }
}