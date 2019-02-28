# C# Minifier

CSharpMinifier is a filter that removes comments and unnecessary whitespace
from _valid_ C# source code in order to arrive at a compressed form without
changing the behaviour of the code. Unlike JavaScript minifiers, the purpose
is not to reduce the download size or parsing effort. Instead, it is useful
for computing hashes or digests for the purpose of detecting _useful
changes_.

It is available as a .NET Standard Library and as a .NET Core console
application that can be installed as a [global tool].

It is a _minifier_ but not an _obfuscator_ or an _uglifier_; that is,
private details like local variable names are not changed.

Before:

```
using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("Hello, world!");
    }
}
```

After:

```
using System;class Program{static void Main(){Console.WriteLine("Hello, world!");}}
```

A monitor that actively watches source files for changes and triggers a
re-compilation of binaries would be one application that could benefit from
minification of sources. The monitor could compute a hash from the minified
sources and only trigger a re-compilation if the hashes of the minified
version are no longer the same (instead of whenever a physical change
occurs). Minification removes C# comments and extraneous whitespace in the
source as they do not constitute a _useful change_ that could affect
behaviour of the code at run-time.


## Building

The .NET Core SDK is the minimum requirement.

To build just the binaries on Windows, run:

    .\build.cmd

On Linux or macOS, run instead:

    ./build.sh

To build the binaries and the NuGet packages on Windows, run:

    .\pack.cmd

On Linux or macOS, run instead:

    ./pack.sh


## Testing

To exercise the unit tests on Windows, run:

    .\test.cmd

On Linux or macOS, run:

    ./test.sh

This will also build the binaries if necessary.


[global tool]: https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools
