using Domain;
using Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between Patient domain entity and PatientDataModel.
/// Uses Mapperly source generator for compile-time mapping.
/// </summary>
[Mapper]
public static partial class PatientPersistenceMapper
{
    [MapProperty(nameof(PatientDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedAt), nameof(Patient.CreatedAt))]
    [MapProperty(nameof(PatientDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedBy), nameof(Patient.CreatedBy))]
    [MapProperty(nameof(PatientDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedAt), nameof(Patient.UpdatedAt))]
    [MapProperty(nameof(PatientDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedBy), nameof(Patient.UpdatedBy))]
    [MapperIgnoreTarget(nameof(Patient.Prescriptions))]
    [MapperIgnoreTarget(nameof(Patient.Orders))]
    public static partial Patient ToDomain(PatientDataModel model);

    // Note: Metadata is managed by repository, not copied from entity
    [MapperIgnoreSource(nameof(Patient.CreatedAt))]
    [MapperIgnoreSource(nameof(Patient.CreatedBy))]
    [MapperIgnoreSource(nameof(Patient.UpdatedAt))]
    [MapperIgnoreSource(nameof(Patient.UpdatedBy))]
    [MapperIgnoreSource(nameof(Patient.Prescriptions))]
    [MapperIgnoreSource(nameof(Patient.Orders))]
    [MapperIgnoreSource(nameof(Patient.FullName))]
    [MapperIgnoreTarget(nameof(PatientDataModel.Metadata))]
    public static partial PatientDataModel ToDataModel(Patient entity);

    public static IEnumerable<Patient> ToDomain(IEnumerable<PatientDataModel> models) =>
        models.Select(ToDomain);
}

