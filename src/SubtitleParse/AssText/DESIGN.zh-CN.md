# AssText（ASS Event Text / Override）设计与用法

## 范围与分层

- `AssEvent`（`Mobsub.SubtitleParse.AssTypes`）：负责解析 **Events 的 CSV 行字段**（Layer/Start/End/Style/Text...）。
- `AssText`（`Mobsub.SubtitleParse.AssText`）：负责解析 **Event 的 `Text` 字段内部**（`{...}` override block、`\\tag`、函数 tag、`\\t(...)` 等）。
- `SubtitleParse.Language`：偏“语言服务”（诊断/规范性检查/补全）方向；底层 override/tag 解析应复用 `AssText` 的解析与 tag 语义表（目前已通过 `AssEventTextRead` + `AssOverrideTagScanner` 复用），避免重复扫描。

## 核心类型（建议从这里开始用）

- `AssEventTextRead`（`src/SubtitleParse/AssText/AssEventTextRead.cs`）
  - “parse once, reuse many times”的读上下文：持有 `Utf8` + 解析后的 `Segments`（来自 pooled buffer）。
  - 推荐用法：
    - **零分配**：`AssEventTextRead.Parse(ReadOnlyMemory<byte> utf8)` / `AssEventTextRead.ParseTextSpan(in AssEvent ev)`
    - **低 GC**：`AssEventTextRead.Parse(string)` / `AssEventTextRead.Parse(ReadOnlySpan<char>)`（内部用 `ArrayPool<byte>` 编码到 UTF-8）
  - 首块判定：`TryGetFirstOverrideBlock(out Range lineRange, out ReadOnlySpan<AssTagSpan> tags)`
    - 只要首段是 `{...}` override block 即返回 `true`；即使是空块 `{}` 也会返回 `true`（此时 `tags` 为空 span）。
    - 建议各类 helper 统一走这个 API，避免重复写 `segments[0]`/`Tags == null` 的分支。
  - 注意：`AssEventTextRead` 是 `IDisposable`；如果来源是 pool（string/char/span），`Dispose()` 后 `Utf8`/tag payload 的切片就不再安全。

- `AssEventTextEdit`（`src/SubtitleParse/AssText/AssEventTextEdit.cs`）
  - 基于 `AssEventTextRead` 的 **批量编辑器**：对 UTF-8 byte ranges 进行 `Insert/Replace/Delete`，最后一次性输出结果字符串。
  - 典型用法：同一行需要做多处修改（删一批 tags + 插入一批 tags + 删片段等）时，避免反复 `string.Replace/Substring` 与重复 parse。
  - 约束：不支持 overlapping edits（重叠区间会抛异常）；建议按从左到右规划 edits，或先合并区间。

- `AssEventTextParser`（`src/SubtitleParse/AssText/AssEventTextParser.cs`）
  - 低层解析器：把一行 Event Text（UTF-8 bytes）解析成 `AssEventSegment[]`（含 TagBlock / Text / `\\N`/`\\n`/`\\h`）。
  - 高性能路径优先使用：
    - `ParseLinePooled(ReadOnlyMemory<byte>)`（保留参数切片，避免 per-tag 分配）
    - `WithParsedSegments(...)`（RAII 包裹 pooled buffer）

- `AssOverrideTagScanner` / `AssOverrideTagToken`（`src/SubtitleParse/AssText/AssOverrideTagScanner.cs`）
  - override payload（`{...}` 内部字节，不含花括号）级别的 scanner：能枚举 **known + unknown** tags，并保留 byte range（用于统计/重写/格式化）。
  - 与 `AssEventTextParser`/`AssOverrideTagValidator` 共享同一套 tag 匹配与 dialect gating（避免“同一行在不同入口解析结果不一致”）。
  - 推荐入口：`AssEventTextRead.TryCreateTagBlockScanner(...)` / `AssEventTextRead.TryGetFirstOverrideTagScanner(...)`（会复用 parse 时的 `AssTextOptions`）。

- `AssOverrideTagValueParser`（`src/SubtitleParse/AssText/AssOverrideTagValueParser.cs`）
  - 把 `AssOverrideTagToken`（或 tag + param span）解析成 `AssTagValue`（typed value），用于 optimizer/editor 复用同一套“宽松解析”语义。

- `AssEventSegment` / `AssTagSpan` / `AssEventSegmentKind`（`src/SubtitleParse/AssText/AssEventSegment.cs`）
  - `AssTagSpan` 是语义化后的 tag（`AssTag` + `Range` + `AssTagValue`），可直接 `TryGet<T>` 取出 typed value（`double/int/byte/bool/color/function/...`）。

- `AssEventTextQuery`（`src/SubtitleParse/AssText/AssEventTextQuery.cs`）
  - 常用查询：`GetWrapStyle(...)`、`HasPolygon(...)`、`FindLastTag(...)`。

## 常用上层工具（都应基于 AssText）

- `AssTagValueParser`：从已解析 `Segments` 中读取 `\\an/\\pos/\\move/\\org/...` 等常见值（不要再做字符串 `IndexOf` 扫描）。
- `AssTransformTokenizer`：基于解析后的 `\\t(...)` tag span 进行 tokenization，避免误改 transform 内部。
- `AssSubtitleParseTagEditor` / `AssSubtitleParseTagStripper` / `AssClipQuadExtractor`：override block 的插入/替换/剥离/提取。
  - 新增（或已补齐）若干接受 `AssEventTextRead` 的 overload：同一行需要“读取 + 编辑 + 提取”时可复用同一次 parse。

## 性能与可维护性约定（建议统一执行）

1. **输入优先级**
   - 最优：从 `AssEvent.LineRaw[ev.TextReadOnly]` 得到 `ReadOnlyMemory<byte>`，直接 parse（零分配）。
   - 次优：从 `string/ReadOnlySpan<char>` parse（使用 pool，避免频繁 `new byte[]`）。

2. **每行只 parse 一次**
   - 同一行需要 align/pos/org/transform/编辑时：先创建一个 `AssEventTextRead`，把它传给所有 helper（避免重复 encode/parse）。

3. **同一行多处修改：一次性写回**
  - 需要对同一行做多个 range edit：优先用 `AssEventTextEdit` 收集 edits，最后 `ApplyToString()` 输出。
  - 需要“首个 override block 插入 + 删除一组 tags”：优先用 `AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlock(...)`（通用 API），把要插入的 tags 拼成一个块、把要删除的 tag 集合用一个 predicate 表达。

4. **“语法/诊断”与“语义/算法”分层**
   - `AssEventTextParser` 负责：结构化分段、tag 匹配、typed value 解析（语义层）。
   - 诊断/补全/规范性检查：优先在 `SubtitleParse.Language` 做，但需要逐步把底层 token/解析复用到 `AssText`，避免维护两套括号/函数 tag 解析。

## 下一步（建议的重构方向）

- 将存量的“字符串扫描 + 手写插入/删除”逐步迁移到 `AssEventTextRead/AssEventTextEdit/AssSubtitleParseTagEditor`（尤其是 AutomationBridge 里对 override block 的编辑路径），统一语义与边界行为。
- 用 source generator 扩展 `AssTagSpec`：生成每个 tag 的约束/校验（range/enum/特殊规则），并提供统一的 validator（供诊断/优化/规范化使用）。

更完整的落地计划见：`src/SubtitleParse/AssText/REFACTOR_PLAN.zh-CN.md`。
