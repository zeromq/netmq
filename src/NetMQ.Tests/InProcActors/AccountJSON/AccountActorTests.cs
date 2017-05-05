using Newtonsoft.Json;
using Xunit;

namespace NetMQ.Tests.InProcActors.AccountJSON
{
    public class AccountActorTests
    {
        [Fact]
        public void AccountActorJsonSendReceiveTests()
        {
            var account = new Account(1, "Test Account", "11223", 0);
            var accountAction = new AccountAction(TransactionType.Credit, 10);

            using (var actor = NetMQActor.Create(new AccountShimHandler()))
            {
                actor.SendMoreFrame("AMEND ACCOUNT");
                actor.SendMoreFrame(JsonConvert.SerializeObject(accountAction));
                actor.SendFrame(JsonConvert.SerializeObject(account));

                var updatedAccount = JsonConvert.DeserializeObject<Account>(actor.ReceiveFrameString());

                Assert.Equal(10.0m, updatedAccount.Balance);
            }
        }
    }
}
