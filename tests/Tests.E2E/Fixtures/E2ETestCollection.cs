namespace Tests.E2E.Fixtures;

/// <summary>
/// Collection definition for E2E tests.
/// All tests in this collection share the same WebApplicationFactory instance.
/// </summary>
[CollectionDefinition("E2E")]
public class E2ETestCollection : ICollectionFixture<ApiWebApplicationFactory>
{
}

