## 🚀 CrmRepository

A lightweight, type-safe, async-friendly repository framework for **Microsoft Dataverse / Dynamics 365 CRM**, built on top of the official **Microsoft.Xrm.Sdk**.

---

### 🧩 Overview

`CrmRepository<T>` provides a clean, extensible abstraction for performing CRUD operations, queries, bulk execution, and transaction batching against Dataverse using the early-bound entity model (`Account`, `Contact`, etc.).

It supports:

* Full synchronous and asynchronous CRUD APIs
* Bulk create/update/upsert/delete using `ExecuteMultiple`
* Transactional operations using `ExecuteTransaction` (≤1000 requests)
* In-memory change tracking pattern similar to `OrganizationServiceContext`:

  * `AddObject`, `Attach`, `DeleteObject`, `UpsertObject`
  * `SaveChanges()` and `SaveChangesAsync()` flush all pending operations
* Safe and explicit `Upsert` behavior (with alternate key validation)
* Automatic duplicate detection suppression
* Early-bound and strongly-typed design
* Paging and helper utilities for large dataset retrieval

---

### 🧱 Core Concepts

| Method                                  | Purpose                                                          |
| --------------------------------------- | ---------------------------------------------------------------- |
| `AddObject(entity)`                     | Queues a new entity for creation                                 |
| `Attach(entity)`                        | Queues an existing entity for update (requires valid `Id`)       |
| `DeleteObject(id)`                      | Queues entity for deletion                                       |
| `UpsertObject(entity, UpsertMode mode)` | Queues entity for Dataverse upsert (key-based or id-based)       |
| `SaveChanges(options, batchSize)`       | Executes all queued requests using `ExecuteMultiple`             |
| `SaveChangesTransactional()`            | Executes all queued requests in a single transaction (≤1000 ops) |

You can enqueue multiple operations and then persist them all in one go - ideal for integration scenarios, imports, and plugin unit of work patterns.

---

### ⚙️ Upsert Modes

`UpsertObject` supports controlled behavior through the `UpsertMode` enum:

| Mode           | Behavior                                                                                                   |
| -------------- | ---------------------------------------------------------------------------------------------------------- |
| **RequireKey** | Requires alternate key(s) (`KeyAttributes`). The safest and default mode.                                  |
| **AllowId**    | Allows upsert by `Id`. Use only when you’re sure the record exists or want Dataverse to create if missing. |
| **KeyOrId**    | Uses alternate key if present; otherwise falls back to `Id`. Throws if neither is provided.                |

Example:

```csharp
// Safe key-based upsert
var account = new Account { Name = "Contoso" };
account.KeyAttributes["new_accountnumber_key"] = "A-00123";
repo.UpsertObject(account, UpsertMode.RequireKey);
```

---

### ⚡ Save Changes Options

`SaveChanges` supports fine-grained control via `RepositorySaveOptions` flags:

| Option                       | Description                                                             |
| ---------------------------- | ----------------------------------------------------------------------- |
| `ContinueOnError`            | Continue batch execution when individual requests fail                  |
| `ReturnResponses`            | Return detailed responses for each operation                            |
| `Transactional`              | Execute all requests as a single transaction (`ExecuteTransaction`)     |
| `SuppressDuplicateDetection` | Adds `SuppressDuplicateDetection=true` to Create/Update/Upsert requests |

Example:

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

// Upsert by alternate key
var acc = new Account { Name = "Contoso" };
acc.KeyAttributes["new_accountnumber_key"] = "C-001";
repo.UpsertObject(acc, UpsertMode.RequireKey);

// Persist all changes in one go
var results = repo.SaveChanges(
    RepositorySaveOptions.ReturnResponses | 
    RepositorySaveOptions.SuppressDuplicateDetection);
```

---

### 🧮 Bulk & Transactional Operations

```csharp
repo.AddObjectRange(accountsToInsert);
repo.AttachRange(accountsToUpdate);
repo.DeleteObjectRange(idsToDelete);

// Execute batched (non-transactional)
repo.SaveChanges(RepositorySaveOptions.ContinueOnError);

// Execute atomically (≤1000 requests)
repo.SaveChanges(RepositorySaveOptions.Transactional);
```

---

### 🧰 Additional Features

* `GetById`, `GetByConditions`, `GetByFilter`, and `GetByFetchXml`
* `CreateMany`, `UpdateMany`, `UpsertMany`, `DeleteMany`
* Async variants for all public methods (`Async` suffix)
* Automatic paging (`RetrieveAll`)
* Safe logical name validation for early-bound entities

---

### 🧑‍💻 Design Philosophy

* **Configuration-first** – No hardcoded schema names or URLs
* **Early-bound only** – Ensures compile-time safety
* **Idempotent and deterministic** – Safe for plugin and integration use
* **Batched and efficient** – Minimized API calls
* **Maintainable** – Readable structure, small methods, predictable behavior

---

### ✅ Example Patterns

#### Bulk import (ExecuteMultiple)

```csharp
repo.AddObjectRange(accountsToCreate);
repo.SaveChanges(RepositorySaveOptions.ContinueOnError);
```

#### Full transaction (ExecuteTransaction)

```csharp
repo.AttachRange(accountsToUpdate);
repo.SaveChanges(RepositorySaveOptions.Transactional);
```

#### Key-based upsert integration

```csharp
repo.UpsertObjectRange(customers, UpsertMode.RequireKey);
repo.SaveChanges();
```

---

### 🧾 License

MIT License © [Dominik Wesołowski](https://github.com/Dominik-Wesolowski)

---
