using Odin.Api.Endpoints.Admin;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.AppSettingsManagement;
using Odin.Api.Endpoints.G25PopulationSampleManagement;
using Odin.Api.Endpoints.G25DistancePopulationSampleManagement;
using Odin.Api.Endpoints.G25PcaPopulationsSampleManagement;
using Odin.Api.Endpoints.QpadmPopulationSampleManagement;
using Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement;
using Odin.Api.Endpoints.G25SavedCoordinateManagement;
using Odin.Api.Endpoints.G25TargetCoordinateManagement;
using Odin.Api.Endpoints.GeneticInspectionManagement;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Endpoints.Payments;
using Odin.Api.Endpoints.PopulationManagement;
using Odin.Api.Endpoints.RawGeneticFileManagement;
using Odin.Api.Endpoints.ReferenceDataManagement;
using Odin.Api.Endpoints.StorageManagement;
using Odin.Api.Endpoints.MediaManagement;
using Odin.Api.Endpoints.ReportManagement;
using Odin.Api.Endpoints.Subscribe;
using Odin.Api.Endpoints.UserManagement;
using Odin.Api.Endpoints.UserFacePhotoManagement;
using Odin.Api.Endpoints.AncestralPortraitManagement;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25ContinentManagement;
using Odin.Api.Endpoints.CalculatorManagement;
using Odin.Api.Endpoints.G25AdmixtureEraManagement;
using Odin.Api.Endpoints.G25DistanceEraManagement;
using Odin.Api.Endpoints.G25EthnicityManagement;
using Odin.Api.Endpoints.G25RegionManagement;

namespace Odin.Api.Extensions
{
    /// <summary>
    /// Maps every versioned (<c>/v1</c>) business endpoint group. Extracted from Program.cs so the
    /// startup file isn't dominated by the registration list. Each <c>Map*Endpoints()</c> call is
    /// independent (registration order doesn't matter), so this is a pure move with no behavior change.
    /// </summary>
    public static class EndpointRouteExtensions
    {
        public static IEndpointRouteBuilder MapOdinV1Endpoints(this IEndpointRouteBuilder v1)
        {
            v1.MapUserEndpoints();
            v1.MapEthnicityEndpoints();
            v1.MapEraEndpoints();
            v1.MapPopulationEndpoints();
            v1.MapRawGeneticFileEndpoints();
            v1.MapGeneticInspectionEndpoints();
            v1.MapOrderEndpoints();
            v1.MapAppStoreWebhookEndpoints();
            v1.MapAppStoreTransactionAdminEndpoints();
            v1.MapAppSettingsEndpoints();
            v1.MapNotificationEndpoints();
            v1.MapSubscribeEndpoints();
            v1.MapReportEndpoints();
            v1.MapMediaEndpoints();
            v1.MapG25PopulationSampleEndpoints();
            v1.MapG25DistancePopulationSampleEndpoints();
            v1.MapG25PcaPopulationsSampleEndpoints();
            v1.MapQpadmPopulationSampleEndpoints();
            v1.MapQpadmPopulationPanelSampleEndpoints();
            v1.MapG25SavedCoordinateEndpoints();
            v1.MapG25TargetCoordinateEndpoints();
            v1.MapG25RegionEndpoints();
            v1.MapG25EthnicityEndpoints();
            v1.MapG25ContinentEndpoints();
            v1.MapG25DistanceEraEndpoints();
            v1.MapG25AdmixtureEraEndpoints();
            v1.MapG25CalculationEndpoints();
            v1.MapG25AdminEndpoints();
            v1.MapCacheAdminEndpoints();
            v1.MapMergePanelAdminEndpoints();
            v1.MapMergePanelLabelsEndpoints();
            v1.MapPanelPromotionEndpoints();
            v1.MapMergeAdminEndpoints();
            v1.MapHangfireSessionEndpoints();
            v1.MapCalculatorEndpoints();
            v1.MapAdmixToolsEraEndpoints();
            v1.MapCladeFinderEndpoints();
            v1.MapImageGenerationEndpoints();
            v1.MapUserFacePhotoEndpoints();
            v1.MapAncestralPortraitEndpoints();
            v1.MapStorageManagementEndpoints();
            return v1;
        }
    }
}
