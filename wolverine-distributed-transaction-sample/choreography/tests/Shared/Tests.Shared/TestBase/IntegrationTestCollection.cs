namespace Tests.Shared.TestBase;

[CollectionDefinition("integration-tests")]
public sealed class IntegrationTestCollection : ICollectionFixture<SharedFixture>;
