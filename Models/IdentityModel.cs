namespace QuilvianSystemBackend.Models
{
    public class IdentityModel
    {
        public DateTime CreateDateTime { get; set; }
        public Guid CreateBy { get; set; }
        public DateTime UpdateDateTime { get; set; }
        public Guid UpdateBy { get; set; }
        public DateTime DeleteDateTime { get; set; }
        public Guid CancelBy { get; set; }
        public DateTime CancelDateTime { get; set; }
        public Guid DeleteBy { get; set; }
        public bool IsCancel { get; set; }
        public bool IsDelete { get; set; }
    }
}
