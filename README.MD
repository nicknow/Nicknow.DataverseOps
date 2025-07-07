#  DataverseOps
> __Easy Parallel and Batch Ops w/ Microsoft Dataverse SDK__
>
**Repository**: https://github.com/nicknow/Nicknow.DataverseOps
**NuGet Package**: [Nicknow.DataverseOps](https://www.nuget.org/packages/Nicknow.DataverseOps)

A .NET class library that simplifies parallel and batch operations using the Microsoft Dataverse SDK. DataverseOps enables high-performance data operations by executing multiple requests concurrently, significantly reducing processing time for bulk operations.

### Why?
I created this library and put it out as a Nuget to make my life easier. I'm regularly asked for source code that handles parallel operations with the Dataverse SDK (usually after asking why something isn't being done in parallel.) Instead of having to dig up the last code I worked with and try to make it generic enough to be useful I wanted to be able to point folks to a simple library. That is _all_ that this solution is intended to provide, a place where the code is generic enough to be useful and easily accessible either by taking the source code or including the Nuget package in yours solution.

This solution is a personal project of mine. It's released under the MIT License. You are welcome to do anything and everything with it within the rules of the MIT License.

### Current Status [As of July 7 2025 / v 0.1.0]
I consider this still in an advanced beta state. I may or may not get around to saying it's officially v1.0 at some point. It really depends on when I get to a place where it is thoroughly tested and has automated testing. That said I've tested it fairly extensively. This documentation, especially the samples, is also a work in progress. My current goals are to add automated tests as a testing project and to implement progress reporting using `IProgress<T>`. Progress reporting is needed for end-user applications.

## Key Features

- **Parallel Execution**: Execute multiple Dataverse requests simultaneously with configurable parallelism
- **Batch Operations**: Support for ExecuteMultiple requests with automatic batching
- **Comprehensive Results**: Detailed success/error tracking with timing data and reference values
- **Flexible Logging**: Optional integration with Microsoft.Extensions.Logging
- **Generic Design**: Works with any OrganizationRequest/Response types
- **Performance Monitoring**: Optional timing data capture for performance analysis

## Installation

Install the DataverseOps NuGet package:

```bash
dotnet add package Nicknow.DataverseOps
```

Or via Package Manager Console:

```powershell
Install-Package Nicknow.DataverseOps
```
The solution is built for .NET Standard 2.1 so should be usable for most projects.

It is __not__ supported for use in a Dataverse Plugin.

## Quick Start

```csharp
using DataverseOps;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

// Initialize your Dataverse ServiceClient
var serviceClient = new ServiceClient("your-connection-string");

// Create the CUDOperations helper for common operations
var cudOps = new CUDOperations(serviceClient, maxParallelOperations: 10);

// Create multiple entities in parallel
var entities = new List<Entity>
{
    new Entity("account") { ["name"] = "Company A" },
    new Entity("account") { ["name"] = "Company B" },
    new Entity("account") { ["name"] = "Company C" }
};

// Execute creates in parallel
var result = cudOps.CreateEntities(entities, referenceAttribute: "name");

// Check results
Console.WriteLine($"Total: {result.TotalProcessed}, Success: {result.SuccessCount}, Errors: {result.ErrorCount}");
```

## Detailed Usage Examples

### Basic Parallel Operations

#### Creating Entities in Parallel

```csharp
using DataverseOps;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Extensions.Logging;

// Setup with logging and timing data
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
var serviceClient = new ServiceClient("your-connection-string");

// Initialize CUDOperations with custom settings
var cudOps = new CUDOperations(
    serviceClient: serviceClient,
    maxParallelOperations: 8,        // Maximum concurrent operations
    logger: logger,                  // Optional logging
    captureTimingData: true         // Capture performance metrics
);

// Prepare entities to create
var accountsToCreate = new List<Entity>();
for (int i = 1; i <= 100; i++)
{
    var account = new Entity("account");
    account["name"] = $"Bulk Account {i}";
    account["accountnumber"] = $"ACC-{i:D4}";
    account["telephone1"] = $"555-{i:D4}";
    accountsToCreate.Add(account);
}

// Execute parallel creates with reference tracking
var createResult = cudOps.CreateEntities(
    entities: accountsToCreate,
    referenceAttribute: "name"  // Use 'name' field for tracking individual results
);

// Process results
Console.WriteLine($"Create Operation Summary:");
Console.WriteLine($"- Total Processed: {createResult.TotalProcessed}");
Console.WriteLine($"- Successful: {createResult.SuccessCount}");
Console.WriteLine($"- Failed: {createResult.ErrorCount}");
Console.WriteLine($"- Duration: {createResult.Duration.TotalSeconds:F2} seconds");

// Handle successful results
foreach (var success in createResult.SuccessResults)
{
    Console.WriteLine($"Created: {success.ReferenceValue} - ID: {success.Response?.id}");
    
    // Access timing data if captured
    if (success.TimingData != null)
    {
        Console.WriteLine($"   Execution time: {success.TimingData.SdkTransactionTimeMilliseconds.TotalMilliseconds}ms");
    }
}

// Handle errors
foreach (var error in createResult.ErrorResults)
{
    Console.WriteLine($"Failed: {error.ReferenceValue} - Error: {error.ErrorMessage}");
}
```

