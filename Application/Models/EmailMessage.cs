using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models
{
    public class EmailMessage
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }
        public Dictionary<string, byte[]>? Attachments { get; set; }
        public bool IsHtml { get; set; } = false;
    }
    // Bulk email için
    public class BulkEmailRequest
    {
        public List<EmailMessage> Messages { get; set; } = new();
        public bool SendAsParallel { get; set; } = false;
        public int BatchSize { get; set; } = 10;
    }
}
