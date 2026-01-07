using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Shouldly;

namespace Tests.Infrastructure.Persistence;

public sealed class PatientPersistenceMapperTests
{
    private static readonly Guid TestId1 = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TestId2 = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Guid CreatedById = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UpdatedById = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void ToDomain_ShouldMapAllProperties()
    {
        // Arrange
        var dataModel = new PatientDataModel
        {
            Id = TestId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234",
            DateOfBirth = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Metadata = new DataModelMetadata
            {
                CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                CreatedBy = CreatedById,
                UpdatedAt = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc),
                UpdatedBy = UpdatedById
            }
        };

        // Act
        var result = PatientPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Email.ShouldBe("john.doe@example.com");
        result.Phone.ShouldBe("555-1234");
        result.DateOfBirth.ShouldBe(new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc));
        result.CreatedAt.ShouldBe(new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        result.CreatedBy.ShouldBe(CreatedById);
        result.UpdatedAt.ShouldBe(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc));
        result.UpdatedBy.ShouldBe(UpdatedById);
    }

    [Fact]
    public void ToDomain_ShouldComputeFullName()
    {
        // Arrange
        var dataModel = new PatientDataModel
        {
            Id = TestId1,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@test.com",
            DateOfBirth = DateTime.UtcNow
        };

        // Act
        var result = PatientPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public void ToDataModel_ShouldMapAllProperties()
    {
        // Arrange
        var entity = new Patient
        {
            Id = TestId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234",
            DateOfBirth = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            // Audit fields should be ignored
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid() // should-be-ignored
        };

        // Act
        var result = PatientPersistenceMapper.ToDataModel(entity);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Email.ShouldBe("john.doe@example.com");
        result.Phone.ShouldBe("555-1234");
        result.DateOfBirth.ShouldBe(new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc));
        // Metadata should NOT be populated by mapper (managed by repository)
        result.Metadata.ShouldNotBeNull();
    }

    [Fact]
    public void ToDomain_WithNullPhone_ShouldMapNullPhone()
    {
        // Arrange
        var dataModel = new PatientDataModel
        {
            Id = TestId1,
            FirstName = "Test",
            LastName = "User",
            Email = "test@test.com",
            Phone = null,
            DateOfBirth = DateTime.UtcNow
        };

        // Act
        var result = PatientPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Phone.ShouldBeNull();
    }

    [Fact]
    public void ToDomain_Collection_ShouldMapAllItems()
    {
        // Arrange
        var dataModels = new List<PatientDataModel>
        {
            new() { Id = TestId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = DateTime.UtcNow },
            new() { Id = TestId2, FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", DateOfBirth = DateTime.UtcNow }
        };

        // Act
        var result = PatientPersistenceMapper.ToDomain(dataModels).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe(TestId1);
        result[0].FirstName.ShouldBe("John");
        result[1].Id.ShouldBe(TestId2);
        result[1].FirstName.ShouldBe("Jane");
    }
}