#### Updating Entities in Parallel

```csharp
// Prepare entities to update (assuming you have existing entity IDs)
var accountsToUpdate = new List<Entity>();
var existingAccountIds = new List<Guid> { /* your existing account IDs */ };

foreach (var accountId in existingAccountIds)
{
    var account = new Entity("account", accountId);
    account["description"] = $"Updated on {DateTime.Now:yyyy-MM-dd}";
    account["creditlimit"] = new Money(50000);
    accountsToUpdate.Add(account);
}

// Execute parallel updates
var updateResult = cudOps.UpdateEntities(
    entities: accountsToUpdate,
    referenceAttribute: "accountid"
);

Console.WriteLine($"Updated {updateResult.SuccessCount} of {updateResult.TotalProcessed} accounts");
```

#### Deleting Entities in Parallel

```csharp
// Delete by entity references
var entityReferencesToDelete = new List<EntityReference>
{
    new EntityReference("account", Guid.Parse("guid1")),
    new EntityReference("account", Guid.Parse("guid2")),
    new EntityReference("contact", Guid.Parse("guid3"))
};

var deleteResult = cudOps.DeleteEntities(entityReferencesToDelete);

// Or delete by entity name and IDs
var accountIdsToDelete = new List<Guid> { /* account IDs to delete */ };
var deleteByIdsResult = cudOps.DeleteEntities("account", accountIdsToDelete);

Console.WriteLine($"Deleted {deleteResult.SuccessCount} entities successfully");
```

### Batch Operations with ExecuteMultiple

For very large datasets, batch operations can be more efficient by grouping requests into ExecuteMultiple calls:

```csharp
// Create 1000 entities using batches of 100
var largeEntitySet = new List<Entity>();
for (int i = 1; i <= 1000; i++)
{
    var contact = new Entity("contact");
    contact["firstname"] = $"Contact";
    contact["lastname"] = $"Number {i}";
    contact["emailaddress1"] = $"contact{i}@example.com";
    largeEntitySet.Add(contact);
}

// Execute in batches with parallel processing
var batchResult = cudOps.CreateEntities(
    entities: largeEntitySet,
    requestsPerBatch: 100,           // 100 requests per ExecuteMultiple call
    referenceAttribute: "emailaddress1",
    continueOnError: true            // Continue processing even if some batches fail
);

Console.WriteLine($"Batch Operation Results:");
Console.WriteLine($"- Total Entities: {batchResult.TotalProcessed}");
Console.WriteLine($"- Successful: {batchResult.SuccessCount}");
Console.WriteLine($"- Failed: {batchResult.ErrorCount}");
Console.WriteLine($"- Total Duration: {batchResult.Duration.TotalMinutes:F2} minutes");

// Access batch-level results if needed
if (batchResult.BatchResults != null)
{
    Console.WriteLine($"- Successful Batches: {batchResult.BatchResults.Count(b => b.IsSuccess)}");
    Console.WriteLine($"- Failed Batches: {batchResult.BatchResults.Count(b => !b.IsSuccess)}");
}
```

### Advanced Usage with Custom Requests

DataverseOps works with any OrganizationRequest/Response types:

```csharp
using Microsoft.Xrm.Sdk.Messages;

// Initialize the core parallel executor for custom requests
var executor = new ExecuteRequestsInParallel(
    serviceClient: serviceClient,
    maxParallelOperations: 5,
    logger: logger,
    captureTimingData: true
);

// Example: Parallel WhoAmI requests (for demonstration)
var whoAmIRequests = Enumerable.Range(1, 10)
    .Select(i => new WhoAmIRequest())
    .ToList();

// Execute custom requests in parallel
var whoAmIResult = executor.ExecuteRequests<WhoAmIRequest, WhoAmIResponse>(
    requests: whoAmIRequests,
    referenceKeySelector: req => $"WhoAmI_{Guid.NewGuid()}" // Custom reference generator
);

// Process results
foreach (var result in whoAmIResult.SuccessResults)
{
    Console.WriteLine($"User ID: {result.Response?.UserId}");
}
```

### Error Handling and Resilience

```csharp
// Configure for maximum resilience
var resilientOps = new CUDOperations(
    serviceClient: serviceClient,
    maxParallelOperations: 4,  // Lower parallelism for stability
    logger: logger,
    captureTimingData: true
);

var mixedEntities = new List<Entity>
{
    new Entity("account") { ["name"] = "Valid Account" },
    new Entity("account") { ["name"] = "" }, // This might fail validation
    new Entity("invalidtable") { ["name"] = "Invalid Table" }, // This will fail
    new Entity("account") { ["name"] = "Another Valid Account" }
};

// Execute with error handling
var mixedResult = resilientOps.CreateEntities(
    entities: mixedEntities,
    requestsPerBatch: 2,
    referenceAttribute: "name",
    continueOnError: true  // Continue processing despite errors
);

// Detailed error analysis
Console.WriteLine($"Execution Summary:");
Console.WriteLine($"Success Rate: {(double)mixedResult.SuccessCount / mixedResult.TotalProcessed:P1}");

if (mixedResult.HasErrors)
{
    Console.WriteLine($"Error Details:");
    foreach (var error in mixedResult.ErrorResults)
    {
        Console.WriteLine($"- Reference: {error.ReferenceValue}");
        Console.WriteLine($"  Error: {error.ErrorMessage}");
        Console.WriteLine($"  Transaction ID: {error.InternalTransactionId}");
        
        // Access full exception details if needed
        if (error.Exception != null)
        {
            Console.WriteLine($"  Exception Type: {error.Exception.GetType().Name}");
        }
    }
}
```

