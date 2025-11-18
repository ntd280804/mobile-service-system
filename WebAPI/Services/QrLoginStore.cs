using System;
using System.Collections.Concurrent;

namespace WebAPI.Services
{
    public class QrLoginStore
    {
        private readonly ConcurrentDictionary<string, QrLoginSession> _sessions = new();
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(2);
        private readonly Random _random = new();

        public QrLoginSession CreateSession()
        {
            var id = Guid.NewGuid().ToString("N");
            var code = GenerateCode(8);
            var now = DateTime.UtcNow;

            var session = new QrLoginSession
            {
                Id = id,
                Code = code,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(_ttl),
                Status = QrLoginStatus.Pending
            };

            _sessions[id] = session;
            return session;
        }

        public QrLoginSession? GetById(string id)
        {
            if (id == null) return null;
            _sessions.TryGetValue(id, out var session);
            TouchExpiration(session);
            return session;
        }

        public QrLoginSession? GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            foreach (var kv in _sessions)
            {
                if (string.Equals(kv.Value.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    TouchExpiration(kv.Value);
                    return kv.Value;
                }
            }

            return null;
        }

        private void TouchExpiration(QrLoginSession? session)
        {
            if (session == null) return;
            if (session.Status == QrLoginStatus.Confirmed) return;

            if (DateTime.UtcNow > session.ExpiresAtUtc)
            {
                session.Status = QrLoginStatus.Expired;
            }
        }

        private string GenerateCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = chars[_random.Next(chars.Length)];
            }
            return new string(buffer);
        }
    }

    public class QrLoginSession
    {
        public string Id { get; set; } = default!;
        public string Code { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public QrLoginStatus Status { get; set; }

        public string? Username { get; set; }
        public string? Roles { get; set; }

        // Token + session dành cho Web sau khi confirm
        public string? WebToken { get; set; }
        public string? WebSessionId { get; set; }
    }

    public enum QrLoginStatus
    {
        Pending = 0,
        Confirmed = 1,
        Expired = 2
    }
}


