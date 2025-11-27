using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Application.Interfaces.Services;
using Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Her kullanımda yeni SmtpClient oluştur
        private SmtpClient CreateSmtpClient()
        {
            return new SmtpClient
            {
                Host = _configuration["EmailSettings:SmtpHost"],
                Port = int.Parse(_configuration["EmailSettings:SmtpPort"]),
                EnableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"]),
                Credentials = new NetworkCredential(
                    _configuration["EmailSettings:Username"],
                    _configuration["EmailSettings:Password"]
                ),
                DeliveryMethod = SmtpDeliveryMethod.Network, 
                Timeout = 30000 // 30 saniye
            };
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            await SendEmailAsync(new EmailMessage
            {
                To = to,
                Subject = subject,
                Body = body,
                IsHtml = true
            });
        }

        public async Task SendEmailAsync(EmailMessage message)
        {
            try
            {
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        _configuration["EmailSettings:FromEmail"],
                        _configuration["EmailSettings:FromName"]
                    ),
                    Subject = message.Subject,
                    Body = message.Body,
                    IsBodyHtml = message.IsHtml
                };

                mailMessage.To.Add(message.To);

                // CC ekle
                if (message.Cc?.Any() == true)
                {
                    foreach (var cc in message.Cc)
                        mailMessage.CC.Add(cc);
                }

                // BCC ekle
                if (message.Bcc?.Any() == true)
                {
                    foreach (var bcc in message.Bcc)
                        mailMessage.Bcc.Add(bcc);
                }

                // Attachments ekle
                if (message.Attachments?.Any() == true)
                {
                    foreach (var attachment in message.Attachments)
                    {
                        var stream = new MemoryStream(attachment.Value);
                        mailMessage.Attachments.Add(new Attachment(stream, attachment.Key));
                    }
                }

                // Her gönderimde yeni SmtpClient
                using var smtpClient = CreateSmtpClient();
                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("✅ Email gönderildi: To={To}, Subject={Subject}",
                    message.To, message.Subject);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "❌ SMTP Hatası: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Email gönderilemedi: To={To}, Subject={Subject}",
                    message.To, message.Subject);
                throw;
            }
        }

        public async Task SendBulkEmailAsync(List<EmailMessage> messages)
        {
            await SendBulkEmailAsync(new BulkEmailRequest
            {
                Messages = messages,
                SendAsParallel = false,
                BatchSize = 10
            });
        }

        public async Task SendBulkEmailAsync(BulkEmailRequest request)
        {
            _logger.LogInformation("📧 Bulk email gönderimi başladı. Toplam: {Count}",
                request.Messages.Count);

            var successCount = 0;
            var failedEmails = new List<string>();

            if (request.SendAsParallel)
            {
                var tasks = request.Messages
                    .Select(async message =>
                    {
                        try
                        {
                            await SendEmailAsync(message);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            failedEmails.Add(message.To);
                            _logger.LogError(ex, "❌ Bulk email hatası: {To}", message.To);
                        }
                    });

                await Task.WhenAll(tasks);
            }
            else
            {
                // Sıralı gönderim (batch'ler halinde)
                foreach (var batch in request.Messages.Chunk(request.BatchSize))
                {
                    foreach (var message in batch)
                    {
                        try
                        {
                            await SendEmailAsync(message);
                            successCount++;

                            // Rate limiting için kısa bekleme
                            await Task.Delay(3000);
                        }
                        catch (Exception ex)
                        {
                            failedEmails.Add(message.To);
                            _logger.LogError(ex, "❌ Bulk email hatası: {To}", message.To);
                        }
                    }

                    // Batch'ler arası bekleme
                    await Task.Delay(5000);
                }
            }

            _logger.LogInformation(
                " Bulk email tamamlandı. Başarılı: {Success}, Başarısız: {Failed}",
                successCount, failedEmails.Count);

            if (failedEmails.Any())
            {
                _logger.LogWarning(" Başarısız emailler: {Emails}",
                    string.Join(", ", failedEmails));
            }
        }

        public async Task SendTemplateEmailAsync(string to, string templateName, object model)
        {
            var template = await LoadEmailTemplate(templateName);
            var body = ReplaceTemplatePlaceholders(template, model);

            await SendEmailAsync(to, GetTemplateSubject(templateName), body);
        }

        private async Task<string> LoadEmailTemplate(string templateName)
        {
            var templatePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "EmailTemplates",
                $"{templateName}.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogError("❌ Email template bulunamadı: {Template}", templateName);
                throw new FileNotFoundException($"Email template not found: {templateName}");
            }

            return await File.ReadAllTextAsync(templatePath);
        }

        private string ReplaceTemplatePlaceholders(string template, object model)
        {
            var properties = model.GetType().GetProperties();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(model)?.ToString() ?? "";
                template = template.Replace($"{{{{{prop.Name}}}}}", value);
            }

            return template;
        }

        private string GetTemplateSubject(string templateName)
        {
            return templateName switch
            {
                "OverdueBook" => "📚 Gecikmiş Kitap İadesi Hatırlatması",
                "BookReserved" => "📖 Kitap Rezervasyonunuz Onaylandı",
                "Welcome" => "👋 Kütüphanemize Hoş Geldiniz",
                _ => "Kütüphane Bildirimi"
            };
        }

        public void Dispose()
        {
            // SmtpClient artık her kullanımda oluşturuluyor, dispose'a gerek yok
        }
    }
}