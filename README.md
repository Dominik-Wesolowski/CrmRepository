# CrmRepository

**CrmRepository** is a lightweight, generic repository abstraction for working with **Dataverse / Dynamics 365 CRM** using the `IOrganizationService` interface. It is designed to be easy to integrate, extend, and drop into your existing solution — no NuGet, no external dependencies, no magic.

> Just copy two files: `IRepository.cs` and `CrmRepository.cs` into your CRM project and you're ready to go.

---

## Features

- Generic repository interface: `IRepository<T>`
- Synchronous and asynchronous method support
- Querying via:
  - `QueryExpression`
  - `FilterExpression` / `ConditionExpression`
  - `FetchXml`
- Paged queries with configurable buffer size
- Native support for:
  - `CreateRequest`, `UpdateRequest`, `DeleteRequest` with dynamic `InputParameters`
  - `Associate` / `Disassociate`
  - `Execute` and `ExecuteAsync`
- Support for `RowVersion`/concurrency on `Update` and `Delete`

---

## Structure

| Method Type | Description |
|-------------|-------------|
| `GetById(...)` | Retrieve entity by ID |
| `GetByConditions(...)` | Filter by conditions |
| `GetByFilter(...)` | FilterExpression-based query |
| `GetByFetchXml(...)` | FetchXml support |
| `Create/Update/Delete/Upsert(...)` | Basic CRUD methods |
| `WithRequest(...)` variants | Build & execute custom OrganizationRequests |
| `Exists(...)`, `Count(...)` | Utility methods |
| `Execute(...)` | Generic OrganizationRequest executor |
| `Associate(...)` / `Disassociate(...)` | Relationship handling |

---

## Usage

```csharp
var repo = new CrmRepository<Contact>(service);

var contact = repo.GetById(contactId);

repo.Update(contact);

bool exists = await repo.ExistsAsync(new[] {
    new ConditionExpression("emailaddress1", ConditionOperator.Equal, "user@domain.com")
});
```
Or build a custom CreateRequest with input parameters:
```csharp
var contact = new Contact { FirstName = "John", LastName = "Smith" };

var contactId = repo.CreateWithRequest(contact, new Dictionary<string, object>
{
    { "MyCustomParam", true }
});
```
## Why not NuGet?

We believe in flexibility. In many CRM/Dataverse solutions, every team has its own conventions, service container, and SDK version.

This repo is intended to be:
  - minimal: two files only
  - adjustable: feel free to modify or extend it for your architecture
  - safe: no SDK version conflicts, no tight coupling

## License
MIT License — free to use, copy, modify.
