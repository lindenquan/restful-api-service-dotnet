using Domain;
using Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between Prescription domain entity and PrescriptionDataModel.
/// Uses Mapperly source generator for compile-time mapping.
/// </summary>
[Mapper]
public static partial class PrescriptionPersistenceMapper
{
    [MapProperty(nameof(PrescriptionDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedAt), nameof(Prescription.CreatedAt))]
    [MapProperty(nameof(PrescriptionDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedBy), nameof(Prescription.CreatedBy))]
    [MapProperty(nameof(PrescriptionDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedAt), nameof(Prescription.UpdatedAt))]
    [MapProperty(nameof(PrescriptionDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedBy), nameof(Prescription.UpdatedBy))]
    [MapperIgnoreTarget(nameof(Prescription.Patient))]
    [MapperIgnoreTarget(nameof(Prescription.Orders))]
    public static partial Prescription ToDomain(PrescriptionDataModel model);

    // Note: Metadata is managed by repository, not copied from entity
    [MapperIgnoreSource(nameof(Prescription.CreatedAt))]
    [MapperIgnoreSource(nameof(Prescription.CreatedBy))]
    [MapperIgnoreSource(nameof(Prescription.UpdatedAt))]
    [MapperIgnoreSource(nameof(Prescription.UpdatedBy))]
    [MapperIgnoreSource(nameof(Prescription.Patient))]
    [MapperIgnoreSource(nameof(Prescription.Orders))]
    [MapperIgnoreSource(nameof(Prescription.IsExpired))]
    [MapperIgnoreSource(nameof(Prescription.CanRefill))]
    [MapperIgnoreTarget(nameof(PrescriptionDataModel.Metadata))]
    public static partial PrescriptionDataModel ToDataModel(Prescription entity);

    public static IEnumerable<Prescription> ToDomain(IEnumerable<PrescriptionDataModel> models) =>
        models.Select(ToDomain);
}

