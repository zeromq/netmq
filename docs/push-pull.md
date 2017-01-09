Push / Pull
=====

`NetMQ`提供了`PushSocket`和`PullSocket`，這些是什麼？要如何使用？

嗯，`PushSocket`一般是用來推送訊息至`PullSocket`，而`PullSocket`是用來從`PushSocket`取得訊息，聽起來很對吧！

你通常使用這種設定的socket來產生一些分佈式的工作，有點像 <a href="http://zguide.zeromq.org/page:all#Divide-and-Conquer" target="_blank">divide and conquer</a> 的安排。

這個想法是，你有一些產生工作的東西，然後將工作分配給多個工人。工人每個都做一些工作，並將結果推送到其他工序（可能是一個執行緒），工人的產出在那裡累積。

在 <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> 中，它顯示了一個範例，其中work generator只是告訴每個工人睡眠一段時間。

我們試圖創建一個比這更複雜的例子，但是最終覺得這個例子的簡單性是相當重要的，所以我們讓每個工人的工作量變成一個代入值，告訴工作休眠幾毫秒（從而模擬一些實際工作）。這個例子，正如我所說，是從 <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> 借來的。

In real life the work could obviously be anything, though you would more than likely want the work to be something that could be cut up and distributed without the work generator caring/knowing how many workers there are.

這裡是我們試圖實作的：

![](Images/Fanout.png)

## Ventilator

    :::csharp
    using System;
    using NetMQ;

    namespace Ventilator
    {
        public class Program
        {
            public static void Main(string[] args)
            {
                // Task Ventilator
                // Binds PUSH socket to tcp://localhost:5557
                // Sends batch of tasks to workers via that socket
                Console.WriteLine("====== VENTILATOR ======");

                using (var sender = new PushSocket("@tcp://*:5557"))
                using (var sink = new PushSocket(">tcp://localhost:5558"))
                {
                    Console.WriteLine("Press enter when worker are ready");
                    Console.ReadLine();

                    //the first message it "0" and signals start of batch
                    //see the Sink.csproj Program.cs file for where this is used
                    Console.WriteLine("Sending start of batch to Sink");
                    sink.SendFrame("0");

                    Console.WriteLine("Sending tasks to workers");

                    //initialise random number generator
                    Random rand = new Random(0);

                    //expected costs in Ms
                    int totalMs = 0;

                    //send 100 tasks (workload for tasks, is just some random sleep time that
                    //the workers can perform, in real life each work would do more than sleep
                    for (int taskNumber = 0; taskNumber < 100; taskNumber++)
                    {
                        //Random workload from 1 to 100 msec
                        int workload = rand.Next(0, 100);
                        totalMs += workload;
                        Console.WriteLine("Workload : {0}", workload);
                        sender.SendFrame(workload.ToString());
                    }
                    Console.WriteLine("Total expected cost : {0} msec", totalMs);
                    Console.WriteLine("Press Enter to quit");
                    Console.ReadLine();
                }
            }
        }
    }

## Worker

    :::csharp
    using System;
    using System.Threading;
    using NetMQ;

    namespace Worker
    {
        public class Program
        {
            public static void Main(string[] args)
            {
                // Task Worker
                // Connects PULL socket to tcp://localhost:5557
                // collects workload for socket from Ventilator via that socket
                // Connects PUSH socket to tcp://localhost:5558
                // Sends results to Sink via that socket
                Console.WriteLine("====== WORKER ======");

                using (var receiver = new PullSocket(">tcp://localhost:5557"))
                using (var sender = new PushSocket(">tcp://localhost:5558"))
                {
                    //process tasks forever
                    while (true)
                    {
                        //workload from the vetilator is a simple delay
                        //to simulate some work being done, see
                        //Ventilator.csproj Proram.cs for the workload sent
                        //In real life some more meaningful work would be done
                        string workload = receiver.ReceiveFrameString();

                        //simulate some work being done
                        Thread.Sleep(int.Parse(workload));

                        //send results to sink, sink just needs to know worker
                        //is done, message content is not important, just the presence of
                        //a message means worker is done.
                        //See Sink.csproj Proram.cs
                        Console.WriteLine("Sending to Sink");
                        sender.SendFrame(string.Empty);
                    }
                }
            }
        }
    }

