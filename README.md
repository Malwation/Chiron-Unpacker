# Chiron Unpacker

> [!CAUTION]
> Since the application to be unpacked is run directly, **it must be run in a secure environment (VM)**.


## About

**Chiron Unpacker** is an Unpacker for Packers that uses the `Assembly.Load(byte[] rawAssembly)` function. Chiron Unpacker creates a special AppDomain and handles the Assembly.Load calls in this AppDomain. This allows us to handle all executable .NET applications loaded into memory after they are loaded.

## Usage

[Latest Release]()

```
Chiron Unpacker by Malwation
Chiron-Unpacker 1.0.0.0
Copyright c 2024

    -f, --file        Required. Select the file to unpack

    -o, --output      Required. Select the location to save the dumped files

    -r, --resource    (Default: false) Use ResourceUnpack feature

    --help            Display this help screen.

    --version         Display version information.
```

Example command:

```
Chiron-Unpacker.exe -f sample.exe -o .\output_folder\ -r
```

## How It Works

It saves executable .NET applications loaded into memory by opening a special AppDomain where `Assembly.Load` events are controlled. When it is run on the packed application sample using the ResourceUnpack feature, the following operations are performed respectively:

1. Creates a custom AppDomain that controls Assembly.Load events.
2. Controls ProcessExit events in the main AppDomain (at this stage, if the ResourceUnpack feature is activated, the next stage is started).
3. Runs the given file inside the created custom AppDomain.

![Unpacker Scheme](images/unpacker.png)

## Unpacking Example

![Unpacker Video](images/ChironUnpacker.gif)


## Third Party Libraries

| Library                                                         | License |
| --------------------------------------------------------------- | ------- |
| [CommandLine](https://github.com/commandlineparser/commandline) | MIT     |
| [dnlib](https://github.com/0xd4d/dnlib)                         | MIT     |

