using Newtonsoft.Json;
using NUnit.Framework;

namespace NetMQ.Tests.InProcActors.AccountJSON
{
    [TestFixture]
    public class AccountActorTests
    {
        [Test]
        public void AccountActorJsonSendReceiveTests()
        {
            var account = new Account(1, "Test Account", "11223", 0);
            var accountAction = new AccountAction(TransactionType.Credit, 10);

            using (var context = NetMQContext.Create())
            using (var actor = NetMQActor.Create(context, new AccountShimHandler()))
            {
                actor.SendMore("AMEND ACCOUNT");
                actor.SendMore(JsonConvert.SerializeObject(accountAction));
                actor.Send(JsonConvert.SerializeObject(account));

                var updatedAccount = JsonConvert.DeserializeObject<Account>(actor.ReceiveFrameString());

                Assert.AreEqual(10.0m, updatedAccount.Balance);
            }
        }
    }
}
