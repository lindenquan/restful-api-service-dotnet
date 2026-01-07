using Domain;
using DTOs.V2;
using Infrastructure.Api.Controllers.V2.Mappers;
using Shouldly;

namespace Tests.Api.Controllers.V2.Mappers;

/// <summary>
/// Unit tests for V2 PatientMapper.
/// </summary>
public class PatientMapperTests
{
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void ToV2Dto_ShouldMapAllProperties()
    {
        // Arrange
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            Phone = "555-0100",
            DateOfBirth = new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = PatientMapper.ToV2Dto(patient);

        // Assert
        result.Id.ShouldBe(PatientId1);
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Email.ShouldBe("john@test.com");
        result.Phone.ShouldBe("555-0100");
        result.FullName.ShouldBe("John Doe");
        result.DateOfBirth.ShouldBe(patient.DateOfBirth);
    }

    [Fact]
    public void ToV2Dto_ShouldCalculateAge()
    {
        // Arrange - someone born 30 years ago
        var birthDate = DateTime.UtcNow.AddYears(-30).AddDays(-10);
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            DateOfBirth = birthDate
        };

        // Act
        var result = PatientMapper.ToV2Dto(patient);

        // Assert
        result.Age.ShouldBe(30);
    }

    [Fact]
    public void ToV2Dto_ShouldCalculateAge_BeforeBirthdayThisYear()
    {
        // Arrange - someone who hasn't had their birthday yet this year
        var birthDate = DateTime.UtcNow.AddYears(-25).AddDays(10);
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@test.com",
            DateOfBirth = birthDate
        };

        // Act
        var result = PatientMapper.ToV2Dto(patient);

        // Assert
        result.Age.ShouldBe(24); // Still 24, birthday hasn't happened
    }

    [Fact]
    public void ToV2Dto_ShouldIncludeAuditTimestamps()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            DateOfBirth = DateTime.UtcNow.AddYears(-30),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Act
        var result = PatientMapper.ToV2Dto(patient);

        // Assert
        result.CreatedAt.ShouldBe(createdAt);
        result.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void ToCommand_ShouldMapAllProperties()
    {
        // Arrange
        var request = new CreatePatientRequest(
            FirstName: "New",
            LastName: "Patient",
            Email: "new@test.com",
            Phone: "555-0200",
            DateOfBirth: new DateTime(1995, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Notes: "Test notes"
        );

        // Act
        var result = PatientMapper.ToCommand(request);

        // Assert
        result.FirstName.ShouldBe("New");
        result.LastName.ShouldBe("Patient");
        result.Email.ShouldBe("new@test.com");
        result.Phone.ShouldBe("555-0200");
        result.DateOfBirth.ShouldBe(request.DateOfBirth);
    }

    [Fact]
    public void ToV2Dtos_ShouldMapMultiplePatients()
    {
        // Arrange
        var patients = new List<Patient>
        {
            new() { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = DateTime.UtcNow.AddYears(-30) },
            new() { Id = PatientId2, FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", Phone = "555-0100", DateOfBirth = DateTime.UtcNow.AddYears(-25) }
        };

        // Act
        var results = PatientMapper.ToV2Dtos(patients).ToList();

        // Assert
        results.Count.ShouldBe(2);
        results[0].Id.ShouldBe(PatientId1);
        results[1].Id.ShouldBe(PatientId2);
        results.ShouldAllBe(p => p.Age > 0);
    }

    [Fact]
    public void ToV2Dto_WithNullPhone_ShouldPreserveNull()
    {
        // Arrange
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            DateOfBirth = DateTime.UtcNow.AddYears(-30)
        };

        // Act
        var result = PatientMapper.ToV2Dto(patient);

        // Assert
        result.Phone.ShouldBeNull();
    }
}

