namespace BitcoinApi.Business.DataAccess.Entities
{
    internal class DbWallet
    {
        public int Id { get; set; }

        public string BitcoindServerAddress { get; set; }

        public string BitcoindUser { get; set; }

        public string BitcoindPassword { get; set; }

        public decimal Balance { get; set; }

        public string WalletPassphrase { get; set; }
    }
}
