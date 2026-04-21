# Mobsub.SubtitleParse.Language (in-process editor features)

Designed for embedding in a GUI editor (fast, low-allocation, NativeAOT-friendly).

## Scope

This package currently focuses on override tags inside the Events `Text` field (e.g. `{\\pos(10,20)\\c&H00FF00&}...`).
Whole-document analysis can be reintroduced later if needed.

## Usage

- Override-tags only (when your GUI editor is only editing the Event `Text` field):
  - `AssOverrideTextAnalyzer.Analyze(textField, context)` → `AssOverrideTextAnalysisResult`
  - Optional context (for warnings based on the current event line / script resolution):
    - `new AssOverrideTextAnalyzerContext(start, end, layoutResX, layoutResY, playResX, playResY)`
    - `new AssOverrideTextAnalyzerContext(eventDurationMs: ..., layoutResX: ..., layoutResY: ..., playResX: ..., playResY: ...)`
    - Coordinate bounds prefer `LayoutResX/Y`, falling back to `PlayResX/Y`.
  - `AssOverrideTextCompletionProvider.GetCompletions(textField, pos, analysis)`

## Public API (text-only)

- Entry points:
  - `AssOverrideTextAnalyzer.Analyze(string, AssOverrideTextAnalyzerContext?)`
  - `AssOverrideTextCompletionProvider.GetCompletions(string, AssPosition, AssOverrideTextAnalysisResult)`
- Types:
  - `AssOverrideTextAnalyzerContext` (optional warnings context)
  - `AssOverrideTextAnalysisResult` (`LineMap`, `Diagnostics`)
  - `AssPosition`, `AssRange`, `TextLineMap`
  - `AssDiagnostic`, `AssSeverity`, `AssCompletionResult`, `AssCompletionItem`

## GUI integration recipe

- Keep the current `Text` field as a `string` in your editor model.
- On every edit (or debounced):
  - Build context from the current event line + script info:
    - `eventDurationMs` from `End - Start` (or pass `AssTime start/end` directly).
    - `layoutResX/Y` from `[Script Info] LayoutResX/LayoutResY` when present, else `playResX/Y`.
  - Call `AssOverrideTextAnalyzer.Analyze(textField, context)` and render `Diagnostics` as squiggles.
- For completion:
  - Use cursor `AssPosition(line, character)` in the `Text` field, then apply `ReplaceRange` from the returned `AssCompletionResult`.

## Diagnostic codes

- Structure/tags:
  - `ass.override.unclosed` (missing `}`)
  - `ass.override.unknownTag`
  - `ass.override.obsoleteTag` (e.g. legacy `\\a` with a suggested `\\an` mapping)
- Value validation:
  - `ass.override.functionInvalid` (expected signature is included when known)
  - `ass.override.intInvalid`, `ass.override.doubleInvalid`, `ass.override.alphaInvalid`, `ass.override.colorInvalid`
- Suggestions / warnings:
  - `ass.override.colorNormalize` (returns a canonical `&HBBGGRR&` suggestion for quick-fix)
  - `ass.override.timeOutOfRange` (relative times vs event duration)
  - `ass.override.coordOutOfRange` (coordinates/clips vs LayoutRes/PlayRes bounds; warning only)

## Code structure (for maintenance)

- `src/SubtitleParse.Language/AssOverrideTextAnalyzer.cs`: public analysis entry, per-line scan using `TextLineMap`.
- `src/SubtitleParse.Language/AssOverrideAnalyzer.cs`: core validator for `{...}` blocks and tag payloads; reuses `Mobsub.SubtitleParse.AssText` parsing/scanning to avoid maintaining a second override-tag parser.
- `src/SubtitleParse.Language/Utf8IndexMap.cs`: pooled UTF-8 byte-index ↔ UTF-16 char-index mapping for accurate editor ranges.
- `src/SubtitleParse.Language/AssOverrideTextCompletionProvider.cs`: tag-name completion (filters `AssTagRegistry`).
- `src/SubtitleParse.Language/AssOverrideTextAnalyzerContext.cs`: optional context for time/coordinate warnings (LayoutRes preferred).
- `src/SubtitleParse.Language/TextLineMap.cs`: offset ↔ (line, column) mapping.
- `src/SubtitleParse/AssTypes/AssTags.cs`: tag definitions + generated fast trie registry (`AssTagRegistry`).
- `src/SubtitleParse.TagGenerator`: source generator that builds the trie/descriptor tables from `[AssTagSpec]` (NativeAOT-friendly, avoids runtime reflection).

- This package currently focuses on override tags inside the Events `Text` field; whole-document analysis can be reintroduced later if needed.
