NetMQ Actor Model
===

## What is an Actor model?

From [Wikipedia's Actor Model page](http://en.wikipedia.org/wiki/Actor_model):

> The actor model in computer science is a mathematical model of concurrent computation that treats “actors” as the universal primitives of concurrent digital computation: in response to a message that it receives, an actor can make local decisions, create more actors, send more messages, and determine how to respond to the next message received.
>
> The Actor model adopts the philosophy that everything is an actor. This is similar to the everything is an object philosophy used by some object-oriented programming languages, but differs in that object-oriented software is typically executed sequentially, while the Actor model is inherently concurrent.
>
> An actor is a computational entity that, in response to a message it receives, can concurrently:
>
> * send a finite number of messages to other actors
> * create a finite number of new actors
> * designate the behavior to be used for the next message it receives
>
> There is no assumed sequence to the above actions and they could be carried out in parallel.
>
> Decoupling the sender from communications sent was a fundamental advance of the Actor model enabling asynchronous communication and control structures as patterns of passing messages.
>
> Recipients of messages are identified by address, sometimes called “mailing address”. Thus an actor can only communicate with actors whose addresses it has. It can obtain those from a message it receives, or if the address is for an actor it has itself created.
>
> The Actor model is characterized by inherent concurrency of computation within and among actors, dynamic creation of actors, inclusion of actor addresses in messages, and interaction only through direct asynchronous message passing with no restriction on message arrival order.

一個很好的思考Actors的方式是─他們是用來減輕一些在同步化時使用共享資料結構需要注意的地方。這是在你的程式中與actor通過訊息傳送/接收實作的。Actor本身可以將訊息傳送給其他actor，或者處理傳送的訊息本身。通過使用訊息傳送而不是使用共享資料結構，它有助於讓你認為actor（或其發送訊息的任何後續actor）實際上是在資料的拷貝上工作，而不是在相同的共享資料結構上工作。讓我們擺脫了多執行緒程式中需要擔心的可怕事情，如鎖和任何討厭的定時問題。如果actor使用自己的資料拷貝，那麼我們應該沒有其他的執行緒想要使用此actor所擁有的資料的問題，因為資料只在actor本身之內可見，unless we pass another message to a different actor。如果我們這樣做，新的訊息給另一個actor也只是另一個資料的拷貝，因此也是執行緒安全的。

I hope you see what I am trying to explain there, maybe a diagram may help.

## Multi-threading with shared data

一個相當普遍的事情是用多個執行緒運行以加快速度，然後你發現到你的執行緒需要改變一些共享資料的狀態，那麼你會涉及到執行緒同步（最常見的 lock(..) statements, 以建立自己的 critical sections）。這有用，但現在你正引入人為的延遲，由於必須等待鎖被釋放，所以你可以執行執行緒X的程式。

![](Images/ActorTrad.png)

更進一步，讓我們看看一些程式，可以說明這一點。想像一下，我們有一個資料結構代表非常簡單的銀行帳戶：

    :::csharp
    public class Account
    {
        public Account(int id, string name, string sortCode, decimal balance)
        {
            Id = id;
            Name = name;
            SortCode = sortCode;
            Balance = balance;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string SortCode { get; set; }
        public decimal Balance { get; set; }

        public override string ToString()
        {
            return string.Format("Id: {0}, Name: {1}, SortCode: {2}, Balance: {3}",
                Id, Name, SortCode, Balance);
        }
    }

這裡沒有什麼特別的，只是一些欄位。讓我們來看一些執行緒程式，我選擇只顯示兩個執行緒共用Account實體的程式。

    :::csharp
    static void Main()
    {
        var account = new Account(1, "sacha barber", "112233", 0);
        var syncLock = new object();

        // start two asynchronous tasks that both mutate the account balance

        var task1 = Task.Run(() =>
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            Console.WriteLine("Thread Id {0}, Account balance before: {1}",
                threadId, account.Balance);

            lock (syncLock)
            {
                Console.WriteLine("Thread Id {0}, Adding 10 to balance",
                    threadId);
                account.Balance += 10;
                Console.WriteLine("Thread Id {0}, Account balance after: {1}",
                    threadId, account.Balance);
            }
        });

        var task2 = Task.Run(() =>
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            Console.WriteLine("Thread Id {0}, Account balance before: {1}",
                threadId, account.Balance);

            lock (syncLock)
            {
                Console.WriteLine("Thread Id {0}, Subtracting 4 from balance",
                   threadId);
                account.Balance -= 4;
                Console.WriteLine("Thread Id {0}, Account balance after: {1}",
                    threadId, account.Balance);
            }
        });

        // wait for all tasks to complete
        task1.Wait();
        task2.Wait();
    }

你也許認為這個範例不會發生在現實生活中，誠實的說，這真的不會發生，誰會真的在一個執行緒當中存款，而在另一個執行緒中取款呢…我們都是聰明的開發者，不會這樣寫的，不是嗎？

老實說，不管這個範例是否會在現實生活中出現，要點出的問題仍然是相同的，因為我們有多個執行緒存取共用資料結構，存取時必須同步，且通常使用lock（.. ）語法，如同程式所見。

現在不要誤會我，上面的程式碼可正常工作，如下面的輸出所示：

    :::text
    Thread Id 6, Account balance before: 0
    Thread Id 6, Adding 10 to balance
    Thread Id 6, Account balance after: 10
    Thread Id 10, Account balance before: 10
    Thread Id 10, Subtracting 4 to balance
    Thread Id 10, Account balance after: 6

也許可能有一個更有趣的方式！


## Actor model

