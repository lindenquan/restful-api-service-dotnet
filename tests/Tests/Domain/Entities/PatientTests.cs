using Domain;
using Shouldly;

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
        patient.FullName.ShouldBe("John Doe");
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
        patient.FullName.ShouldBe("John ");
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
        patient.FullName.ShouldBe(" Doe");
    }

    [Fact]
    public void Patient_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var patient = new Patient();

        // Assert
        patient.Prescriptions.ShouldNotBeNull();
        patient.Prescriptions.ShouldBeEmpty();
        patient.Orders.ShouldNotBeNull();
        patient.Orders.ShouldBeEmpty();
    }

    [Fact]
    public void Patient_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var patient = new Patient();

        // Assert
        patient.FirstName.ShouldBeEmpty();
        patient.LastName.ShouldBeEmpty();
        patient.Email.ShouldBeEmpty();
        patient.Phone.ShouldBeNull();
    }
}

