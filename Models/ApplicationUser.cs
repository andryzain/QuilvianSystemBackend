using Microsoft.AspNetCore.Identity;
using QuilvianSystemBackend.Areas.Administrator.MasterData.Models;
using QuilvianSystemBackend.Enum;

namespace QuilvianSystemBackend.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;

        public UserType UserType { get; set; }

        public Guid? HospitalId { get; set; }

        public Guid? DepartmentId { get; set; }

        public Guid? PositionId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool MustChangePassword { get; set; } = false;

        public DateTime? LastLoginAt { get; set; }

        public DateTime? AccessValidUntil { get; set; }

        public DateTime CreateDateTime { get; set; } = DateTime.UtcNow;

        public DateTime? UpdateDateTime { get; set; }

        public MstDepartment? Department { get; set; }

        public MstPosition? Position { get; set; }
    }
}
