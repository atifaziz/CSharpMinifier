# Plan: Add Raw String Literals Support

Comprehensive implementation plan for C# raw string literals across all forms (single-line, multi-line, interpolated). Execute incrementally by phase across multiple sessions.

## Phase 1: Raw String Literal (Single-Line)

Implement non-interpolated raw string literals (`"""text"""`) without newline support.

### Steps

1. **Add new TokenKind values to Token.cs**: Insert `RawStringLiteral` after `VerbatimStringLiteral` in the enum (line ~34), maintaining order for TraitsByKind synchronization

2. **Update TokenTraits in TokenTraits.cs**: Add trait flag `RawString = 0x200` and insert `Literal | String | RawString` entry at matching position in TraitsByKind array

3. **Add Scanner states to Scanner.cs**: Add `Quote`, `QuoteQuote`, `RawString`, `RawStringQuote`, `RawStringQuoteQuote` to State enum (~line 30-62)

4. **Add tracking variables in ScanImpl**: Add `rawStringQuoteCount` variable after `interpolationStateStack` declaration (~line 85) to track opening delimiter length (minimum 3)

5. **Implement state transitions**:
   - In Text state case, modify `'"'` handling to transition to `Quote` state
   - Add Quote state handler: if next char is `"`, go to QuoteQuote; else transition to String state
   - Add QuoteQuote state handler: if next char is `"`, set rawStringQuoteCount=3 and go to RawString; else emit empty StringLiteral token
   - In RawString state: accumulate content, track closing quote sequences; when closing quote count matches opening count, emit RawStringLiteral token
   - Add RawStringQuote/RawStringQuoteQuote states for counting closing delimiter quotes

6. **Add EOF handling**: Add RawString/RawStringQuote/RawStringQuoteQuote to the EOF check switch statement to throw "Unterminated string" error

7. **Add test cases in ScannerTests.cs**:
   - Empty: `""""""`
   - Simple: `"""hello"""`
   - With quotes inside: `"""he said "hi" """`
   - With 4-quote delimiter: `""""has """ inside""""`
   - With braces (literal): `"""{x}"""`
   - With backslashes (literal): `"""\n\t"""`

### State Disambiguation Rules
- `"x"`: Text→Quote (see `"`), Quote→String (see `x`, restart)
- `""`: Text→Quote, Quote→QuoteQuote, QuoteQuote→emit empty StringLiteral (see non-`"`)
- `"""x"""`: Text→Quote, Quote→QuoteQuote, QuoteQuote→RawString (count=3), accumulate until matching closing quotes
- `""""x""""`: Text→Quote, Quote→QuoteQuote, QuoteQuote→RawString (count=3), see another `"` (count=4), then `x` stops quote counting

---

## Phase 2: Raw String Literal (Multi-Line)

Extend Phase 1 to support newlines within raw strings.

### Steps

1. **Extend RawString state to handle newlines**:
   - In RawString state, add cases for `\n`, `\r`
   - For `\n`: increment line, reset column, stay in RawString
   - For `\r`: transition to RawStringCr state

2. **Add RawStringCr state**: Handle `\r` followed by optional `\n`
   - If next char is `\n`: increment line, reset column, return to RawString
   - If next char is not `\n`: increment line, reset column, return to RawString with goto restart

