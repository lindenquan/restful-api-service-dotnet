namespace Tests.Api.E2E.Fixtures;

/// <summary>
/// xUnit test collection for API E2E tests.
/// Ensures all tests share the same fixture instance (single API instance).
/// </summary>
[CollectionDefinition(nameof(ApiE2ETestCollection))]
public class ApiE2ETestCollection : ICollectionFixture<ApiE2ETestFixture>
{
    // This class is never instantiated. It's just a marker for xUnit.
}

