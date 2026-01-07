using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Shouldly;

namespace Tests.Infrastructure.Persistence;

public sealed class UserPersistenceMapperTests
{
    private static readonly Guid TestId1 = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TestId2 = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Guid CreatedById = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void ToDomain_ShouldMapAllPropertiesIncludingEnumConversion()
    {
        // Arrange
        var dataModel = new UserDataModel
        {
            Id = TestId1,
            ApiKeyHash = "abc123hash",
            ApiKeyPrefix = "abc12345",
            UserName = "testuser",
            Email = "test@example.com",
            UserType = UserTypeData.Admin,
            IsActive = true,
            Description = "Test admin user",
            LastUsedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            Metadata = new DataModelMetadata
            {
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = CreatedById,
                UpdatedAt = new DateTime(2024, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                UpdatedBy = CreatedById
            }
        };

        // Act
        var result = UserPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.ApiKeyHash.ShouldBe("abc123hash");
        result.ApiKeyPrefix.ShouldBe("abc12345");
        result.UserName.ShouldBe("testuser");
        result.Email.ShouldBe("test@example.com");
        result.UserType.ShouldBe(UserType.Admin);
        result.IsActive.ShouldBeTrue();
        result.Description.ShouldBe("Test admin user");
        result.LastUsedAt.ShouldBe(new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        result.CreatedAt.ShouldBe(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.CreatedBy.ShouldBe(CreatedById);
    }

    [Theory]
    [InlineData(UserTypeData.Regular, UserType.Regular)]
    [InlineData(UserTypeData.Admin, UserType.Admin)]
    public void ToDomain_ShouldMapAllUserTypeValues(UserTypeData input, UserType expected)
    {
        // Arrange
        var dataModel = new UserDataModel
        {
            Id = TestId1,
            ApiKeyHash = "hash",
            ApiKeyPrefix = "prefix",
            UserName = "user",
            Email = "user@test.com",
            UserType = input,
            IsActive = true
        };

        // Act
        var result = UserPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.UserType.ShouldBe(expected);
    }

    [Fact]
    public void ToDataModel_ShouldMapAllPropertiesIncludingEnumConversion()
    {
        // Arrange
        var entity = new User
        {
            Id = TestId1,
            ApiKeyHash = "abc123hash",
            ApiKeyPrefix = "abc12345",
            UserName = "testuser",
            Email = "test@example.com",
            UserType = UserType.Admin,
            IsActive = true,
            Description = "Test admin user",
            LastUsedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = UserPersistenceMapper.ToDataModel(entity);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.ApiKeyHash.ShouldBe("abc123hash");
        result.ApiKeyPrefix.ShouldBe("abc12345");
        result.UserName.ShouldBe("testuser");
        result.Email.ShouldBe("test@example.com");
        result.UserType.ShouldBe(UserTypeData.Admin);
        result.IsActive.ShouldBeTrue();
        result.Description.ShouldBe("Test admin user");
        result.LastUsedAt.ShouldBe(new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(UserType.Regular, UserTypeData.Regular)]
    [InlineData(UserType.Admin, UserTypeData.Admin)]
    public void ToDataModel_ShouldMapAllUserTypeValues(UserType input, UserTypeData expected)
    {
        // Arrange
        var entity = new User
        {
            Id = TestId1,
            ApiKeyHash = "hash",
            ApiKeyPrefix = "prefix",
            UserName = "user",
            Email = "user@test.com",
            UserType = input,
            IsActive = true
        };

        // Act
        var result = UserPersistenceMapper.ToDataModel(entity);

        // Assert
        result.UserType.ShouldBe(expected);
    }

    [Fact]
    public void ToDomain_Collection_ShouldMapAllItems()
    {
        // Arrange
        var dataModels = new List<UserDataModel>
        {
            new() { Id = TestId1, ApiKeyHash = "h1", ApiKeyPrefix = "p1", UserName = "user1", Email = "u1@test.com", UserType = UserTypeData.Regular, IsActive = true },
            new() { Id = TestId2, ApiKeyHash = "h2", ApiKeyPrefix = "p2", UserName = "user2", Email = "u2@test.com", UserType = UserTypeData.Admin, IsActive = false }
        };

        // Act
        var result = UserPersistenceMapper.ToDomain(dataModels).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].UserType.ShouldBe(UserType.Regular);
        result[0].IsActive.ShouldBeTrue();
        result[1].UserType.ShouldBe(UserType.Admin);
        result[1].IsActive.ShouldBeFalse();
    }
}

