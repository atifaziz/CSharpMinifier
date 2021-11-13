# C# Minifier

CSharpMinifier filters comments and unnecessary whitespace from _valid_ C#
source code in order to arrive at a compressed form without changing the
behaviour of the code. Unlike JavaScript minifiers, the goal is not to reduce
the download size or parsing effort. Instead, it is best used for computing
hashes or digests for the purpose of detecting _potentially useful_ as opposed
to _any physical changes_.

It is [available as a .NET Standard Library][lib] as well as a .[NET Core
console application][app] that can be installed as a [global tool].

It is a _minifier_ but not an _obfuscator_ or an _uglifier_; that is,
private details like local variable names are not abbreviated.

Before minification:

```c#
// Author: John Doe <johndoe@example.com>

using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("Hello, world!");
    }
}
```

After minification:

```c#
using System;class Program{static void Main(){Console.WriteLine("Hello, world!");}}
```

A monitor that actively watches source files for changes and triggers a
re-compilation of binaries would be one application that could benefit from
minification of sources. The monitor could compute a hash from the minified
sources and only trigger a re-compilation if the hashes of the minified and
original versions no longer compare equal (instead of whenever a physical
change occurs). Minification removes C# comments and extraneous whitespace in
the source as they do not constitute a _useful change_ that could affect
behaviour of the code at run-time.

## Minification

CSharpMinifier assumes that the input C# source is syntactically sound;
otherwise the results are undefined (even if they may appear to resemble some
defined behaviour).

For the purpose of minification, especially that of whitespace and comments,
CSharpMinifier needs to ensure that it does not confuse, for example, a comment
appearing in a string or a commented-out string. Therefore it parses some
minimal grammar of a C# source, such as:

- Horizontal whitespace (space or tab)
- New-line sequences like `CR`, `LF` or `CRLF`
- Single-line comments, staring with `//` and until a new-line sequence or
  end of input
- Multi-line comments; that is, everything between `/*` and `*/`
- Pre-processor directives
- Strings literals of all sorts:
  - regular e,g, , e.g. `"..."`
  - verbatim, e.g. , e.g. `@"..."`
  - interpolated, e.g. `$"..."`
  - interpolated verbatim, e.g. `$@"..."`
    (or [`@$"..."` starting with C# 8][alt-ivs])

Everything surrounding or in-between the above is treated as raw and unparsed
_text_. As a consequence, the C# source does not have to be a full C# program
or library code. You can minify C# snippets like scripts and expressions as
long as they are syntactically valid.

All whitespace within the following type of lexical tokens is maintained:

- string literals
- pre-processor directive text

CSharpMinifier preserves all pre-processor directives except `#region` and
`#endregion`. These are filtered but content in-between is subject to
minification.

Horizontal (e.g. space and tab) and vertical (e.g. carriage-return and
line-feed) whitespace is eliminated in all cases except:

- a single horizontal space is maintained between words and some some obscure
  cases of operators (e.g. `x = i+++ +2`) to prevent minification from
  introducing ambiguities.
- a pre-processor directive; it must appear on a line of its own so it is
  succeeded by a new-line sequence.

CSharpMinifier provides offset, line and column information about all lexical
tokens it recognizes.

The scanner/parser is hand-written. It does not use Roslyn so it is extremely
lightweight to use and in processing. It is implemented as a simple state
machine and practically makes no heap allocations.

While CSharpMinifier does detect some syntactic errors, like unterminated
strings and comments, and reports them through raised exceptions, there should
be no expectation that such checks will be maintained in future versions.
After all, as stated earlier, the input C# source is expected to be
syntactically correct and all results otherwise are undefined.

The minification process can be customized through options to prevent the
following source code (single- or multi-line) comments from being subject
to minification:

- those matching a user-defined regular expression pattern
- those that appear at the start of the source (e.g. typically copyright,
  notices, terms and conditions)
- those that are marked important, either `/*! ... */` or `//! ...`

For C# syntax validation, consider using [CSharpSyntaxValidator][csval].


## Installation

To install the library for use in a project, do either:

    nuget install CSharpMinifier

or for projects based on .NET Core SDK:

    dotnet add package CSharpMinifier

See also the various installation instructions on [the library package
page on nuget.org][nupkg]

To install the command-line application as .NET Core global tool, do:

    dotnet tool install -g CSharpMinifierConsole


## Usage

Suppose the following C# source text is loaded in a string variable called
`source`:

```c#
// Author: John Doe <johndoe@example.com>

using System;

static class Program
{
    static void Main()
    {
        Console.WriteLine($"Today's date is {DateTime.Today:MMM dd, yyyy}.");
    }
}
```

To minify, do:

```c#
var minifiedSource = Minifier.Minify(source);
```

Alternatively, to analyse and visit the tokens identified by the scanner:

```c#
var tokens =
    from token in Scanner.Scan(source)
    where token.Kind != TokenKind.WhiteSpace
       && token.Kind != TokenKind.NewLine
    select token;

foreach (var token in tokens)
    Console.WriteLine($"{token.Kind}({token.Start.Line},{token.Start.Column}): {token.Substring(source)}");
```

The output from the above will be:

```
SingleLineComment(2,1): // Author: John Doe <johndoe@example.com>
Text(4,1): using
Text(4,7): System;
Text(6,1): static
Text(6,8): class
Text(6,14): Program
Text(7,1): {
Text(8,5): static
Text(8,12): void
Text(8,17): Main()
Text(9,5): {
Text(10,9): Console.WriteLine(
InterpolatedStringStart(10,27): $"Today's date is {
Text(10,46): DateTime.Today
InterpolatedStringEnd(10,60): :MMM dd, yyyy}."
Text(10,76): );
Text(11,5): }
Text(12,1): }
```

## Command-Line Usage

    csmin < example.cs > example.min.cs

For more information, run `csmin help` or see the [on-line
documentation][wiki].


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


[nupkg]: https://www.nuget.org/packages/CSharpMinifier/
[global tool]: https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools
[app]: https://www.nuget.org/packages/CSharpMinifierConsole/
[lib]: https://www.nuget.org/packages/CSharpMinifier/
[wiki]: https://github.com/atifaziz/CSharpMinifier/wiki
[csval]: https://www.nuget.org/packages/CSharpSyntaxValidator/
[alt-ivs]: https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#enhancement-of-interpolated-verbatim-strings
