using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Services
{
    /// <summary>
    /// سمتِ انتخاب‌شده در «نشست» نگه داشته می‌شود و به شناسه‌ی کاربر بسته است («userId|Owner»)؛
    /// اگر کاربر دیگری در همان مرورگر وارد شود، مقدارِ کاربر قبلی بی‌اثر است.
    /// با خروج (SignOut) پاک می‌شود (رویداد کوکی در Program.cs).
    /// </summary>
    public class SessionLoginCapacityAccessor : ILoginCapacityAccessor
    {
        private const string Key = "LoginCapacity";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionLoginCapacityAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public LoginCapacity? GetChosen(string userId)
        {
            var raw = Session()?.GetString(Key);
            if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(userId))
                return null;

            var parts = raw.Split('|', 2);

            if (parts.Length == 2
                && parts[0] == userId
                && Enum.TryParse<LoginCapacity>(parts[1], out var capacity)
                && Enum.IsDefined(capacity))
            {
                return capacity;
            }

            return null;
        }

        public void Set(string userId, LoginCapacity capacity)
            => Session()?.SetString(Key, $"{userId}|{capacity}");

        public void Clear()
            => Session()?.Remove(Key);

        private ISession? Session()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.Session;
            }
            catch (InvalidOperationException)
            {
                // Session برای این درخواست فعال نیست
                return null;
            }
        }
    }
}
