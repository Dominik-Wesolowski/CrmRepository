using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CrmRepository.Repository
{
    public class CrmRepository<T> : IRepository<T> where T : Entity
    {
        private readonly string _entityLogicalName;
        private readonly IOrganizationService _service;

        public CrmRepository(IOrganizationService service)
        {
            _service = service;
            _entityLogicalName = typeof(T).GetField("EntityLogicalName")?.GetValue(null)?.ToString()
                                 ?? throw new InvalidOperationException("Missing EntityLogicalName.");
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
                if (hasMoreRecords)
                {
                    queryExpression.PageInfo.PageNumber++;
                    queryExpression.PageInfo.PagingCookie = response.PagingCookie;
                }
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

                if (!continueOnError)
                {
                }

                if (returnResponses && resp?.Responses != null && resp.Responses.Count > 0)
                    allItems.AddRange(resp.Responses);

                currentBatch.Clear();
            }

            foreach (var req in requests)
            {
                currentBatch.Add(req);
                if (currentBatch.Count >= batchSize)
                    Flush();
            }

            Flush();

            return allItems;
        }
    }
}