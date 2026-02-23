namespace Odin.Api.Data.Entities
{
    public class TimeEra : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<SubEra> SubEras { get; set; } = [];
    }
}
