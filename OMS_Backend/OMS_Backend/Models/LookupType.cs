public class LookupType : BaseEntity
{
    public string Name { get; set; }
    public ICollection<LookupItem> LookupItems { get; set; }
}