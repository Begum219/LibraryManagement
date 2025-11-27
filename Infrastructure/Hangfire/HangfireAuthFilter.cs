using Hangfire.Dashboard; // ✅ EKLENDI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Hangfire
{
    public class HangfireAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            
            // Sadece Admin kullanıcılar erişebilmeli onu değiştiricem sonra

           
            return true;

           
            // var httpContext = context.GetHttpContext();
            // return httpContext.User.IsInRole("Admin");
        }
    }
}