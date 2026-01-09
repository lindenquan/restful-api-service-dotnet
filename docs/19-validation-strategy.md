# Validation Strategy

This document describes the multi-layer validation approach used to ensure data integrity.

## Overview

The API uses a **defense-in-depth** validation strategy with two complementary layers:

| Layer | Technology | Purpose | When It Runs |
|-------|------------|---------|--------------|
| **Application** | FluentValidation | Business rules, user-friendly errors | Before handler executes |
| **Database** | MongoDB JSON Schema | Last line of defense, data integrity | On document insert/update |

## Why Two Layers?

| Scenario | App Validation | DB Validation |
|----------|----------------|---------------|
| Normal API request | ✅ Catches errors | Never reached (app catches first) |
| Direct DB access (admin, migration) | ❌ Bypassed | ✅ Catches errors |
| Bug in application code | ❌ May be bypassed | ✅ Catches errors |
| Malicious request manipulation | ✅ Catches errors | ✅ Backup protection |

**For healthcare/financial data, both layers are essential.**

## Application Layer: FluentValidation

### How It Works

FluentValidation integrates with MediatR via `ValidationBehavior`:

```
Request → ValidationBehavior → [Validators] → Handler → Response
                    ↓
            ValidationException (if invalid)
```

### Validator Location

All validators are in the `Application` layer, co-located with their commands:

```
src/Application/
├── Patients/
│   └── Operations/
│       ├── CreatePatientCommand.cs
│       └── CreatePatientValidator.cs    ← Validates CreatePatientCommand
├── Prescriptions/
│   └── Operations/
│       ├── CreatePrescriptionCommand.cs
│       └── CreatePrescriptionValidator.cs
└── Orders/
    └── Operations/
        ├── CreateOrderCommand.cs
        └── CreateOrderValidator.cs
```

### Types of Validation Rules

#### 1. Basic Field Validation

```csharp
RuleFor(x => x.Email)
    .NotEmpty().WithMessage("Email is required")
    .EmailAddress().WithMessage("Invalid email format")
    .MaximumLength(200).WithMessage("Email cannot exceed 200 characters");
```

#### 2. Business Range Limits

```csharp
RuleFor(x => x.Quantity)
    .GreaterThan(0).WithMessage("Quantity must be positive")
    .LessThanOrEqualTo(1000).WithMessage("Quantity cannot exceed 1000");

RuleFor(x => x.DateOfBirth)
    .LessThan(DateTime.UtcNow).WithMessage("Date of birth must be in the past")
    .GreaterThan(DateTime.UtcNow.AddYears(-150)).WithMessage("Invalid date of birth");
```

#### 3. Cross-Field Validation

```csharp
RuleFor(x => x.EndDate)
    .GreaterThan(x => x.StartDate)
    .WithMessage("End date must be after start date");
```

#### 4. Conditional Validation

```csharp
// Phone required only for emergency contacts
RuleFor(x => x.Phone)
    .NotEmpty()
    .When(x => x.IsEmergencyContact)
    .WithMessage("Phone is required for emergency contacts");

// Controlled substances have stricter limits
RuleFor(x => x.RefillsAllowed)
    .Equal(0)
    .When(x => x.IsControlledSubstance)
    .WithMessage("Controlled substances cannot have refills");
```

#### 5. Async Database Validation

```csharp
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator(IUnitOfWork unitOfWork)
    {
        // Patient must exist in database
        RuleFor(x => x.PatientId)
            .MustAsync(async (patientId, ct) =>
            {
                var patient = await unitOfWork.Patients.GetByIdAsync(patientId, ct);
                return patient != null && !patient.IsDeleted;
            })
            .WithMessage("Patient does not exist");

        // Prescription must be valid and not expired
        RuleFor(x => x.PrescriptionId)
            .MustAsync(async (prescriptionId, ct) =>
            {
                var rx = await unitOfWork.Prescriptions.GetByIdAsync(prescriptionId, ct);
                return rx != null && rx.ExpiryDate > DateTime.UtcNow && rx.RefillsRemaining > 0;
            })
            .WithMessage("Prescription is invalid, expired, or has no refills remaining");
    }
}
```

### Error Response Format

