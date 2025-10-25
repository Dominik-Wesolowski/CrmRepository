using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CrmRepository.Repository
{
    public enum UpsertMode
    {
        RequireKey,
        AllowId,
        KeyOrId
    }

    [Flags]
    public enum RepositorySaveOptions
    {
        None = 0,
        ContinueOnError = 1,
        ReturnResponses = 2,
        Transactional = 4,
        SuppressDuplicateDetection = 8
    }

    public class CrmRepository<T> : IRepository<T> where T : Entity
    {
        private readonly string _entityLogicalName;
        private readonly object _lock = new object();

        private readonly List<OrganizationRequest> _pending = new List<OrganizationRequest>(1024);
        private readonly IOrganizationService _service;

        public CrmRepository(IOrganizationService service)
        {
            _service = service;
            _entityLogicalName = typeof(T).GetField("EntityLogicalName")?.GetValue(null)?.ToString()
                                 ?? throw new InvalidOperationException("Missing EntityLogicalName.");
        }

        public int PendingCount
        {
            get
            {
                lock (_lock)
                {
                    return _pending.Count;
                }
            }
        }

        public T GetById(Guid id, ColumnSet columns = null)
            => _service.Retrieve(_entityLogicalName, id, columns ?? new ColumnSet(true)).ToEntity<T>();

        public async Task<T> GetByIdAsync(Guid id, ColumnSet columns = null)
            => await Task.FromResult(GetById(id, columns));

        public IEnumerable<T> GetByConditions(IEnumerable<ConditionExpression> conditions,
            LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null)
        {
            var filter = new FilterExpression(logicalOperator);
            foreach (var condition in conditions) filter.AddCondition(condition);
            return GetByFilter(filter, columns);
        }

        public async Task<IEnumerable<T>> GetByConditionsAsync(IEnumerable<ConditionExpression> conditions,
            LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null)
            => await Task.FromResult(GetByConditions(conditions, logicalOperator, columns));

        public IEnumerable<T> GetByFilter(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000,
            int pageNumber = 1)
        {
            var queryExpression = new QueryExpression(_entityLogicalName)
            {
                ColumnSet = columns ?? new ColumnSet(true),
                Criteria = filter
            };
            return RetrieveAll(queryExpression, pageSize, pageNumber);
        }

        public async Task<IEnumerable<T>> GetByFilterAsync(FilterExpression filter, ColumnSet columns = null,
            int pageSize = 5000, int pageNumber = 1)
            => await Task.FromResult(GetByFilter(filter, columns, pageSize, pageNumber));

        public IEnumerable<T> GetByFetchXml(string fetchXml)
        {
            var fetchExpression = new FetchExpression(fetchXml);
            var response = _service.RetrieveMultiple(fetchExpression);
            return response.Entities.Select(e => e.ToEntity<T>());
        }

        public async Task<IEnumerable<T>> GetByFetchXmlAsync(string fetchXml)
            => await Task.FromResult(GetByFetchXml(fetchXml));

        public Guid Create(T entity)
            => CreateWithRequest(entity);

        public async Task<Guid> CreateAsync(T entity)
            => await Task.FromResult(Create(entity));

        public void Update(T entity)
        {
            UpdateWithRequest(entity);
        }

        public async Task UpdateAsync(T entity)
            => await Task.Run(() => Update(entity));

        public void Upsert(T entity)
        {
            if (entity.Id == Guid.Empty)
                throw new InvalidOperationException("Entity must have a valid Id for upsert.");
            var request = new UpsertRequest { Target = entity };
            _service.Execute(request);
        }

        public async Task UpsertAsync(T entity)
            => await Task.Run(() => Upsert(entity));

        public void Delete(Guid id)
        {
            _service.Delete(_entityLogicalName, id);
        }

        public async Task DeleteAsync(Guid id)
            => await Task.Run(() => Delete(id));

        public bool Exists(IEnumerable<ConditionExpression> conditions)
        {
            var queryExpression = new QueryExpression(_entityLogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1,
                Criteria = new FilterExpression()
            };
            foreach (var condition in conditions)
                queryExpression.Criteria.AddCondition(condition);
            return RetrieveAll(queryExpression).Any();
        }

        public async Task<bool> ExistsAsync(IEnumerable<ConditionExpression> conditions) =>
            await Task.FromResult(Exists(conditions));

        public async Task<int> CountAsync(IEnumerable<ConditionExpression> conditions)
            => await Task.FromResult(Count(conditions));

        public int Count(IEnumerable<ConditionExpression> conditions)
        {
            var queryExpression = new QueryExpression(_entityLogicalName)
            {
                ColumnSet = new ColumnSet(false),
                Criteria = new FilterExpression()
            };
            foreach (var condition in conditions)
                queryExpression.Criteria.AddCondition(condition);
            return RetrieveAll(queryExpression).Count();
        }

        public void Associate(Guid sourceId, string relationshipName, EntityReferenceCollection targets)
            => _service.Associate(_entityLogicalName, sourceId, new Relationship(relationshipName), targets);

        public async Task AssociateAsync(Guid sourceId, string relationshipName, EntityReferenceCollection targets)
            => await Task.Run(() => Associate(sourceId, relationshipName, targets));

        public void Disassociate(Guid sourceId, string relationshipName, EntityReferenceCollection targets)
            => _service.Disassociate(_entityLogicalName, sourceId, new Relationship(relationshipName), targets);

        public async Task DisassociateAsync(Guid sourceId, string relationshipName, EntityReferenceCollection targets)
            => await Task.Run(() => Disassociate(sourceId, relationshipName, targets));

        public OrganizationResponse Execute(OrganizationRequest request)
            => _service.Execute(request);

        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
            => await Task.FromResult(Execute(request));

        public Guid CreateWithRequest(T entity, Dictionary<string, object> inputParams = null)
        {
            var request = new CreateRequest { Target = entity };
            if (inputParams != null)
                foreach (KeyValuePair<string, object> param in inputParams)
                    request.Parameters[param.Key] = param.Value;
            var response = (CreateResponse)_service.Execute(request);
            return response.id;
        }

        public void UpdateWithRequest(T entity, Dictionary<string, object> inputParams = null)
        {
            var request = new UpdateRequest { Target = entity };
            if (inputParams != null)
                foreach (KeyValuePair<string, object> param in inputParams)
                    request.Parameters[param.Key] = param.Value;
            _service.Execute(request);
        }

        public void DeleteWithRequest(Guid id, Dictionary<string, object> inputParams = null)
        {
            var request = new DeleteRequest { Target = new EntityReference(_entityLogicalName, id) };
            if (inputParams != null)
                foreach (KeyValuePair<string, object> param in inputParams)
                    request.Parameters[param.Key] = param.Value;
            _service.Execute(request);
        }

        public IReadOnlyList<Guid> CreateMany(IEnumerable<T> entities, int batchSize = 200,
            bool continueOnError = false)
        {
            if (entities == null) return new List<Guid>();
            IEnumerable<OrganizationRequest> requests =
                entities.Select(e => (OrganizationRequest)new CreateRequest { Target = e });
            IReadOnlyList<ExecuteMultipleResponseItem> responses =
                ExecuteMultipleInBatches(requests, batchSize, continueOnError, true);
            List<Guid> ids = new List<Guid>();
            foreach (var r in responses)
            {
                if (r.Fault != null)
                    continue;
                if (r.Response is CreateResponse cr)
                    ids.Add(cr.id);
            }

            return ids;
        }

        public Task<IReadOnlyList<Guid>> CreateManyAsync(IEnumerable<T> entities, int batchSize = 200,
            bool continueOnError = false)
            => Task.FromResult(CreateMany(entities, batchSize, continueOnError));

        public void UpdateMany(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false)
        {
            if (entities == null) return;
            IEnumerable<OrganizationRequest> requests =
                entities.Select(e => (OrganizationRequest)new UpdateRequest { Target = e });
            ExecuteMultipleInBatches(requests, batchSize, continueOnError, false);
        }

        public Task UpdateManyAsync(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false)
        {
            UpdateMany(entities, batchSize, continueOnError);
            return Task.CompletedTask;
        }

        public void UpsertMany(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false)
        {
            if (entities == null) return;
            IEnumerable<OrganizationRequest> requests =
                entities.Select(e => (OrganizationRequest)new UpsertRequest { Target = e });
            ExecuteMultipleInBatches(requests, batchSize, continueOnError, false);
        }

        public Task UpsertManyAsync(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false)
        {
            UpsertMany(entities, batchSize, continueOnError);
            return Task.CompletedTask;
        }

        public void DeleteMany(IEnumerable<Guid> ids, int batchSize = 200, bool continueOnError = false)
        {
            if (ids == null) return;
            IEnumerable<OrganizationRequest> requests = ids.Select(id =>
                (OrganizationRequest)new DeleteRequest { Target = new EntityReference(_entityLogicalName, id) });
            ExecuteMultipleInBatches(requests, batchSize, continueOnError, false);
        }

        public Task DeleteManyAsync(IEnumerable<Guid> ids, int batchSize = 200, bool continueOnError = false)
        {
            DeleteMany(ids, batchSize, continueOnError);
            return Task.CompletedTask;
        }

        public IReadOnlyList<OrganizationResponse> ExecuteTransaction(IEnumerable<OrganizationRequest> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            var txn = new ExecuteTransactionRequest
            {
                Requests = new OrganizationRequestCollection()
            };
            foreach (var r in requests) txn.Requests.Add(r);
            var resp = (ExecuteTransactionResponse)_service.Execute(txn);
            return resp.Responses?.ToList() ?? new List<OrganizationResponse>();
        }

        public Task<IReadOnlyList<OrganizationResponse>> ExecuteTransactionAsync(
            IEnumerable<OrganizationRequest> requests)
            => Task.FromResult(ExecuteTransaction(requests));

        public void AddObject(T entity, Dictionary<string, object> requestParameters = null)
        {
            if (entity == null) return;
            EnsureLogicalName(entity.LogicalName);
            var target = CloneForRequest(entity, true);
            var req = new CreateRequest { Target = target };
            if (requestParameters != null)
                foreach (KeyValuePair<string, object> kv in requestParameters)
                    req.Parameters[kv.Key] = kv.Value;
            Enqueue(req);
        }

        public void AddObjectRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null)
        {
            if (entities == null) return;
            foreach (var e in entities) AddObject(e, requestParameters);
        }

        public void Attach(T entity, Dictionary<string, object> requestParameters = null)
        {
            if (entity == null) return;
            EnsureLogicalName(entity.LogicalName);
            if (entity.Id == Guid.Empty) throw new InvalidOperationException("Attach requires entity.Id.");
            var target = CloneForRequest(entity, false);
            var req = new UpdateRequest { Target = target };
            if (requestParameters != null)
                foreach (KeyValuePair<string, object> kv in requestParameters)
                    req.Parameters[kv.Key] = kv.Value;
            Enqueue(req);
        }

        public void AttachRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null)
        {
            if (entities == null) return;
            foreach (var e in entities) Attach(e, requestParameters);
        }

        public void DeleteObject(Guid id, Dictionary<string, object> requestParameters = null)
        {
            if (id == Guid.Empty) return;
            var req = new DeleteRequest { Target = new EntityReference(_entityLogicalName, id) };
            if (requestParameters != null)
                foreach (KeyValuePair<string, object> kv in requestParameters)
                    req.Parameters[kv.Key] = kv.Value;
            Enqueue(req);
        }

        public void DeleteObject(T entity, Dictionary<string, object> requestParameters = null)
        {
            if (entity == null || entity.Id == Guid.Empty)
                throw new InvalidOperationException("DeleteObject requires an entity with Id.");
            EnsureLogicalName(entity.LogicalName);
            DeleteObject(entity.Id, requestParameters);
        }

        public void DeleteObjectRange(IEnumerable<Guid> ids, Dictionary<string, object> requestParameters = null)
        {
            if (ids == null) return;
            foreach (var id in ids) DeleteObject(id, requestParameters);
        }

        public void DeleteObjectRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null)
        {
            if (entities == null) return;
            foreach (var e in entities) DeleteObject(e, requestParameters);
        }

        public void UpsertObject(T entity, UpsertMode mode = UpsertMode.RequireKey,
            Dictionary<string, object> requestParameters = null)
        {
            if (entity == null) return;
            EnsureLogicalName(entity.LogicalName);
            var req = BuildUpsertRequest(entity, mode);
            if (requestParameters != null)
                foreach (KeyValuePair<string, object> kv in requestParameters)
                    req.Parameters[kv.Key] = kv.Value;
            Enqueue(req);
        }

        public void UpsertObjectRange(IEnumerable<T> entities, UpsertMode mode = UpsertMode.RequireKey,
            Dictionary<string, object> requestParameters = null)
        {
            if (entities == null) return;
            foreach (var e in entities) UpsertObject(e, mode, requestParameters);
        }

        public void Attach(OrganizationRequest request)
        {
            if (request == null) return;
            Enqueue(request);
        }

        public void AttachRange(IEnumerable<OrganizationRequest> requests)
        {
            if (requests == null) return;
            foreach (var r in requests)
            {
                if (r != null)
                    Enqueue(r);
            }
        }

        public IReadOnlyList<object> SaveChanges(
            RepositorySaveOptions options =
                RepositorySaveOptions.ReturnResponses | RepositorySaveOptions.SuppressDuplicateDetection,
            int batchSize = 200)
        {
            List<OrganizationRequest> toRun = DequeueAll();
            if (toRun.Count == 0) return Array.Empty<object>();
            var suppressDup = options.HasFlag(RepositorySaveOptions.SuppressDuplicateDetection);
            var cont = options.HasFlag(RepositorySaveOptions.ContinueOnError);
            var ret = options.HasFlag(RepositorySaveOptions.ReturnResponses);
            var transactional = options.HasFlag(RepositorySaveOptions.Transactional);

            if (suppressDup)
                foreach (var r in toRun)
                    TrySetSuppressDup(r);

            if (transactional)
            {
                if (toRun.Count > 1000)
                    throw new InvalidOperationException("ExecuteTransaction supports up to 1000 requests.");
                var txn = new ExecuteTransactionRequest { Requests = new OrganizationRequestCollection() };
                foreach (var r in toRun) txn.Requests.Add(r);
                var resp = (ExecuteTransactionResponse)_service.Execute(txn);
                return resp.Responses?.Cast<object>().ToList() ?? new List<object>();
            }

            IReadOnlyList<ExecuteMultipleResponseItem> items = ExecuteMultipleInBatches(toRun, batchSize, cont, ret);
            return items?.Cast<object>().ToList() ?? new List<object>();
        }

        public Task<IReadOnlyList<object>> SaveChangesAsync(
            RepositorySaveOptions options =
                RepositorySaveOptions.ReturnResponses | RepositorySaveOptions.SuppressDuplicateDetection,
            int batchSize = 200)
            => Task.FromResult(SaveChanges(options, batchSize));


        public void ClearPending()
        {
            lock (_lock)
            {
                _pending.Clear();
            }
        }

        private IEnumerable<T> RetrieveAll(QueryExpression queryExpression, int pageSize = 5000, int pageNumber = 1)
        {
            List<T> results = new List<T>();
            queryExpression.PageInfo = new PagingInfo { PageNumber = pageNumber, Count = pageSize };
            var hasMoreRecords = true;
            while (hasMoreRecords)
            {
                var response = _service.RetrieveMultiple(queryExpression);
                results.AddRange(response.Entities.Select(e => e.ToEntity<T>()));
                hasMoreRecords = response.MoreRecords;
                if (!hasMoreRecords) continue;
                queryExpression.PageInfo.PageNumber++;
                queryExpression.PageInfo.PagingCookie = response.PagingCookie;
            }

            return results;
        }

        private IReadOnlyList<ExecuteMultipleResponseItem> ExecuteMultipleInBatches(
            IEnumerable<OrganizationRequest> requests,
            int batchSize,
            bool continueOnError,
            bool returnResponses)
        {
            if (batchSize <= 0 || batchSize > 1000)
                batchSize = 200;
            List<ExecuteMultipleResponseItem> allItems = new List<ExecuteMultipleResponseItem>();
            List<OrganizationRequest> currentBatch = new List<OrganizationRequest>(batchSize);

            foreach (var req in requests)
            {
                currentBatch.Add(req);
                if (currentBatch.Count >= batchSize)
                    Flush();
            }

            Flush();

            return allItems;

            void Flush()
            {
                if (currentBatch.Count == 0) return;
                var em = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = continueOnError,
                        ReturnResponses = returnResponses
                    },
                    Requests = new OrganizationRequestCollection()
                };
                foreach (var r in currentBatch) em.Requests.Add(r);
                var resp = (ExecuteMultipleResponse)_service.Execute(em);
                if (returnResponses && resp?.Responses != null && resp.Responses.Count > 0)
                    allItems.AddRange(resp.Responses);
                currentBatch.Clear();
            }
        }

        public Task<IReadOnlyList<object>> SaveChangesAsync(
            RepositorySaveOptions options =
                RepositorySaveOptions.ReturnResponses | RepositorySaveOptions.SuppressDuplicateDetection,
            int batchSize = 200,
            CancellationToken ct = default)
            => Task.FromResult(SaveChanges(options, batchSize));

        private void Enqueue(OrganizationRequest req)
        {
            lock (_lock)
            {
                _pending.Add(req);
            }
        }

        private List<OrganizationRequest> DequeueAll()
        {
            lock (_lock)
            {
                if (_pending.Count == 0) return new List<OrganizationRequest>();
                List<OrganizationRequest> copy = new List<OrganizationRequest>(_pending);
                _pending.Clear();
                return copy;
            }
        }

        private static void TrySetSuppressDup(OrganizationRequest req)
        {
            if (!(req is CreateRequest) && !(req is UpdateRequest) && !(req is UpsertRequest)) return;
            if (!req.Parameters.ContainsKey("SuppressDuplicateDetection"))
                req.Parameters["SuppressDuplicateDetection"] = true;
        }

        private OrganizationRequest BuildUpsertRequest(T source, UpsertMode mode)
        {
            switch (mode)
            {
                case UpsertMode.RequireKey:
                    if (source.KeyAttributes == null || source.KeyAttributes.Count == 0)
                        throw new InvalidOperationException("UpsertMode.RequireKey requires KeyAttributes.");
                    return new UpsertRequest { Target = CloneForRequest(source, true) };
                case UpsertMode.AllowId:
                    if (source.Id == Guid.Empty && (source.KeyAttributes == null || source.KeyAttributes.Count == 0))
                        throw new InvalidOperationException("UpsertMode.AllowId requires Id or KeyAttributes.");
                    return new UpsertRequest { Target = CloneForRequest(source, false) };
                case UpsertMode.KeyOrId:
                    if (source.KeyAttributes != null && source.KeyAttributes.Count > 0)
                        return new UpsertRequest { Target = CloneForRequest(source, true) };
                    return source.Id != Guid.Empty
                        ? new UpsertRequest { Target = CloneForRequest(source, false) }
                        : throw new InvalidOperationException("UpsertMode.KeyOrId requires KeyAttributes or Id.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private Entity CloneForRequest(T source, bool forceNoId)
        {
            var clone = new Entity(_entityLogicalName);
            foreach (KeyValuePair<string, object> kv in source.Attributes)
                clone.Attributes[kv.Key] = kv.Value;
            if (source.KeyAttributes != null && source.KeyAttributes.Count > 0)
                foreach (KeyValuePair<string, object> kv in source.KeyAttributes)
                    clone.KeyAttributes[kv.Key] = kv.Value;
            if (!forceNoId && source.Id != Guid.Empty)
                clone.Id = source.Id;
            return clone;
        }

        private void EnsureLogicalName(string logical)
        {
            if (!string.Equals(logical, _entityLogicalName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Entity logical name mismatch. Expected '{_entityLogicalName}', got '{logical}'.");
        }
    }
}