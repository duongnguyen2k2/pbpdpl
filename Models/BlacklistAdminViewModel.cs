using System;

namespace API.Models
{
    public class BlacklistAdminViewModel
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int? ClearedCount { get; set; }
        public string Ip { get; set; }
        public DateTime? Expiry { get; set; }
        public DateTime Timestamp { get; set; }
        public string Secret { get; set; }
        public string Error { get; set; }
    }
}
