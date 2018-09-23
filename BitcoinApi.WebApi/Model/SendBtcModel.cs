using System.ComponentModel.DataAnnotations;

namespace BitcoinApi.WebApi.Model
{
    public class SendBtcModel
    {
        [Required]
        [StringLength(35)]
        public string Address { get; set; }

        [Required]
        [StringLength(20 + 1 + 8)] // DECIMAL(20, 8) in database, I hope it should be enough
        public string Amount { get; set; }
    }
}