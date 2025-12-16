namespace Odin.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
