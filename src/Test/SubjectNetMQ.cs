using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NUnit.Framework;
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable InvertIf
#pragma warning disable 649

namespace Test
{
	/// <summary>
	///	Intent: Pub/sub across different processes.
	/// </summary>
	/// <threadSafe>Yes</threadSafe>
	public class SubjectNetMQ<T> : IObservable<T>, IObserver<T>, IDisposable
	{
		private CancellationTokenSource _cancellationTokenSource;
		public string QueueName { get; private set; }
		private readonly List<IObserver<T>> _subscribers = new List<IObserver<T>>();
		private readonly object _subscribersLock = new object();

		public string ZeroMqAddress { get; set; } = null;

		public SubjectNetMQ(string zeroMqAddress, string queueName = "default", CancellationTokenSource cancellationTokenSource = default(CancellationTokenSource))
		{
			_cancellationTokenSource = cancellationTokenSource;
			QueueName = queueName;
			if (string.IsNullOrEmpty(Thread.CurrentThread.Name) == true)
			{
				// Cannot set the thread name twice.
				Thread.CurrentThread.Name = queueName;
			}

			ZeroMqAddress = zeroMqAddress;
			if (string.IsNullOrEmpty(zeroMqAddress))
			{
				ZeroMqAddress = ConfigurationManager.AppSettings["ZeroMqAddress"];
			}
			if (string.IsNullOrEmpty(ZeroMqAddress))
			{
				throw new Exception("Error E26624. Must define the address for ZeroMQ.");
			}
		}

		#region Initialize publisher on demand.
		private PublisherSocket _publisherSocket;
		private volatile bool _initializePublisherDone = false;
		private readonly object _initializePublisherLock = new object();
		ManualResetEvent publisherReadySignal = new ManualResetEvent(false);
		private void InitializePublisherOnFirstUse()
		{
			if (_initializePublisherDone == false) // Double checked locking.
			{
				lock (_initializePublisherLock)
				{
					if (_initializePublisherDone == false)
					{
						Console.WriteLine($"Publisher socket binding to: {ZeroMqAddress}");
						_publisherSocket = new PublisherSocket();

						NetMQMonitor monitor;
						{
							// Must ensure that we have a unique monitor name for every instance of this class.
							monitor = new NetMQMonitor(_publisherSocket, $"inproc://#PublisherInterProcess#{this.QueueName}",
								SocketEvents.Accepted | SocketEvents.Listening
								//SocketEvents.All
								);
							monitor.Accepted += Monitor_Accepted;
							monitor.Listening += Monitor_Listening;
							//monitor.EventReceived += Monitor_EventReceived;
							monitor.StartAsync();
						}


						_publisherSocket.Options.SendHighWatermark = 1000;
						_publisherSocket.Bind(this.ZeroMqAddress);

						// Corner case: wait until the publisher is properly set up. If we omit this code, then we will
						// get intermittent failures if we start the subscriber first, then the publisher.
						{
							Stopwatch sw = Stopwatch.StartNew();
							publisherReadySignal.WaitOne(TimeSpan.FromMilliseconds(3000));
							Console.Write($"Publisher: Waited {sw.ElapsedMilliseconds} ms for binding.\n");
						}
						Thread.Sleep(100); // Otherwise, the first item we publish may get missed by the subscriber.

						// Stop monitor.
						{
							//monitor.Accepted -= MonitorOnAccepted;
							//monitor.Stop();
							//monitor.Dispose();
						}

						_initializePublisherDone = true;
					}
				}
			}
		}

		private void Monitor_Listening(object sender, NetMQMonitorSocketEventArgs e)
		{
			Console.Write($"Publisher event: {e.SocketEvent}\n");
			publisherReadySignal.Set();
		}

		/*private void Monitor_EventReceived(object sender, NetMQMonitorEventArgs e)
		{
			Console.Write($"Publisher event: {e.SocketEvent}\n");
		}*/

		private void Monitor_Accepted(object sender, NetMQMonitorSocketEventArgs e)
		{
			Console.Write($"Publisher event: {e.SocketEvent}\n");
			publisherReadySignal.Set();
		}
		#endregion

		#region Initialize subscriber on demand.
		private SubscriberSocket _subscriberSocket;
		private volatile bool _initializeSubscriberDone = false;
		private Thread _thread;

		ManualResetEvent subscriberReadySignal = new ManualResetEvent(false);

