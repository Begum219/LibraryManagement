using Domain.Entities;

namespace Application.Extensions
{
    public static class MaskExtensions
    {
        public static string MaskName(this string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return fullName;

            var parts = fullName.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length > 1)
                    parts[i] = p[0] + new string('*', p.Length - 1);
            }
            return string.Join(' ', parts);
        }

        public static string MaskEmail(this string email)
        {
            if (string.IsNullOrEmpty(email)) return email;

            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 1)
                return "*" + "@" + domain;

            return name[0] + new string('*', name.Length - 1) + "@" + domain;
        }
    }
}
