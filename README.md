NetMQ
=====

NetMQ is port of zeromq to .net.

The project based on zeromq version 3.2, the project is still under development and not stable yet.

The following is still under development:
* Error handling
* High level API
* Support clrzmq API
* Testing and port the testing from the original project
* Check performance
* Check compatibility to original zeromq
* PGM protocol
* TCP Keep alive
* Proxy is still not supported
* Mono support
* IPv6

Bonus features planned to be developed:
* IO completion ports
* SSL
* New PUB/SUB sockets that the server decide on the subscriptions of the client (to support permissions on topics).

## Contribution Process

This project uses the [C4 process](http://rfc.zeromq.org/spec:16) for all code changes. "Everyone,
without distinction or discrimination, SHALL have an equal right to become a Contributor under the
terms of this contract."

## Who owns NetMQ?

NetMQ is owned by all its authors and contributors. 
This is an open source project licensed under the LGPLv3. 
To contribute to NetMQ please read the [C4 process](http://rfc.zeromq.org/spec:16), it's what we use.