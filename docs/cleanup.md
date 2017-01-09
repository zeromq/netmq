Cleanup
=======

NetMQ第4版中我們拿掉了NetMQContext，現在我們可以用新的運算子建立sockets了，雖然這讓函式庫使用上較簡單，但也增加了一些需要清除的複雜性。

# 為什麼NetMQ需要清除

NetMQ在背景建立了一些執行緒。因此，當你在Socket上呼叫Dispose時，這個處理是非同步的且發生在背景執行緒中。而因為NetMQ的執行緒是屬於背景執行緒，所以你實際上可以不正確清除並離開程式，但不建議。

當離開AppDomain時會更複雜，所以你需要清除NetMQ。

# What is Linger?

Linger是socket在被dispose時傳送當下尚未傳送所有訊息的允許時間。所以當我們在一個Linger設為1秒的socket上呼叫Dispose時，它會最多花費一秒直到socket被disposed，此時函式庫會試著傳送所有等待中的訊息，如果它在linger時間到達前傳送完成，此socket會馬上被disposed。

正如所說，這一切發生在背景中，所以若linger有被設置，但我們沒有正確清除函式庫，linger會被略過。如果linger對你很重要，要確保你正確的清除函式庫。

第四版中預設的Linger值是零，表示函式庫不會在dispose前等待。你可以變更單一socket的linger值，也可以透過NetMQConfig.Linger設定所有linger的值。

# How to Cleanup?

關於cleanup最重要的是你要在呼叫Cleanup前呼叫所有socket的Dispose，也要確認NetMQ函式庫中的其它資源如NetMQPoller、NetMQQueue等被正確cleanup，如果socket沒有被disposed，那NetMQConfig.Cleanup會永遠阻塞。

最後你需要呼叫NetMQConfig.Cleanup，你可以如下所示的方式：

    :::csharp
	static void Main(string[] args)
	{
	    try
	    {
	        // Do you logic here
	    }
	    finally
	    {
	        NetMQConfig.Cleanup();
	    }
	}

如果你很懶惰，不關心清理函式庫，你也可以呼叫NetMQConfig.Cleanup並將block參數設為false。當設為false時，cleanup不會等待Sockets發送所有訊息，並且只會kill背景執行緒。

# Tests

若你在你的測試中使用NetMQ，你也要確認你正確的對函式庫做cleanup。

這邊建議可加一個全域的tear down在你的測試中，並呼叫NetMQConfig.Cleanup。

示範若是在NUnit中可以：

    :::csharp
    [SetUpFixture]
    public class Setup
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            NetMQConfig.Cleanup(false);
        }
    }

在測試中，呼叫Cleanup並代入false可讓你在測試失敗時不讓程式中斷。
