Beacon
======
`NetMQBeacon`實作了在區域網路中點對點的discovery服務。

一個`beacon`可在區域網路中透過UDP做擴播或捕捉service announcements，你可以定義廣播出去的beacon，也可以設定過濾器以過濾接收到的beacons。Beacons會在背景非同步的執行傳送及接收的動作。

我們可以使用`NetMQBeacon`自動地在網路中尋找及連線至其它`NetMQ/CZMQ`的服務而不需要一個中央的設定。請注意若要使用`NetMQBeacon`在你的架構中需要支援廣播(broadcast)服務。而目前大部份的服端服務商並不支援。

這個實作使用IPv4 UDP廣播，屬於[zbeacon from czmq](https://github.com/zeromq/czmq#toc4-425)並加上維護網路相容性的擴充函式。

## Example: Implementing a Bus
`NetMQBeacon`可以用來建立簡單的bus系統，讓一組節點僅需透過一個共享的埠號即可找到其它的節點。

- 每個bus的節點綁定至一個subscriber socket且靠publisher socket連線至其它節點。
- 每個節點會透過`NetMQBeacon`去公告它的存在及尋找其它節點，我們將使用`NetMQActor`來實作我們的節點。

範例在此：

[bus.cs](https://github.com/NetMQ/Samples/blob/master/src/Beacon/BeaconDemo/Bus.cs)
(原文連結有誤，此處已修改)

## Further reading

* [Solving the Discovery Problem](http://hintjens.com/blog:32)
