# SemanticMapper

Map external JSON or XML documents into your C# models when the source field names don't match your property names. Matching is based on **semantic meaning**, not string similarity, aliases or naming conventions.

```json
{ "given_name": "Akbar", "surname": "Dizaji", "dob": "1993-06-10", "identity_no": "123456" }
```

```csharp
public class Customer
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string NationalId { get; set; } = "";
}

var result = await mapper.MapAsync<Customer>(input);
Customer customer = result.Value;
```

```text
given_name  -> FirstName
surname     -> LastName
dob         -> BirthDate
identity_no -> NationalId
```

## Packages

| Package | Purpose |
| --- | --- |
| `SemanticMapper.Core` | Parsing, schema extraction, assignment, confidence policies, value conversion, diagnostics, plan caching. No provider dependency. |
| `SemanticMapper.Jev` | `ISemanticFieldMatcher` backed by the [TypeSafe](https://docs.typesafe.ai) Jev model. |

## Setup

```csharp
services.AddSemanticMapper(options =>
{
    options.MinimumConfidence = 0.80;
    options.MinimumConfidenceGap = 0.10;
    options.OnLowConfidence = MappingFailureBehavior.Throw;
    options.OnAmbiguousMatch = MappingFailureBehavior.Throw;
})
.UseJev(options =>
{
    options.ApiKey = configuration["TypeSafe:ApiKey"];
    options.Model = "jev-1.13.0"; // optional: pin the model once your thresholds are calibrated
});
```

Then inject `ISemanticMapper`.

### API key

Each consumer uses their own TypeSafe API key. Requests go directly from your application to TypeSafe; nothing is proxied. The key is resolved in this order:

1. `JevMatcherOptions.ApiKey`
2. `IConfiguration["TypeSafe:ApiKey"]`
3. the `TYPESAFE_API_KEY` environment variable

If no key is found, the first match throws `JevAuthenticationException`. The key is never logged.

By default only field **names, paths and inferred kinds** are sent to TypeSafe. Set `JevMatcherOptions.IncludeSourceValues = true` to also send values as context.

Retries are not built in. Configure the named HTTP client if you want resilience:

```csharp
services.AddHttpClient(JevDefaults.HttpClientName).AddStandardResilienceHandler();
```

## How mapping works

1. **Parse.** The input is auto-detected as JSON or XML and flattened into leaf `SourceField`s (path, name, value, inferred kind). Both formats normalize to the same paths. For example, `<c><address><city>…` and `{"address":{"city":…}}` both yield `address.city`. XML attributes become `@name`, and repeated simple XML elements become arrays.
2. **Inspect the destination.** Public settable (and `init`) properties become `TargetField`s. Nested classes are flattened (`Address.City`). Scalar collections (`T[]`, `List<T>`, `IReadOnlyList<T>`, …) are supported.
3. **Score.** On a plan-cache miss, `ISemanticFieldMatcher` scores each source field against the compatible candidates: array sources are offered collection properties, and scalar sources are offered scalar properties.
4. **Assign.** Assignment runs over the whole candidate matrix. Pairs are taken in descending confidence, so each source maps to at most one property and each property receives at most one source. A source whose best property was taken by a stronger match falls back to its next candidate. That candidate is judged by the policy on its own.
5. **Apply the policy.** A match is safe when

   ```text
   confidence >= MinimumConfidence
   AND confidence - next best available candidate >= MinimumConfidenceGap
   ```

   "Available" means not assigned to another source field. A candidate that is confidently claimed elsewhere does not make a match ambiguous.

   | Behavior | Effect |
   | --- | --- |
   | `Throw` | Mapping fails with `UnsafeMappingException`. It lists every failed decision at once. |
   | `LeaveDefault` | The property keeps its default, and the freed property can go to another source. |
   | `UseBestMatch` | The best available candidate is used anyway. |

   Low confidence is checked before ambiguity. Some source fields have no counterpart: there are no candidates, every score is zero, or the matcher is more confident in "none" than in any property. These fields are reported as `NoMatch` and never fail the mapping, so extra fields in the input are harmless.
6. **Convert.** Values are converted with the invariant culture to strings, numbers, `bool`, enums (by name), `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Uri`, nullable types and scalar collections. A failed conversion throws `ValueConversionException`, whose message does not include the value.

Defaults: `MinimumConfidence = 0.80`, `MinimumConfidenceGap = 0.10`, both behaviors `LeaveDefault`.

## Diagnostics

```csharp
var result = await mapper.MapAsync<Customer>(input);

result.UsedCachedPlan;
foreach (var decision in result.Diagnostics.Decisions)
{
    Console.WriteLine(decision);
}
```

```text
Source: dob

Candidates:
  BirthDate   0.97
  CreatedAt   0.41
  Age         0.14

Selected: BirthDate
Status: Safe -> Applied
Threshold: 0.80
ConfidenceGap: 0.56
```

Every decision keeps all candidates and scores, the selected target, the status (`Safe`, `LowConfidence`, `Ambiguous`, `NoMatch`, `Conflict`), the outcome and the behavior applied. `MappingDiagnostics` also lists unmapped destination properties and unsupported source paths, such as arrays of objects. Diagnostics never contain source values.

### Logging

The mapper logs structured events through `Microsoft.Extensions.Logging`: `PlanCacheHit`, `PlanCacheMiss`, `SemanticMatchPerformed`, `CandidateSelected`, `CandidateRejected`, `LowConfidenceMatch`, `AmbiguousMatch` and `NoSemanticMatch`. Field values are never logged.

## Mapping plan cache

After a successful mapping, the decisions are stored as a `MappingPlan`. Later documents with the same source schema reuse it without calling the semantic provider, and `UsedCachedPlan` is `true`. The key is a SHA-256 hash of:

- the sorted source field paths (not values, and not value kinds, so a field that is `null` in one document still hits the cache)
- the destination schema fingerprint: the type name plus every property path, type and nullability
- `ISemanticFieldMatcher.Version`. For Jev this includes the model and prompt version.
- the confidence policy

When the destination type changes, the fingerprint changes and the old plan is not reused. Mappings rejected with `Throw` are not cached.

The default `InMemoryMappingPlanCache` is thread-safe and size-bounded (`InMemoryMappingPlanCacheOptions.SizeLimit`, default 1024). To use a distributed store, implement `IMappingPlanCache`. `MappingPlan` is plain, serializable data. Register it with:

```csharp
services.AddSemanticMapper().UseJev().UsePlanCache<MyRedisPlanCache>();
```

## Custom providers

Implement `ISemanticFieldMatcher` and register it with `.UseMatcher<TMatcher>()`. Return a confidence in [0, 1] for each candidate. Optionally return a "no match" confidence. Change `Version` whenever your scoring changes.

## Scope

Supported: JSON and XML input. Not supported: plain text, CSV, YAML, manual mapping rules, alias attributes, fuzzy or naming-convention matching. Arrays of objects in the source are skipped and reported. Destination types need a public parameterless constructor.

## Building and testing

```bash
dotnet test                                    # unit tests; no API key required
TYPESAFE_API_KEY=tsk_... dotnet test --filter "Category=Integration"
dotnet pack -c Release -o artifacts
```
