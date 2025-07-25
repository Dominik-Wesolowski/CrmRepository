using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

public class CrmRepository<T> : IRepository<T> where T : Entity
{
    private readonly IOrganizationService _service;
    private readonly string _entityLogicalName;

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

    public IEnumerable<T> GetByConditions(IEnumerable<ConditionExpression> conditions, LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null)
    {
        var filter = new FilterExpression(logicalOperator);
        foreach (var condition in conditions) filter.AddCondition(condition);
        return GetByFilter(filter, columns);
    }

    public async Task<IEnumerable<T>> GetByConditionsAsync(IEnumerable<ConditionExpression> conditions, LogicalOperator logicalOperator = LogicalOperator.And, ColumnSet columns = null)
        => await Task.FromResult(GetByConditions(conditions, logicalOperator, columns));

    public IEnumerable<T> GetByFilter(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000, int pageNumber = 1)
    {
        var queryExpression = new QueryExpression(_entityLogicalName)
        {
            ColumnSet = columns ?? new ColumnSet(true),
            Criteria = filter
        };
        return RetrieveAll(queryExpression, pageSize, pageNumber);
    }

    public async Task<IEnumerable<T>> GetByFilterAsync(FilterExpression filter, ColumnSet columns = null, int pageSize = 5000, int pageNumber = 1)
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

    public void Update(T entity, string rowVersion = null)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
            entity.RowVersion = rowVersion;

        UpdateWithRequest(entity);
    }

    public async Task UpdateAsync(T entity, string rowVersion = null)
        => await Task.Run(() => Update(entity, rowVersion));

    public void Upsert(T entity)
    {
        if (entity.Id == Guid.Empty)
            throw new InvalidOperationException("Entity must have a valid Id for upsert.");

        var request = new UpsertRequest { Target = entity };
        _service.Execute(request);
    }

    public async Task UpsertAsync(T entity)
        => await Task.Run(() => Upsert(entity));

    public void Delete(Guid id, string rowVersion = null)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            var entityRef = new EntityReference(_entityLogicalName, id)
            {
                RowVersion = rowVersion
            };
            var request = new DeleteRequest { Target = entityRef };
            _service.Execute(request);
        }
        else
        {
            DeleteWithRequest(id);
        }
    }

    public async Task DeleteAsync(Guid id, string rowVersion = null)
        => await Task.Run(() => Delete(id, rowVersion));

    public bool Exists(IEnumerable<ConditionExpression> conditions)
    {
        var queryExpression = new QueryExpression(_entityLogicalName)
        {
            ColumnSet = new ColumnSet(false),
            TopCount = 1,
            Criteria = new FilterExpression()
        };

        foreach (var condition in conditions) queryExpression.Criteria.AddCondition(condition);
        var response = _service.RetrieveMultiple(queryExpression);
        return response.Entities.Any();
    }

    public async Task<bool> ExistsAsync(IEnumerable<ConditionExpression> conditions)
        => await Task.FromResult(Exists(conditions));

    public int Count(IEnumerable<ConditionExpression> conditions)
    {
        var queryExpression = new QueryExpression(_entityLogicalName)
        {
            ColumnSet = new ColumnSet(false),
            Criteria = new FilterExpression()
        };

        foreach (var condition in conditions) queryExpression.Criteria.AddCondition(condition);
        return RetrieveAll(queryExpression).Count();
    }

    public async Task<int> CountAsync(IEnumerable<ConditionExpression> conditions)
        => await Task.FromResult(Count(conditions));

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
            foreach (var param in inputParams)
                request.Parameters[param.Key] = param.Value;

        var response = (CreateResponse)_service.Execute(request);
        return response.id;
    }

    public void UpdateWithRequest(T entity, Dictionary<string, object> inputParams = null)
    {
        var request = new UpdateRequest { Target = entity };

        if (inputParams != null)
            foreach (var param in inputParams)
                request.Parameters[param.Key] = param.Value;

        _service.Execute(request);
    }

    public void DeleteWithRequest(Guid id, Dictionary<string, object> inputParams = null)
    {
        var request = new DeleteRequest { Target = new EntityReference(_entityLogicalName, id) };

        if (inputParams != null)
            foreach (var param in inputParams)
                request.Parameters[param.Key] = param.Value;

        _service.Execute(request);
    }

    private IEnumerable<T> RetrieveAll(QueryExpression queryExpression, int pageSize = 5000, int pageNumber = 1)
    {
        var results = new List<T>();
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
}
