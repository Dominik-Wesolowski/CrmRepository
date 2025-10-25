using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrmRepository.Repository
{
    public interface IRepository<T> where T : Entity
    {
        int PendingCount { get; }
        T GetById(Guid id, ColumnSet columns = null);
        Task<T> GetByIdAsync(Guid id, ColumnSet columns = null);

        IEnumerable<T> GetByConditions(IEnumerable<ConditionExpression> conditions,
            LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null);

        Task<IEnumerable<T>> GetByConditionsAsync(IEnumerable<ConditionExpression> conditions,
            LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null);

        IEnumerable<T> GetByFilter(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000,
            int pageNumber = 1);

        Task<IEnumerable<T>> GetByFilterAsync(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000,
            int pageNumber = 1);

        IEnumerable<T> GetByFetchXml(string fetchXml);
        Task<IEnumerable<T>> GetByFetchXmlAsync(string fetchXml);

        Guid Create(T entity);
        Task<Guid> CreateAsync(T entity);

        void Update(T entity);
        Task UpdateAsync(T entity);

        void Upsert(T entity);
        Task UpsertAsync(T entity);

        void Delete(Guid id);
        Task DeleteAsync(Guid id);

        bool Exists(IEnumerable<ConditionExpression> conditions);
        Task<bool> ExistsAsync(IEnumerable<ConditionExpression> conditions);

        int Count(IEnumerable<ConditionExpression> conditions);
        Task<int> CountAsync(IEnumerable<ConditionExpression> conditions);

        void Associate(Guid sourceId, string relationshipName, EntityReferenceCollection targets);
        Task AssociateAsync(Guid sourceId, string relationshipName, EntityReferenceCollection targets);

        void Disassociate(Guid sourceId, string relationshipName, EntityReferenceCollection targets);
        Task DisassociateAsync(Guid sourceId, string relationshipName, EntityReferenceCollection targets);

        OrganizationResponse Execute(OrganizationRequest request);
        Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request);

        Guid CreateWithRequest(T entity, Dictionary<string, object> inputParams = null);
        void UpdateWithRequest(T entity, Dictionary<string, object> inputParams = null);
        void DeleteWithRequest(Guid id, Dictionary<string, object> inputParams = null);

        IReadOnlyList<Guid> CreateMany(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false);

        Task<IReadOnlyList<Guid>> CreateManyAsync(IEnumerable<T> entities, int batchSize = 200,
            bool continueOnError = false);

        void UpdateMany(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false);
        Task UpdateManyAsync(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false);

        void UpsertMany(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false);
        Task UpsertManyAsync(IEnumerable<T> entities, int batchSize = 200, bool continueOnError = false);

        void DeleteMany(IEnumerable<Guid> ids, int batchSize = 200, bool continueOnError = false);
        Task DeleteManyAsync(IEnumerable<Guid> ids, int batchSize = 200, bool continueOnError = false);

        IReadOnlyList<OrganizationResponse> ExecuteTransaction(IEnumerable<OrganizationRequest> requests);
        Task<IReadOnlyList<OrganizationResponse>> ExecuteTransactionAsync(IEnumerable<OrganizationRequest> requests);

        void AddObject(T entity, Dictionary<string, object> requestParameters = null);
        void AddObjectRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null);

        void Attach(T entity, Dictionary<string, object> requestParameters = null);
        void AttachRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null);

        void DeleteObject(Guid id, Dictionary<string, object> requestParameters = null);
        void DeleteObject(T entity, Dictionary<string, object> requestParameters = null);
        void DeleteObjectRange(IEnumerable<Guid> ids, Dictionary<string, object> requestParameters = null);
        void DeleteObjectRange(IEnumerable<T> entities, Dictionary<string, object> requestParameters = null);

        void UpsertObject(T entity, UpsertMode mode = UpsertMode.RequireKey,
            Dictionary<string, object> requestParameters = null);

        void UpsertObjectRange(IEnumerable<T> entities, UpsertMode mode = UpsertMode.RequireKey,
            Dictionary<string, object> requestParameters = null);

        void Attach(OrganizationRequest request);
        void AttachRange(IEnumerable<OrganizationRequest> requests);

        IReadOnlyList<object> SaveChanges(
            RepositorySaveOptions options =
                RepositorySaveOptions.ReturnResponses | RepositorySaveOptions.SuppressDuplicateDetection,
            int batchSize = 200);

        Task<IReadOnlyList<object>> SaveChangesAsync(
            RepositorySaveOptions options =
                RepositorySaveOptions.ReturnResponses | RepositorySaveOptions.SuppressDuplicateDetection,
            int batchSize = 200);

        void ClearPending();
    }
}