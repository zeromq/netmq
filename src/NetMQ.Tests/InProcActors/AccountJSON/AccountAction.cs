using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Tests.InProcActors.AccountJSON
{
    public enum TransactionType { Debit = 1, Credit = 2 }
    public class AccountAction
    {
        public AccountAction()
        {

        }

        public AccountAction(TransactionType transactionType, decimal amount)
        {
            TransactionType = transactionType;
            Amount = amount;
        }

        public TransactionType TransactionType { get; set; }
        public decimal Amount { get; set; }
    }
}
