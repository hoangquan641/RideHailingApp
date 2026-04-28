using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RideHailingApp.DAL.Entities
{
    public class UserWallet
    {
        [Key, ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }

        public decimal CashBalance { get; set; } = 0m;
        public decimal CreditBalance { get; set; } = 0m;
    }
}