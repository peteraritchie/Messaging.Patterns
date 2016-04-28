Messaging.Patterns
==================
[![Build status](https://ci.appveyor.com/api/projects/status/gdkvga7qylhs8jue?svg=true)](https://ci.appveyor.com/project/peteraritchie/messaging-patterns) [![NuGet](https://img.shields.io/nuget/v/Nuget.Core.svg?maxAge=2592000)](https://www.nuget.org/packages/PRI.Messaging.Patterns/)

Messaging.Patterns is a library that contains patterns for implementing message-oriented systems.  The patterns are implemetations from [Messaging.Primitives](https://github.com/peteraritchie/Messaging.Primitives)

##Bus
A Bus is an simple implementation of [IBus](https://github.com/peteraritchie/Messaging.Primitives/blob/master/PRI.Messaging.Primitives/IBus.cs).  This class currently facilitates chaining message handlers or or consumers (implementations of [IConsumer](https://github.com/peteraritchie/Messaging.Primitives/blob/master/PRI.Messaging.Primitives/IConsumer.cs).
This bus provides the ability to automatically find and chain together handlers by providing a directory, wildcard and namespace specifier with the [AddHandlersAndTranslators](https://github.com/peteraritchie/Messaging.Patterns/blob/master/PRI.Messaging.Patterns/Extensions/Bus/BusExtensions.cs#L28) extension method.
A handler is an IConsumer implementation and a translator is an IPipe implementation and IPipes are also consumers.  As pipes are encountered they are connected to consumers of the pipes outgoing type.  So, when the bus is given a message to handle, the message is broadcast to all consumers; much like a [publish-subscribe channel](http://www.enterpriseintegrationpatterns.com/patterns/messaging/PublishSubscribeChannel.html).  If a consumer is a pipe, the pipe processes the message then sends it to another consumer.  If there is only one consumer of the message type to be handled by the bus, it will not broadcast but send to the one and only handler; like a [point-to-point channel](http://www.enterpriseintegrationpatterns.com/patterns/messaging/PointToPointChannel.html). 
##`ActionConsumer<TMessage>`
From [Primitives](https://github.com/peteraritchie/Messaging.Primitives), `IConsumer<TMessage>` provides an interface to implement and pass-around message handlers.  But sometimes creating a new type to implement `IConsumer</TMessage>` may not make any sense.  `ActionConsumer<TMessage>` is an  implementation that lets you pass in a delegate or anonymous method that will handle the message.  For example, if you had a `MoveClientCommand` message that you needed to handle, you could add a handler to a bus like this:
```C#
    bus.AddHandler(new ActionConsumer<MoveClientCommand>(message => {
        var client = clientRepository.Get(message.ClientId);
        client.ChangeAddress(message.NewAddress);
    }));
```

##`ActionPipe<TMessageIn, TMessageOut>`
Along the same vane as `ActionConsumer<TMessage>`, from [Primitives](https://github.com/peteraritchie/Messaging.Primitives), `IPipe<TMessageIn, TMessageOut>` provides an interface to implement and pass around a message translator or pipe.  Sometimes creating a new type to implement `IPipe<TMessageIn, TMessageOut>` is not the right thing to do.  `ActionPipe<TMessageIn, TMessageOut>` provides an `IPipe<TMessageIn, TMessageOut>` implementation where a translation method or anonymous method can be provided to perform the translation.  For example:
```C#
    bus.AddTranslator(new ActionPipe<MoveClientCommand,
        ChangeClientAddressCommand>(m=>new ChangeClientAddressCommand
            {
                CorrelationId = m.CorrelationId,
                NewAddress = m.NewAddress
            }
        ));
```
