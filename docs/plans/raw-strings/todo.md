---
title: Raw String Literal Support
description: Track implementation progress for C# 11 raw string literal support in the scanner
ms.date: 2026-01-09
---

# Raw String Literal Support

Track implementation progress for C# 11 raw string literal support in the scanner.

## Implementation Phases

- [x] **Phase 1: TokenKind and Traits**
  - [x] Add `RawStringLiteral` to `TokenKind` enum
  - [x] Add `InterpolatedRawStringLiteral` to `TokenKind` enum
  - [x] Add `InterpolatedRawStringLiteralStart` to `TokenKind` enum
  - [x] Add `InterpolatedRawStringLiteralMid` to `TokenKind` enum
  - [x] Add `InterpolatedRawStringLiteralEnd` to `TokenKind` enum
  - [x] Add `RawString = 0x200` trait flag
  - [x] Update `TraitsByKind` array for new token kinds

- [x] **Phase 2: Scanner Infrastructure**
  - [x] Extend `InterpolatedStringKind` with `Raw`
  - [x] Extend `InterpolationState` with `DollarCount` and `QuoteCount`
  - [x] Ensure raw-string hole boundaries require `Parentheses == 0`, `Braces == 0`, and `Brackets == 0`
  - [x] Add new scanner states: `RawString`, `RawStringQuote`, `RawStringCr`
  - [x] Add new scanner states: `InterpolatedRawString`, `InterpolatedRawStringQuote`, `InterpolatedRawStringBrace`, `InterpolatedRawStringCr`

- [x] **Phase 3: Single-Line Non-Interpolated Raw Strings**
  - [x] Detect `"""` opening delimiter (third quote after `""`)
  - [x] Count total opening quotes
  - [x] Scan content until matching closing quote sequence
  - [x] Emit `RawStringLiteral` token
  - [ ] Add unit tests for single-line raw strings
  - [ ] Debug and fix implementation issues

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
