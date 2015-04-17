﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Takenet.SimplePersistence.Sql.Mapping;
using static Takenet.SimplePersistence.Sql.SqlHelper;
using static Takenet.SimplePersistence.Sql.DatabaseSchema;

namespace Takenet.SimplePersistence.Sql
{
    public abstract class StorageBase<TEntity> : IQueryableStorage<TEntity>
    {                                                
        protected StorageBase(ITable table, string connectionString)
        {
            Table = table;
            ConnectionString = connectionString;
        }

        protected ITable Table { get; }        

        protected string ConnectionString { get; }

        protected abstract IMapper<TEntity> Mapper { get; }

        protected abstract IDatabaseDriver DatabaseDriver { get; }

        protected async Task<bool> TryRemoveAsync(IDictionary<string, object> filterValues, DbConnection connection, CancellationToken cancellationToken, DbTransaction sqlTransaction = null)
        {            
            using (var command = connection.CreateDeleteCommand(Table.Name, filterValues))
            {
                if (sqlTransaction != null) command.Transaction = sqlTransaction;                
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
            }
        }

        protected async Task<bool> ContainsAsync(IDictionary<string, object> filterValues, DbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = connection.CreateContainsCommand(Table.Name, filterValues))
            {
                return (bool)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #region IQueryableStorage<TEntity>

        public async Task<QueryResult<TEntity>> QueryAsync<TResult>(Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, TResult>> select, int skip, int take, CancellationToken cancellationToken)
        {
            if (select != null && 
                select.ReturnType != typeof(TEntity))
            {
                throw new NotImplementedException("The select parameter is not supported yet");
            }

            var selectColumns = Table.Columns.Keys.ToArray();
            var orderByColumns = Table.KeyColumns;
            var filter = GetFilters(where);        
            var connection = await GetConnectionAsync(cancellationToken);            
            int totalCount;
            using (var countCommand = connection.CreateSelectCountCommand(Table.Name, filter))
            {
                totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }

            var command = connection.CreateSelectSkipTakeCommand(
                Table.Name, selectColumns, filter, skip, take, orderByColumns);
                                            
            return new QueryResult<TEntity>(
                new DbDataReaderAsyncEnumerable<TEntity>(command, Mapper, selectColumns), totalCount);
        }

        #endregion

        protected async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = DatabaseDriver.CreateConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CheckTableSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }

        #region Protected Members

        protected CancellationToken CreateCancellationToken()
        {
            return CancellationToken.None;
        }

        protected virtual IDictionary<string, object> GetColumnValues(TEntity entity)
        {
            return Mapper.GetColumnValues(entity);
        }

        protected IDictionary<string, object> GetKeyColumnValues(TEntity entity)
        {
            return GetKeyColumnValues(GetColumnValues(entity));
        }

        protected virtual IDictionary<string, object> GetKeyColumnValues(IDictionary<string, object> columnValues)
        {
            return Table
                .KeyColumns
                .Where(columnValues.ContainsKey)
                .Select(c => new { Key = c, Value = columnValues[c] })
                .ToDictionary(t => t.Key, t => t.Value);
        }

        #endregion

        #region Private Methods

        private bool _schemaChecked;
        private readonly SemaphoreSlim _schemaValidationSemaphore = new SemaphoreSlim(1);

        private async Task CheckTableSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            if (!_schemaChecked)
            {
                await _schemaValidationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (!_schemaChecked)
                    {
                        // Check if the table exists
                        var tableExists = await connection.ExecuteScalarAsync<bool>(
                            DatabaseDriver.GetSqlStatementTemplate(SqlStatement.TableExists).Format(
                            new
                            {
                                tableName = Table.Name
                            }),
                            cancellationToken).ConfigureAwait(false);

                        if (!tableExists)
                        {
                            await CreateTableAsync(DatabaseDriver, connection, Table, cancellationToken).ConfigureAwait(false);
                        }

                        await UpdateTableSchemaAsync(DatabaseDriver, connection, Table, cancellationToken).ConfigureAwait(false);
                        _schemaChecked = true;
                    }
                }
                finally
                {
                    _schemaValidationSemaphore.Release();
                }
            }
        }

        #endregion
    }
}