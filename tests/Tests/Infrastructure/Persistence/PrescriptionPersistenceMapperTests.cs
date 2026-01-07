using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Shouldly;

namespace Tests.Infrastructure.Persistence;

public sealed class PrescriptionPersistenceMapperTests
{
    private static readonly Guid TestId1 = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TestId2 = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CreatedById = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void ToDomain_ShouldMapAllProperties()
    {
        // Arrange
        var dataModel = new PrescriptionDataModel
        {
            Id = TestId1,
            PatientId = PatientId1,
            MedicationName = "Amoxicillin",
            Dosage = "500mg",
            Frequency = "3 times daily",
            Quantity = 30,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Smith",
            PrescribedDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Instructions = "Take with food",
            Metadata = new DataModelMetadata
            {
                CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                CreatedBy = CreatedById,
                UpdatedAt = new DateTime(2024, 2, 1, 14, 0, 0, DateTimeKind.Utc),
                UpdatedBy = CreatedById
            }
        };

        // Act
        var result = PrescriptionPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.PatientId.ShouldBe(PatientId1);
        result.MedicationName.ShouldBe("Amoxicillin");
        result.Dosage.ShouldBe("500mg");
        result.Frequency.ShouldBe("3 times daily");
        result.Quantity.ShouldBe(30);
        result.RefillsRemaining.ShouldBe(2);
        result.PrescriberName.ShouldBe("Dr. Smith");
        result.PrescribedDate.ShouldBe(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.ExpiryDate.ShouldBe(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.Instructions.ShouldBe("Take with food");
        result.CreatedAt.ShouldBe(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        result.CreatedBy.ShouldBe(CreatedById);
    }

    [Fact]
    public void ToDomain_ShouldComputeIsExpiredAndCanRefill()
    {
        // Arrange - expired prescription
        var expiredDataModel = new PrescriptionDataModel
        {
            Id = TestId1,
            PatientId = PatientId1,
            MedicationName = "Test Med",
            Dosage = "10mg",
            Frequency = "daily",
            Quantity = 10,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Test",
            PrescribedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc) // Already expired
        };

        // Act
        var result = PrescriptionPersistenceMapper.ToDomain(expiredDataModel);

        // Assert - computed properties work correctly
        result.IsExpired.ShouldBeTrue();
        result.CanRefill.ShouldBeFalse(); // Can't refill expired prescription
    }

    [Fact]
    public void ToDomain_ValidPrescription_CanRefill()
    {
        // Arrange - valid prescription with refills
        var validDataModel = new PrescriptionDataModel
        {
            Id = TestId1,
            PatientId = PatientId1,
            MedicationName = "Test Med",
            Dosage = "10mg",
            Frequency = "daily",
            Quantity = 10,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Test",
            PrescribedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        // Act
        var result = PrescriptionPersistenceMapper.ToDomain(validDataModel);

        // Assert
        result.IsExpired.ShouldBeFalse();
        result.CanRefill.ShouldBeTrue();
    }

    [Fact]
    public void ToDataModel_ShouldMapAllProperties()
    {
        // Arrange
        var entity = new Prescription
        {
            Id = TestId1,
            PatientId = PatientId1,
            MedicationName = "Amoxicillin",
            Dosage = "500mg",
            Frequency = "3 times daily",
            Quantity = 30,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Smith",
            PrescribedDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Instructions = "Take with food"
        };

        // Act
        var result = PrescriptionPersistenceMapper.ToDataModel(entity);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.PatientId.ShouldBe(PatientId1);
        result.MedicationName.ShouldBe("Amoxicillin");
        result.Dosage.ShouldBe("500mg");
        result.Frequency.ShouldBe("3 times daily");
        result.Quantity.ShouldBe(30);
        result.RefillsRemaining.ShouldBe(2);
        result.PrescriberName.ShouldBe("Dr. Smith");
        result.PrescribedDate.ShouldBe(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.ExpiryDate.ShouldBe(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.Instructions.ShouldBe("Take with food");
    }

    [Fact]
    public void ToDomain_Collection_ShouldMapAllItems()
    {
        // Arrange
        var dataModels = new List<PrescriptionDataModel>
        {
            new() { Id = TestId1, PatientId = PatientId1, MedicationName = "Med1", Dosage = "10mg", Frequency = "daily", Quantity = 10, RefillsRemaining = 1, PrescriberName = "Dr. A", PrescribedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddYears(1) },
            new() { Id = TestId2, PatientId = PatientId2, MedicationName = "Med2", Dosage = "20mg", Frequency = "twice daily", Quantity = 20, RefillsRemaining = 3, PrescriberName = "Dr. B", PrescribedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddYears(1) }
        };

        // Act
        var result = PrescriptionPersistenceMapper.ToDomain(dataModels).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].MedicationName.ShouldBe("Med1");
        result[1].MedicationName.ShouldBe("Med2");
    }
}

