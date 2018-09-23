using BitcoinApi.Business.BitcoinApi;
using BitcoinApi.Business.BitcoinApi.Entities;
using BitcoinApi.Business.DataAccess;
using BitcoinApi.Business.DataAccess.Entities;
using BitcoinApi.Business.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BitcoinApi.Business
{
    public class BitcoinService
    {
        /*
            SemaphoreSlim because:
            - ration between SendBtc/GetLast is not clear by the task description.
            - it has non-blocking WaitAsync, therefore supports async/await.
        */
        private static class Locker
        {
            private static readonly Dictionary<int, SemaphoreSlim> SemaphorePerWallet =
                new DataAccessService()
                    .GetWallets()
                    .ToDictionary(x => x.Id, x => new SemaphoreSlim(1));

            public static async Task<IDisposable> Lock(int walletId)
            {
                var semaphore = SemaphorePerWallet[walletId];
                await semaphore.WaitAsync();
                return new Releaser(semaphore);
            }

            private class Releaser : IDisposable
            {
                private SemaphoreSlim SemaphoreSlim { get; }
                public Releaser(SemaphoreSlim semaphoreSlim)
                {
                    SemaphoreSlim = semaphoreSlim;
                }
                public void Dispose()
                {
                    SemaphoreSlim.Release();
                }
            }
        }

        /*
            If reads (GetLast) is much more often call ReaderWriterLockSlim can be used.
            - unfortunately it is thread-affine lock type, therefore async/await cannot be used.
            - this implementation can be used after rewriting BitcoinClient and rest calls chain to sync semantic.
        */
        private static class LockerByReaderWriterLockSlimImplementation
        {
            private static readonly Dictionary<int, ReaderWriterLockSlim> LocksPerWallet =
                new DataAccessService()
                    .GetWallets()
                    .ToDictionary(x => x.Id, x => new ReaderWriterLockSlim());

            // read = true for GetLast
            // read = false for SendBtc
            public static IDisposable Lock(int walletId, bool read)
            {
                var lockSlim = LocksPerWallet[walletId];
                if (read)
                {
                    lockSlim.EnterReadLock();
                }
                else
                {
                    lockSlim.EnterWriteLock();
                }
                return new Releaser(read, lockSlim);
            }

            private class Releaser : IDisposable
            {
                private bool Read { get; }

                private ReaderWriterLockSlim LockSlim { get; }

                public Releaser(bool read, ReaderWriterLockSlim lockSlim)
                {
                    Read = read;
                    LockSlim = lockSlim;
                }

                public void Dispose()
                {
                    if (Read)
                    {
                        LockSlim.ExitReadLock();
                    }
                    else
                    {
                        LockSlim.ExitWriteLock();
                    }
                }
            }
        }

        private DataAccessService DataAccessService { get; } = new DataAccessService();

        private BitcoinClient BitcoinClient { get; } = new BitcoinClient();

        public async Task<(bool success, string errorMessage)> SendBtc(string address, decimal amount)
        {
            // x.Balance > amount because total amount = amount + fee
            // As I understand, it is really hard to calculate accurate fee before send transaction (or I didn't found proper way).
            // Anyway, if funds is not enough transaction will fail.  
            var wallet = DataAccessService.GetWallets()
                .OrderByDescending(x => x.Balance)
                .FirstOrDefault(x => x.Balance > amount);

            if (wallet == null)
            {
                return (false, "Insufficient funds");
            }

            using (await Locker.Lock(wallet.Id))
            {
                if (!string.IsNullOrEmpty(wallet.WalletPassphrase) &&
                    Break(await BitcoinClient.MakeRequest("walletpassphrase", ToRequestParams(wallet), wallet.WalletPassphrase, 1000), out var result))
                {
                    return result;
                }
                if (Break(await BitcoinClient.MakeRequest("sendtoaddress", ToRequestParams(wallet), address, amount), out result))
                {
                    return result;
                }
                if (!string.IsNullOrEmpty(wallet.WalletPassphrase) &&
                    Break(await BitcoinClient.MakeRequest("walletlock", ToRequestParams(wallet)), out result))
                {
                    return result;
                }
            }

            return (true, string.Empty);

            bool Break(BitcoinApiResult apiResult, out (bool success, string errorMessage) result)
            {
                result = (string.IsNullOrEmpty(apiResult.Error), apiResult.Error);
                return !result.success;
            }
        }

        public async Task<(bool success, string errorMessage, Transaction[] trasactions)> GetLast()
        {
            var wallets = DataAccessService.GetWallets();
            var transactions = new List<Transaction>();

            foreach (var wallet in wallets)
            {
                (bool success, string errorMessage) = await AppendTransactions(transactions, wallet);
                if (!success)
                {
                    return (success: false, errorMessage, trasactions: new Transaction[0]);
                }
            }

            return (success: true, errorMessage: string.Empty, transactions.ToArray());
        }

        private async Task<(bool success, string errorMessage)> AppendTransactions(List<Transaction> result, DbWallet wallet)
        {
            var transactionsApiResult = await FetchTransactions(wallet);
            if (!string.IsNullOrEmpty(transactionsApiResult.Error))
            {
                return (false, transactionsApiResult.Error);
            }

            var transactionsFromApi = transactionsApiResult.Result.ToDictionary(x => GetKey(x.TxId, IsSend(x.Category)), x => x, StringComparer.InvariantCulture);
            var transactionsFromDb = DataAccessService.GetTransactions(wallet.Id).ToDictionary(x => GetKey(x.TxId, x.Category), x => x, StringComparer.InvariantCulture);

            var transactionsToInsertOrUpdate = new List<DbTransactionParam>(transactionsFromApi.Count);

            foreach (var transaction in transactionsFromApi)
            {
                AppendTransaction(transaction, result, transactionsToInsertOrUpdate, transactionsFromDb);
            }

            if (transactionsToInsertOrUpdate.Count > 0)
            {
                if (!DataAccessService.InsertOrUpdateTransactions(wallet.Id, transactionsToInsertOrUpdate.ToArray()))
                {
                    return (success: false, errorMessage: "Please try again later.");
                }
            }

            return (true, string.Empty);

            string GetKey(string txId, bool send)
            {
                return $"{txId}_{(send ? 1 : 0)}";
            }
        }

        private async Task<BitcoinApiResult<ApiTransaction[]>> FetchTransactions(DbWallet wallet)
        {
            var result = new List<ApiTransaction>();
            var step = 10;

            var accounts = await BitcoinClient.MakeRequest<Dictionary<string, string>>("listaccounts", ToRequestParams(wallet));
            if (!string.IsNullOrEmpty(accounts.Error))
            {
                return new BitcoinApiResult<ApiTransaction[]>
                {
                    Error = accounts.Error,
                    Id = accounts.Id,
                    Result = new ApiTransaction[0],
                };
            }

            using (await Locker.Lock(wallet.Id))
            {
                foreach (var account in accounts.Result)
                {
                    for (int i = 0; ; i += step)
                    {
                        var transactionsResult = await BitcoinClient.MakeRequest<ApiTransaction[]>("listtransactions", ToRequestParams(wallet), account.Key, step, i);
                        if (!string.IsNullOrEmpty(transactionsResult.Error))
                        {
                            return transactionsResult;
                        }

                        if (transactionsResult.Result.Length == 0)
                        {
                            break;
                        }

                        result.AddRange(transactionsResult.Result);
                    }
                }

                return new BitcoinApiResult<ApiTransaction[]>
                {
                    Result = result.ToArray(),
                };
            }
        }

        private void AppendTransaction(
            KeyValuePair<string, ApiTransaction> transaction,
            List<Transaction> result,
            List<DbTransactionParam> transactionsToInsertOrUpdate,
            Dictionary<string, DbTransaction> transactionsFromDb)
        {
            if (!transactionsFromDb.ContainsKey(transaction.Key))
            {
                transactionsToInsertOrUpdate.Add(ToDbTransactionParam(transaction.Value, null));
                result.Add(ToTransaction(transaction.Value));
            }
            else
            {
                if (NeedToUpdate(transactionsFromDb, transaction))
                {
                    transactionsToInsertOrUpdate.Add(ToDbTransactionParam(transaction.Value, transactionsFromDb[transaction.Key].Id));
                }

                if (transaction.Value.Confirmations < 3)
                {
                    result.Add(ToTransaction(transaction.Value));
                }
            }

            DbTransactionParam ToDbTransactionParam(ApiTransaction from, int? id)
            {
                return new DbTransactionParam
                {
                    Id = id,
                    TxId = from.TxId,
                    Confirmations = from.Confirmations,
                    Address = from.Address,
                    Amount = from.Amount,
                    Category = IsSend(from.Category),
                    Fee = from.Fee,
                    ReceivedDateTime = ToDateTime(from.TimeReceived),  
                };
            }

            Transaction ToTransaction(ApiTransaction from)
            {
                return new Transaction
                {
                    Confirmations = from.Confirmations,
                    Address = from.Address,
                    Amount = from.Amount,
                    Date = ToDateTime(from.TimeReceived),
                };
            }

            bool NeedToUpdate(Dictionary<string, DbTransaction> targetTransactions, KeyValuePair<string, ApiTransaction> sourceTransaction)
            {
                var confirmationsFromDb = targetTransactions[sourceTransaction.Key].Confirmations;
                return
                    confirmationsFromDb <= 6 &&
                    confirmationsFromDb != sourceTransaction.Value.Confirmations;
            }
        }

        private static bool IsSend(string transactionCategory)
        {
            return transactionCategory.Equals("send", StringComparison.InvariantCultureIgnoreCase);
        }

        private (string bitcoindAddress, string user, string password) ToRequestParams(DbWallet wallet)
        {
            return
            (
                wallet.BitcoindServerAddress,
                wallet.BitcoindUser,
                wallet.BitcoindPassword
            );
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private DateTime ToDateTime(long unixTime)
        {
            return Epoch.AddSeconds(unixTime);
        }
    }
}
