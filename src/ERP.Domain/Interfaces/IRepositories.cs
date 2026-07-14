using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByCodeAsync(string code);
}

public interface IProductRepository : IRepository<Product>
{
}

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<IEnumerable<Order>> GetPendingOrdersAsync();
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetPaymentsByCustomerAsync(int customerId);
}

public interface IAiActionLogRepository : IRepository<AiActionLog>
{
}

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ICustomerRepository Customers { get; }
    IProductRepository Products { get; }
    IOrderRepository Orders { get; }
    IPaymentRepository Payments { get; }
    IAiActionLogRepository AiActionLogs { get; }
    Task<int> CompleteAsync();
    
    // Low-level helper to execute raw SQL directly, which is needed by the NLP engine
    Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters);
    Task<IEnumerable<Dictionary<string, object>>> QuerySqlRawAsync(string sql, params object[] parameters);
}