Actor模型採用不同的方法，其中使用訊息傳遞的方式可能會涉及某種形式的序列化，因為訊息是向下傳遞的，保證沒有共享結構的競爭。我不是說所有的Actor框架都使用訊息傳遞（序列化），但本文中提供的程式碼是。

基本思想是每個執行緒都會與一個actor交談，並與actor傳送/接收訊息。

如果你想要得到更多的隔離性，你可以使用執行緒的local storage，每個執行緒可以有自己的actor的副本。

![](Images/ActorPass.png)

談的夠多了，讓我們來看程式碼吧…


## Actor demo

我們會持續使用和傳統上的locking/sahred data相同類型的範例。

讓我們先介紹幾個helper類別：

### AccountAction

    :::csharp
    public enum TransactionType { Debit = 1, Credit = 2 }

    public class AccountAction
    {
        public AccountAction(TransactionType transactionType, decimal amount)
        {
            TransactionType = transactionType;
            Amount = amount;
        }

        public TransactionType TransactionType { get; set; }
        public decimal Amount { get; set; }
    }

### Account

和之前的一樣。

    :::csharp
    public class Account
    {
        public Account(int id, string name, string sortCode, decimal balance)
        {
            Id = id;
            Name = name;
            SortCode = sortCode;
            Balance = balance;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string SortCode { get; set; }
        public decimal Balance { get; set; }

        public override string ToString()
        {
            return string.Format("Id: {0}, Name: {1}, SortCode: {2}, Balance: {3}",
                Id, Name, SortCode, Balance);
        }
    }

### AccountActioner

以下是處理帳戶操作的Actor的完整程式。這個例子是故意簡單化的，我們只用一筆金額借/貸一個帳戶。你可以發送任何命令到Actor，而Actor只是一個一般化的處理訊息的系統。

程式碼如下：

    :::csharp
    public class AccountActioner
    {
        public class ShimHandler : IShimHandler
        {
            private PairSocket shim;
            private NetMQPoller poller;

            public void Initialise(object state)
            {
            }

            public void Run(PairSocket shim)
            {
                this.shim = shim;
                shim.ReceiveReady += OnShimReady;
                shim.SignalOK();

                poller = new NetMQPoller { shim };
                poller.Run();
            }

            private void OnShimReady(object sender, NetMQSocketEventArgs e)
            {
                string command = e.Socket.ReceiveFrameString();

                switch (command)
                {
                    case NetMQActor.EndShimMessage:
                        Console.WriteLine("Actor received EndShimMessage");
                        poller.Stop();
                        break;
                    case "AmmendAccount":
                        Console.WriteLine("Actor received AmmendAccount message");
                        string accountJson = e.Socket.ReceiveFrameString();
                        Account account
                            = JsonConvert.DeserializeObject<Account>(accountJson);
                        string accountActionJson = e.Socket.ReceiveFrameString();
                        AccountAction accountAction
                            = JsonConvert.DeserializeObject<AccountAction>(
                                accountActionJson);
                        Console.WriteLine("Incoming Account details are");
                        Console.WriteLine(account);
                        AmmendAccount(account, accountAction);
                        shim.SendFrame(JsonConvert.SerializeObject(account));
                        break;
                }
            }

            private void AmmendAccount(Account account, AccountAction accountAction)
            {
                switch (accountAction.TransactionType)
                {
                    case TransactionType.Credit:
                        account.Balance += accountAction.Amount;
                        break;
                    case TransactionType.Debit:
                        account.Balance -= accountAction.Amount;
                        break;
                }
            }
        }

        private NetMQActor actor;

        public void Start()
        {
            if (actor != null)
                return;

            actor = NetMQActor.Create(new ShimHandler());
        }

        public void Stop()
        {
            if (actor != null)
            {
                actor.Dispose();
                actor = null;
            }
        }

        public void SendPayload(Account account, AccountAction accountAction)
        {
            if (actor == null)
                return;

            Console.WriteLine("About to send person to Actor");

            var message = new NetMQMessage();
            message.Append("AmmendAccount");
            message.Append(JsonConvert.SerializeObject(account));
            message.Append(JsonConvert.SerializeObject(accountAction));
            actor.SendMultipartMessage(message);
        }

        public Account GetPayLoad()
        {
            return JsonConvert.DeserializeObject<Account>(actor.ReceiveFrameString());
        }
    }

### Tying it all together

你可以使用下列程式碼和Actor溝通，再次地說，你可以使用任何命令，這個範例只顯示對一個帳戶的借/貸。

    :::csharp
    class Program
    {
        static void Main(string[] args)
        {
            // CommandActioner uses an NetMq.Actor internally
            var accountActioner = new AccountActioner();

            var account = new Account(1, "Doron Somech", "112233", 0);
            PrintAccount(account);

            accountActioner.Start();
            Console.WriteLine("Sending account to AccountActioner/Actor");
            accountActioner.SendPayload(account,
                new AccountAction(TransactionType.Credit, 15));

            account = accountActioner.GetPayLoad();
            PrintAccount(account);

            accountActioner.Stop();
            Console.WriteLine();
            Console.WriteLine("Sending account to AccountActioner/Actor");
            accountActioner.SendPayload(account,
                new AccountAction(TransactionType.Credit, 15));
            PrintAccount(account);

            Console.ReadLine();
        }

        static void PrintAccount(Account account)
        {
            Console.WriteLine("Account now");
            Console.WriteLine(account);
            Console.WriteLine();
        }
    }

執行時應可以看見如下輸出：

![](Images/ActorsOut.png)

我們希望這可以讓你知道可以用一個Actor做些什麼事…
