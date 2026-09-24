# SemanticMapper

Map JSON or XML into your C# models when the source field names don't match your property names. Fields are matched by **meaning**, not by string similarity or naming conventions. For example, `given_name` maps to `FirstName` and `dob` maps to `BirthDate`.

## 1. Install

```bash
dotnet add package SemanticMapper.Core
dotnet add package SemanticMapper.Jev
```

Requires .NET 10 and a [TypeSafe](https://docs.typesafe.ai) API key.

## 2. Provide your API key

Pick one:

```bash
export TYPESAFE_API_KEY=tsk_...            # environment variable
```

```json
// appsettings.json
{ "TypeSafe": { "ApiKey": "tsk_..." } }
```

## 3. Register the mapper

```csharp
builder.Services
    .AddSemanticMapper()
    .UseJev();
```

## 4. Define a model

Use a class with a public parameterless constructor and settable (or `init`) properties. Nested classes and scalar collections work too.

```csharp
public class Customer
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public Address Address { get; set; } = new();
}

public class Address
{
    public string City { get; set; } = "";
}
```

## 5. Map

Inject `ISemanticMapper` and call `MapAsync<T>`. It detects JSON or XML automatically.

```csharp
using SemanticMapper;

public class CustomerImporter(ISemanticMapper mapper)
{
    public async Task<Customer> ImportAsync(string payload, CancellationToken ct)
    {
        var result = await mapper.MapAsync<Customer>(payload, ct);
        return result.Value;
    }
}
```

Both of these inputs produce the same `Customer`:

```json
{ "given_name": "Akbar", "surname": "Dizaji", "dob": "1993-06-10", "address": { "town": "Tabriz" } }
```

```xml
<person><given_name>Akbar</given_name><surname>Dizaji</surname><dob>1993-06-10</dob><address><town>Tabriz</town></address></person>
```

## 6. Choose how strict to be

By default, a match that is uncertain is skipped and the property keeps its default value. To fail instead, use `Throw`:

```csharp
builder.Services
    .AddSemanticMapper(o =>
    {
        o.MinimumConfidence = 0.80;                        // minimum score to accept a match
        o.MinimumConfidenceGap = 0.10;                     // how far it must beat the runner-up
        o.OnLowConfidence = MappingFailureBehavior.Throw;  // Throw | LeaveDefault | UseBestMatch
        o.OnAmbiguousMatch = MappingFailureBehavior.Throw;
    })
    .UseJev(o => o.Model = "jev-1.13.0");                  // pin the model once thresholds are tuned
```

Source fields that have no counterpart in your model are ignored. They never cause a failure.

## 7. Handle errors

```csharp
try
{
    var customer = (await mapper.MapAsync<Customer>(payload)).Value;
}
catch (UnsafeMappingException ex)          // a match broke your confidence rules (Throw mode only)
{
    foreach (var d in ex.FailedDecisions)
        logger.LogWarning("{Source} -> {Target}: {Status}", d.SourcePath, d.SelectedTargetPath, d.Status);
}
catch (InvalidSourceDocumentException) { /* the input is not valid JSON or XML */ }
catch (ValueConversionException)       { /* e.g. "abc" into an int property */ }
catch (SemanticMatcherException)       { /* the provider call failed */ }
```

All of these derive from `SemanticMapperException`.

## 8. See what happened

```csharp
var result = await mapper.MapAsync<Customer>(payload);

foreach (var d in result.Diagnostics.Decisions)
    Console.WriteLine(d);                       // candidates, scores, selected target, status

result.Diagnostics.UnmappedTargetPaths;         // properties nothing mapped to
result.UsedCachedPlan;                          // true = no provider call was made
```

The mapper caches a plan for each input shape. Later documents with the same field paths skip the provider call.

## Optional extras

**Retries** for the Jev HTTP client:

```csharp
builder.Services.AddHttpClient(JevDefaults.HttpClientName).AddStandardResilienceHandler();
```

**Your own matcher** instead of Jev:

```csharp
public class MyMatcher : ISemanticFieldMatcher
{
    public string Version => "my-matcher-v1";   // change this whenever scoring changes

    public Task<FieldMatchResult> MatchAsync(SourceField source, IReadOnlyList<TargetField> candidates, CancellationToken ct = default)
        => Task.FromResult(new FieldMatchResult(candidates.Select(c => new CandidateScore(c, Score(source, c)))));

    private static double Score(SourceField s, TargetField t) => /* 0..1 */ 0;
}

builder.Services.AddSemanticMapper().UseMatcher<MyMatcher>();
```

**A shared plan cache** (for example Redis): implement `IMappingPlanCache` and register it with `.UsePlanCache<MyCache>()`.

## Limitations

- Input must be JSON or XML.
- Arrays of objects in the source are skipped and reported in `Diagnostics.UnsupportedSourcePaths`.
- Only field names, paths and value kinds are sent to TypeSafe. Source values are not sent unless you set `IncludeSourceValues = true` in the Jev options.

## Build and test

```bash
dotnet test                                                        # no API key needed
TYPESAFE_API_KEY=tsk_... dotnet test --filter "Category=Integration"
```
