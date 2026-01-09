---
description: "Plan for adding C# 11 raw string literal support, aligned with the current Scanner interpolation-state design"
---

# Plan: Add C# Raw String Literal Support

Add scanner support for [C# 11 raw string literals (`"""..."""`)][rawstr] in all forms: single-line, multi-line, interpolated single-line, and interpolated multi-line. Use Start/Mid/End segmentation for interpolated forms to enable minification of hole contents. Follow with `CSharpString` updates and nested string combination tests.

[rawstr]: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/raw-string

## Steps

1. **Add new `TokenKind` values** in [Token.cs](../../src/CSharpMinifier/Token.cs): Add `RawStringLiteral`, `InterpolatedRawStringLiteral`, `InterpolatedRawStringLiteralStart`, `InterpolatedRawStringLiteralMid`, `InterpolatedRawStringLiteralEnd`.

2. **Add `RawString` trait** in [TokenTraits.cs](../../src/CSharpMinifier/TokenTraits.cs): Add `RawString = 0x200` flag and update `TraitsByKind` array with trait combinations for new token kinds.

3. **Align raw-string support with current interpolation tracking** in [Scanner.cs](../../src/CSharpMinifier/Scanner.cs):
	- Extend `InterpolatedStringKind` to include `Raw` (alongside `None`, `Regular`, `Verbatim`).
	- Extend `InterpolationState` with `DollarCount` and `QuoteCount` for raw interpolations.
	- Keep the current model: use a single `interpolationState` local for the active interpolation and use the stack only for nesting.
	- Ensure raw-string hole boundary detection only triggers when `Parentheses`, `Braces`, and `Brackets` are all zero (same boundary rule used for regular/verbatim interpolations).

4. **Add scanner states** in [Scanner.cs](../../src/CSharpMinifier/Scanner.cs): Add `RawString`, `RawStringQuote`, `RawStringCr`, `InterpolatedRawString`, `InterpolatedRawStringQuote`, `InterpolatedRawStringBrace`, `InterpolatedRawStringCr`.

5. **Implement single-line non-interpolated raw strings**: After detecting `"""` (third quote following `""`), count total opening quotes, scan content until matching closing quote sequence, emit `RawStringLiteral`.

6. **Extend to multi-line non-interpolated raw strings**: Use `RawStringCr` state for `\r\n` line tracking; treat newline after opening delimiter as multi-line indicator.

7. **Implement single-line interpolated raw strings**: Track `$` count before `"""`, use `DollarCount` to determine brace depth for hole detection, emit Start/Mid/End tokens.

8. **Extend to multi-line interpolated raw strings**: Combine multi-line handling with interpolation hole tracking.

9. **Add unit tests for each phase** in [ScannerTests.cs](../../tests/ScannerTests.cs): Single-line `"""text"""`, multi-line, `$"""..."""`, `$$"""...{{x}}..."""`, nested quotes in content.

10. **Add combined/nesting tests that exercise delimiter tracking inside holes**:
	- `$"""{xs[1,2]}"""` (brackets inside hole)
	- `$"""{x switch { 1 => "a", _ => "b" }}"""` (braces/switch inside hole)
	- `$"""hello {"""world"""}"""` (raw in raw hole)
	- `$"hello {"""world"""}"` (raw in regular interpolated hole)
	- `$"""hello {"world"}"""` (regular string in raw hole)

## Design Decisions

1. **Quote counting strategy**: Count all consecutive quotes at closing delimiter and verify match against opening count. This is simpler given the valid-input assumption.

2. **Interpolation tracking model**: Use the existing `InterpolationState` + stack-for-nesting design. Raw string support extends that model rather than introducing an alternate tracking tuple.

3. **Progress tracking**: Use [todo.md](todo.md) in the same directory as this plan to track implementation progress across sessions.

To consider a phase complete, validate that all tests are passing, then create a Git commit for that phase before moving on to the next phase.
