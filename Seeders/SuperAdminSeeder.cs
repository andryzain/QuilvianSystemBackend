using Microsoft.AspNetCore.Identity;
using QuilvianSystemBackend.Enum;
using QuilvianSystemBackend.Models;

namespace QuilvianSystemBackend.Seeders
{
    public static class SuperAdminSeeder
    {
        private const string SuperAdminRoleName = "SuperAdmin";
        private const string UserRoleName = "User";

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

            // 1. Pastikan role global tersedia
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

            // 2. Ambil config superadmin
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

            // 3. Cek apakah user superadmin sudah ada
            var existingUser = await userManager.FindByNameAsync(username);

            if (existingUser == null)
            {
                existingUser = await userManager.FindByEmailAsync(email);
            }

            // 4. Jika belum ada, buat user superadmin
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpper(),
                    Email = email,
                    NormalizedEmail = email.ToUpper(),
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(fullName)
                        ? "Super Administrator"
                        : fullName,
                    UserType = UserType.SuperAdmin,
                    IsActive = true,
                    MustChangePassword = false,
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

            // 5. Jika user sudah ada, pastikan tetap punya role SuperAdmin
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
                NormalizedName = roleName.ToUpper(),
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
    }
}