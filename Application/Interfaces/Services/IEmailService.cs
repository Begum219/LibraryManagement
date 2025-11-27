using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Models;

namespace Application.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(EmailMessage message);
        Task SendBulkEmailAsync(List<EmailMessage> messages);
        Task SendBulkEmailAsync(BulkEmailRequest request);
        // Template based email
        Task SendTemplateEmailAsync(string to, string templateName, object model);
    }

   
}