		private void InitializeSubscriberOnFirstUse()
		{
			if (_initializeSubscriberDone == false) // Double checked locking.
			{
				lock (_subscribersLock)
				{
					if (_initializeSubscriberDone == false)
					{

						Console.WriteLine($"Subscriber socket connecting to: {ZeroMqAddress}");
						_subscriberSocket = new SubscriberSocket();

						//_subscriberSocket.Monitor("", SocketEvents.AcceptFailed);

						// Corner case: part of code to wait until socket is ready (see matching code below that
						// consumes "subscriberReadySignal").

						NetMQMonitor monitor;
						{
							// Must ensure that we have a unique monitor name for every instance of this class.
							monitor = new NetMQMonitor(_subscriberSocket, $"inproc://#SubscriberInterProcess#{this.QueueName}",
								//ownsSocket//false);
								SocketEvents.ConnectRetried | SocketEvents.Connected);


							//monitor.Timeout = TimeSpan.FromMilliseconds(500);
							//PollEvents flags = _subscriberSocket.Poll(PollEvents.PollError, TimeSpan.FromMilliseconds(500));

							monitor.ConnectRetried += Monitor_ConnectRetried; /*(sender, args) =>
							{
								Console.Write($"Subscriber event: {args.SocketEvent}\n");
								subscriberReadySignal.Set();
							};*/
							monitor.Connected += Monitor_Connected; /*(sender, args) =>
							{
								Console.Write($"Subscriber event: {args.SocketEvent}\n");
								subscriberReadySignal.Set();
							};*/

							monitor.StartAsync();
						}

						/*var poller = new Poller();
						poller.PollTimeout = 500;
						poller.AddSocket(_subscriberSocket);
						{
							// these event will be raised by the Poller
							_subscriberSocket.ReceiveReady += (s, a) =>
							{
								Console.Write("Subscriber: ReceiveReady: Event!");
							};
							
							// start polling (on this thread)
						}
						poller.PollTillCancelledNonBlocking();*/

						_subscriberSocket.Options.ReceiveHighWatermark = 1000;
						_subscriberSocket.Connect(this.ZeroMqAddress);
						_subscriberSocket.Subscribe(this.QueueName);

						//_subscriberSocket.Monitor(this.ZeroMqAddress, SocketEvents.All);

						if (_cancellationTokenSource == null)
						{
							_cancellationTokenSource = new CancellationTokenSource();
						}

						ManualResetEvent threadReadySignal = new ManualResetEvent(false);

						_thread = new Thread(() =>
						{
							try
							{
								Console.Write($"Thread initialized.\n");
								threadReadySignal.Set();
								while (_cancellationTokenSource.IsCancellationRequested == false)
								{
									string messageTopicReceived = _subscriberSocket.ReceiveFrameString();
									if (messageTopicReceived != QueueName)
									{
										throw new Exception($"Error E65724. We should always subscribe on the queue name '{QueueName}', instead we got '{messageTopicReceived}'.");
									}
									var type = _subscriberSocket.ReceiveFrameString();
									switch (type)
									{
										// Originated from "OnNext".
										case "N":
											T messageReceived = _subscriberSocket.ReceiveFrameBytes().ProtoBufDeserialize<T>();
											lock (_subscribersLock)
											{
												_subscribers.ForEach(o => o.OnNext(messageReceived));
											}
											break;
										// Originated from "OnCompleted".
										case "C":
											lock (_subscribersLock)
											{
												_subscribers.ForEach(o => o.OnCompleted());
											}
											break;
										// Originated from "OnException".
										case "E":
											Exception ex = _subscriberSocket.ReceiveFrameBytes().ProtoBufDeserialize<Exception>();
											lock (_subscribersLock)
											{
												_subscribers.ForEach(o => o.OnError(ex));
											}
											break;
										// Originated from a "Ping" request.
										case "P":
											// Do nothing, this is a ping command used to wait until sockets are initialized properly.
											Console.Write("Received ping.\n");
											break;
										default:
											throw new Exception($"Error E28734. Something is wrong - received '{type}' when we expected \"N\", \"C\" or \"E\" - are we out of sync?");
									}
								}
							}
							catch (Exception ex)
							{
								Console.Write($"Error E23844. Exception in threadName \"{QueueName}\". Thread exiting. Exception: \"{ex.Message}\".\n");
								lock (_subscribersLock)
								{
									this._subscribers.ForEach((ob) => ob.OnError(ex));
								}
							}
							lock (_subscribersLock)
							{
								_subscribers.Clear();
							}
							_cancellationTokenSource.Dispose();
						})
						{
							Name = this.QueueName,
							IsBackground = true // Have to set it to background, or else it will not exit when the program exits.
						};
						_thread.Start();

						// Wait for thread to properly spin up.
						threadReadySignal.WaitOne(TimeSpan.FromMilliseconds(3000));

						// Corner case: wait until we have connected to the publisher, *or* (if there is no publisher
						// started yet) we have started a retry. If we omit this code, then we will get intermittent
						// failures if we start the subscriber first, then the publisher.
						{
							Stopwatch sw = Stopwatch.StartNew();
							subscriberReadySignal.WaitOne(TimeSpan.FromMilliseconds(3000));
							Console.Write($"Subscriber: Waited {sw.ElapsedMilliseconds} ms for connection.\n");


							monitor.ConnectRetried -= Monitor_ConnectRetried;
							monitor.Connected -= Monitor_Connected;
							//monitor.Stop();
							//monitor -= Monitor_Connected;
							//monitor -= Monitor_Connected;

							//monitor.Stop();
							//Thread.Sleep(TimeSpan.FromMilliseconds(1000));
							//monitor.Dispose();
							//monitor.DetachFromPoller();
							//monitor.Stop();
							//monitor.Dispose();
						}

						Console.Write("Subscriber: finished setup.\n");

						Thread.Sleep(100); // Otherwise, the first item we subsmay get missed by the subscriber.

						_initializeSubscriberDone = true;
					}
				}
			}

		}

