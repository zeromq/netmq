Beacon
======

`NetMQBeacon` implements a peer-to-peer discovery service for local networks.

A beacon can broadcast and/or capture service announcements using UDP messages on the local area network.
You can define the format of your outgoing beacons, and set a filter that validates incoming beacons.
Beacons are sent and received asynchronously in the background.

We can use the `NetMQBeacon` to discover and connect to other NetMQ/CZMQ services in the network automatically
without central configuration. Please note that to use `NetMQBeacon` your infrastructure must support broadcast.
Most cloud providers doesn't support broadcast.

This implementation uses IPv4 UDP broadcasts, and is a port of [zbeacon from czmq](https://github.com/zeromq/czmq#toc4-425)
with some extensions, though it maintains network compatibility.

## Example: Implementing a Bus

`NetMQBeacon` can be used to create a simple bus that allows a set of nodes
to discover one another, configured only via a shared port number.

* Each bus node binds a subscriber socket and connects to other nodes with a publisher socket.
* Each node will use `NetMQBeacon` to announce its existence and to discover other nodes. We will also use `NetMQActor` to implement our node.

The source for sample project is available online at:

* [https://github.com/NetMQ/Samples/blob/master/src/Beacon/BeaconDemo/Bus.cs](https://github.com/NetMQ/Samples/blob/master/src/Beacon/BeaconDemo/Bus.cs)


## Further reading

* [Solving the Discovery Problem](http://hintjens.com/blog:32)
