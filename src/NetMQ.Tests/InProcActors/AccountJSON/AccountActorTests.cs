using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Actors;
using NetMQ.Tests.InProcActors.AccountJSON;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NetMQ.Tests.InProcActors.Echo
{
    [TestFixture]
    public class AccountActorTests
    {
        [Test]
        public void AccountActorJSONSendReceiveTests()
        {
            AccountShimHandler accountShimHandler = new AccountShimHandler();

            AccountAction accountAction = new AccountAction(TransactionType.Credit, 10);
            Account account = new Account(1, "Test Account", "11223", 0);

            Actor<object> accountActor = new Actor<object>(NetMQContext.Create(), accountShimHandler,null);
            accountActor.SendMore("AMEND ACCOUNT");
            accountActor.SendMore(JsonConvert.SerializeObject(accountAction));
            accountActor.Send(JsonConvert.SerializeObject(account));
            Account updatedAccount =
                         JsonConvert.DeserializeObject<Account>(accountActor.ReceiveString());
            decimal expectedAccountBalance = 10.0m;
            Assert.AreEqual(expectedAccountBalance, updatedAccount.Balance);
            accountActor.Dispose();
        }
    }
}
