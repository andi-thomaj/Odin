namespace Odin.Api.Data.Entities
{
    public class SubEra : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int TimeEraId { get; set; }
        public TimeEra TimeEra { get; set; }
    }
}
