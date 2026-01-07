using Application.Patients.Operations;
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
/// Unit tests for V2 PatientsController with OData support.
/// </summary>
public class PatientsControllerTests
{
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PatientId3 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IMediator> _mediatorMock;
    private readonly PatientsController _controller;
    private readonly PaginationSettings _paginationSettings;

    public PatientsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _paginationSettings = new PaginationSettings { DefaultPageSize = 20, MaxPageSize = 100 };
        _controller = new PatientsController(_mediatorMock.Object, _paginationSettings);

        // Set up HttpContext for pagination helper
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/api/v2/patients";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetAll_WithODataQuery_ShouldReturnOkWithPagedPatients()
    {
        // Arrange
        var patients = new List<Patient>
        {
            new() { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", Phone = "555-0100", DateOfBirth = new DateTime(1990, 1, 15) },
            new() { Id = PatientId2, FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", DateOfBirth = new DateTime(1985, 6, 20) }
        };
        var pagedData = new PagedData<Patient>(patients, 2);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPatientsPagedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions { Top = 20, Skip = 0 };

        // Act
        var result = await _controller.GetAll(query, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var pagedResult = okResult.Value.ShouldBeOfType<PagedResult<PatientDto>>();
        pagedResult.Value.ShouldNotBeNull();
        pagedResult.Value.Count.ShouldBe(2);
        pagedResult.Value.First().Age.ShouldBeGreaterThan(0); // V2 includes age
    }

    [Fact]
    public async Task GetAll_WithSorting_ShouldPassParametersToHandler()
    {
        // Arrange
        var patients = new List<Patient>
        {
            new() { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = new DateTime(1990, 1, 15) }
        };
        var pagedData = new PagedData<Patient>(patients, 1);

        GetPatientsPagedQuery? capturedQuery = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPatientsPagedQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PagedData<Patient>>, CancellationToken>((query, _) =>
            {
                if (query is GetPatientsPagedQuery pagedQuery)
                    capturedQuery = pagedQuery;
            })
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions
        {
            Top = 10,
            Skip = 20,
            OrderBy = "lastName desc"
        };

        // Act
        await _controller.GetAll(query, CancellationToken.None);

        // Assert
        capturedQuery.ShouldNotBeNull();
        capturedQuery!.Skip.ShouldBe(20);
        capturedQuery.Top.ShouldBe(10);
        capturedQuery.OrderBy.ShouldBe("lastName");
        capturedQuery.Descending.ShouldBeTrue();
    }

    [Fact]
    public async Task GetById_ExistingPatient_ShouldReturnOk()
    {
        // Arrange
        var patient = new Patient
        {
            Id = PatientId1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            Phone = "555-0100",
            DateOfBirth = new DateTime(1990, 1, 15)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPatientByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        // Act
        var result = await _controller.GetById(PatientId1, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returnedPatient = okResult.Value.ShouldBeOfType<PatientDto>();
        returnedPatient.Id.ShouldBe(PatientId1);
        returnedPatient.FullName.ShouldBe("John Doe");
        returnedPatient.Age.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetById_NonExistentPatient_ShouldReturnNotFound()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPatientByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        // Act
        var result = await _controller.GetById(NonExistentId, CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Search_WithMatchingName_ShouldReturnFilteredResults()
    {
        // Arrange
        var patients = new List<Patient>
        {
            new() { Id = PatientId1, FirstName = "John", LastName = "Doe", Email = "john@test.com", DateOfBirth = new DateTime(1990, 1, 15) },
            new() { Id = PatientId2, FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", DateOfBirth = new DateTime(1985, 6, 20) }
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllPatientsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patients);

        // Act
        var result = await _controller.Search("John", CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var returnedPatients = okResult.Value.ShouldBeAssignableTo<IEnumerable<PatientDto>>();
        returnedPatients.ShouldNotBeNull();
        returnedPatients.Count().ShouldBe(1);
        returnedPatients.First().FirstName.ShouldBe("John");
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var request = new CreatePatientRequest("New", "Patient", "new@test.com", "555-0200",
            new DateTime(1995, 3, 10), null);

        var createdPatient = new Patient
        {
            Id = PatientId3,
            FirstName = "New",
            LastName = "Patient",
            Email = "new@test.com",
            Phone = "555-0200",
            DateOfBirth = new DateTime(1995, 3, 10)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreatePatientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPatient);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.ShouldBeOfType<CreatedAtActionResult>();
        var returnedPatient = createdResult.Value.ShouldBeOfType<PatientDto>();
        returnedPatient.Id.ShouldBe(PatientId3);
        returnedPatient.FullName.ShouldBe("New Patient");
    }

    [Fact]
    public async Task Create_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreatePatientRequest("", "Patient", "invalid", null,
            new DateTime(1995, 3, 10), null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreatePatientCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid email format"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }
}

