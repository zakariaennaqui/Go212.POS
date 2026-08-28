using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Interfaces;
using Go212.POS.Infrastructure.Data.Repositories;
using Go212.POS.Infrastructure.Backup;
using Dapper;
using MySqlConnector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Infrastructure.Data;

/// <summary>
/// Database connection factory.
/// Uses MySqlConnector (not the old MySql.Data).
/// Connection string read from appsettings.json.
/// MySQL account should have minimum required permissions only.
/// </summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Go212POS")
            ?? throw new InvalidOperationException("Connection string 'Go212POS' not found in configuration.");
    }

    public MySqlConnection CreateConnection()
        => new MySqlConnection(_connectionString);
}

/// <summary>
/// Unit of Work implementation using Dapper + MySqlConnector.
/// Wraps a single connection and transaction for atomic operations.
/// Rule: if any step fails → RollbackAsync() → full operation cancelled.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbConnectionFactory _factory;
    private MySqlConnection? _connection;
    private MySqlTransaction? _transaction;

    public IUserRepository Users { get; private set; }
    public ICategoryRepository Categories { get; private set; }
    public IProductRepository Products { get; private set; }
    public ISaleRepository Sales { get; private set; }
    public ICashSessionRepository CashSessions { get; private set; }
    public IStockMovementRepository StockMovements { get; private set; }
    public ICustomerRepository Customers { get; private set; }
    public IExpenseRepository Expenses { get; private set; }
    public IReturnRepository Returns { get; private set; }
    public ISettingRepository Settings { get; private set; }
    public IAuditRepository Audit { get; private set; }

    public UnitOfWork(DbConnectionFactory factory)
    {
        _factory       = factory;
        _connection    = factory.CreateConnection();

        Users          = new UserRepository(_connection);
        Categories     = new CategoryRepository(_connection);
        Products       = new ProductRepository(_connection);
        Sales          = new SaleRepository(_connection);
        CashSessions   = new CashSessionRepository(_connection);
        StockMovements = new StockMovementRepository(_connection);
        Customers      = new CustomerRepository(_connection);
        Expenses       = new ExpenseRepository(_connection);
        Returns        = new ReturnRepository(_connection);
        Settings       = new SettingRepository(_connection);
        Audit          = new AuditRepository(_connection);
    }

    public async Task BeginTransactionAsync()
    {
        if (_connection!.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();

        // Pass transaction to all repositories
        SetTransaction(_transaction);
    }

    public async Task CommitAsync()
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync();
        _transaction = null;
        SetTransaction(null);
    }

    public async Task RollbackAsync()
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync();
        _transaction = null;
        SetTransaction(null);
    }

    private void SetTransaction(MySqlTransaction? tx)
    {
        ((UserRepository)Users).SetTransaction(tx);
        ((CategoryRepository)Categories).SetTransaction(tx);
        ((ProductRepository)Products).SetTransaction(tx);
        ((SaleRepository)Sales).SetTransaction(tx);
        ((CashSessionRepository)CashSessions).SetTransaction(tx);
        ((StockMovementRepository)StockMovements).SetTransaction(tx);
        ((CustomerRepository)Customers).SetTransaction(tx);
        ((ExpenseRepository)Expenses).SetTransaction(tx);
        ((ReturnRepository)Returns).SetTransaction(tx);
        ((SettingRepository)Settings).SetTransaction(tx);
        ((AuditRepository)Audit).SetTransaction(tx);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null)  await _connection.DisposeAsync();
    }
}
