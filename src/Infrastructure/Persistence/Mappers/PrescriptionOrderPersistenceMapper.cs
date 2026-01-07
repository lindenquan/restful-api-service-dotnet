using Domain;
using Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between PrescriptionOrder domain entity and PrescriptionOrderDataModel.
/// Uses Mapperly source generator for compile-time mapping.
/// </summary>
[Mapper]
public static partial class PrescriptionOrderPersistenceMapper
{
    [MapProperty(nameof(PrescriptionOrderDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedAt), nameof(PrescriptionOrder.CreatedAt))]
    [MapProperty(nameof(PrescriptionOrderDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedBy), nameof(PrescriptionOrder.CreatedBy))]
    [MapProperty(nameof(PrescriptionOrderDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedAt), nameof(PrescriptionOrder.UpdatedAt))]
    [MapProperty(nameof(PrescriptionOrderDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedBy), nameof(PrescriptionOrder.UpdatedBy))]
    [MapperIgnoreTarget(nameof(PrescriptionOrder.Patient))]
    [MapperIgnoreTarget(nameof(PrescriptionOrder.Prescription))]
    public static partial PrescriptionOrder ToDomain(PrescriptionOrderDataModel model);

    // Note: Metadata is managed by repository, not copied from entity
    [MapperIgnoreSource(nameof(PrescriptionOrder.CreatedAt))]
    [MapperIgnoreSource(nameof(PrescriptionOrder.CreatedBy))]
    [MapperIgnoreSource(nameof(PrescriptionOrder.UpdatedAt))]
    [MapperIgnoreSource(nameof(PrescriptionOrder.UpdatedBy))]
    [MapperIgnoreSource(nameof(PrescriptionOrder.Patient))]
    [MapperIgnoreSource(nameof(PrescriptionOrder.Prescription))]
    [MapperIgnoreTarget(nameof(PrescriptionOrderDataModel.Metadata))]
    public static partial PrescriptionOrderDataModel ToDataModel(PrescriptionOrder entity);

    // Custom mapping for enum conversion
    private static OrderStatus MapStatus(OrderStatusData status) => (OrderStatus)status;
    private static OrderStatusData MapStatus(OrderStatus status) => (OrderStatusData)status;

    public static IEnumerable<PrescriptionOrder> ToDomain(IEnumerable<PrescriptionOrderDataModel> models) =>
        models.Select(ToDomain);
}

