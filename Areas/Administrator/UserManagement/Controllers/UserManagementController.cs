using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuilvianSystemBackend.Areas.Administrator.UserManagement.DTOs;
using QuilvianSystemBackend.Areas.Administrator.UserManagement.Enum;
using QuilvianSystemBackend.Areas.Administrator.UserManagement.Models;
using QuilvianSystemBackend.Attributes;
using QuilvianSystemBackend.Enum;
using QuilvianSystemBackend.Models;
using QuilvianSystemBackend.Repositories;
using QuilvianSystemBackend.Responses;
using QuilvianSystemBackend.Services.Logging;
using System.Security.Claims;

namespace QuilvianSystemBackend.Areas.Administrator.UserManagement.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/administrator/user-management/users")]
    [Tags("User Management / User")]
    [AccessController(
        moduleCode: "USER_MANAGEMENT",
        moduleName: "User Management",
        displayName: "User Management",
        AreaName = "Administrator",
        ControllerName = "UserManagement",
        Description = "Pengelolaan user aplikasi",
        SortOrder = 1
    )]
    public class UserManagementController : ControllerBase
    {
        private const string DefaultUserRole = "User";
        private const string UserCodePrefix = "USR-RSMMC";
        private const string DefaultProfilePhotoFileName = "user.jpeg";

        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly LoggerService _loggerService;
        private readonly IConfiguration _configuration;

        public UserManagementController(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            LoggerService loggerService,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _loggerService = loggerService;
            _configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CreateUserResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [AccessAction(
            actionName: "Create",
            displayName: "Tambah Data",
            Description = "Membuat user baru",
            SortOrder = 3
        )]
        [AccessPermission("UserManagement", "Create")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var validation = await ValidateProvisionCreateRequestAsync(request);

            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest,
                    validation.Message
                ));
            }

            var roleExists = await _roleManager.RoleExistsAsync(DefaultUserRole);

            if (!roleExists)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest,
                    "Role User belum tersedia."
                ));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var userCode = await GenerateUserCodeAsync();
                var username = request.Email.Trim().ToLower();
                var email = request.Email.Trim().ToLower();
                var initialPassword = GeneratePasswordFromBirthDate(request.BirthDate);
                var defaultPhotoPath = BuildDefaultProfilePhotoUrl();

                var profileResult = await ProvisionProfileAsync(request);

                if (!profileResult.IsValid)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(ApiResponse<object>.Fail(
                        StatusCodes.Status400BadRequest,
                        profileResult.Message
                    ));
                }

                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserCode = userCode,
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    PhoneNumber = Normalize(request.PhoneNumber),
                    FullName = request.FullName.Trim(),
                    UserType = request.UserType,
                    BirthDate = request.BirthDate.Date,
                    IdentityNumber = Normalize(request.IdentityNumber),
                    DepartmentId = profileResult.DepartmentId,
                    PositionId = profileResult.PositionId,
                    EmployeeId = profileResult.EmployeeId,
                    DoctorId = profileResult.DoctorId,
                    ExternalUserId = profileResult.ExternalUserId,
                    IsActive = true,
                    MustChangePassword = true,
                    AccessValidUntil = request.AccessValidUntil,
                    ProfilePhotoPath = defaultPhotoPath,
                    CreateDateTime = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user, initialPassword);

                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync();

                    var errors = string.Join(", ", createResult.Errors.Select(x => x.Description));

                    await _loggerService.WarningAsync(
                        "UserManagement",
                        "User.Create",
                        "Gagal membuat user.",
                        new
                        {
                            userCode,
                            username,
                            email,
                            Errors = errors
                        }
                    );

                    return BadRequest(ApiResponse<object>.Fail(
                        StatusCodes.Status400BadRequest,
                        errors
                    ));
                }

                var addRoleResult = await _userManager.AddToRoleAsync(user, DefaultUserRole);

                if (!addRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();

                    var errors = string.Join(", ", addRoleResult.Errors.Select(x => x.Description));

                    return BadRequest(ApiResponse<object>.Fail(
                        StatusCodes.Status400BadRequest,
                        $"Gagal assign role User: {errors}"
                    ));
                }

                await transaction.CommitAsync();

                await _loggerService.InfoAsync(
                    "UserManagement",
                    "User.Create",
                    "User berhasil dibuat.",
                    new
                    {
                        UserId = user.Id,
                        user.UserCode,
                        Username = user.UserName,
                        Email = user.Email,
                        user.UserType,
                        user.DepartmentId,
                        user.PositionId,
                        user.EmployeeId,
                        user.DoctorId,
                        user.ExternalUserId
                    }
                );

                var savedUser = await GetUserWithRelationsAsync(user.Id);
                var userResponse = await BuildUserResponseAsync(savedUser!);

                var response = new CreateUserResponse
                {
                    User = userResponse,
                    InitialPassword = initialPassword
                };

                return Ok(ApiResponse<CreateUserResponse>.Ok(
                    response,
                    "User berhasil dibuat."
                ));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                await _loggerService.ErrorAsync(
                    "UserManagement",
                    "User.Create",
                    "Terjadi error saat membuat user.",
                    ex,
                    new
                    {
                        request.FullName,
                        request.Email,
                        request.UserType
                    }
                );

                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest,
                    "Terjadi error saat membuat user."
                ));
            }
        }

        private async Task<(bool IsValid, string Message)> ValidateProvisionCreateRequestAsync(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return (false, "Nama lengkap wajib diisi.");
            }

            if (request.UserType == UserType.SuperAdmin)
            {
                return (false, "User SuperAdmin tidak boleh dibuat dari menu ini.");
            }

            if (request.UserType == UserType.Patient)
            {
                return (false, "UserType Patient belum didukung dari menu user management ini.");
            }

            if (request.BirthDate == default)
            {
                return (false, "Tanggal lahir wajib diisi.");
            }

            if (request.BirthDate.Date > DateTime.UtcNow.Date)
            {
                return (false, "Tanggal lahir tidak boleh lebih besar dari tanggal hari ini.");
            }

            if (string.IsNullOrWhiteSpace(request.IdentityNumber))
            {
                return (false, "Nomor identitas wajib diisi.");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return (false, "Email wajib diisi.");
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return (false, "Nomor handphone wajib diisi.");
            }

            if (request.DepartmentId == Guid.Empty)
            {
                return (false, "Department wajib dipilih.");
            }

            if (request.PositionId == Guid.Empty)
            {
                return (false, "Position wajib dipilih.");
            }

            var departmentPositionValidation = await ValidateDepartmentAndPositionAsync(
                request.DepartmentId,
                request.PositionId
            );

            if (!departmentPositionValidation.IsValid)
            {
                return departmentPositionValidation;
            }

            var email = request.Email.Trim().ToLower();

            var usernameExists = await _userManager.FindByNameAsync(email);

            if (usernameExists != null)
            {
                return (false, "Username/email sudah digunakan.");
            }

            var emailExists = await _userManager.FindByEmailAsync(email);

            if (emailExists != null)
            {
                return (false, "Email sudah digunakan.");
            }

            var identityNumber = request.IdentityNumber.Trim();

            var identityExists = await _dbContext.Users
                .AnyAsync(x =>
                    x.IdentityNumber != null &&
                    x.IdentityNumber == identityNumber);

            if (identityExists)
            {
                return (false, "Nomor identitas sudah digunakan oleh user lain.");
            }

            return (true, string.Empty);
        }

        private async Task<UserProfileProvisionResult> ProvisionProfileAsync(CreateUserRequest request)
        {
            if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
            {
                return await UseExistingEmployeeProfileAsync(request.EmployeeId.Value);
            }

            if (request.DoctorId.HasValue && request.DoctorId.Value != Guid.Empty)
            {
                return await UseExistingDoctorProfileAsync(request.DoctorId.Value, request.DepartmentId, request.PositionId);
            }

            if (request.ExternalUserId.HasValue && request.ExternalUserId.Value != Guid.Empty)
            {
                return await UseExistingExternalProfileAsync(request.ExternalUserId.Value, request.DepartmentId, request.PositionId);
            }

            return request.UserType switch
            {
                UserType.Employee => await CreateMinimalEmployeeProfileAsync(request),

                UserType.PermanentDoctor => await CreateMinimalDoctorProfileAsync(request, DoctorType.PermanentDoctor),

                UserType.GuestDoctor => await CreateMinimalDoctorProfileAsync(request, DoctorType.GuestDoctor),

                UserType.ExternalUser => await CreateMinimalExternalProfileAsync(request),

                UserType.Vendor => await CreateMinimalExternalProfileAsync(request),

                _ => UserProfileProvisionResult.Fail("UserType belum didukung untuk pembuatan akun user.")
            };
        }

        private async Task<UserProfileProvisionResult> CreateMinimalEmployeeProfileAsync(CreateUserRequest request)
        {
            var employeeCode = await GenerateEmployeeCodeAsync();

            var employee = new MstEmployee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = employeeCode,
                FullName = request.FullName.Trim(),
                Gender = request.Gender,
                BirthDate = request.BirthDate.Date,
                IdentityNumber = Normalize(request.IdentityNumber),
                Email = Normalize(request.Email),
                PhoneNumber = Normalize(request.PhoneNumber),
                WhatsAppNumber = Normalize(request.PhoneNumber),
                Address = Normalize(request.Address),
                DepartmentId = request.DepartmentId,
                PositionId = request.PositionId,
                EmployeeStatus = request.EmployeeStatus ?? EmployeeStatus.Contract,
                IsActive = true,
                CreateDateTime = DateTime.UtcNow,
                CreateBy = GetCurrentUserId(),
                IsDelete = false,
                IsCancel = false
            };

            _dbContext.MstEmployees.Add(employee);

            await _dbContext.SaveChangesAsync();

            return UserProfileProvisionResult.Ok(
                departmentId: employee.DepartmentId,
                positionId: employee.PositionId,
                employeeId: employee.Id,
                doctorId: null,
                externalUserId: null
            );
        }

        private async Task<UserProfileProvisionResult> CreateMinimalDoctorProfileAsync(
            CreateUserRequest request,
            DoctorType doctorType)
        {
            var doctorCode = await GenerateDoctorCodeAsync();

            var doctor = new MstDoctor
            {
                Id = Guid.NewGuid(),
                DoctorCode = doctorCode,
                FullName = request.FullName.Trim(),
                DoctorType = doctorType,
                Gender = request.Gender,
                BirthDate = request.BirthDate.Date,
                IdentityNumber = Normalize(request.IdentityNumber),
                Email = Normalize(request.Email),
                PhoneNumber = Normalize(request.PhoneNumber),
                WhatsAppNumber = Normalize(request.PhoneNumber),
                Address = Normalize(request.Address),
                DepartmentId = request.DepartmentId,
                PositionId = request.PositionId,
                IsAvailableForAppointment = false,
                IsActive = true,
                CreateDateTime = DateTime.UtcNow,
                CreateBy = GetCurrentUserId(),
                IsDelete = false,
                IsCancel = false
            };

            _dbContext.MstDoctors.Add(doctor);

            await _dbContext.SaveChangesAsync();

            return UserProfileProvisionResult.Ok(
                departmentId: request.DepartmentId,
                positionId: request.PositionId,
                employeeId: null,
                doctorId: doctor.Id,
                externalUserId: null
            );
        }

        private async Task<UserProfileProvisionResult> CreateMinimalExternalProfileAsync(CreateUserRequest request)
        {
            var externalCode = await GenerateExternalCodeAsync();

            var externalUserType = request.UserType == UserType.Vendor
                ? ExternalUserType.Vendor
                : request.ExternalUserType ?? ExternalUserType.Other;

            var externalUser = new MstExternalUser
            {
                Id = Guid.NewGuid(),
                ExternalCode = externalCode,
                ExternalUserType = externalUserType,
                FullName = request.FullName.Trim(),
                CompanyName = Normalize(request.CompanyName),
                CompanyCode = Normalize(request.CompanyCode),
                JobTitle = Normalize(request.JobTitle),
                PhoneNumber = Normalize(request.PhoneNumber),
                WhatsAppNumber = Normalize(request.PhoneNumber),
                Email = Normalize(request.Email),
                Address = Normalize(request.Address),
                IdentityNumber = Normalize(request.IdentityNumber),
                IsActive = true,
                CreateDateTime = DateTime.UtcNow,
                CreateBy = GetCurrentUserId(),
                IsDelete = false,
                IsCancel = false
            };

            _dbContext.MstExternalUsers.Add(externalUser);

            await _dbContext.SaveChangesAsync();

            return UserProfileProvisionResult.Ok(
                departmentId: request.DepartmentId,
                positionId: request.PositionId,
                employeeId: null,
                doctorId: null,
                externalUserId: externalUser.Id
            );
        }

        private async Task<UserProfileProvisionResult> UseExistingEmployeeProfileAsync(Guid employeeId)
        {
            var employee = await _dbContext.MstEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == employeeId &&
                    x.IsActive &&
                    !x.IsDelete);

            if (employee == null)
            {
                return UserProfileProvisionResult.Fail("Employee tidak valid atau tidak aktif.");
            }

            var alreadyUsed = await _dbContext.Users
                .AnyAsync(x => x.EmployeeId == employee.Id);

            if (alreadyUsed)
            {
                return UserProfileProvisionResult.Fail("Employee ini sudah terhubung dengan akun user lain.");
            }

            return UserProfileProvisionResult.Ok(
                departmentId: employee.DepartmentId,
                positionId: employee.PositionId,
                employeeId: employee.Id,
                doctorId: null,
                externalUserId: null
            );
        }

        private async Task<UserProfileProvisionResult> UseExistingDoctorProfileAsync(
            Guid doctorId,
            Guid departmentId,
            Guid positionId)
        {
            var doctor = await _dbContext.MstDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == doctorId &&
                    x.IsActive &&
                    !x.IsDelete);

            if (doctor == null)
            {
                return UserProfileProvisionResult.Fail("Doctor tidak valid atau tidak aktif.");
            }

            var alreadyUsed = await _dbContext.Users
                .AnyAsync(x => x.DoctorId == doctor.Id);

            if (alreadyUsed)
            {
                return UserProfileProvisionResult.Fail("Doctor ini sudah terhubung dengan akun user lain.");
            }

            var resolvedDepartmentId = doctor.DepartmentId ?? departmentId;
            var resolvedPositionId = doctor.PositionId ?? positionId;

            var validation = await ValidateDepartmentAndPositionAsync(resolvedDepartmentId, resolvedPositionId);

            if (!validation.IsValid)
            {
                return UserProfileProvisionResult.Fail(validation.Message);
            }

            return UserProfileProvisionResult.Ok(
                departmentId: resolvedDepartmentId,
                positionId: resolvedPositionId,
                employeeId: null,
                doctorId: doctor.Id,
                externalUserId: null
            );
        }

        private async Task<UserProfileProvisionResult> UseExistingExternalProfileAsync(
            Guid externalUserId,
            Guid departmentId,
            Guid positionId)
        {
            var externalUser = await _dbContext.MstExternalUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == externalUserId &&
                    x.IsActive &&
                    !x.IsDelete);

            if (externalUser == null)
            {
                return UserProfileProvisionResult.Fail("External user tidak valid atau tidak aktif.");
            }

            var alreadyUsed = await _dbContext.Users
                .AnyAsync(x => x.ExternalUserId == externalUser.Id);

            if (alreadyUsed)
            {
                return UserProfileProvisionResult.Fail("External user ini sudah terhubung dengan akun user lain.");
            }

            var validation = await ValidateDepartmentAndPositionAsync(departmentId, positionId);

            if (!validation.IsValid)
            {
                return UserProfileProvisionResult.Fail(validation.Message);
            }

            return UserProfileProvisionResult.Ok(
                departmentId: departmentId,
                positionId: positionId,
                employeeId: null,
                doctorId: null,
                externalUserId: externalUser.Id
            );
        }

        private async Task<string> GenerateUserCodeAsync()
        {
            var totalData = await _dbContext.Users
                .IgnoreQueryFilters()
                .CountAsync();

            var nextNumber = totalData + 1;

            while (true)
            {
                var code = $"{UserCodePrefix}-{nextNumber.ToString("D5")}";

                var exists = await _dbContext.Users
                    .AnyAsync(x => x.UserCode == code);

                if (!exists)
                {
                    return code;
                }

                nextNumber++;
            }
        }

        private async Task<string> GenerateEmployeeCodeAsync()
        {
            const string prefix = "EMP-RSMMC";

            var totalData = await _dbContext.MstEmployees
                .IgnoreQueryFilters()
                .CountAsync();

            var nextNumber = totalData + 1;

            while (true)
            {
                var code = $"{prefix}-{nextNumber.ToString("D5")}";

                var exists = await _dbContext.MstEmployees
                    .AnyAsync(x => x.EmployeeCode == code);

                if (!exists)
                {
                    return code;
                }

                nextNumber++;
            }
        }

        private async Task<string> GenerateEmployeeNumberAsync()
        {
            var now = GetSystemNow();
            var datePart = now.ToString("yyyyMMdd");
            var prefix = $"RSMMC-{datePart}";

            var todayCount = await _dbContext.MstEmployees
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.EmployeeNumber != null &&
                    x.EmployeeNumber.StartsWith(prefix));

            var nextNumber = todayCount + 1;

            while (true)
            {
                var employeeNumber = $"{prefix}{nextNumber.ToString("D3")}";

                var exists = await _dbContext.MstEmployees
                    .AnyAsync(x => x.EmployeeNumber == employeeNumber);

                if (!exists)
                {
                    return employeeNumber;
                }

                nextNumber++;
            }
        }

        private async Task<string> GenerateDoctorCodeAsync()
        {
            const string prefix = "DCT-RSMMC";

            var totalData = await _dbContext.MstDoctors
                .IgnoreQueryFilters()
                .CountAsync();

            var nextNumber = totalData + 1;

            while (true)
            {
                var code = $"{prefix}-{nextNumber.ToString("D5")}";

                var exists = await _dbContext.MstDoctors
                    .AnyAsync(x => x.DoctorCode == code);

                if (!exists)
                {
                    return code;
                }

                nextNumber++;
            }
        }

        private async Task<string> GenerateExternalCodeAsync()
        {
            const string prefix = "EXT-RSMMC";

            var totalData = await _dbContext.MstExternalUsers
                .IgnoreQueryFilters()
                .CountAsync();

            var nextNumber = totalData + 1;

            while (true)
            {
                var code = $"{prefix}-{nextNumber.ToString("D5")}";

                var exists = await _dbContext.MstExternalUsers
                    .AnyAsync(x => x.ExternalCode == code);

                if (!exists)
                {
                    return code;
                }

                nextNumber++;
            }
        }

        private static string GeneratePasswordFromBirthDate(DateTime birthDate)
        {
            var monthNames = new[]
            {
                string.Empty,
                "Jan",
                "Feb",
                "Mar",
                "Apr",
                "Mei",
                "Jun",
                "Jul",
                "Agu",
                "Sep",
                "Okt",
                "Nov",
                "Des"
            };

            return $"{birthDate.Day.ToString("D2")}{monthNames[birthDate.Month]}{birthDate.Year}";
        }

        private string BuildDefaultProfilePhotoUrl()
        {
            var uploadUrl = _configuration["FileStorage:UploadUrl"]?.TrimEnd('/');
            var folder = _configuration["FileStorage:ProfilePhotoFolder"]?.Trim('/');

            if (string.IsNullOrWhiteSpace(uploadUrl))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = "default-photo";
            }

            var baseUrl = uploadUrl.EndsWith("/upload", StringComparison.OrdinalIgnoreCase)
                ? uploadUrl[..^"/upload".Length]
                : uploadUrl;

            return $"{baseUrl}/uploads/{folder}/{DefaultProfilePhotoFileName}";
        }

        private async Task<(bool IsValid, string Message)> ValidateDepartmentAndPositionAsync(
            Guid? departmentId,
            Guid? positionId)
        {
            if (!departmentId.HasValue || departmentId.Value == Guid.Empty)
            {
                return (false, "Department wajib dipilih.");
            }

            if (!positionId.HasValue || positionId.Value == Guid.Empty)
            {
                return (false, "Position wajib dipilih.");
            }

            var departmentExists = await _dbContext.MstDepartments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == departmentId.Value &&
                    x.IsActive &&
                    !x.IsDelete);

            if (!departmentExists)
            {
                return (false, "Department tidak valid atau tidak aktif.");
            }

            var positionExists = await _dbContext.MstPositions
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == positionId.Value &&
                    x.DepartmentId == departmentId.Value &&
                    x.IsActive &&
                    !x.IsDelete);

            if (!positionExists)
            {
                return (false, "Position tidak valid atau tidak sesuai dengan department.");
            }

            return (true, string.Empty);
        }

        private async Task<ApplicationUser?> GetUserWithRelationsAsync(Guid id)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Department)
                .Include(x => x.Position)
                .Include(x => x.Employee)
                .Include(x => x.Doctor)
                .Include(x => x.ExternalUser)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        private async Task<UserManagementResponse> BuildUserResponseAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var profileCode = GetProfileCode(user);
            var profileName = GetProfileName(user);
            var profileType = GetProfileType(user);

            return new UserManagementResponse
            {
                Id = user.Id,
                UserCode = user.UserCode,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                UserType = user.UserType,
                UserTypeName = user.UserType.ToString(),
                BirthDate = user.BirthDate,
                IdentityNumber = user.IdentityNumber,
                PhoneNumber = user.PhoneNumber,
                DepartmentId = user.DepartmentId,
                DepartmentCode = user.Department?.DepartmentCode,
                DepartmentName = user.Department?.DepartmentName,
                PositionId = user.PositionId,
                PositionCode = user.Position?.PositionCode,
                PositionName = user.Position?.PositionName,
                EmployeeId = user.EmployeeId,
                DoctorId = user.DoctorId,
                ExternalUserId = user.ExternalUserId,
                ProfileCode = profileCode,
                ProfileName = profileName,
                ProfileType = profileType,
                ProfilePhotoPath = user.ProfilePhotoPath,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                LastLoginAt = user.LastLoginAt,
                AccessValidUntil = user.AccessValidUntil,
                CreateDateTime = user.CreateDateTime
            };
        }

        private static string? GetProfileCode(ApplicationUser user)
        {
            if (user.Employee != null)
            {
                return user.Employee.EmployeeCode;
            }

            if (user.Doctor != null)
            {
                return user.Doctor.DoctorCode;
            }

            if (user.ExternalUser != null)
            {
                return user.ExternalUser.ExternalCode;
            }

            return null;
        }

        private static string? GetProfileName(ApplicationUser user)
        {
            if (user.Employee != null)
            {
                return user.Employee.FullName;
            }

            if (user.Doctor != null)
            {
                return user.Doctor.FullName;
            }

            if (user.ExternalUser != null)
            {
                return user.ExternalUser.FullName;
            }

            return null;
        }

        private static string? GetProfileType(ApplicationUser user)
        {
            if (user.Employee != null)
            {
                return "Employee";
            }

            if (user.Doctor != null)
            {
                return "Doctor";
            }

            if (user.ExternalUser != null)
            {
                return "ExternalUser";
            }

            return null;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private Guid GetCurrentUserId()
        {
            var userIdText = User.FindFirstValue("user_id");

            if (Guid.TryParse(userIdText, out var userId))
            {
                return userId;
            }

            return Guid.Empty;
        }

        private static DateTime GetSystemNow()
        {
            try
            {
                var timezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(7);
            }
        }

        private class UserProfileProvisionResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; } = string.Empty;

            public Guid DepartmentId { get; set; }

            public Guid PositionId { get; set; }

            public Guid? EmployeeId { get; set; }

            public Guid? DoctorId { get; set; }

            public Guid? ExternalUserId { get; set; }

            public static UserProfileProvisionResult Ok(
                Guid departmentId,
                Guid positionId,
                Guid? employeeId,
                Guid? doctorId,
                Guid? externalUserId)
            {
                return new UserProfileProvisionResult
                {
                    IsValid = true,
                    DepartmentId = departmentId,
                    PositionId = positionId,
                    EmployeeId = employeeId,
                    DoctorId = doctorId,
                    ExternalUserId = externalUserId
                };
            }

            public static UserProfileProvisionResult Fail(string message)
            {
                return new UserProfileProvisionResult
                {
                    IsValid = false,
                    Message = message
                };
            }            
        }
    }
}