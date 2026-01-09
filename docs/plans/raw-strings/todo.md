---
title: Raw String Literal Support
description: Track implementation progress for C# 11 raw string literal support in the scanner
ms.date: 2026-01-09
---

# Raw String Literal Support

Track implementation progress for C# 11 raw string literal support in the scanner.

## Implementation Phases

- [ ] **Phase 1: TokenKind and Traits**
  - [ ] Add `RawStringLiteral` to `TokenKind` enum
  - [ ] Add `InterpolatedRawStringLiteral` to `TokenKind` enum
  - [ ] Add `InterpolatedRawStringLiteralStart` to `TokenKind` enum
  - [ ] Add `InterpolatedRawStringLiteralMid` to `TokenKind` enum
  - [ ] Add `InterpolatedRawStringLiteralEnd` to `TokenKind` enum
  - [ ] Add `RawString = 0x200` trait flag
  - [ ] Update `TraitsByKind` array for new token kinds

- [ ] **Phase 2: Scanner Infrastructure**
  - [ ] Extend `InterpolatedStringKind` with `Raw`
  - [ ] Extend `InterpolationState` with `DollarCount` and `QuoteCount`
  - [ ] Ensure raw-string hole boundaries require `Parentheses == 0`, `Braces == 0`, and `Brackets == 0`
  - [ ] Add new scanner states: `RawString`, `RawStringQuote`, `RawStringCr`
  - [ ] Add new scanner states: `InterpolatedRawString`, `InterpolatedRawStringQuote`, `InterpolatedRawStringBrace`, `InterpolatedRawStringCr`

- [ ] **Phase 3: Single-Line Non-Interpolated Raw Strings**
  - [ ] Detect `"""` opening delimiter (third quote after `""`)
  - [ ] Count total opening quotes
  - [ ] Scan content until matching closing quote sequence
  - [ ] Emit `RawStringLiteral` token
  - [ ] Add unit tests for single-line raw strings

- [ ] **Phase 4: Multi-Line Non-Interpolated Raw Strings**
  - [ ] Handle newline after opening delimiter as multi-line indicator
  - [ ] Implement `RawStringCr` state for `\r\n` line tracking
  - [ ] Add unit tests for multi-line raw strings

- [ ] **Phase 5: Single-Line Interpolated Raw Strings**
  - [ ] Track `$` count before `"""`
  - [ ] Use `$` count to determine brace depth for hole detection
  - [ ] Emit Start/Mid/End tokens for holes
  - [ ] Add unit tests for single-line interpolated raw strings

- [ ] **Phase 6: Multi-Line Interpolated Raw Strings**
  - [ ] Combine multi-line handling with interpolation hole tracking
  - [ ] Add unit tests for multi-line interpolated raw strings

- [ ] **Phase 7: Nested String Combination Tests**
  - [ ] Test `$"""hello {"""world"""}"""` (raw in raw hole)
  - [ ] Test `$"hello {"""world"""}"` (raw in regular interpolated hole)
  - [ ] Test `$"""hello {"world"}"""` (regular in raw hole)
  - [ ] Test other mixing scenarios

## Design Decisions

- **Quote counting strategy**: Count all consecutive quotes at closing delimiter and verify match against opening count. Simpler given valid-input assumption.
- **Interpolation state**: Extend `InterpolationState` (and `InterpolatedStringKind`) to support raw string interpolation; do not introduce alternate tuple tracking.
- **Start/Mid/End segmentation**: Used for interpolated raw strings to enable minification of code inside holes.
