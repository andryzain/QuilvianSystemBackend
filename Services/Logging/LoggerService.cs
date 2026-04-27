using System.Text;
using System.Text.Json;

namespace QuilvianSystemBackend.Services.Logging
{
    public class LoggerService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoggerService(
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task InfoAsync(string module, string action, string message, object? data = null)
        {
            return WriteAsync("INFO", module, action, message, null, data);
        }

        public Task WarningAsync(string module, string action, string message, object? data = null)
        {
            return WriteAsync("WARNING", module, action, message, null, data);
        }

        public Task ErrorAsync(string module, string action, string message, Exception? exception = null, object? data = null)
        {
            return WriteAsync("ERROR", module, action, message, exception, data);
        }

        public Task AuditAsync(string module, string action, string message, object? data = null)
        {
            return WriteAsync("AUDIT", module, action, message, null, data);
        }

        private async Task WriteAsync(
            string level,
            string module,
            string action,
            string message,
            Exception? exception,
            object? data)
        {
            var now = DateTime.Now;
            var dateText = now.ToString("dd-MM-yyyy");

            var rootPath = _environment.ContentRootPath;
            var logDirectory = Path.Combine(rootPath, "Logs", module.ToLower());

            Directory.CreateDirectory(logDirectory);

            var filePath = Path.Combine(logDirectory, $"logger-{dateText}.txt");

            var httpContext = _httpContextAccessor.HttpContext;

            var ipAddress = GetIpAddress(httpContext);
            var path = httpContext?.Request.Path.ToString() ?? "-";
            var method = httpContext?.Request.Method ?? "-";
            var host = httpContext?.Request.Host.ToString() ?? "-";
            var scheme = httpContext?.Request.Scheme ?? "-";
            var queryString = httpContext?.Request.QueryString.ToString() ?? "-";

            var userAgent = httpContext?.Request.Headers["User-Agent"].FirstOrDefault() ?? "-";
            var referer = httpContext?.Request.Headers["Referer"].FirstOrDefault() ?? "-";
            var origin = httpContext?.Request.Headers["Origin"].FirstOrDefault() ?? "-";
            var acceptLanguage = httpContext?.Request.Headers["Accept-Language"].FirstOrDefault() ?? "-";

            var browserInfo = ParseBrowser(userAgent);
            var osInfo = ParseOperatingSystem(userAgent);
            var deviceInfo = ParseDevice(userAgent);

            var userId = httpContext?.User?.FindFirst("user_id")?.Value ?? "-";
            var email = httpContext?.User?.FindFirst("email")?.Value ?? "-";
            var username = httpContext?.User?.FindFirst("username")?.Value ?? "-";

            var logBuilder = new StringBuilder();

            logBuilder.AppendLine("==================================================");
            logBuilder.AppendLine($"Time        : {now:yyyy-MM-dd HH:mm:ss}");
            logBuilder.AppendLine($"Level       : {level}");
            logBuilder.AppendLine($"Module      : {module}");
            logBuilder.AppendLine($"Action      : {action}");
            logBuilder.AppendLine($"Message     : {message}");
            logBuilder.AppendLine($"Method      : {method}");
            logBuilder.AppendLine($"Scheme      : {scheme}");
            logBuilder.AppendLine($"Host        : {host}");
            logBuilder.AppendLine($"Path        : {path}");
            logBuilder.AppendLine($"QueryString : {queryString}");
            logBuilder.AppendLine($"IP Address  : {ipAddress}");
            logBuilder.AppendLine($"UserId      : {userId}");
            logBuilder.AppendLine($"Username    : {username}");
            logBuilder.AppendLine($"Email       : {email}");
            logBuilder.AppendLine($"Browser     : {browserInfo}");
            logBuilder.AppendLine($"OS          : {osInfo}");
            logBuilder.AppendLine($"Device      : {deviceInfo}");
            logBuilder.AppendLine($"Language    : {acceptLanguage}");
            logBuilder.AppendLine($"Origin      : {origin}");
            logBuilder.AppendLine($"Referer     : {referer}");
            logBuilder.AppendLine($"User-Agent  : {userAgent}");

            if (data != null)
            {
                logBuilder.AppendLine("Data        :");
                logBuilder.AppendLine(JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }

            if (exception != null)
            {
                logBuilder.AppendLine("Exception   :");
                logBuilder.AppendLine(exception.ToString());
            }

            logBuilder.AppendLine();

            await File.AppendAllTextAsync(filePath, logBuilder.ToString());
        }

        private static string GetIpAddress(HttpContext? httpContext)
        {
            if (httpContext == null)
            {
                return "-";
            }

            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }

            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
        }

        private static string ParseBrowser(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent) || userAgent == "-")
            {
                return "-";
            }

            if (userAgent.Contains("Edg/"))
            {
                return "Microsoft Edge";
            }

            if (userAgent.Contains("OPR/") || userAgent.Contains("Opera"))
            {
                return "Opera";
            }

            if (userAgent.Contains("Chrome/") && !userAgent.Contains("Edg/"))
            {
                return "Google Chrome";
            }

            if (userAgent.Contains("Firefox/"))
            {
                return "Mozilla Firefox";
            }

            if (userAgent.Contains("Safari/") && !userAgent.Contains("Chrome/"))
            {
                return "Safari";
            }

            if (userAgent.Contains("PostmanRuntime"))
            {
                return "Postman";
            }

            if (userAgent.Contains("Swagger"))
            {
                return "Swagger";
            }

            return "Unknown Browser";
        }

        private static string ParseOperatingSystem(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent) || userAgent == "-")
            {
                return "-";
            }

            if (userAgent.Contains("Windows NT 10.0"))
            {
                return "Windows 10 / Windows 11";
            }

            if (userAgent.Contains("Windows"))
            {
                return "Windows";
            }

            if (userAgent.Contains("Android"))
            {
                return "Android";
            }

            if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            {
                return "iOS / iPadOS";
            }

            if (userAgent.Contains("Mac OS X"))
            {
                return "macOS";
            }

            if (userAgent.Contains("Linux"))
            {
                return "Linux";
            }

            return "Unknown OS";
        }

        private static string ParseDevice(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent) || userAgent == "-")
            {
                return "-";
            }

            if (userAgent.Contains("Mobile") || userAgent.Contains("Android") || userAgent.Contains("iPhone"))
            {
                return "Mobile";
            }

            if (userAgent.Contains("iPad") || userAgent.Contains("Tablet"))
            {
                return "Tablet";
            }

            return "Desktop";
        }
    }
}