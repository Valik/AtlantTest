using System;

namespace BitcoinApi.Business.DataAccess.Entities
{
    internal class DbTransaction
    {
        public int Id { get; set; }

        public string TxId { get; set; }

        public decimal Amount { get; set; }

        public decimal Fee { get; set; }

        public bool Category { get; set; }

        public string Address { get; set; }

        public DateTime ReceivedDateTime { get; set; }

        public int Confirmations { get; set; }
    }
}
