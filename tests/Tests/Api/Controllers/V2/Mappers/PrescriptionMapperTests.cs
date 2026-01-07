using Domain;
using DTOs.V2;
using Infrastructure.Api.Controllers.V2.Mappers;
using Shouldly;

namespace Tests.Api.Controllers.V2.Mappers;

/// <summary>
/// Unit tests for V2 PrescriptionMapper.
/// </summary>
public class PrescriptionMapperTests
{
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PrescriptionId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PrescriptionId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ToV2Dto_ShouldMapAllProperties()
    {
        // Arrange
        var prescription = CreateTestPrescription();

        // Act
        var result = PrescriptionMapper.ToV2Dto(prescription);

        // Assert
        result.Id.ShouldBe(PrescriptionId1);
        result.PatientId.ShouldBe(PatientId1);
        result.PatientName.ShouldBe("John Doe");
        result.MedicationName.ShouldBe("Amoxicillin");
        result.Dosage.ShouldBe("500mg");
        result.Frequency.ShouldBe("Three times daily");
        result.Quantity.ShouldBe(30);
        result.RefillsRemaining.ShouldBe(2);
        result.PrescriberName.ShouldBe("Dr. Smith");
        result.Instructions.ShouldBe("Take with food");
        result.IsExpired.ShouldBeFalse();
        result.CanRefill.ShouldBeTrue();
    }

    [Fact]
    public void ToV2Dto_ShouldCalculateDaysUntilExpiry()
    {
        // Arrange - expires in 30 days
        var expiryDate = DateTime.UtcNow.Date.AddDays(30);
        var prescription = CreateTestPrescription(expiryDate: expiryDate);

        // Act
        var result = PrescriptionMapper.ToV2Dto(prescription);

        // Assert
        result.DaysUntilExpiry.ShouldBe(30);
    }

    [Fact]
    public void ToV2Dto_ShouldShowNegativeDaysForExpired()
    {
        // Arrange - expired 5 days ago
        var expiryDate = DateTime.UtcNow.Date.AddDays(-5);
        var prescription = CreateTestPrescription(expiryDate: expiryDate);

        // Act
        var result = PrescriptionMapper.ToV2Dto(prescription);

        // Assert
        result.DaysUntilExpiry.ShouldBe(-5);
        result.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void ToV2Dto_ShouldIncludeUpdatedAt()
    {
        // Arrange
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var prescription = CreateTestPrescription();
        prescription.UpdatedAt = updatedAt;

        // Act
        var result = PrescriptionMapper.ToV2Dto(prescription);

        // Assert
        result.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void ToCommand_ShouldMapAllProperties()
    {
        // Arrange
        var request = new CreatePrescriptionRequest(
            PatientId: PatientId1,
            MedicationName: "Ibuprofen",
            Dosage: "400mg",
            Frequency: "Twice daily",
            Quantity: 60,
            RefillsAllowed: 2,
            PrescriberName: "Dr. Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(12),
            Instructions: "Take with food",
            IsControlledSubstance: false
        );

        // Act
        var result = PrescriptionMapper.ToCommand(request);

        // Assert
        result.PatientId.ShouldBe(PatientId1);
        result.MedicationName.ShouldBe("Ibuprofen");
        result.Dosage.ShouldBe("400mg");
        result.Frequency.ShouldBe("Twice daily");
        result.Quantity.ShouldBe(60);
        result.RefillsAllowed.ShouldBe(2);
        result.PrescriberName.ShouldBe("Dr. Test");
        result.Instructions.ShouldBe("Take with food");
    }

    [Fact]
    public void ToCommand_WithControlledSubstance_ShouldAddPrefix()
    {
        // Arrange
        var request = new CreatePrescriptionRequest(
            PatientId: PatientId1, MedicationName: "Oxycodone", Dosage: "5mg", Frequency: "As needed",
            Quantity: 30, RefillsAllowed: 0, PrescriberName: "Dr. Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(1), Instructions: "Pain management",
            IsControlledSubstance: true
        );

        // Act
        var result = PrescriptionMapper.ToCommand(request);

        // Assert
        result.Instructions.ShouldStartWith("[CONTROLLED SUBSTANCE]");
        result.Instructions.ShouldContain("Pain management");
    }

    [Fact]
    public void ToV2Dtos_ShouldMapMultiplePrescriptions()
    {
        // Arrange
        var prescriptions = new List<Prescription>
        {
            CreateTestPrescription(id: PrescriptionId1),
            CreateTestPrescription(id: PrescriptionId2)
        };

        // Act
        var results = PrescriptionMapper.ToV2Dtos(prescriptions).ToList();

        // Assert
        results.Count.ShouldBe(2);
        results[0].Id.ShouldBe(PrescriptionId1);
        results[1].Id.ShouldBe(PrescriptionId2);
    }

    private Prescription CreateTestPrescription(Guid? id = null, DateTime? expiryDate = null)
    {
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            DateOfBirth = DateTime.UtcNow.AddYears(-30)
        };

        return new Prescription
        {
            Id = id ?? PrescriptionId1,
            PatientId = PatientId1,
            Patient = patient,
            MedicationName = "Amoxicillin",
            Dosage = "500mg",
            Frequency = "Three times daily",
            Quantity = 30,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Smith",
            PrescribedDate = DateTime.UtcNow.AddDays(-7),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddMonths(6),
            Instructions = "Take with food",
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
    }
}

