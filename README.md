# Chiron Unpacker

> [!CAUTION]
> Since the application to be unpacked is run directly, it must be run in a secure environment (VM).


## About

**Chiron Unpacker** is an Unpacker for Packers that uses the `Assembly.Load(byte[] rawAssembly)` function.


## How It Works

It saves executable .NET applications loaded into memory by opening a special AppDomain where `Assembly.Load` events are controlled.

![Unpacker Scheme](images/unpacker.png)


## Third Party Libraries

....
