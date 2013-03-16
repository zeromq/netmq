NetMQ
=====

NetMQ is 100% native C# port of ZeroMQ.

NetMQ is lightweight messaging library which extends the
standard socket interfaces with features traditionally provided by
specialised messaging middleware products. NetMQ sockets provide an
abstraction of asynchronous message queues, multiple messaging patterns,
message filtering (subscriptions), seamless access to multiple transport
protocols and more.

NetMQ is still under development, although the current repository is pretty stable.

## Contributing

We need help, so if you have good knowledge in C# and ZeroMQ just grab one of the issues and add a pull request.
We are using [C4 process](http://rfc.zeromq.org/spec:16), so make sure you read this before.

Some of the areas we need help with:
* Testing with libzmq (original zeromq library) in version 2.2 and 3.2.
* Porting tests from ZeroMQ to c#.
* Document the High Level API using C# style comments
* Test IPv6
* Compile on Mono and run on linux
* Make another High Level API which is the same as CLRZMQ

You can also help us with the following
* Joining our mailing list and be an active member
* Write tutorials in the github wiki
* Write about the probject in your blog

## Installation

Binaries of NetMQ is still not available, so to install NetMQ just download the source code and compile the project.
We will add binaries and tutorial soon.

## Mailing list

you can join our mailing list at https://groups.google.com/d/forum/netmq-dev?hl=en. 

## Who owns NetMQ?

NetMQ is owned by all its authors and contributors. 
This is an open source project licensed under the LGPLv3. 
To contribute to NetMQ please read the [C4 process](http://rfc.zeromq.org/spec:16), it's what we use.
Their are open issues in the issues tab that still need to take care of, feel free to pick one up and submit a patch to the project.

