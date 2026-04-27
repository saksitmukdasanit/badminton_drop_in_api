using System;
using System.Collections.Generic;

namespace DropInBadAPI.Dtos
{
    public class WalletDto
    {
        public decimal Balance { get; set; }
        public List<WalletTransactionDto> Transactions { get; set; } = new();
    }
    public class WalletTransactionDto
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public short TransactionType { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class WithdrawRequestDto
    {
        public decimal Amount { get; set; }
    }
}