## 🚀 CrmRepository

A lightweight, type-safe, async-friendly repository framework for **Microsoft Dataverse / Dynamics 365 CRM**, built on top of **Microsoft.Xrm.Sdk**.

---

### 🧩 Overview

`CrmRepository<T>` provides a consistent, extensible abstraction for working with Dataverse entities using early-bound models.
It includes CRUD operations, queries, batching, transactions, and an in-memory tracking model similar to `OrganizationServiceContext`.

---

### 🧱 Core Concepts

| Category                    | Method(s)                                                                  | Description                                                           |
| --------------------------- | -------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| **Retrieval**               | `GetById`, `GetByConditions`, `GetByFilter`, `GetByFetchXml`               | Retrieve records by ID, filter, or FetchXML                           |
| **Creation**                | `Create`, `CreateAsync`, `CreateMany`, `AddObject`, `AddObjectRange`       | Create single or multiple entities, or queue them for later save      |
| **Update**                  | `Update`, `UpdateAsync`, `UpdateMany`, `Attach`, `AttachRange`             | Update single/multiple records or queue them for batch execution      |
| **Upsert**                  | `Upsert`, `UpsertAsync`, `UpsertMany`, `UpsertObject`, `UpsertObjectRange` | Perform Dataverse Upsert operations (Id-based or key-based)           |
| **Delete**                  | `Delete`, `DeleteAsync`, `DeleteMany`, `DeleteObject`, `DeleteObjectRange` | Delete single or multiple records immediately or through queued batch |
| **Bulk Execution**          | `SaveChanges`, `SaveChangesAsync`, `SaveChangesTransactional`              | Flush queued operations via `ExecuteMultiple` or `ExecuteTransaction` |
| **Batch Management**        | `PendingCount`, `ClearPending`                                             | Inspect or clear queued operations before committing                  |
| **Associations**            | `Associate`, `Disassociate`, `Attach(OrganizationRequest)`                 | Manage N:N and 1:N relationships or queue custom requests             |
| **Transactions**            | `ExecuteTransaction`, `ExecuteTransactionAsync`                            | Execute multiple requests atomically (rollback on failure)            |
| **Utility / Query Helpers** | `Exists`, `Count`, `Execute`, `ExecuteAsync`                               | Check record existence, count entities, execute raw SDK requests      |

---

### ⚙️ Upsert Modes

| Mode           | Description                                                                          |
| -------------- | ------------------------------------------------------------------------------------ |
| **RequireKey** | Requires alternate key(s). The safest and default mode. Prevents accidental creates. |
| **AllowId**    | Allows Id-based upsert. Use only when safe (for example, during controlled imports). |
| **KeyOrId**    | Uses key-based upsert if keys are set; falls back to Id-based if not.                |

```csharp
var account = new Account { Name = "Contoso" };
account.KeyAttributes["new_accountnumber_key"] = "A-001";
repo.UpsertObject(account, UpsertMode.RequireKey);
```

---

### ⚡ SaveChanges Options

| Flag                         | Behavior                                                                       |
| ---------------------------- | ------------------------------------------------------------------------------ |
| `ContinueOnError`            | Continue processing even if some operations fail                               |
| `ReturnResponses`            | Return SDK responses for each executed request                                 |
| `Transactional`              | Execute all queued requests in one transaction (`ExecuteTransaction`)          |
| `SuppressDuplicateDetection` | Automatically sets `SuppressDuplicateDetection = true` on Create/Update/Upsert |

```csharp
repo.SaveChanges(
    options: RepositorySaveOptions.ReturnResponses |
             RepositorySaveOptions.SuppressDuplicateDetection,
    batchSize: 200);
```

---

### 🧠 Example Usage

```csharp
var repo = new CrmRepository<Account>(service);

// Queue operations
repo.AddObject(new Account { Name = "Acme Ltd." });
repo.Attach(new Account { Id = accId, Telephone1 = "123456789" });
repo.DeleteObject(oldId);

// Safe key-based upsert
var acc = new Account { Name = "Contoso" };
acc.KeyAttributes["new_accountnumber_key"] = "C-001";
repo.UpsertObject(acc, UpsertMode.RequireKey);

// Commit everything at once
var results = repo.SaveChanges(
    RepositorySaveOptions.ReturnResponses | 
    RepositorySaveOptions.SuppressDuplicateDetection);
```

---

### 🧮 Bulk & Transactional Operations

```csharp
// Non-transactional (ExecuteMultiple)
repo.AddObjectRange(accountsToInsert);
repo.AttachRange(accountsToUpdate);
repo.DeleteObjectRange(idsToDelete);
repo.SaveChanges(RepositorySaveOptions.ContinueOnError);

// Transactional (ExecuteTransaction ≤1000)
repo.SaveChanges(RepositorySaveOptions.Transactional);
```

---

### 🔍 Query Helpers

```csharp
bool exists = repo.Exists(new[]
{
    new ConditionExpression(Account.Fields.Name, ConditionOperator.Equal, "Acme Ltd.")
});

int count = repo.Count(new[]
{
    new ConditionExpression(Account.Fields.statecode, ConditionOperator.Equal, 0)
});

var list = repo.GetByFilter(new FilterExpression
{
    Conditions =
    {
        new ConditionExpression(Account.Fields.Address1_City, ConditionOperator.Equal, "London")
    }
});
```

---

### 🧰 Additional Capabilities

* Fully early-bound - compile-time schema validation
* Async-safe - all operations have async counterparts
* Paging support for large datasets (`RetrieveAll`)
* Execute raw `OrganizationRequest` (custom APIs, associate, disassociate)
* Idempotent and deterministic - safe for plugin or integration use
* Optional duplicate detection suppression
* Environment-agnostic - works both in plugins and external services

---

### 🧑‍💻 Design Principles

* **Configuration-first:** no hardcoded schema, URLs, or constants
* **Early-bound only:** ensures compile-time safety
* **Explicit over implicit:** no automatic Create/Update inference
* **Optimized batching:** controlled ExecuteMultiple behavior
* **Predictable state:** operations queued before commit
* **Idempotent execution:** safe for retryable operations

---

### ✅ Example Patterns

#### 1️⃣ Bulk import

```csharp
repo.AddObjectRange(accountsToCreate);
repo.SaveChanges(RepositorySaveOptions.ContinueOnError);
```

#### 2️⃣ Transactional update

```csharp
repo.AttachRange(accountsToUpdate);
repo.SaveChanges(RepositorySaveOptions.Transactional);
```

#### 3️⃣ Alternate key-based upsert

```csharp
repo.UpsertObjectRange(customers, UpsertMode.RequireKey);
repo.SaveChanges();
```

---

### 🧾 License

MIT License © [Dominik Wesołowski](https://github.com/Dominik-Wesolowski)

---
