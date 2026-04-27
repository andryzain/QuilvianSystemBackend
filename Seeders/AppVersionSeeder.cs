using Microsoft.EntityFrameworkCore;
using QuilvianSystemBackend.Models;
using QuilvianSystemBackend.Repositories;

namespace QuilvianSystemBackend.Seeders
{
    public static class AppVersionSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var versionExists = await dbContext.SysAppVersions.AnyAsync();

            if (versionExists)
            {
                return;
            }

            var appName = configuration["AppInfo:Name"] ?? "Quilvian System Backend";
            var backendVersion = configuration["AppInfo:BackendVersion"] ?? "1.0.0";
            var apiVersion = configuration["AppInfo:ApiVersion"] ?? "v1";
            var frontendMinimumVersion = configuration["AppInfo:FrontendMinimumVersion"] ?? "1.0.0";
            var frontendRecommendedVersion = configuration["AppInfo:FrontendRecommendedVersion"] ?? "1.0.0";
            var releaseName = configuration["AppInfo:ReleaseName"] ?? "Initial Development Version";
            var description = configuration["AppInfo:Description"] ?? "Initial backend version.";

            var appVersion = new SysAppVersion
            {
                AppName = appName,
                BackendVersion = backendVersion,
                ApiVersion = apiVersion,
                FrontendMinimumVersion = frontendMinimumVersion,
                FrontendRecommendedVersion = frontendRecommendedVersion,
                ReleaseName = releaseName,
                Description = description,
                IsLatest = true,
                IsActive = true,
                ReleaseDateTime = DateTime.UtcNow,
                CreateDateTime = DateTime.UtcNow
            };

            dbContext.SysAppVersions.Add(appVersion);

            await dbContext.SaveChangesAsync();
        }
    }
}
