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


## Important note on backward compatibility 

Since version 3.3.07 NetMQ changed the number serialization from Little Endian to Big Endian to be compatible with ZeroMQ.
Any NetMQ version prior to 3.3.0.7 is not compatible with the new version. To support older versions you can set Endian option on a NetMQ socket to Little Endian,
however doing so will make it incompatible with ZeroMQ.

We recommend to update to the latest version and use Big Endian which is now the default behavior.

## Installation

You can find NetMQ in [nuget](https://nuget.org/packages/NetMQ/).

## Using

Before using NetMQ, make sure to read the [ZeroMQ Guide](http://zguide.zeromq.org/page:all). You can also read more about NetMQ at [Somdorons blog](http://somdoron.com/category/netmq/).

NetMQ documentation is still work in progress, but you can find a small example [here](https://gist.github.com/somdoron/5175967).

	using (NetMQContext ctx = NetMQContext.Create())
	{
		using (var server = ctx.CreateResponseSocket())
		{
			server.Bind("tcp://127.0.0.1:5556");
			using (var client = ctx.CreateRequestSocket())
			{
				client.Connect("tcp://127.0.0.1:5556");
				client.Send("Hello"); 

				string m1 = server.ReceiveString();
 				Console.WriteLine("From Client: {0}", m1);
 				server.Send("Hi Back");

				string m2 = client.ReceiveString();
				Console.WriteLine("From Server: {0}", m2);
				Console.ReadLine();
			}
		}
	}

## Contributing

We need help, so if you have good knowledge of C# and ZeroMQ just grab one of the issues and add a pull request.
We are using [C4 process](http://rfc.zeromq.org/spec:16), so make sure you read this before.

Some of the areas we need help with:

* Testing with libzmq (original zeromq library) in version 2.2 and 3.2.
* Porting tests from ZeroMQ to c#.
* Documenting the High Level API using C# style comments
* Testing IPv6
* Compiling on Mono and run on linux
* Creating another High Level API which is the same as CLRZMQ

You can also help us by:

* Joining our [mailing list](https://groups.google.com/d/forum/netmq-dev?hl=en) and be an active member
* Writing tutorials in the github wiki
* Writing about the project in your blog

## Mailing list

You can join our mailing list [here](https://groups.google.com/d/forum/netmq-dev?hl=en). 

## Who owns NetMQ?

NetMQ is owned by all its authors and contributors. 
This is an open source project licensed under the LGPLv3. 
To contribute to NetMQ please read the [C4 process](http://rfc.zeromq.org/spec:16), it's what we use.
There are open issues in the issues tab that still need to be taken care of, feel free to pick one up and submit a patch to the project.

## Build Server

[TeamCity at CodeBetter](http://teamcity.codebetter.com/project.html?projectId=project372&tab=projectOverview)

![Code Better](http://www.jetbrains.com/img/banners/Codebetter300x250.png)

[YouTrack by JetBrains - keyboard-centric bug tracker](http://www.jetbrains.com/youtrack)

[TeamCity by JetBrains - continuous integration server](http://www.jetbrains.com/teamcity)

