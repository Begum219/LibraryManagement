using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces.Services;
using Application.Models;

namespace Infrastructure.Jobs
{
    public class OverdueBookJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly ILogger<OverdueBookJob> _logger;

        public OverdueBookJob(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ILogger<OverdueBookJob> logger)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
        }





        public async Task SendOverdueNotifications()
        {
            _logger.LogInformation("🔔 Hangfire: Gecikmiş kitap bildirimleri başladı - {Time}", DateTime.Now);

            try
            {
                var overdueLoans = await _unitOfWork.Loans.GetOverdueLoansAsync();

                _logger.LogInformation("📚 Toplam {Count} gecikmiş ödünç bulundu", overdueLoans.Count());

                var emailMessages = new List<EmailMessage>();

                foreach (var loan in overdueLoans)
                {
                    try
                    {
                        var user = await _unitOfWork.Users.GetByIdAsync(loan.UserId ?? 0);
                        var book = await _unitOfWork.Books.GetByIdAsync(loan.BookId ?? 0);

                        if (user != null && book != null && !string.IsNullOrEmpty(user.Email))
                        {
                            var daysOverdue = (DateTime.Now - loan.DueDate).Days;

                            emailMessages.Add(new EmailMessage
                            {
                                To = user.Email,
                                Subject = "📚 Gecikmiş Kitap İadesi Hatırlatması",
                                Body = GenerateOverdueEmailContent(user.FullName, book.Title, loan.DueDate, daysOverdue),
                                IsHtml = true
                            });

                            _logger.LogInformation(
                                "✉️ Email hazırlandı: {UserName} - {BookTitle} ({DaysOverdue} gün gecikti)",
                                user.FullName, book.Title, daysOverdue
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Email hazırlanamadı: LoanId={LoanId}", loan.Id);
                    }
                }

                // Toplu email gönderimi
                if (emailMessages.Any())
                {
                    await _emailService.SendBulkEmailAsync(new BulkEmailRequest
                    {
                        Messages = emailMessages,
                        SendAsParallel = false,
                        BatchSize = 1
                    });

                    _logger.LogInformation("✅ Toplu e-posta gönderimi tamamlandı. Gönderilen sayı: {Count}", emailMessages.Count);
                }
                else
                {
                    _logger.LogInformation("ℹ️ Gönderilecek email bulunamadı");
                }

                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Gecikmiş kitap bildirimleri gönderilirken HATA oluştu");
                throw; // Hangfire retry için
            }
        }

        private string GenerateOverdueEmailContent(string userName, string bookTitle, DateTime dueDate, int daysOverdue)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 30px auto; background: white; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ padding: 30px; }}
        .info-box {{ background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #dc3545; }}
        .info-row {{ margin: 10px 0; font-size: 15px; }}
        .label {{ font-weight: bold; color: #495057; }}
        .value {{ color: #212529; }}
        .warning {{ color: #dc3545; font-weight: bold; font-size: 18px; }}
        .footer {{ background-color: #f8f9fa; padding: 20px; text-align: center; color: #6c757d; font-size: 13px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; background-color: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>📚 Kütüphane Bildirimi</h1>
            <p style='margin: 10px 0 0 0; opacity: 0.9;'>Gecikmiş Kitap İadesi Hatırlatması</p>
        </div>
        
        <div class='content'>
            <h2 style='color: #333;'>Merhaba {userName},</h2>
            
            <p style='color: #666; line-height: 1.6;'>
                Ödünç aldığınız kitabın iade tarihi geçmiş durumdadır. 
                Lütfen en kısa sürede kitabı iade ediniz.
            </p>
            
            <div class='info-box'>
                <div class='info-row'>
                    <span class='label'>📖 Kitap Adı:</span>
                    <span class='value'>{bookTitle}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>📅 İade Tarihi:</span>
                    <span class='value'>{dueDate:dd MMMM yyyy}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>⏰ Gecikme Süresi:</span>
                    <span class='warning'>{daysOverdue} gün</span>
                </div>
                <div class='info-row'>
                    <span class='label'>💰 Gecikme Cezası:</span>
                    <span class='value'>{daysOverdue * 1} TL</span>
                </div>
            </div>
            
            <p style='color: #666; line-height: 1.6;'>
                Kitabınızı iade etmek için en yakın kütüphane şubemize başvurabilirsiniz.
            </p>
        </div>
        
        <div class='footer'>
            <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
            <p style='margin: 5px 0;'>Lütfen bu mesajı yanıtlamayınız.</p>
            <p style='margin: 15px 0 5px 0; font-weight: bold;'>Kütüphane Yönetim Sistemi</p>
            <p style='margin: 5px 0;'>© 2024 - Tüm hakları saklıdır</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}