using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIllyrianGeoJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE populations SET ""GeoJson"" = '{""type"":""Polygon"",""coordinates"":[[[20.8794763,38.9917533],[21.608429,39.4435554],[21.6604971,40.7025563],[21.4730521,41.930455],[20.9731988,42.8304391],[20.5675264,43.9231005],[19.1166899,44.936097],[15.9248494,45.1411376],[15.4412372,44.38573],[19.2456531,42.060298],[19.439098,40.5098967],[19.6325429,40.0918907],[20.8794763,38.9917533]]]}' WHERE ""Name"" = 'Illyrian';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE populations SET ""GeoJson"" = '{""type"":""Polygon"",""coordinates"":[[[16.0,39.5],[17.0,40.0],[18.0,40.5],[19.0,41.0],[20.0,41.5],[20.5,42.0],[21.0,42.5],[20.0,43.5],[19.0,44.0],[18.0,44.5],[17.0,44.0],[16.0,43.5],[15.5,43.0],[15.0,42.5],[15.5,41.5],[16.0,40.5],[16.0,39.5]]]}' WHERE ""Name"" = 'Illyrian';");
        }
    }
}
