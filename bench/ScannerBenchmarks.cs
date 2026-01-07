using BenchmarkDotNet.Attributes;
using CSharpMinifier;

namespace Benchmarks;

[MemoryDiagnoser]
public class ScannerBenchmarks
{
    [Benchmark]
    public void InterpolatedStrings()
    {
        foreach (var _ in Scanner.Scan("""
            // Deep nesting with all bracket types (maximizes stack depth and counter increments):

            var x = $"{(a[b[c[d]]][e])}{(f[(g)])}{{{h}}}";
            var y = $"{$"{$"{$"{deep}"}"}"}{a[b](c){d}}";

            // Wide interpolation (many expressions, lots of push/pop, format specifiers with `:`):

            var z = $"{a}{b}{c}{d}{e}{f}{g}{h}{i}{j}{k}{l}{m}{n}{o}{p}{q}{r}{s}{t}{u}{v}{w}{x}{y}{z}";
            var w = $"{a:d}{b:x}{c[(d > e ? f : g)]}{h,10:N2}{i[j[k[l[m]]]]}{(((((n)))))}{o ?? p ?? q}";

            // Verbatim interpolated with nested interpolations (tests $@"" and @$"" paths):

            var v = $@"{a[b]}{(c)}{$@"{d[e]}{(f)}{$@"{g}"}"}{{{h}}}";
            var u = @$"{x[(y > z ? $"{a}" : $@"{b}")]}{c}{d}{e}";

            // Combined benchmark string (one monster expression):

            _ = $"{a[b[c[d[e]]]]}{(f(g(h(i(j)))))}{$"{$"{$"{k[l],(m):n}"}"}"}{{{o}}}{p ?? q[r] ?? s(t)}{$@"{u[v[w]]}{(x(y(z)))}"}{a:b}{c,1:d2}";
            """))
        {
            // Do nothing
        }
    }
}
