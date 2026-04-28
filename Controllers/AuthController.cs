using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using QuilvianSystemBackend.Constants;
using QuilvianSystemBackend.DTOs.Auth;
using QuilvianSystemBackend.Models;
using QuilvianSystemBackend.Responses;
using QuilvianSystemBackend.Services.Language;
using QuilvianSystemBackend.Services.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuilvianSystemBackend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly LanguageService _languageService;
        private readonly LoggerService _loggerService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            LanguageService languageService,
            LoggerService loggerService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _environment = environment;
            _languageService = languageService;
            _loggerService = loggerService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApiResponse<LoginDataResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Email kosong."
                );

                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest,
                    _languageService.GetMessage(MessageKeys.AuthEmailRequired)
                ));
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Password kosong.",
                    new
                    {
                        request.Email
                    }
                );

                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest,
                    _languageService.GetMessage(MessageKeys.AuthPasswordRequired)
                ));
            }

            var email = request.Email.Trim();

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Email tidak ditemukan.",
                    new
                    {
                        Email = email
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthInvalidCredential)
                ));
            }

            if (!user.IsActive)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Akun tidak aktif.",
                    new
                    {
                        UserId = user.Id,
                        Username = user.UserName,
                        Email = user.Email
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccountInactive)
                ));
            }

            if (user.AccessValidUntil.HasValue && user.AccessValidUntil.Value < DateTime.UtcNow)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Masa akses akun sudah berakhir.",
                    new
                    {
                        user.Id,
                        user.Email,
                        user.UserName,
                        user.AccessValidUntil
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccessExpired)
                ));
            }

            var signInResult = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true
            );

            if (signInResult.IsLockedOut)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Akun terkunci.",
                    new
                    {
                        user.Id,
                        user.Email,
                        user.UserName
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccountLocked)
                ));
            }

            if (!signInResult.Succeeded)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Login",
                    "Login gagal. Password salah.",
                    new
                    {
                        UserId = user.Id,
                        Username = user.UserName,
                        Email = user.Email
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthInvalidCredential)
                ));
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles);

            SetAuthCookie(token);

            await _loggerService.InfoAsync(
                "Auth",
                "Login",
                "Login berhasil.",
                new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    Email = user.Email
                }
            );

            return Ok(ApiResponse<LoginDataResponse>.Ok(
                new LoginDataResponse
                {
                    Auth = BuildAuthInfoResponse(),
                    Endpoints = new AuthEndpointResponse(),
                    User = BuildUserResponse(user, roles)
                },
                _languageService.GetMessage(MessageKeys.AuthLoginSuccess)
            ));
        }

        [HttpGet("me")]
        [Authorize]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApiResponse<UserLoginResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdText, out var userId))
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Me",
                    "Gagal mengambil profile. Token tidak valid.",
                    new
                    {
                        UserIdText = userIdText
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthTokenInvalid)
                ));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Me",
                    "Gagal mengambil profile. User tidak ditemukan.",
                    new
                    {
                        UserId = userId
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthUserNotFound)
                ));
            }

            if (!user.IsActive)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Me",
                    "Gagal mengambil profile. Akun tidak aktif.",
                    new
                    {
                        user.Id,
                        user.Email,
                        user.UserName
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccountInactive)
                ));
            }

            var roles = await _userManager.GetRolesAsync(user);

            await _loggerService.InfoAsync(
                "Auth",
                "Me",
                "Profile user berhasil diambil.",
                new
                {
                    user.Id,
                    user.Email,
                    user.UserName,
                    user.FullName,
                    user.UserType,
                    user.DepartmentId,
                    user.PositionId,
                    Roles = roles
                }
            );

            return Ok(ApiResponse<UserLoginResponse>.Ok(
                BuildUserResponse(user, roles),
                "User profile berhasil diambil."
            ));
        }

        [HttpPost("refresh")]
        [Authorize]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApiResponse<LoginDataResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdText, out var userId))
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Refresh",
                    "Gagal refresh session. Session tidak valid.",
                    new
                    {
                        UserIdText = userIdText
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthSessionInvalid)
                ));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Refresh",
                    "Gagal refresh session. User tidak ditemukan.",
                    new
                    {
                        UserId = userId
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthUserNotFound)
                ));
            }

            if (!user.IsActive)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Refresh",
                    "Gagal refresh session. Akun tidak aktif.",
                    new
                    {
                        user.Id,
                        user.Email,
                        user.UserName
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccountInactive)
                ));
            }

            if (user.AccessValidUntil.HasValue && user.AccessValidUntil.Value < DateTime.UtcNow)
            {
                await _loggerService.WarningAsync(
                    "Auth",
                    "Refresh",
                    "Gagal refresh session. Masa akses akun sudah berakhir.",
                    new
                    {
                        user.Id,
                        user.Email,
                        user.UserName,
                        user.AccessValidUntil
                    }
                );

                return Unauthorized(ApiResponse<object>.Fail(
                    StatusCodes.Status401Unauthorized,
                    _languageService.GetMessage(MessageKeys.AuthAccessExpired)
                ));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var newToken = GenerateJwtToken(user, roles);

            SetAuthCookie(newToken);

            await _loggerService.InfoAsync(
                "Auth",
                "Refresh",
                "Session diperpanjang.",
                new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    Email = user.Email
                }
            );

            return Ok(ApiResponse<LoginDataResponse>.Ok(
                new LoginDataResponse
                {
                    Auth = BuildAuthInfoResponse(),
                    Endpoints = new AuthEndpointResponse(),
                    User = BuildUserResponse(user, roles)
                },
                _languageService.GetMessage(MessageKeys.AuthSessionRefreshed)
            ));
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout()
        {
            var userId =
                User.FindFirstValue("user_id") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            var username =
                User.FindFirstValue("username") ??
                User.FindFirstValue(ClaimTypes.Name);

            var email =
                User.FindFirstValue("email") ??
                User.FindFirstValue(ClaimTypes.Email);

            ClearAuthCookie();

            await _loggerService.InfoAsync(
                "Auth",
                "Logout",
                "Logout berhasil.",
                new
                {
                    UserId = userId,
                    Username = username,
                    Email = email
                }
            );

            return Ok(ApiResponse<object>.Ok(
                null,
                _languageService.GetMessage(MessageKeys.AuthLogoutSuccess)
            ));
        }

        private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException("Jwt:Key belum dikonfigurasi.");
            }

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            );

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),

                new Claim("user_id", user.Id.ToString()),
                new Claim("username", user.UserName ?? string.Empty),
                new Claim("email", user.Email ?? string.Empty),
                new Claim("full_name", user.FullName),
                new Claim("user_type", user.UserType.ToString()),
                new Claim("hospital_id", user.HospitalId?.ToString() ?? string.Empty),
                new Claim("department_id", user.DepartmentId?.ToString() ?? string.Empty),
                new Claim("position_id", user.PositionId?.ToString() ?? string.Empty),

                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }

            var expires = DateTime.UtcNow.AddMinutes(GetJwtExpireMinutes());

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private int GetJwtExpireMinutes()
        {
            var expireMinutes = _configuration.GetValue<int>("Jwt:ExpireMinutes");

            return expireMinutes <= 0 ? 60 : expireMinutes;
        }

        private void SetAuthCookie(string token)
        {
            var cookieName = _configuration["Jwt:CookieName"] ?? "quilvian_access_token";
            var expireMinutes = GetJwtExpireMinutes();

            Response.Cookies.Append(cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = _environment.IsDevelopment()
                    ? SameSiteMode.Lax
                    : SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddMinutes(expireMinutes),
                MaxAge = TimeSpan.FromMinutes(expireMinutes),
                Path = "/"
            });
        }

        private void ClearAuthCookie()
        {
            var cookieName = _configuration["Jwt:CookieName"] ?? "quilvian_access_token";

            Response.Cookies.Delete(cookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = _environment.IsDevelopment()
                    ? SameSiteMode.Lax
                    : SameSiteMode.None,
                Path = "/"
            });
        }

        private AuthInfoResponse BuildAuthInfoResponse()
        {
            var cookieName = _configuration["Jwt:CookieName"] ?? "quilvian_access_token";
            var expireMinutes = GetJwtExpireMinutes();
            var isDevelopment = _environment.IsDevelopment();

            return new AuthInfoResponse
            {
                Scheme = "Cookie",
                CookieName = cookieName,
                IsHttpOnly = true,
                SameSite = isDevelopment ? "Lax" : "None",
                Secure = !isDevelopment,
                ExpiresInMinutes = expireMinutes,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expireMinutes),
                FrontendInstruction = "Gunakan credentials: 'include' pada setiap request ke backend. Access token tidak dikirim di response body karena disimpan di HttpOnly cookie."
            };
        }

        private static UserLoginResponse BuildUserResponse(ApplicationUser user, IList<string> roles)
        {
            return new UserLoginResponse
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                UserType = user.UserType.ToString(),
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                HospitalId = user.HospitalId,
                DepartmentId = user.DepartmentId,
                PositionId = user.PositionId
            };
        }
    }
}