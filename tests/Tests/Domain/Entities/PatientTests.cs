using Entities;
using FluentAssertions;

namespace Tests.Domain.Entities;

/// <summary>
/// Unit tests for Patient entity.
/// </summary>
public class PatientTests
{
    [Fact]
    public void FullName_ShouldCombineFirstAndLastName()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act & Assert
        patient.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void FullName_WithEmptyLastName_ShouldReturnFirstNameWithSpace()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "John",
            LastName = ""
        };

        // Act & Assert
        patient.FullName.Should().Be("John ");
    }

    [Fact]
    public void FullName_WithEmptyFirstName_ShouldReturnSpaceWithLastName()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "",
            LastName = "Doe"
        };

        // Act & Assert
        patient.FullName.Should().Be(" Doe");
    }

    [Fact]
    public void Patient_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var patient = new Patient();

        // Assert
        patient.Prescriptions.Should().NotBeNull();
        patient.Prescriptions.Should().BeEmpty();
        patient.Orders.Should().NotBeNull();
        patient.Orders.Should().BeEmpty();
    }

    [Fact]
    public void Patient_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var patient = new Patient();

        // Assert
        patient.FirstName.Should().BeEmpty();
        patient.LastName.Should().BeEmpty();
        patient.Email.Should().BeEmpty();
        patient.Phone.Should().BeNull();
    }
}

