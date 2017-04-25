namespace NetMQ.Tests.InProcActors.AccountJSON
{
    /// <summary>
    /// This enum-type indicates the type of payment-card to be used in a transaction,
    /// either Debit or Credit.
    /// </summary>
    public enum TransactionType
    {
        Debit = 1,
        Credit = 2
    }

    /// <summary>
    /// An AccountAction represents a payment - providing an Amount and the type of payment-card.
    /// </summary>
    public class AccountAction
    {
        /// <summary>
        /// Create a new AccountAction object with a default TransactionType of Debit, and Amount of 0.
        /// </summary>
        public AccountAction()
        {}

        /// <summary>
        /// Create a new AccountAction object with the given TransactionType and Amount.
        /// </summary>
        /// <param name="transactionType">either Debit or Credit</param>
        /// <param name="amount">the amount of the transaction (units of currency)</param>
        public AccountAction(TransactionType transactionType, decimal amount)
        {
            TransactionType = transactionType;
            Amount = amount;
        }

        /// <summary>
        /// Get or set the type of payment-card to be used,
        /// which is either TransactionType.Debit or TransactionType.Credit.
        /// </summary>
        public TransactionType TransactionType { get; set; }

        /// <summary>
        /// Get or set the amount of this transaction (units of currency).
        /// </summary>
        public decimal Amount { get; set; }
    }
}
