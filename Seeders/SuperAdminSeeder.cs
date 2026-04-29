using Microsoft.AspNetCore.Identity;
using QuilvianSystemBackend.Enum;
using QuilvianSystemBackend.Models;

namespace QuilvianSystemBackend.Seeders
{
    public static class SuperAdminSeeder
    {
        private const string SuperAdminRoleName = "SuperAdmin";
        private const string UserRoleName = "User";

        private const string SuperAdminUserCode = "USR-RSMMC-00000";
        private const string DefaultProfilePhotoFileName = "user.jpeg";

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            var seedEnabled = configuration.GetValue<bool>("SeedSuperAdmin:Enabled");

            if (!seedEnabled)
            {
                return;
            }

            await EnsureRoleAsync(
                roleManager,
                SuperAdminRoleName,
                "Full access system administrator",
                true
            );

            await EnsureRoleAsync(
                roleManager,
                UserRoleName,
                "Default application user",
                true
            );

            var username = configuration["SeedSuperAdmin:Username"];
            var email = configuration["SeedSuperAdmin:Email"];
            var password = configuration["SeedSuperAdmin:Password"];
            var fullName = configuration["SeedSuperAdmin:FullName"];

            if (
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password)
            )
            {
                throw new InvalidOperationException("Konfigurasi SeedSuperAdmin belum lengkap di appsettings.json.");
            }

            username = username.Trim();
            email = email.Trim().ToLowerInvariant();

            var defaultPhotoPath = BuildDefaultProfilePhotoUrl(configuration);

            var existingUser = await userManager.FindByNameAsync(username);

            if (existingUser == null)
            {
                existingUser = await userManager.FindByEmailAsync(email);
            }

            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),

                    UserCode = SuperAdminUserCode,

                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant(),

                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,

                    FullName = string.IsNullOrWhiteSpace(fullName)
                        ? "Super Administrator"
                        : fullName.Trim(),

                    UserType = UserType.SuperAdmin,

                    BirthDate = null,
                    IdentityNumber = null,

                    DepartmentId = null,
                    PositionId = null,
                    EmployeeId = null,
                    DoctorId = null,
                    ExternalUserId = null,

                    IsActive = true,
                    MustChangePassword = false,
                    AccessValidUntil = null,

                    ProfilePhotoPath = defaultPhotoPath,

                    CreateDateTime = DateTime.UtcNow
                };

                var createUserResult = await userManager.CreateAsync(user, password);

                if (!createUserResult.Succeeded)
                {
                    var errors = string.Join(", ", createUserResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal membuat user SuperAdmin: {errors}");
                }

                var addRoleResult = await userManager.AddToRoleAsync(user, SuperAdminRoleName);

                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal menambahkan role SuperAdmin ke user: {errors}");
                }

                return;
            }

            var needUpdate = false;

            if (string.IsNullOrWhiteSpace(existingUser.UserCode))
            {
                existingUser.UserCode = SuperAdminUserCode;
                needUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(existingUser.FullName))
            {
                existingUser.FullName = string.IsNullOrWhiteSpace(fullName)
                    ? "Super Administrator"
                    : fullName.Trim();

                needUpdate = true;
            }

            if (existingUser.UserType != UserType.SuperAdmin)
            {
                existingUser.UserType = UserType.SuperAdmin;
                needUpdate = true;
            }

            if (!existingUser.IsActive)
            {
                existingUser.IsActive = true;
                needUpdate = true;
            }

            if (existingUser.MustChangePassword)
            {
                existingUser.MustChangePassword = false;
                needUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(existingUser.ProfilePhotoPath))
            {
                existingUser.ProfilePhotoPath = defaultPhotoPath;
                needUpdate = true;
            }

            if (!string.Equals(existingUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                existingUser.Email = email;
                existingUser.NormalizedEmail = email.ToUpperInvariant();
                existingUser.EmailConfirmed = true;
                needUpdate = true;
            }

            existingUser.UpdateDateTime = DateTime.UtcNow;

            if (needUpdate)
            {
                var updateResult = await userManager.UpdateAsync(existingUser);

                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal update user SuperAdmin: {errors}");
                }
            }

            var isInSuperAdminRole = await userManager.IsInRoleAsync(existingUser, SuperAdminRoleName);

            if (!isInSuperAdminRole)
            {
                var addRoleResult = await userManager.AddToRoleAsync(existingUser, SuperAdminRoleName);

                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal menambahkan role SuperAdmin ke existing user: {errors}");
                }
            }
        }

        private static async Task EnsureRoleAsync(
            RoleManager<ApplicationRole> roleManager,
            string roleName,
            string description,
            bool isSystemRole)
        {
            var roleExists = await roleManager.RoleExistsAsync(roleName);

            if (roleExists)
            {
                return;
            }

            var role = new ApplicationRole
            {
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                Description = description,
                IsSystemRole = isSystemRole,
                CreateDateTime = DateTime.UtcNow
            };

            var createRoleResult = await roleManager.CreateAsync(role);

            if (!createRoleResult.Succeeded)
            {
                var errors = string.Join(", ", createRoleResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Gagal membuat role {roleName}: {errors}");
            }
        }

        private static string BuildDefaultProfilePhotoUrl(IConfiguration configuration)
        {
            var uploadUrl = configuration["FileStorage:UploadUrl"]?.TrimEnd('/');
            var folder = configuration["FileStorage:ProfilePhotoFolder"]?.Trim('/');

            if (string.IsNullOrWhiteSpace(uploadUrl))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = "default-photo";
            }

            var baseUrl = uploadUrl.EndsWith("/uploads", StringComparison.OrdinalIgnoreCase)
                ? uploadUrl[..^"/uploads".Length]
                : uploadUrl;

            return $"{baseUrl}/uploads/{folder}/{DefaultProfilePhotoFileName}";
        }
    }
}