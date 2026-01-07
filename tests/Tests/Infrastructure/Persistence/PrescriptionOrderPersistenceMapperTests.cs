using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Shouldly;

namespace Tests.Infrastructure.Persistence;

public sealed class PrescriptionOrderPersistenceMapperTests
{
    private static readonly Guid TestId1 = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TestId2 = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PrescriptionId1 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PrescriptionId2 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid CreatedById = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid UpdatedById = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [Fact]
    public void ToDomain_ShouldMapAllPropertiesIncludingEnumConversion()
    {
        // Arrange
        var dataModel = new PrescriptionOrderDataModel
        {
            Id = TestId1,
            PatientId = PatientId1,
            PrescriptionId = PrescriptionId1,
            OrderDate = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc),
            Status = OrderStatusData.Ready,
            Notes = "Ready for pickup",
            FulfilledDate = new DateTime(2024, 3, 16, 14, 0, 0, DateTimeKind.Utc),
            PickupDate = null,
            Metadata = new DataModelMetadata
            {
                CreatedAt = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = CreatedById,
                UpdatedAt = new DateTime(2024, 3, 16, 14, 0, 0, DateTimeKind.Utc),
                UpdatedBy = UpdatedById
            }
        };

        // Act
        var result = PrescriptionOrderPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.PatientId.ShouldBe(PatientId1);
        result.PrescriptionId.ShouldBe(PrescriptionId1);
        result.OrderDate.ShouldBe(new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        result.Status.ShouldBe(OrderStatus.Ready);
        result.Notes.ShouldBe("Ready for pickup");
        result.FulfilledDate.ShouldBe(new DateTime(2024, 3, 16, 14, 0, 0, DateTimeKind.Utc));
        result.PickupDate.ShouldBeNull();
        result.CreatedAt.ShouldBe(new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        result.CreatedBy.ShouldBe(CreatedById);
    }

    [Theory]
    [InlineData(OrderStatusData.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatusData.Processing, OrderStatus.Processing)]
    [InlineData(OrderStatusData.Ready, OrderStatus.Ready)]
    [InlineData(OrderStatusData.Completed, OrderStatus.Completed)]
    [InlineData(OrderStatusData.Cancelled, OrderStatus.Cancelled)]
    public void ToDomain_ShouldMapAllOrderStatusValues(OrderStatusData input, OrderStatus expected)
    {
        // Arrange
        var dataModel = new PrescriptionOrderDataModel
        {
            Id = TestId1,
            PatientId = PatientId1,
            PrescriptionId = PrescriptionId1,
            OrderDate = DateTime.UtcNow,
            Status = input
        };

        // Act
        var result = PrescriptionOrderPersistenceMapper.ToDomain(dataModel);

        // Assert
        result.Status.ShouldBe(expected);
    }

    [Fact]
    public void ToDataModel_ShouldMapAllPropertiesIncludingEnumConversion()
    {
        // Arrange
        var entity = new PrescriptionOrder
        {
            Id = TestId1,
            PatientId = PatientId1,
            PrescriptionId = PrescriptionId1,
            OrderDate = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Ready,
            Notes = "Ready for pickup",
            FulfilledDate = new DateTime(2024, 3, 16, 14, 0, 0, DateTimeKind.Utc),
            PickupDate = null
        };

        // Act
        var result = PrescriptionOrderPersistenceMapper.ToDataModel(entity);

        // Assert
        result.Id.ShouldBe(TestId1);
        result.PatientId.ShouldBe(PatientId1);
        result.PrescriptionId.ShouldBe(PrescriptionId1);
        result.OrderDate.ShouldBe(new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        result.Status.ShouldBe(OrderStatusData.Ready);
        result.Notes.ShouldBe("Ready for pickup");
        result.FulfilledDate.ShouldBe(new DateTime(2024, 3, 16, 14, 0, 0, DateTimeKind.Utc));
        result.PickupDate.ShouldBeNull();
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatusData.Pending)]
    [InlineData(OrderStatus.Processing, OrderStatusData.Processing)]
    [InlineData(OrderStatus.Ready, OrderStatusData.Ready)]
    [InlineData(OrderStatus.Completed, OrderStatusData.Completed)]
    [InlineData(OrderStatus.Cancelled, OrderStatusData.Cancelled)]
    public void ToDataModel_ShouldMapAllOrderStatusValues(OrderStatus input, OrderStatusData expected)
    {
        // Arrange
        var entity = new PrescriptionOrder
        {
            Id = TestId1,
            PatientId = PatientId1,
            PrescriptionId = PrescriptionId1,
            OrderDate = DateTime.UtcNow,
            Status = input
        };

        // Act
        var result = PrescriptionOrderPersistenceMapper.ToDataModel(entity);

        // Assert
        result.Status.ShouldBe(expected);
    }

    [Fact]
    public void ToDomain_Collection_ShouldMapAllItems()
    {
        // Arrange
        var dataModels = new List<PrescriptionOrderDataModel>
        {
            new() { Id = TestId1, PatientId = PatientId1, PrescriptionId = PrescriptionId1, OrderDate = DateTime.UtcNow, Status = OrderStatusData.Pending },
            new() { Id = TestId2, PatientId = PatientId2, PrescriptionId = PrescriptionId2, OrderDate = DateTime.UtcNow, Status = OrderStatusData.Completed }
        };

        // Act
        var result = PrescriptionOrderPersistenceMapper.ToDomain(dataModels).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].Status.ShouldBe(OrderStatus.Pending);
        result[1].Status.ShouldBe(OrderStatus.Completed);
    }
}