### Performance Monitoring

```csharp
// Enable detailed performance monitoring
var performanceOps = new CUDOperations(
    serviceClient: serviceClient,
    maxParallelOperations: 10,
    logger: logger,
    captureTimingData: true  // Enable timing data capture
);

var testEntities = Enumerable.Range(1, 50)
    .Select(i => new Entity("account") { ["name"] = $"Performance Test {i}" })
    .ToList();

var perfResult = performanceOps.CreateEntities(testEntities, "name");

// Analyze performance metrics
var timingData = perfResult.SuccessResults
    .Where(r => r.TimingData != null)
    .Select(r => r.TimingData!.SdkTransactionTimeMilliseconds)
    .ToList();

if (timingData.Any())
{
    Console.WriteLine($"Performance Metrics:");
    Console.WriteLine($"- Average Request Time: {timingData.Average():F2}ms");
    Console.WriteLine($"- Fastest Request: {timingData.Min():F2}ms");
    Console.WriteLine($"- Slowest Request: {timingData.Max():F2}ms");
    Console.WriteLine($"- Total Parallel Duration: {perfResult.Duration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"- Estimated Sequential Time: {timingData.Sum():F2}ms");    
}
```

## API Reference

### Core Classes

#### `CUDOperations`
Convenience wrapper for common Create, Update, Upsert, and Delete operations.

**Constructor:**
```csharp
CUDOperations(ServiceClient serviceClient, int maxParallelOperations = 8, ILogger? logger = null, bool captureTimingData = false)
```

**Key Methods:**
- `CreateEntities(IEnumerable<Entity>, string referenceAttribute = "")` - Parallel creates
- `UpdateEntities(IEnumerable<Entity>, string referenceAttribute = "")` - Parallel updates  
- `UpsertEntities(IEnumerable<Entity>, string referenceAttribute = "")` - Parallel upserts
- `DeleteEntities(IEnumerable<EntityReference>)` - Parallel deletes
- Batch versions with `requestsPerBatch` parameter for all operations

#### `ExecuteRequestsInParallel`
Core engine for executing any OrganizationRequest types in parallel.

**Constructor:**
```csharp
ExecuteRequestsInParallel(ServiceClient serviceClient, int maxParallelOperations = 8, ILogger? logger = null, bool captureTimingData = false)
```

**Key Methods:**
- `ExecuteRequests<TRequest, TResponse>(IEnumerable<TRequest>, Func<TRequest, string>? referenceKeySelector = null)`
- `ExecuteRequests<TRequest, TResponse>(IEnumerable<TRequest>, int requestsPerBatch, Func<TRequest, string>? referenceKeySelector = null, bool continueOnError = true)`

#### `ExecutionResult<TResponse>`
Contains comprehensive results from parallel execution.

**Properties:**
- `TotalProcessed` - Total number of requests processed
- `SuccessCount` - Number of successful requests
- `ErrorCount` - Number of failed requests
- `Duration` - Total execution time
- `Results` - List of individual results
- `SuccessResults` - Filtered successful results
- `ErrorResults` - Filtered error results

#### `SingleExecutionResult<TResponse>`
Result of a single request execution.

**Properties:**
- `IsSuccess` - Whether the request succeeded
- `Response` - The response object (if successful)
- `ErrorMessage` - Error message (if failed)
- `ReferenceValue` - Custom reference value for tracking
- `TimingData` - Performance timing information (if enabled)
- `InternalTransactionId` - Unique identifier for tracking

#### `referenceKeySelector` and `referenceAttribute`
These are designed to solve for a problem in parallel operations where you have a large collection of responses but you may not be able to correlate them to the requests. This most commonly is an issue when creating records but can also exist when using alternate keys for updates/upserts.

Imagine you have a `List<Entity>` object with 1000 account records to be created. Dataverse's response will be the primary key (accountid) but you have no way of knowing which item in the request list goes to which i in the response list. Assuming you have some attribute/field on the record that is unique within the list you can have this value included in the `SingleExecutionResult<TResponse>` object as `ReferenceValue`.

If you don't need or care about this information leave `referenceKeySelector` or `referenceAttribute` null and it will be skipped.

### Logging
Logging is optional. If you don't pass an `ILogger` object then logging will be skipped. Any object that implements `Microsoft.Extensions.Logging.ILogger` is supported.


## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## 📄 License

Copyright © 2025 Nicolas Nowinski. All Rights Reserved.
This project is licensed under the MIT License. See the [LICENSE.MD](LICENSE.MD) file for details.

---



