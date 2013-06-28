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


## Important Issue

From version 3.3.07 NetMQ change the number serialization from Little Endian to Big Endian to be compitable with ZeroMQ.
Any version prior to 3.3.0.7 are not compitable with the new version, to support older version you can set Endian option on a socket to Little,
however changing the option to little will make it incompitable with ZeroMQ.

We recommend to update to latest version and use Big Endian (the default behavior).

## Installation

You can find NetMQ in [nuget](https://nuget.org/packages/NetMQ/).

## Using

For using NetMQ make sure you read the [ZeroMQ Guide](http://zguide.zeromq.org/page:all). You can also read more about NetMQ at my [blog](http://somdoron.com/category/netmq/).

NetMQ documentation is still work in progress but you can found small example [here](https://gist.github.com/somdoron/5175967).

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
* Write about the project in your blog

## Mailing list

You can join our mailing list at https://groups.google.com/d/forum/netmq-dev?hl=en. 

## Who owns NetMQ?

NetMQ is owned by all its authors and contributors. 
This is an open source project licensed under the LGPLv3. 
To contribute to NetMQ please read the [C4 process](http://rfc.zeromq.org/spec:16), it's what we use.
Their are open issues in the issues tab that still need to take care of, feel free to pick one up and submit a patch to the project.

<div style="background: #00578e url('http://www.jetbrains.com/img/banners/Codebetter.png') no-repeat 0 50%; margin:0;padding:0;text-decoration:none;text-indent:0;letter-spacing:-0.001em; width:728px; height:90px"> <a href="http://www.jetbrains.com/youtrack" title="YouTrack by JetBrains" style="margin: 60px 0 0 190px;padding: 0; float: left;font-size: 14px; background-image:none;border:0;color: #acc4f9; font-family: trebuchet ms,arial,sans-serif;font-weight: normal;text-align:left;">keyboard-centric bug tracker</a> <a href="http://www.jetbrains.com/teamcity" tit                  le="TeamCity by J etBrains" style="margin:0 0 0 400px;padding:60px 0 2px 0;font-size:14px; background-image:none;border:0;display:block; color: #acc4f9; font-family: trebuchet ms,arial,sans-serif;font-weight: normal;text-align:left;">continuous integration server</a> </div>   
