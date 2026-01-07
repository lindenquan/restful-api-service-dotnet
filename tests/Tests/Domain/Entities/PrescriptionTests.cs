using Domain;
using Shouldly;

namespace Tests.Domain.Entities;

/// <summary>
/// Unit tests for Prescription entity.
/// </summary>
public class PrescriptionTests
{
    [Fact]
    public void IsExpired_WhenExpiryDateInFuture_ShouldReturnFalse()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };

        // Act & Assert
        prescription.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiryDateInPast_ShouldReturnTrue()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        prescription.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiryDateIsNow_ShouldReturnFalse()
    {
        // Arrange - Use a time slightly in the future to avoid race conditions
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddSeconds(1)
        };

        // Act & Assert
        prescription.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void CanRefill_WhenNotExpiredAndHasRefills_ShouldReturnTrue()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            RefillsRemaining = 3
        };

        // Act & Assert
        prescription.CanRefill.ShouldBeTrue();
    }

    [Fact]
    public void CanRefill_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            RefillsRemaining = 3
        };

        // Act & Assert
        prescription.CanRefill.ShouldBeFalse();
    }

    [Fact]
    public void CanRefill_WhenNoRefillsRemaining_ShouldReturnFalse()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            RefillsRemaining = 0
        };

        // Act & Assert
        prescription.CanRefill.ShouldBeFalse();
    }

    [Fact]
    public void CanRefill_WhenExpiredAndNoRefills_ShouldReturnFalse()
    {
        // Arrange
        var prescription = new Prescription
        {
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            RefillsRemaining = 0
        };

        // Act & Assert
        prescription.CanRefill.ShouldBeFalse();
    }
}

