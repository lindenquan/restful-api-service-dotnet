# DIP & Clean Architecture Cheat Sheet

> **For presentations and demos** - Quick reference for explaining DIP correctly.

---

## DIP in One Sentence

> **"High-level modules define interfaces, low-level modules implement them."**

---

## DIP Has TWO Parts (Both Required!)

| Part | Rule | Common Mistake |
|------|------|----------------|
| **Part 1** | Depend on abstractions (interfaces), not concrete classes | ✅ Most devs get this |
| **Part 2** | Abstractions must be OWNED by the **consumer**, not the implementer | ❌ Most devs miss this! |

### Consumer vs Implementer

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│   CONSUMER (Caller)              IMPLEMENTER (Provider)                 │
│   ─────────────────              ─────────────────────                  │
│   Application Layer              Infrastructure/Infrastructure                │
│   OrderHandler                   MongoOrderRepository                   │
│                                                                         │
│   "I NEED to save orders"        "I KNOW HOW to save to MongoDB"        │
│   "I define WHAT I need"         "I provide HOW it's done"              │
│                                                                         │
│   OWNS the interface ✅          IMPLEMENTS the interface               │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

**Key Insight:** The one who USES the interface should OWN it, not the one who implements it.

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Part 1 alone is NOT enough!                                            │
│                                                                         │
│  You can use interfaces everywhere and STILL violate DIP                │
│  if the interface lives in the wrong layer (owned by implementer).      │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## The 3-Stage Progression

### Stage 1: No Interfaces ❌❌

```
  Business Layer                    Data Layer
 ┌─────────────────┐              ┌─────────────────┐
 │  OrderService   │──depends on─▶│ SqlRepository   │
 │                 │              │   (concrete)    │
 │ new SqlRepo()   │              └─────────────────┘
 └─────────────────┘
```
- ❌ Concrete depends on concrete
- ❌ Can't test, can't swap database

---

### Stage 2: Interface in Wrong Place 🟡

```
  Business Layer                    Data Layer
 ┌─────────────────┐              ┌─────────────────┐
 │  OrderService   │──depends on─▶│ IRepository     │ ← Interface HERE (wrong!)
 │                 │              ├─────────────────┤
 │ IRepository repo│              │ SqlRepository   │
 └─────────────────┘              └─────────────────┘

 Project Reference: Business.csproj → DataAccess.csproj  ❌ Still wrong!
```
- ✅ Uses interface (Part 1 satisfied)
- ❌ Interface owned by low-level (Part 2 violated)
- ❌ Business still references Data layer

---

### Stage 3: DIP Applied ✅✅

```
  Infrastructure Layer              Application Layer
 ┌─────────────────┐              ┌─────────────────┐
 │ MongoRepository │──implements─▶│ IRepository     │ ← Interface HERE (correct!)
 │   (concrete)    │              ├─────────────────┤
 └─────────────────┘              │ OrderHandler    │
                                  └─────────────────┘

 Project Reference: Infrastructure.csproj → Application.csproj  ✅ INVERTED!
```
- ✅ Uses interface (Part 1 satisfied)
- ✅ Interface owned by high-level (Part 2 satisfied)
- ✅ Infrastructure depends on Application (inverted!)

---

## Why It's Called "INVERSION"

```
TRADITIONAL (N-Layer):
    Business ────────▶ Data Access
              depends on

INVERTED (Clean Architecture):
    Infrastructure ────────▶ Application
                   depends on
                   (implements interface)
```

**The dependency arrow FLIPS DIRECTION** because we moved the interface.

---

## Quick Verification: Check Your .csproj Files

```xml
<!-- ❌ WRONG: Application depends on Adapters (high-level depends on low-level) -->
<!-- Application.csproj -->
<ProjectReference Include="..\Adapters\Infrastructure.csproj" />  <!-- NEVER do this! -->

<!-- ✅ CORRECT: Adapters depends on Application (low-level depends on high-level) -->
<!-- Infrastructure.csproj -->
<ProjectReference Include="..\Application\Application.csproj" />  <!-- This is DIP! -->
```

**Our project does it correctly:**
```
Application.csproj
  └── References: Entities.csproj only ✅ (no infrastructure dependencies)

Infrastructure.csproj
  └── References: Application.csproj ✅ (implements interfaces from Application)
  └── References: Entities.csproj
  └── References: DTOs.csproj
```

---

## One-Liner Explanations

| Concept | One-Liner |
|---------|-----------|
| **DIP** | "The consumer defines what it NEEDS, the implementer adapts to it" |
| **Inversion** | "Move interface ownership from implementer to consumer, so the dependency arrow flips" |
| **Clean Architecture** | "Dependencies point inward toward business rules" |
| **Why it matters** | "Business logic has zero knowledge of databases, frameworks, or external services" |

---

## Common Interview/Demo Questions

**Q: "Isn't using interfaces enough for DIP?"**
> No! The interface must be OWNED by the **consumer** (who uses it), not the **implementer** (who provides it). If `IRepository` lives in the Data layer (implementer), Business still depends on Data.

**Q: "What does 'inversion' mean?"**
> In traditional N-Layer, the implementer (Data layer) defines the interface. With DIP, the consumer (Application) defines the interface. Now the implementer must depend on the consumer to implement it. The dependency arrow inverts.

**Q: "How do I verify DIP in code?"**
> Check project references. The consumer should NOT reference the implementer. In our project: `Application.csproj` has no reference to `Infrastructure.csproj` ✅

**Q: "Consumer vs Implementer - what's the difference?"**
> **Consumer** = the code that CALLS the interface (e.g., `OrderHandler` calls `_repository.SaveAsync()`)
> **Implementer** = the code that PROVIDES the implementation (e.g., `MongoOrderRepository` implements `SaveAsync()`)

---

## Visual Summary

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│   N-LAYER                           CLEAN ARCHITECTURE (Our Project)    │
│                                                                         │
│   ┌─────────┐                       ┌─────────────┐                     │
│   │   UI    │                       │   Adapters  │  (outer - Mongo,    │
│   └────┬────┘                       └──────┬──────┘   Redis, API)       │
│        │                                   │                            │
│        ▼                                   │ implements                 │
│   ┌─────────┐                              ▼                            │
│   │   BLL   │                       ┌─────────────┐                     │
│   └────┬────┘                       │ Application │  (defines IRepo,    │
│        │                            └──────┬──────┘   Use Cases)        │
│        ▼                                   │                            │
│   ┌─────────┐                              ▼                            │
│   │   DAL   │ ◀── IRepo here        ┌─────────────┐                     │
│   └────┬────┘     (WRONG)           │  Entities   │  (inner - Domain)   │
│        │                            └─────────────┘                     │
│        ▼                                                                │
│   ┌─────────┐                       Dependencies point INWARD ✅        │
│   │   DB    │                                                           │
│   └─────────┘                                                           │
│                                                                         │
│   Dependencies point DOWN ❌                                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Remember

1. **Adding interfaces ≠ DIP** (interface location matters!)
2. **Check project references** to verify DIP
3. **"Inversion" = interface moves up, dependency arrow flips**
4. **Business defines contracts, Infrastructure implements them**

