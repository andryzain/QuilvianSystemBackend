using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuilvianSystemBackend.Areas.Administrator.MasterData.Models;
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
        public DbSet<SysApplicationModule> SysApplicationModules { get; set; }
        public DbSet<SysControllerAccess> SysControllerAccesses { get; set; }
        public DbSet<SysActionAccess> SysActionAccesses { get; set; }
        public DbSet<SysAccessPolicy> SysAccessPolicies { get; set; }

        public DbSet<MstDepartment> MstDepartments { get; set; }
        public DbSet<MstPosition> MstPositions { get; set; }
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

                entity.HasOne(x => x.Department)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Position)
                    .WithMany()
                    .HasForeignKey(x => x.PositionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.DepartmentId);

                entity.HasIndex(x => x.PositionId);

                entity.HasIndex(x => x.UserType);
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

            builder.Entity<SysApplicationModule>(entity =>
            {
                entity.ToTable("SysApplicationModule", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ModuleCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.ModuleName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.AreaName)
                    .HasMaxLength(100);

                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.SortOrder)
                    .HasDefaultValue(0);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasIndex(x => x.ModuleCode)
                    .IsUnique();

                entity.HasIndex(x => x.ModuleName);
            });

            builder.Entity<SysControllerAccess>(entity =>
            {
                entity.ToTable("SysControllerAccess", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ModuleId)
                    .IsRequired();

                entity.Property(x => x.ControllerName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.DisplayName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.RoutePath)
                    .HasMaxLength(250);

                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.SortOrder)
                    .HasDefaultValue(0);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Module)
                    .WithMany(x => x.Controllers)
                    .HasForeignKey(x => x.ModuleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.ModuleId, x.ControllerName })
                    .IsUnique();

                entity.HasIndex(x => x.DisplayName);
            });

            builder.Entity<SysActionAccess>(entity =>
            {
                entity.ToTable("SysActionAccess", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ControllerAccessId)
                    .IsRequired();

                entity.Property(x => x.ActionName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.DisplayName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.HttpMethod)
                    .HasMaxLength(20);

                entity.Property(x => x.RoutePath)
                    .HasMaxLength(250);

                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.SortOrder)
                    .HasDefaultValue(0);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.ControllerAccess)
                    .WithMany(x => x.Actions)
                    .HasForeignKey(x => x.ControllerAccessId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.ControllerAccessId, x.ActionName })
                    .IsUnique();

                entity.HasIndex(x => x.DisplayName);
            });

            builder.Entity<SysAccessPolicy>(entity =>
            {
                entity.ToTable("SysAccessPolicy", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.DepartmentId)
                    .IsRequired();

                entity.Property(x => x.PositionId)
                    .IsRequired();

                entity.Property(x => x.UserType)
                    .IsRequired();

                entity.Property(x => x.ControllerAccessId)
                    .IsRequired();

                entity.Property(x => x.ActionAccessId)
                    .IsRequired();

                entity.Property(x => x.IsAllowed)
                    .HasDefaultValue(true);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Department)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Position)
                    .WithMany()
                    .HasForeignKey(x => x.PositionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.ControllerAccess)
                    .WithMany()
                    .HasForeignKey(x => x.ControllerAccessId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.ActionAccess)
                    .WithMany()
                    .HasForeignKey(x => x.ActionAccessId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new
                {
                    x.DepartmentId,
                    x.PositionId,
                    x.UserType,
                    x.ControllerAccessId,
                    x.ActionAccessId
                }).IsUnique();

                entity.HasIndex(x => x.DepartmentId);
                entity.HasIndex(x => x.PositionId);
                entity.HasIndex(x => x.UserType);
                entity.HasIndex(x => x.ControllerAccessId);
                entity.HasIndex(x => x.ActionAccessId);
            });

            builder.Entity<MstDepartment>(entity =>
            {
                entity.ToTable("MstDepartment", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.DepartmentCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.DepartmentName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.UpdateDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.DeleteDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.CancelDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasIndex(x => x.DepartmentCode)
                    .IsUnique();

                entity.HasIndex(x => x.DepartmentName);
            });

            builder.Entity<MstPosition>(entity =>
            {
                entity.ToTable("MstPosition", "public");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.DepartmentId)
                    .IsRequired();

                entity.Property(x => x.PositionCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.PositionName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(250);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreateDateTime)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(x => x.UpdateDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.DeleteDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.CancelDateTime)
                    .HasColumnType("timestamp with time zone")
                    .IsRequired(false);

                entity.Property(x => x.IsDelete)
                    .HasDefaultValue(false);

                entity.Property(x => x.IsCancel)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Department)
                    .WithMany(x => x.Positions)
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.DepartmentId, x.PositionCode })
                    .IsUnique();

                entity.HasIndex(x => new { x.DepartmentId, x.PositionName });
            });
        }
    }
}