When validation fails, the API returns a structured error response:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["Email is required", "Invalid email format"],
    "Quantity": ["Quantity must be positive"]
  }
}
```

## Database Layer: MongoDB Schema Validation

### How It Works

MongoDB supports JSON Schema validation at the collection level. Documents that violate the schema are rejected on insert/update.

### Schema Definition

Schemas are applied during database migration:

```javascript
db.createCollection("patients", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["firstName", "lastName", "email", "dateOfBirth"],
      properties: {
        firstName: {
          bsonType: "string",
          minLength: 1,
          maxLength: 100,
          description: "First name is required"
        },
        lastName: {
          bsonType: "string",
          minLength: 1,
          maxLength: 100
        },
        email: {
          bsonType: "string",
          pattern: "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
        },
        dateOfBirth: {
          bsonType: "date"
        },
        phone: {
          bsonType: ["string", "null"],
          maxLength: 20
        }
      }
    }
  },
  validationLevel: "strict",
  validationAction: "error"
});
```

### Validation Levels

| Level | Behavior |
|-------|----------|
| `strict` | Validates all inserts and updates (recommended) |
| `moderate` | Only validates documents that already match the schema |
| `off` | No validation |

### Validation Actions

| Action | Behavior |
|--------|----------|
| `error` | Reject the document (recommended for critical data) |
| `warn` | Accept but log a warning |

### Applying Schema via Migration

The `MongoDbMigrator` tool applies schemas during database setup:

```csharp
// In MongoDbMigrator.cs
private async Task ApplySchemaValidationAsync()
{
    Console.WriteLine("[3/3] Applying schema validation...");

    await ApplyPatientSchemaAsync();
    await ApplyPrescriptionSchemaAsync();
    await ApplyOrderSchemaAsync();
}

private async Task ApplyPatientSchemaAsync()
{
    var schema = new BsonDocument
    {
        ["bsonType"] = "object",
        ["required"] = new BsonArray { "firstName", "lastName", "email", "dateOfBirth" },
        ["properties"] = new BsonDocument
        {
            ["firstName"] = new BsonDocument
            {
                ["bsonType"] = "string",
                ["minLength"] = 1,
                ["maxLength"] = 100
            },
            // ... more properties
        }
    };

    var command = new BsonDocument
    {
        ["collMod"] = "patients",
        ["validator"] = new BsonDocument { ["$jsonSchema"] = schema },
        ["validationLevel"] = "strict",
        ["validationAction"] = "error"
    };

    await _database.RunCommandAsync<BsonDocument>(command);
}
```

## Validation Comparison

| Aspect | FluentValidation | MongoDB Schema |
|--------|------------------|----------------|
| **Error messages** | User-friendly, localized | Technical, generic |
| **Async validation** | ✅ Supports (DB lookups) | ❌ No |
| **Cross-field** | ✅ Full support | ⚠️ Limited |
| **Complex logic** | ✅ Any C# code | ❌ JSON Schema only |
| **Bypass possible** | ✅ Direct DB access | ❌ Always enforced |
| **Performance** | Runs before DB call | Runs during DB call |

## Best Practices

### 1. Keep Validators Close to Commands

```
Application/
└── Patients/
    └── Operations/
        ├── CreatePatientCommand.cs
        └── CreatePatientValidator.cs  ← Same folder
```

### 2. Descriptive Error Messages

```csharp
// ❌ Bad
RuleFor(x => x.Quantity).GreaterThan(0);

// ✅ Good
RuleFor(x => x.Quantity)
    .GreaterThan(0)
    .WithMessage("Quantity must be at least 1");
```

### 3. Domain-Specific Rules

```csharp
// Healthcare-specific validation
RuleFor(x => x.RefillsAllowed)
    .LessThanOrEqualTo(12)
    .WithMessage("DEA regulations limit refills to 12 per prescription");

RuleFor(x => x.ExpiryDate)
    .LessThan(DateTime.UtcNow.AddYears(1))
    .When(x => IsControlledSubstance(x.MedicationName))
    .WithMessage("Controlled substance prescriptions expire within 1 year");
```

### 4. Test Validators Thoroughly

```csharp
public class CreatePatientValidatorTests
{
    private readonly CreatePatientValidator _validator = new();

    [Fact]
    public void Validate_FutureDateOfBirth_ShouldHaveError()
    {
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            DateOfBirth: DateTime.UtcNow.AddDays(1)  // Future date
        );

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }
}
```

## Summary

| Layer | Validates | Catches |
|-------|-----------|---------|
| **FluentValidation** | Business rules, relationships | Invalid business logic |
| **MongoDB Schema** | Data types, required fields | Corrupt/malformed data |

**Use both layers for healthcare/financial applications.**