## Sink

    ::csharp
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NetMQ;

    namespace Sink
    {
        public class Program
        {
            public static void Main(string[] args)
            {
                // Task Sink
                // Bindd PULL socket to tcp://localhost:5558
                // Collects results from workers via that socket
                Console.WriteLine("====== SINK ======");

                //socket to receive messages on
                using (var receiver = new PullSocket("@tcp://localhost:5558"))
                {
                    //wait for start of batch (see Ventilator.csproj Program.cs)
                    var startOfBatchTrigger = receiver.ReceiveFrameString();
                    Console.WriteLine("Seen start of batch");

                    //Start our clock now
                    var watch = Stopwatch.StartNew();

                    for (int taskNumber = 0; taskNumber < 100; taskNumber++)
                    {
                        var workerDoneTrigger = receiver.ReceiveFrameString();
                        if (taskNumber % 10 == 0)
                        {
                            Console.Write(":");
                        }
                        else
                        {
                            Console.Write(".");
                        }
                    }
                    watch.Stop();
                    //Calculate and report duration of batch
                    Console.WriteLine();
                    Console.WriteLine("Total elapsed time {0} msec", watch.ElapsedMilliseconds);
                    Console.ReadLine();
                }
            }
        }
    }

## Running the sample

要執行這個，這三個批次檔會很有用，若你選擇將此程式碼複製到新方案中，你需要更改路徑以符合。

### Run1Worker.bat

    :::text
    cd Ventilator/bin/Debug
    start Ventilator.exe
    cd../../..
    cd Sink/bin/Debug
    start Sink.exe
    cd../../..
    cd Worker/bin/Debug
    start Worker.exe


在這個Sink的Process執行後，應該會在Console有如下的輸出：（顯然你的PC可能運行比我的更快/更慢）：

    :::text
    ====== SINK ======
    Seen start of batch
    :.........:.........:.........:.........:.........:.........:.........:.........
    :.........:.........
    Total elapsed time 5695 msec


### Run2Workers.bat

    :::text
    cd Ventilator/bin/Debug
    start Ventilator.exe
    cd../../..
    cd Sink/bin/Debug
    start Sink.exe
    cd../../..
    cd Worker/bin/Debug
    start Worker.exe
    start Worker.exe

在這個Sink的Process執行後，應該會在Console有如下的輸出：（顯然你的PC可能運行比我的更快/更慢）：

    :::text
    ====== SINK ======
    Seen start of batch
    :.........:.........:.........:.........:.........:.........:.........:.........
    :.........:.........
    Total elapsed time 2959 msec


### Run4Workers.bat

    :::text
    cd Ventilator/bin/Debug
    start Ventilator.exe
    cd../../..
    cd Sink/bin/Debug
    start Sink.exe
    cd../../..
    cd Worker/bin/Debug
    start Worker.exe
    start Worker.exe
    start Worker.exe
    start Worker.exe


在這個Sink的Process執行後，應該會在Console有如下的輸出：（顯然你的PC可能運行比我的更快/更慢）：

    :::text
    ====== SINK ======
    Seen start of batch
    :.........:.........:.........:.........:.........:.........:.........:.........
    :.........:.........
    Total elapsed time 1492 msec

這個模式有幾個要注意的重點：

+ `Ventilator`使用`NetMQ`中的`PushSocket`以將工作發佈至`Worker`，這也稱為負載平衡。
+ `Ventilator`和`Sink`是系統中固定的部份，而`Worker`是動態的，添加更多`Worker`的是很簡單的事，我們可以啟動一個新的`Worker`實體，在理論上，工作會更快完成（越多`Worker`越快）。
+ 我們要同步啟動批次檔（當`Worker`準備好時），如果沒有，最先連線的`Worker`會比其它的取得更多的訊息，那就不夠負載平衡了。
+ `Sink`使用`NetMQ`的`PullSocket`去累積`Worker`的產出。