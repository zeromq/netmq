using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.InProcActors;
using NetMQ.Sockets;
using NetMQ.zmq;
using NetMQ.Actors;
using Newtonsoft.Json;

namespace NetMQ.Tests.InProcActors.AccountJSON
{
    /// <summary>
    /// This hander class is specific implementation that you would need
    /// to implement per actor. This essentially contains your commands/protocol
    /// and should deal with any command workload, as well as sending back to the
    /// other end of the PairSocket which calling code would receive by using the
    /// Actor classes various RecieveXXX() methods
    /// 
    /// This is a VERY simple protocol but it just demonstrates what you would need
    /// to do to implement your own Shim handler
    /// 
    /// The only things you MUST do is to follow this example for handling
    /// a fews things
    /// 
    /// 1. Bad commands should always send the following message
    ///    "Error: invalid message to actor"
    /// 2. When we recieve a command from the actor telling us to exit the pipeline we should immediately
    ///    break out of the while loop, and dispose of the shim socket
    /// 3. When an Exception occurs you should send that down the wire to Actors calling code
    /// </summary>
    public class AccountShimHandler : IShimHandler<object>
    {

        public void Initialise(object state)
        {
            //not used in this example
        }


		public void RunPipeline(IPairSocket shim)
        {
            shim.SignalOK();

            while (true)
            {
                try
                {
                    //Message for this actor/shim handler is expected to be 
                    //Frame[0] : Command
                    //Frame[2] : AccountAction as JSON
                    //Frame[1] : Account as JSON
                    //
                    //Result back to actor is a JSON message of the amended Account
                    NetMQMessage msg = shim.ReceiveMessage();

                    string command = msg[0].ConvertToString();

                    if (command == ActorKnownMessages.END_PIPE)
                        break;

                    if (msg[0].ConvertToString() == "AMEND ACCOUNT")
                    {
                        string accountActionJson = msg[1].ConvertToString();
                        AccountAction accountAction = JsonConvert.DeserializeObject<AccountAction>(accountActionJson);
                        
                        string accountJson = msg[2].ConvertToString();
                        Account account = JsonConvert.DeserializeObject<Account>(accountJson);
                        AmmendAccount(accountAction, account);
                        shim.Send(JsonConvert.SerializeObject(account));
                    }

                    else
                    {
                        shim.Send("Error: invalid message to actor");
                    }
                }
                //You WILL need to decide what Exceptions should be caught here, this is for 
                //demonstration purposes only, any unhandled falut will bubble up to callers code
                catch (Exception e)
                {
                    shim.Send(string.Format("Error: Exception occurred {0}", e.Message));
                }
            }        
        }

        private void AmmendAccount(AccountAction action, Account account)
        {
            decimal currentAmount = account.Balance;
            account.Balance = action.TransactionType == TransactionType.Debit
                ? currentAmount - action.Amount
                : currentAmount + action.Amount;
        }
    }
}

