using System;
using System.Collections.Concurrent;

using WebAPI.Models.Auth;

namespace WebAPI.Services
{
    public class WebToMobileQrStore
    {
        private readonly ConcurrentDictionary<string, WebToMobileQrSession> _sessions = new();
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(2);
        private readonly Random _random = new();

        public WebToMobileQrSession Create(string sourceUsername, string sourceRoles, string? sourcePlatform)
        {
            var id = Guid.NewGuid().ToString("N");
            var code = GenerateCode(8);
            var now = DateTime.UtcNow;

            var session = new WebToMobileQrSession
            {
                Id = id,
                Code = code,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(_ttl),
                Status = WebToMobileQrStatus.Pending,
                SourceUsername = sourceUsername,
                SourceRoles = sourceRoles ?? string.Empty,
                SourcePlatform = sourcePlatform ?? "WEB",
                TargetPlatform = "MOBILE"
            };

            _sessions[id] = session;
            return session;
        }

        public WebToMobileQrSession? GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (_sessions.TryGetValue(id, out var session))
            {
                TouchExpiration(session);
                return session;
            }
            return null;
        }

        public WebToMobileQrSession? GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            foreach (var session in _sessions.Values)
            {
                if (string.Equals(session.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    TouchExpiration(session);
                    return session;
                }
            }

            return null;
        }

        private void TouchExpiration(WebToMobileQrSession session)
        {
            if (session.Status == WebToMobileQrStatus.Confirmed) return;
            if (DateTime.UtcNow > session.ExpiresAtUtc)
            {
                session.Status = WebToMobileQrStatus.Expired;
            }
        }

        public void MarkConfirmed(WebToMobileQrSession session, string token, string sessionId, string roles)
        {
            session.Status = WebToMobileQrStatus.Confirmed;
            session.MobileToken = token;
            session.MobileSessionId = sessionId;
            session.MobileRoles = roles;
        }

        private string GenerateCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<char> buffer = stackalloc char[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = chars[_random.Next(chars.Length)];
            }
            return new string(buffer);
        }
    }

    public class WebToMobileQrSession
    {
        public string Id { get; set; } = default!;
        public string Code { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public WebToMobileQrStatus Status { get; set; }

        public string SourceUsername { get; set; } = default!;
        public string SourceRoles { get; set; } = string.Empty;
        public string SourcePlatform { get; set; } = "WEB";
        public string TargetPlatform { get; set; } = "MOBILE";

        public string? MobileToken { get; set; }
        public string? MobileSessionId { get; set; }
        public string? MobileRoles { get; set; }
    }

    // enum defined in WebAPI.Models.Auth
}