3. **Verify quote state behavior**: Confirm newlines in RawStringQuote/RawStringQuoteQuote states correctly reset to RawString state (already handled by Phase 1's non-quote logic)

4. **Add test cases**:
   - Basic multi-line with `\n`
   - Multi-line with `\r\n`
   - Multi-line with `\r`
   - Closing delimiter on separate line
   - Empty lines within content
   - Mixed quote counts across lines

---

## Phase 3: Interpolated Raw String (Single-Line)

Implement interpolated raw strings (`$"""text{expr}"""`, `$$"""text{{expr}}"""`, etc.) without newline support.

### Steps

1. **Add new TokenKind values to Token.cs**: Insert after existing raw string kinds:
   - `InterpolatedRawStringLiteral`
   - `InterpolatedRawStringLiteralStart`
   - `InterpolatedRawStringLiteralMid`
   - `InterpolatedRawStringLiteralEnd`

2. **Update TokenTraits in TokenTraits.cs**: Add entries for new token kinds with flags: `Literal | String | InterpolatedString | RawString` plus appropriate Start/Mid/End flags

3. **Add Scanner states**: Add `DollarQuote`, `DollarQuoteQuote`, `InterpolatedRawString`, `InterpolatedRawStringQuote`, `InterpolatedRawStringQuoteQuote`, `InterpolatedRawStringBrace` to State enum

4. **Add tracking variables**: Add `interpolatedRawStringDollarCount` and `interpolatedRawStringBraceCount` in ScanImpl

5. **Implement Dollar state extensions**:
   - In Dollar state, when seeing `"`, transition to DollarQuote
   - In DollarQuote: if `"`, go to DollarQuoteQuote; else go to regular InterpolatedString
   - In DollarQuoteQuote: if `"`, initialize interpolatedRawStringDollarCount=1, rawStringQuoteCount=3, go to InterpolatedRawString; else emit empty InterpolatedStringLiteral

6. **Handle multiple dollar signs**: Track consecutive `$` characters to set interpolatedRawStringDollarCount (e.g., `$$` = 2, `$$$` = 3)

7. **Implement brace counting logic** (opening):
   - In InterpolatedRawString, when seeing `{`, transition to InterpolatedRawStringBrace state
   - In InterpolatedRawStringBrace, count consecutive `{` characters (interpolatedRawStringBraceCount)
   - When seeing non-`{` character:
     - B = interpolatedRawStringBraceCount, D = interpolatedRawStringDollarCount
     - If B < D: all braces literal, return to InterpolatedRawString, goto restart
     - If B = D: emit token (Start or Mid), transition to Text (hole), push interpolation state
     - If B > D: emit token with max(B, 2D-1) braces, transition to Text (hole) with (B - (2D-1)) as initial brace balance

8. **Implement closing brace shortcut**: In Text state (inside hole), any `}` ends the hole, emit Mid or End token, return to InterpolatedRawString

9. **Add quote counting for closing delimiter**: Similar to Phase 1, track closing quote sequences

10. **Add test cases**:
    - `$"""hello{name}"""`
    - `$$"""hello{literal}{{expr}}"""`
    - `$$$"""x{lit}{{lit}}{{{expr}}}"""`
    - Empty interpolated raw string
    - Multiple interpolations
    - Edge cases: B=D, B>D, B<D scenarios

---

## Phase 4: Interpolated Raw String (Multi-Line)

Extend Phase 3 to support newlines within interpolated raw strings.

### Steps

1. **Extend InterpolatedRawString state**: Add newline handling similar to Phase 2
   - Handle `\n`: increment line, reset column, stay in InterpolatedRawString
   - Handle `\r`: transition to InterpolatedRawStringCr state

2. **Add InterpolatedRawStringCr state**: Handle `\r` with optional `\n`, return to InterpolatedRawString

3. **Update quote/brace states**: Ensure newlines reset closing quote counts but not brace counts (braces can span lines in holes)

4. **Add test cases**:
   - Multi-line with simple interpolation
   - Multi-line with multiple `$` and complex brace counting
   - Interpolation spanning multiple lines (hole expression with newlines)
   - Various newline types (`\n`, `\r`, `\r\n`)
   - Closing delimiter on separate line

---

## Final Validation

After all phases complete:

1. **Run all tests**: Verify Scanner correctly handles all raw string forms
2. **Edge cases**: Confirm unterminated strings throw errors (already covered in phase EOF handling)
3. **Smoke test**: Manual validation with complex mixed inputs (regular strings, raw strings, comments, etc.)

---

## Implementation Notes

### Key Design Principles
- **State-based disambiguation**: No peeking ahead; use states to resolve ambiguities character-by-character
- **No character dropping**: Every character belongs to some token
- **Assume valid input**: Don't over-validate; focus on minification-relevant parsing

### Brace Counting Rules (Phase 3/4 Reference)
- D = dollar count, B = consecutive `{` count
- B < D: all literal braces
- B = D: enter hole, all braces consumed as delimiter
- B > D: max(B, 2D-1) braces in token, rest initialize hole brace balance
- Closing: single `}` ends hole (shortcut, no exact count validation)
