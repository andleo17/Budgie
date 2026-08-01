# Budgie.Shared

The shared package used by every Budgie module. It contains the `Entity` base, the value objects (`Money`, `CurrencyCode`, `Name`, `EmailAddress`, `HexColor`, `Percentage`), the `ICurrentUserProvider` abstraction, enums and other cross-cutting types.

## Usage

Value objects are created through their static factory methods, which validate the input and throw on invalid values:

```csharp
using Budgie.Shared.Domain.ValueObjects;

// Default max length is 100 characters
var name = Name.From("Checking account");

// Custom max length
var shortName = Name.From("Home", maxLength: 10);

// Implicit conversion to string
string value = name;
```

`Name.From` throws an `ArgumentException` when the value is null, empty/whitespace, or exceeds the max length. The stored value is trimmed.
