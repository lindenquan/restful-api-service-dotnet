using Application.Interfaces.Repositories;
using Application.Patients.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Patients;

/// <summary>
/// Unit tests for CreatePatientHandler.
/// </summary>
public class CreatePatientHandlerTests
{
    private static readonly Guid TestPatientId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly CreatePatientHandler _handler;

    public CreatePatientHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);

        _handler = new CreatePatientHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePatient()
    {
        // Arrange
        var dateOfBirth = new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        _patientRepoMock
            .Setup(r => r.GetByEmailAsync("john.doe@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        Patient? capturedPatient = null;
        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, CancellationToken>((p, _) => capturedPatient = p)
            .Returns(Task.CompletedTask);

        _patientRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new Patient
            {
                Id = id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "555-1234",
                DateOfBirth = dateOfBirth
            });

        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            Phone: "555-1234",
            DateOfBirth: dateOfBirth
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedPatient.ShouldNotBeNull();
        capturedPatient!.FirstName.ShouldBe("John");
        capturedPatient.LastName.ShouldBe("Doe");
        capturedPatient.Email.ShouldBe("john.doe@example.com");
        capturedPatient.Phone.ShouldBe("555-1234");
        capturedPatient.DateOfBirth.ShouldBe(dateOfBirth);

        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.FullName.ShouldBe("John Doe");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldThrowArgumentException()
    {
        // Arrange
        var existingPatient = new Patient
        {
            Id = TestPatientId,
            FirstName = "Existing",
            LastName = "Patient",
            Email = "existing@example.com"
        };

        _patientRepoMock
            .Setup(r => r.GetByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        var command = new CreatePatientCommand(
            FirstName: "New",
            LastName: "Patient",
            Email: "existing@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("existing@example.com");
        exception.Message.ShouldContain("already exists");
    }

    [Fact]
    public async Task Handle_WithNullPhone_ShouldCreatePatientWithNullPhone()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        Patient? capturedPatient = null;
        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, CancellationToken>((p, _) => capturedPatient = p)
            .Returns(Task.CompletedTask);

        _patientRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = TestPatientId, FirstName = "John", LastName = "Doe", Email = "test@example.com" });

        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "test@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedPatient.ShouldNotBeNull();
        capturedPatient!.Phone.ShouldBeNull();
    }
}

