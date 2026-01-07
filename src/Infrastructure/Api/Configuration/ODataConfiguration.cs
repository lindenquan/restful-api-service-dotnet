using Domain;
using DTOs.V1;
using DTOs.V2;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Infrastructure.Api.Configuration;

/// <summary>
/// OData EDM (Entity Data Model) configuration.
/// Provides metadata endpoint ($metadata) and service document without using [EnableQuery].
/// We maintain Clean Architecture by parsing OData queries manually in controllers.
/// </summary>
public static class ODataConfiguration
{
    /// <summary>
    /// Builds the OData EDM model for V1 API.
    /// Uses simplified DTOs (Order, Patient, Prescription).
    /// </summary>
    public static IEdmModel GetEdmModelV1()
    {
        var builder = new ODataConventionModelBuilder();

        // V1 Entity Sets - using simplified DTO names
        builder.EntitySet<OrderDto>("Orders");
        builder.EntitySet<Patient>("Patients");
        builder.EntitySet<Prescription>("Prescriptions");

        // Configure entity types
        builder.EntityType<OrderDto>().HasKey(o => o.Id);
        builder.EntityType<Patient>().HasKey(p => p.Id);
        builder.EntityType<Prescription>().HasKey(p => p.Id);

        // Ignore navigation properties (we don't support $expand in V1)
        builder.EntityType<Patient>().Ignore(p => p.Prescriptions);
        builder.EntityType<Patient>().Ignore(p => p.Orders);
        builder.EntityType<Prescription>().Ignore(p => p.Patient);

        return builder.GetEdmModel();
    }

    /// <summary>
    /// Builds the OData EDM model for V2 API.
    /// Uses descriptive DTOs (PrescriptionOrder, Patient, Prescription).
    /// </summary>
    public static IEdmModel GetEdmModelV2()
    {
        var builder = new ODataConventionModelBuilder();

        // V2 Entity Sets - using descriptive DTO names
        builder.EntitySet<PrescriptionOrderDto>("Orders");
        builder.EntitySet<PatientDto>("Patients");
        builder.EntitySet<PrescriptionDto>("Prescriptions");

        // Configure entity types
        builder.EntityType<PrescriptionOrderDto>().HasKey(o => o.Id);
        builder.EntityType<PatientDto>().HasKey(p => p.Id);
        builder.EntityType<PrescriptionDto>().HasKey(p => p.Id);

        return builder.GetEdmModel();
    }

    /// <summary>
    /// Builds a combined OData EDM model for both V1 and V2.
    /// This allows a single $metadata endpoint that describes both API versions.
    /// </summary>
    public static IEdmModel GetCombinedEdmModel()
    {
        var builder = new ODataConventionModelBuilder();

        // V1 Entity Sets (simplified names)
        var ordersV1 = builder.EntitySet<OrderDto>("OrdersV1");
        ordersV1.EntityType.HasKey(o => o.Id);

        // V2 Entity Sets (descriptive names)
        var ordersV2 = builder.EntitySet<PrescriptionOrderDto>("OrdersV2");
        ordersV2.EntityType.HasKey(o => o.Id);

        var patientsV2 = builder.EntitySet<PatientDto>("Patients");
        patientsV2.EntityType.HasKey(p => p.Id);

        var prescriptionsV2 = builder.EntitySet<PrescriptionDto>("Prescriptions");
        prescriptionsV2.EntityType.HasKey(p => p.Id);

        // Domain entities (for reference)
        builder.EntityType<Patient>().HasKey(p => p.Id);
        builder.EntityType<Patient>().Ignore(p => p.Prescriptions);
        builder.EntityType<Patient>().Ignore(p => p.Orders);
        builder.EntityType<Patient>().Ignore(p => p.FullName);

        builder.EntityType<Prescription>().HasKey(p => p.Id);
        builder.EntityType<Prescription>().Ignore(p => p.Patient);
        builder.EntityType<Prescription>().Ignore(p => p.IsExpired);
        builder.EntityType<Prescription>().Ignore(p => p.CanRefill);

        builder.EntityType<PrescriptionOrder>().HasKey(o => o.Id);
        builder.EntityType<PrescriptionOrder>().Ignore(o => o.Patient);
        builder.EntityType<PrescriptionOrder>().Ignore(o => o.Prescription);

        return builder.GetEdmModel();
    }
}

