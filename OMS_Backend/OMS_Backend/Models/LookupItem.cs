public class LookupItem : BaseEntity
{
    public int LookupDataTypeId { get; set; }
    public string Name { get; set; }
    public LookupType LookupType { get; set; }
}