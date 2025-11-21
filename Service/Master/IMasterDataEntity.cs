namespace DropInBadAPI.Interfaces
{
    public interface IMasterDataEntity
    {
        public int Id { get; set; }
        public bool? IsActive { get; set; }
    }
}