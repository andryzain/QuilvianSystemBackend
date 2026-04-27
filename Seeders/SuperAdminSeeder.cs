using Microsoft.AspNetCore.Identity;
using QuilvianSystemBackend.Enum;
using QuilvianSystemBackend.Models;

namespace QuilvianSystemBackend.Seeders
{
    public static class SuperAdminSeeder
    {
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

            const string superAdminRoleName = "SuperAdmin";

            var roleExists = await roleManager.RoleExistsAsync(superAdminRoleName);

            if (!roleExists)
            {
                var role = new ApplicationRole
                {
                    Name = superAdminRoleName,
                    NormalizedName = superAdminRoleName.ToUpper(),
                    Description = "Full access system administrator",
                    IsSystemRole = true,
                    CreateDateTime = DateTime.UtcNow
                };

                var createRoleResult = await roleManager.CreateAsync(role);

                if (!createRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", createRoleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal membuat role SuperAdmin: {errors}");
                }
            }

            var existingUser = await userManager.FindByNameAsync(username);

            if (existingUser == null)
            {
                existingUser = await userManager.FindByEmailAsync(email);
            }

            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpper(),
                    Email = email,
                    NormalizedEmail = email.ToUpper(),
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? "Super Administrator" : fullName,
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

                var addRoleResult = await userManager.AddToRoleAsync(user, superAdminRoleName);

                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal menambahkan role SuperAdmin ke user: {errors}");
                }

                return;
            }

            var isInRole = await userManager.IsInRoleAsync(existingUser, superAdminRoleName);

            if (!isInRole)
            {
                var addRoleResult = await userManager.AddToRoleAsync(existingUser, superAdminRoleName);

                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Gagal menambahkan role SuperAdmin ke existing user: {errors}");
                }
            }
        }
    }
}