		private void Monitor_Connected(object sender, NetMQMonitorSocketEventArgs e)
		{
			Console.Write($"Subscriber event: {e.SocketEvent}\n");
			subscriberReadySignal.Set();
		}

		private void Monitor_ConnectRetried(object sender, NetMQMonitorIntervalEventArgs e)
		{
			Console.Write($"Subscriber event: {e.SocketEvent}\n");
			subscriberReadySignal.Set();
		}
		#endregion

		public IDisposable Subscribe(IObserver<T> observer)
		{
			lock (_subscribersLock)
			{
				this._subscribers.Add(observer);
			}

			InitializeSubscriberOnFirstUse();

			// Could return ".this", but this would introduce an issue: if one subscriber unsubscribed, it would
			// unsubscribe all subscribers.
			return new AnonymousDisposable(() =>
			{
				lock (_subscribersLock)
				{
					this._subscribers.Remove(observer);
				}
			});
		}

		public void OnNext(T message)
		{
			try
			{
				InitializePublisherOnFirstUse();

				// Publish message using ZeroMQ as the transport mechanism.
				_publisherSocket.SendMoreFrame(QueueName)
					.SendMoreFrame("N") // "N", "E" or "C" for "OnNext", "OnError" or "OnCompleted".
					.SendFrame(message.ProtoBufSerialize<T>());

				// Comment in the remaining code for the standard pub/sub pattern.

				//if (this.HasObservers == false)
				//{
				//throw new QxNoSubscribers("Error E23444. As there are no subscribers to this publisher, this event will be lost.");
				//}

				//lock (_subscribersLock)
				//{
				//this._subscribers.ForEach(msg => msg.OnNext(message));
				//}
			}
			catch (Exception ex)
			{
				var col = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Exception: {ex.Message}");
				Console.ForegroundColor = col;
				this.OnError(ex);
				throw;
			}
		}

		public void OnError(Exception ex)
		{
			InitializePublisherOnFirstUse();

			_publisherSocket.SendMoreFrame(QueueName)
					.SendMoreFrame("E") // "N", "E" or "C" for "OnNext", "OnError" or "OnCompleted".
					.SendFrame(ex.ProtoBufSerialize<Exception>());

			// Comment in the remaining code for the standard pub/sub pattern.

			//if (this.HasObservers == false)
			//{
			//throw new QxNoSubscribers("Error E28244. As there are no subscribers to this publisher, this published exception will be lost.");
			//}

			//lock (_subscribersLock)
			//{
			//this._subscribers.ForEach(msg => msg.OnError(ex));
			//}
		}

		public void OnCompleted()
		{
			InitializePublisherOnFirstUse();

			_publisherSocket.SendMoreFrame(QueueName)
				.SendFrame("C"); // "N", "E" or "C" for "OnNext", "OnError" or "OnCompleted".

			this.Dispose();
		}

		public void Dispose()
		{
			lock (_subscribersLock)
			{
				_subscribers.Clear();
			}
			_cancellationTokenSource.Cancel();

			// Wait until the thread has exited.
			bool threadExitedProperly = _thread.Join(TimeSpan.FromSeconds(30));
			if (threadExitedProperly == false)
			{
				throw new Exception("Error E62724. Thread did not exit when requested.");
			}
		}

		/// <summary>
		/// Intent: True if there are any subscribers registered.
		/// </summary>
		public bool HasObservers
		{
			get
			{
				lock (_subscribersLock)
				{
					return this._subscribers.Count > 0;
				}
			}
		}
	}

	[TestFixture]
	public class UnitTest
	{

		[Test]
		public void PubSubShouldNotCrashIfNoThreadSleep()
		{
			using (var pub = new PublisherSocket())
			{
				using (var sub = new SubscriberSocket())
				{
					int port = pub.BindRandomPort("tcp://127.0.0.1");
					sub.Connect("tcp://127.0.0.1:" + port);

					sub.Subscribe("*");

					Stopwatch sw = Stopwatch.StartNew();
					{
						for (int i = 0; i < 50; i++)
						{
							pub.SendFrame("*"); // Ping.

							Console.Write("*");
							string topic;
							var gotTopic = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out topic);
							string ping;
							var gotPing = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out ping);
							if (gotTopic == true)
							{
								Console.Write("\n");
								break;
							}
						}
					}
					Console.WriteLine($"Connected in {sw.ElapsedMilliseconds} ms.");
				}
			}
		}
	}
}

