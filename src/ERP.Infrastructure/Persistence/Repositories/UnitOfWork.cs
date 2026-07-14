using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence.Contexts;

namespace ERP.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public IUserRepository Users { get; }
    public ICustomerRepository Customers { get; }
    public IProductRepository Products { get; }
    public IOrderRepository Orders { get; }
    public IPaymentRepository Payments { get; }
    public IAiActionLogRepository AiActionLogs { get; }

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new UserRepository(_context);
        Customers = new CustomerRepository(_context);
        Products = new ProductRepository(_context);
        Orders = new OrderRepository(_context);
        Payments = new PaymentRepository(_context);
        AiActionLogs = new AiActionLogRepository(_context);
    }

    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters)
    {
        return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    public async Task<IEnumerable<Dictionary<string, object>>> QuerySqlRawAsync(string sql, params object[] parameters)
    {
        var connection = _context.Database.GetDbConnection();
        bool wasClosed = connection.State == ConnectionState.Closed;
        
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = parameters[i] ?? DBNull.Value;
                    command.Parameters.Add(param);
                }
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var val = reader.GetValue(i);
                    row[colName] = val == DBNull.Value ? null! : val;
                }
                results.Add(row);
            }

            return results;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
