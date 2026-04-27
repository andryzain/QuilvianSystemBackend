using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuilvianSystemBackend.Models;

namespace QuilvianSystemBackend.Repositories
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SysAppVersion> SysAppVersions { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(x => x.FullName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.UserType)
                    .IsRequired();

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.MustChangePassword)
                    .HasDefaultValue(false);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            builder.Entity<ApplicationRole>(entity =>
            {
                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.IsSystemRole)
                    .HasDefaultValue(false);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            builder.Entity<SysAppVersion>(entity =>
            {
                entity.ToTable("SysAppVersion", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.AppName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.BackendVersion)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.ApiVersion)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.FrontendMinimumVersion)
                    .HasMaxLength(50);

                entity.Property(x => x.FrontendRecommendedVersion)
                    .HasMaxLength(50);

                entity.Property(x => x.ReleaseName)
                    .HasMaxLength(200);

                entity.Property(x => x.IsLatest)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.ReleaseDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasIndex(x => x.IsLatest);
                entity.HasIndex(x => x.IsActive);
                entity.HasIndex(x => x.BackendVersion);
                entity.HasIndex(x => x.ApiVersion);
            });
        }
    }
}
