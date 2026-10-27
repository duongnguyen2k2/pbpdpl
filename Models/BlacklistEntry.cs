namespace API.Models
{
    using Microsoft.Extensions.Caching.Memory;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class BlacklistEntry
    {
        public string Ip { get; set; }
        public DateTime Expiry { get; set; } = DateTime.UtcNow.AddDays(1); // 1 ngày
    }

    public class BlacklistOptions
    {
        public const string SectionName = "Blacklist";

        public string AdminSecret { get; set; } = "PortalPhuongXaBlacklistAdmin2025SuperSecureKey$%^";

        // Có thể mở rộng sau này
        public int ResetBlacklistMaxAttemptsPerHour { get; set; } = 5;
        public int AddIpMaxAttemptsPerHour { get; set; } = 10;
    }

    public static class BlacklistService
    {
        private static readonly ConcurrentDictionary<string, BlacklistEntry> _blacklist = new();
        private const string BlacklistFolder = "wwwroot/uploads"; // Thư mục mới
        private static readonly string BlacklistFile = Path.Combine(BlacklistFolder, "blacklist.json");
        private static readonly TimeSpan _countWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan _blacklistDuration = TimeSpan.FromDays(1);
        // Tăng ngưỡng blacklist cho môi trường dùng chung IP public (~25x).
        private static readonly int _defaultRejectThreshold = 60;
        private static readonly IReadOnlyDictionary<string, int> _policyRejectThresholds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin_login"] = 50,
            ["contact"] = 60
        };
        public static IMemoryCache Cache { get; set; } // Inject từ DI

        public static void LoadFromFile()
        {
            // Tạo thư mục nếu chưa tồn tại
            Directory.CreateDirectory(BlacklistFolder);

            if (!File.Exists(BlacklistFile))
            {
                // File chưa tồn tại → tạo file rỗng và thoát
                File.WriteAllText(BlacklistFile, "[]"); // mảng rỗng hợp lệ
                return;
            }

            var json = File.ReadAllText(BlacklistFile).Trim();

            // Kiểm tra nếu file rỗng hoặc chỉ chứa khoảng trắng
            if (string.IsNullOrWhiteSpace(json))
            {
                File.WriteAllText(BlacklistFile, "[]"); // reset về mảng rỗng
                return;
            }

            try
            {
                var entries = JsonSerializer.Deserialize<List<BlacklistEntry>>(json);
                if (entries != null)
                {
                    foreach (var entry in entries.Where(e => e?.Expiry > DateTime.UtcNow))
                    {
                        if (!string.IsNullOrWhiteSpace(entry?.Ip))
                        {
                            _blacklist.TryAdd(entry.Ip, entry);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                // Log lỗi (có thể dùng ILogger nếu có, hoặc Console)
                Console.WriteLine($"Lỗi đọc blacklist.json: {ex.Message}. Reset file về rỗng.");

                // Reset file về mảng rỗng để tránh lỗi lặp lại
                File.WriteAllText(BlacklistFile, "[]");
            }
        }

        public static void SaveToFile()
        {
            // Tạo thư mục nếu chưa có
            Directory.CreateDirectory(BlacklistFolder);

            var entries = _blacklist.Values.ToList();
            var json = JsonSerializer.Serialize(entries);
            File.WriteAllText(BlacklistFile, json);
        }

        public static bool IsBlacklisted(string ip)
        {
            if (_blacklist.TryGetValue(ip, out var entry) && entry.Expiry > DateTime.UtcNow)
                return true;

            _blacklist.TryRemove(ip, out _);
            return false;
        }

        public static void AddReject(string ip, string policyName = "")
        {
            if (Cache == null || string.IsNullOrWhiteSpace(ip) || ip == "unknown")
                return;

            policyName = string.IsNullOrWhiteSpace(policyName) ? "default" : policyName.Trim().ToLowerInvariant();
            var cacheKey = $"reject_{policyName}_{ip}";
            var rejectCount = Cache.TryGetValue(cacheKey, out int currentCount) ? currentCount + 1 : 1;

            // Đếm reject trong cửa sổ thời gian và tự reset khi hết hạn.
            Cache.Set(cacheKey, rejectCount, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _countWindow
            });

            var threshold = _policyRejectThresholds.TryGetValue(policyName, out var policyThreshold)
                ? policyThreshold
                : _defaultRejectThreshold;

            // Nếu vượt ngưỡng trong cửa sổ thời gian -> blacklist tạm thời.
            if (rejectCount >= threshold)
            {
                _blacklist[ip] = new BlacklistEntry
                {
                    Ip = ip,
                    Expiry = DateTime.UtcNow.Add(_blacklistDuration)
                };
                SaveToFile();
                Cache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Reset toàn bộ blacklist: xóa memory, file, trả về số lượng đã xóa.
        /// Reject caches tự expire sau 10 phút.
        /// </summary>
        public static int ClearAll()
        {
            int clearedCount = _blacklist.Count;
            _blacklist.Clear();
            Directory.CreateDirectory(BlacklistFolder);
            File.WriteAllText(BlacklistFile, "[]");
            return clearedCount;
        }

        /// <summary>
        /// Manually add single IP to blacklist (admin).
        /// Returns the entry.
        /// </summary>
        public static BlacklistEntry AddIpManually(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return null;

            var entry = new BlacklistEntry
            {
                Ip = ip,
                Expiry = DateTime.UtcNow.Add(_blacklistDuration)
            };
            _blacklist[ip] = entry;
            SaveToFile();
            return entry;
        }
    }
}
