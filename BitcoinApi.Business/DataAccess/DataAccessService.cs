using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using BitcoinApi.Business.DataAccess.Entities;

namespace BitcoinApi.Business.DataAccess
{
    internal class DataAccessService
    {
        public DbWallet[] GetWallets()
        {
            using (var dataTable = ExecuteStoredProcedure("[dbo].[sp_SelectWallets]"))
            {
                var result = Map<DbWallet>(dataTable);
                return result.ToArray();
            }
        }

        public DbTransaction[] GetTransactions(int walletId)
        {
            using (var dataTable = ExecuteStoredProcedure("[dbo].[sp_SelectTransactions]", ("@walletId", SqlDbType.Int, string.Empty, walletId)))
            {
                var result = Map<DbTransaction>(dataTable);
                return result.ToArray(); 
            }
        }

        public bool InsertOrUpdateTransactions(int walletId, DbTransactionParam[] transactions)
        {
            using (var transactionsTable = ToDataTable())
            {
                try
                {
                    using (ExecuteStoredProcedure(
                        IsolationLevel.Serializable,
                        "[dbo].[sp_InsertOrUpdateTransactions]",
                        ("@walletId", SqlDbType.Int, string.Empty, walletId),
                        ("@transactions", SqlDbType.Structured, "dbo.type_Transaction", transactionsTable)))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    do
                    {
                        if (ex.Message.Contains("UQ_tbl_Transaction_TxId"))
                        {
                            return false;
                        }
                        ex = ex.InnerException;
                    } while (ex != null);
                    throw;
                }
            }

            DataTable ToDataTable()
            {
                var dataTable = new DataTable();
                dataTable.Columns.Add(nameof(DbTransactionParam.Id), typeof(int));
                dataTable.Columns.Add(nameof(DbTransactionParam.TxId), typeof(string));
                dataTable.Columns.Add(nameof(DbTransactionParam.Amount), typeof(decimal));
                dataTable.Columns.Add(nameof(DbTransactionParam.Fee), typeof(decimal));
                dataTable.Columns.Add(nameof(DbTransactionParam.Category), typeof(bool));
                dataTable.Columns.Add(nameof(DbTransactionParam.Address), typeof(string));
                dataTable.Columns.Add(nameof(DbTransactionParam.ReceivedDateTime), typeof(DateTime));
                dataTable.Columns.Add(nameof(DbTransactionParam.Confirmations), typeof(int));

                foreach (var transaction in transactions)
                {
                    dataTable.Rows.Add(
                        transaction.Id,
                        transaction.TxId,
                        transaction.Amount,
                        transaction.Fee,
                        transaction.Category,
                        transaction.Address,
                        transaction.ReceivedDateTime,
                        transaction.Confirmations);
                }

                return dataTable;
            }
        }

        private static DataTable ExecuteStoredProcedure(string storedProcedureName, params (string key, SqlDbType type, string typeName, object value)[] args)
        {
            return ExecuteStoredProcedure(IsolationLevel.ReadCommitted, storedProcedureName, args);
        }

        private static DataTable ExecuteStoredProcedure(IsolationLevel isolationLevel, string storedProcedureName, params (string key, SqlDbType type, string typeName, object value)[] args)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    command.Transaction = transaction;
                    command.CommandText = storedProcedureName;
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters
                        .AddRange(args.Select(x => new SqlParameter(x.key, x.type)
                            {
                                Value = x.value,
                                TypeName = x.typeName,
                            })
                            .ToArray());

                    using (var dataAdapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        dataAdapter.Fill(dataTable);

                        transaction.Commit();
                        return dataTable;
                    }
                }
            }
        }

        private static IEnumerable<T> Map<T>(DataTable table) where T : new()
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            foreach (var row in table.AsEnumerable())
            {
                var item = new T();

                foreach (var prop in properties)
                {
                    var propertyInfo = type.GetProperty(prop.Name);
                    propertyInfo?.SetValue(item, Convert.ChangeType(row[prop.Name], propertyInfo.PropertyType), null);
                }

                yield return item;
            }
        }
    }
}
