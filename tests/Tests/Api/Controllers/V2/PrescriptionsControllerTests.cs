using Application.Prescriptions.Operations;
using Domain;
using DTOs.Shared;
using DTOs.V2;
using Infrastructure.Api.Controllers.V2;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;

namespace Tests.Api.Controllers.V2;

/// <summary>
/// Unit tests for V2 PrescriptionsController.
/// </summary>
public class PrescriptionsControllerTests
{
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PrescriptionId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PrescriptionId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PrescriptionId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IMediator> _mediatorMock;
    private readonly PrescriptionsController _controller;
    private readonly PaginationSettings _paginationSettings;

    public PrescriptionsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _paginationSettings = new PaginationSettings { DefaultPageSize = 20, MaxPageSize = 100 };
        _controller = new PrescriptionsController(_mediatorMock.Object, _paginationSettings);

        // Set up HttpContext for pagination helper
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/api/v2/prescriptions";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkWithPagedPrescriptions()
    {
        // Arrange
        var prescriptions = CreateTestPrescriptions();
        var pagedData = new PagedData<Prescription>(prescriptions, 2);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPrescriptionsPagedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions { Count = true };

        // Act
        var result = await _controller.GetAll(query, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var pagedResult = okResult.Value.ShouldBeOfType<PagedResult<PrescriptionDto>>();
        pagedResult.Value.ShouldNotBeNull();
        pagedResult.Value.Count.ShouldBe(2);
        pagedResult.Value.First().DaysUntilExpiry.ShouldBeGreaterThan(0); // V2 includes days until expiry
    }

    [Fact]
    public async Task GetById_ExistingPrescription_ShouldReturnOk()
    {
        // Arrange
        var prescription = CreateTestPrescription(PrescriptionId1);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPrescriptionByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);

        // Act
        var result = await _controller.GetById(PrescriptionId1, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returned = okResult.Value.ShouldBeOfType<PrescriptionDto>();
        returned.Id.ShouldBe(PrescriptionId1);
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturnNotFound()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPrescriptionByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prescription?)null);

        // Act
        var result = await _controller.GetById(NonExistentId, CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetByPatient_ShouldReturnFilteredResults()
    {
        // Arrange
        var prescriptions = CreateTestPrescriptions();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllPrescriptionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescriptions);

        // Act
        var result = await _controller.GetByPatient(PatientId1, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returned = okResult.Value.ShouldBeAssignableTo<IEnumerable<PrescriptionDto>>();
        returned.ShouldNotBeNull();
        returned.Count().ShouldBe(1);
        returned.First().PatientId.ShouldBe(PatientId1);
    }

    [Fact]
    public async Task GetActive_ShouldReturnOnlyActivePrescriptions()
    {
        // Arrange
        var prescriptions = CreateTestPrescriptions();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllPrescriptionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescriptions);

        // Act
        var result = await _controller.GetActive(CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returned = okResult.Value.ShouldBeAssignableTo<IEnumerable<PrescriptionDto>>();
        returned.ShouldNotBeNull();
        returned.ShouldAllBe(p => !p.IsExpired && p.CanRefill);
    }

    [Fact]
    public async Task GetExpired_ShouldReturnOnlyExpiredPrescriptions()
    {
        // Arrange
        var prescriptions = CreateTestPrescriptions();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllPrescriptionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescriptions);

        // Act
        var result = await _controller.GetExpired(CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returned = okResult.Value.ShouldBeAssignableTo<IEnumerable<PrescriptionDto>>();
        returned.ShouldNotBeNull();
        // In test data, second prescription is expired
        returned.ShouldAllBe(p => p.IsExpired);
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var request = new CreatePrescriptionRequest(PatientId1, "Ibuprofen", "400mg", "Twice daily",
            60, 2, "Dr. Test", DateTime.UtcNow.AddMonths(12), "Take with food", false);

        var created = CreateTestPrescription(PrescriptionId3);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreatePrescriptionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.ShouldBeOfType<CreatedAtActionResult>();
    }

    private List<Prescription> CreateTestPrescriptions()
    {
        var patient1 = new Patient { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = DateTime.UtcNow.AddYears(-30) };
        var patient2 = new Patient { Id = PatientId2, FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", DateOfBirth = DateTime.UtcNow.AddYears(-25) };

        return
        [
            CreateTestPrescription(PrescriptionId1, patient1, expiryDate: DateTime.UtcNow.AddMonths(6)),
            CreateTestPrescription(PrescriptionId2, patient2, expiryDate: DateTime.UtcNow.AddDays(-30)) // expired
        ];
    }

    private static Prescription CreateTestPrescription(Guid id, Patient? patient = null, DateTime? expiryDate = null)
    {
        patient ??= new Patient { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = DateTime.UtcNow.AddYears(-30) };

        return new Prescription
        {
            Id = id,
            PatientId = patient.Id,
            Patient = patient,
            MedicationName = "Amoxicillin",
            Dosage = "500mg",
            Frequency = "Daily",
            Quantity = 30,
            RefillsRemaining = 2,
            PrescriberName = "Dr. Smith",
            PrescribedDate = DateTime.UtcNow.AddDays(-7),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddMonths(6),
            Instructions = "Take with food",
            CreatedAt = DateTime.UtcNow
        };
    }
}

