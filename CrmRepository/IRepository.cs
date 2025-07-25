using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public interface IRepository<T> where T : Entity
{
    T GetById(Guid id, ColumnSet columns = null);
    Task<T> GetByIdAsync(Guid id, ColumnSet columns = null);

    IEnumerable<T> GetByConditions(IEnumerable<ConditionExpression> conditions, LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null);
    Task<IEnumerable<T>> GetByConditionsAsync(IEnumerable<ConditionExpression> conditions, LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null);

    IEnumerable<T> GetByFilter(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000, int pageNumber = 1);
    Task<IEnumerable<T>> GetByFilterAsync(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000, int pageNumber = 1);

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
}
