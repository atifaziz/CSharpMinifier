# TODO

## Raw String Literals Support

### Phase 1 — Raw string literal (single-line)
- [x] Add TokenKind.RawStringLiteral and traits
- [x] Extend scanner state machine to recognize """...""" and """"...""""
- [x] Add unit tests for single-line raw strings
- [x] Mark phase complete

### Phase 2 — Raw string literal (multi-line)
- [x] Extend scanner to allow newlines in raw strings and track line/col correctly
- [x] Add unit tests for multi-line raw strings (\n, \r, \r\n)
- [x] Mark phase complete

### Phase 3 — Interpolated raw string (single-line)
- [ ] Add interpolated-raw TokenKinds (InterpolatedRawStringLiteral, Start/Mid/End) and traits
- [ ] Extend scanner: dollar-count + opening-brace counting rules (D/B rules)
- [ ] Add unit tests for interpolated raw strings (including multiple $)
- [ ] Mark phase complete

### Phase 4 — Interpolated raw string (multi-line)
- [ ] Extend interpolated-raw scanner states for newlines and CR handling
- [ ] Add unit tests for multi-line interpolated raw strings
- [ ] Mark phase complete

## Notes / tricky cases

### General raw-string delimiter rules
- Raw strings start and end with 3+ double quotes.
- Delimiter length is the number of quotes in the opener; the closer must match that length.
- Scanner approach: disambiguate via states (no peeking); once sure it’s raw, start counting delimiter quotes.

### Interpolated raw strings: brace counting rules (opening)
Assume:
- D = dollar count at start ($, $$, $$$, ...)
- B = consecutive “{” count seen while scanning inside interpolated raw string
- The scanner is in the brace-counting state and then sees a character that is not “{”

Rules:
- If B < D: all braces are literal; revert to “in raw string literal” state.
- If B = D: enter “hole” state.
- If B > D:
  - Enter “hole” state.
  - Some braces belong to the raw string token before the hole; the rest belong inside the hole and initialize brace-balance tracking.
  - Specifically: a maximum of B or (2D - 1) braces go into the emitted raw string token, and the remaining braces belong to the hole and initialize its braces balance tracking.

Example (D=2, B=4):
- Input: $$"""text {{{{x}}}}"""
- Tokens contain: $$"""text {{{, {x}, }}}"""

### Interpolated raw strings: brace shortcut (closing)
- Shortcut applies only to closing: a single “}” is treated as the end of the hole.
- Exact count of closing braces is not validated; remaining “}” characters become part of subsequent raw-string tokens.

## Session Log
- 2026-01-14: Planned phased implementation and clarified interpolated raw-string D/B brace rules.
