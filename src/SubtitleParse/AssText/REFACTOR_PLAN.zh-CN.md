# ASS Override Tags（Spec/Generator/Validator）统一化重构计划

日期：2026-02-11  
范围：`src/SubtitleParse`、`src/SubtitleParse.Language`、`src/Ikkoku`、（联动）`src/SimpleTools/AutomationBridge`

> 目标：让所有 override tag 的 **解析/约束/校验/补全/优化** 都围绕同一份 `AssTagSpec` 数据源与 source generator 产物展开，避免“重复扫描 + 多套语义/边界行为”。  
> 新增要求：基于 VSFilterMod（代码 + Wiki）的 **Mod 标签解析**，仅在启用 `mod_mode` 时生效；在 mod 模式下解析到这些标签时需 **标注 mod 并产生 warning**。

---

## 0. 已有基础（当前状态）

已落地（以当前仓库为准）：
- `AssTag` + `[AssTagSpec(...)]` 作为单一 truth source：`src/SubtitleParse/AssTypes/AssTags.cs`
- `AssTagRegistryGenerator`：生成 trie + per-tag 表（kind/value_kind/函数种类/obsolete/range/mask/alpha 标记等）：`src/SubtitleParse.TagGenerator/AssTagRegistryGenerator.cs`
- `AssOverrideTagValidator`：统一 validator（供 Language/CLI/未来优化器复用）：`src/SubtitleParse/AssText/AssOverrideTagValidator.cs`
- `SubtitleParse.Language` 与 `Ikkoku check` 已复用同一 validator（不再维护两套验证逻辑）

进度/待办跟踪：
- `AssTag/AssTagSpec` 专项计划与进度：`src/SubtitleParse/AssTypes/ASS_TAGS_PLAN.zh-CN.md`

---

## 1. 总目标与约束

### 1.1 目标
- **完整 tag 集合**：把 ASS（含常用 VSFilter 扩展）完整集合纳入 `AssTag` + spec，并补齐 enum/特殊规则/约束。
- **约束/校验统一**：range/enum/allowed-set/特殊规则（如 `\t`、`\fade`、legacy `\a`、颜色/alpha 规范化）全部走 spec+generator 体系，validator 只做“表驱动 + 少量函数 tag 解析”。
- **可扩展**：支持更多上层能力：tag 补全、规范性检查、tag 统计、tag 编辑、tag 优化（去冗余）等都复用同一套 AST/token。
- **高性能**：
  - 读：UTF-8/`ReadOnlySpan<byte>` 为主，零分配或 pool（`AssEventTextRead`）
  - 写：一次性编辑（`AssEventTextEdit`/`AssSubtitleParseTagEditor`），低 GC
- **AOT 友好**：避免 runtime reflection；所有 tag 元数据都应由 generator 生成常量表。
- **为未来渲染器留接口**（可选目标）：即使短期不做渲染，也要保证“token/typed tag/约束表”足以作为渲染器（状态机求值/时间采样/transform 展开）的底座，且不引入会阻碍后续渲染实现的抽象负债。

### 1.2 新增：VSFilterMod 支持（mod_mode）
- 仅当启用 `mod_mode` 时，才识别 VSFilterMod 新增/扩展的 override tags（以及其 overload）。
- 在 `mod_mode` 下：
  - 解析结果可通过 `AssTagKind.IsVsFilterMod`（或等价标记）识别。
  - validator 必须产生 warning（例如 `ass.override.vsfiltermodTag`），用于诊断/规范化/优化时提示“非标准 ASS”。
- 在非 `mod_mode` 下：
  - VSFilterMod 标签应当 **不被识别**（表现为 unknown tag error），避免被错误拆成短前缀 tag（例如 `\blend1` 被拆成 `\b` + `lend1`）。

### 1.3 对标库与用例（上层 API 目标）

目标：解析与上层 API 不仅能支撑本仓库的诊断/编辑/优化，还要能支撑（甚至超越）类似的第三方库形态：
- `ass_tag_analyzer`（Python）：能把一行文本解析成“tag block 开始/结束 + 若干 tag（valid/invalid）+ 文本”的 item 列表，支持搜索 tag、修改值并回写文本、自定义浮点格式等。

因此本项目需要提供两类能力：
- **语法层 token**：即使 tag 未知/无效，也能被枚举出来（保留原始字节区间），用于统计/重写/格式化。
- **语义层 typed tag**：对已知 tag 解析成 typed 值（int/double/byte/bool/color/function），并能携带“非规范/无效”的诊断信息，供 analyzer/optimizer 使用。

性能约束（对标库通常不强调，但我们必须做到）：
- 默认提供 **streaming/visitor** 形态的遍历 API（`ref struct`/`struct` enumerator），避免“解析 -> 构造大对象树”成为常态。
- 需要“对象化 AST”的场景（例如 IDE、交互式编辑、可视化）再提供可选的 builder（显式分配，由调用方选择）。

---

## 2. 工作流与里程碑（一步到位，但分阶段落地）

### Phase A：Tag 集合补齐（ASS + 常用扩展 + VSFilterMod）
交付物：
- `AssTag` 枚举补齐为“完整集合”（ASS 标准 + 现有工具链常见扩展）
- 每个 tag 都有 `[AssTagSpec]`：`name/value_kind/tag_kind/function_kind/obsolete` 至少齐全

实施要点：
1. 从权威来源整理 tag 列表与语义
   - ASS v4+ 文档 / Aegisub 行为
   - VSFilter / libass（用于“现实世界兼容性”）
2. 为每个 tag 指定：
   - `AssTagValueKind`：Int/Double/Bool/Byte/Color/Bytes/Function
   - `AssTagKind`：render-first/latest、animateable、should_be_function、ignored 等
3. VSFilterMod tags（见 §5）加入，但在 spec 中标记 `IsVsFilterMod`

验收标准：
- `AssTagRegistryGenerator` 能稳定生成 registry，`dotnet build/test` 通过
- `AssOverrideTextCompletionProvider` 能列出完整 tag（且可按 dialect 过滤）

---

### Phase B：约束/枚举/特殊规则补齐到 spec+generator
交付物：
- 所有 tag 的 **range/enum/allowed-set/特殊规则** 完整标注到 spec，并由 generator 产出表（validator 复用）

实施要点：
1. 扩展 `AssTagSpecAttribute` 以承载更多约束（保持 AOT-friendly）
   - 数值范围：`IntMin/IntMax`、`DoubleMin/DoubleMax`
   - bitmask（已存在）：`IntAllowedMask`
   - **枚举/关键字**（新增）：如 `\blend(over|add|...)`、`WrapStyle`、未来可能的 `\p`/`\q` 等
   - **特殊规则标识**（新增）：例如：
     - `AlphaHexRule`（alpha 允许的写法、规范化建议）
     - `ColorHexRule`（颜色 digits/高位忽略/规范化建议）
     - `TransformRule`（`\t` header 规则）
     - `FadeRule`（`\fade` alpha + time order）
     - `LegacyAlignRule`（`\a` -> `\an` 映射与 allowed 集合）
     - `VsFilterModRule`（统一生成 mod warning）
2. generator 生成：
   - `TryGetSpec(tag, out spec)` 的紧凑表（减少 validator 的多次查表调用）
   - per-tag enum/keyword 表、special_rule id 表
3. validator 从“手写 switch”逐步退化为：
   - 通用：value_kind + range/mask/enum
   - 函数：function_kind + function parser
   - 少量特殊：按 `special_rule` dispatch

验收标准：
- `AssOverrideTagValidator` 的手写分支显著减少；新增规则不需要在多个项目复制逻辑
- `SubtitleParse.Language`、`Ikkoku`、AutomationBridge 的诊断行为一致

---

### Phase C：Dialect/RendererProfile/Strictness（标准 vs VSFilter vs VSFilterMod）
交付物：
- 统一的 dialect/profile/strictness 开关贯穿 parse/validate/complete/optimize

实施要点：
1. 引入统一的配置对象（建议）
   - `AssTextDialect`：`ass` / `vsfilter` / `vsfiltermod`
   - `AssRendererProfile`：例如 `vsfilter`、`libass_0_17_4`
   - `AssValidationStrictness`：`compat` / `strict`（或 Warning/Info 分级策略）
2. 在以下入口贯通：
   - `AssEventTextRead.Parse*`（提供带 options 的 overload，默认保持兼容）
   - `SubtitleParse.Language.AssOverrideTextAnalyzerContext`
   - `Ikkoku check` CLI 参数（例如 `--dialect vsfiltermod` / `--strict`）
3. Completion/Analyzer：
   - completion 根据 dialect 决定是否建议 mod tags（默认不建议或降权）
   - analyzer 在 mod_mode 下为 mod tags 产出 warning（见 §5.3）

验收标准：
- 同一段文本在不同 dialect 下，unknown/mod-warning 的行为符合预期

---

### Phase D：扫描/解析去重（parse once, reuse many times）
交付物：
- 统一所有“override 解析 + tag 遍历”路径，减少重复扫描与重复字符串编码

实施要点：
1. 盘点存量路径（AutomationBridge/SubtitleParse.Language/CLI/工具脚本）中：
   - `IndexOf` / `Regex` / `gsub` / 手写插入删除
2. 逐步迁移到：
   - 读：`AssEventTextRead`（UTF-8 segments）
   - 写：`AssEventTextEdit` / `AssSubtitleParseTagEditor`
   - 校验：`AssOverrideTagValidator.ValidateOverrideBlocks(...)`
3. 对“只需要诊断”的路径，提供更轻量的入口：
   - 例如 `ValidateOverrideBlocksFromText(ReadOnlySpan<char> ...)` 内部一次性 `Utf8IndexMap + AssEventTextRead`（避免上层重复准备）

验收标准：
- 上层项目不再维护任何“自定义 override/tag 扫描器”
- 典型批处理（AutomationBridge motion）每行最多 parse 一次

---

### Phase E：Tag 优化/去冗余（基于 spec 的 optimizer）
交付物：
- 统一的 tag optimizer（可用于 AutomationBridge、CLI、Language “quick-fix”）

实施要点（建议分两层）：
1. “纯语义去重”：
   - 在同一 block 内，针对 `BlockOnlyRenderLatest` 的 tags：只保留最后一次生效（可选保留 `\t(...)` 内的独立语义）
   - 对 `LineOnlyRenderFirst`：只保留第一次（或按规范处理）
2. “零变化不输出”：
   - 当写入的值与现有值相同（或在阈值内等价）时不生成 tag，减少冗余
   - 该逻辑必须基于统一的 typed value + spec（避免每个脚本写一套）

验收标准：
- AutomationBridge motion 在勾选选项但结果未变化时，不生成冗余 tag（按你提出的目标）

---

## 3. 关键设计点（需要在实现时一次定好，避免返工）

### 3.1 “仅 mod_mode 解析 mod tags”如何实现（必须）
推荐实现（优先级从高到低）：
1. **在 tag 匹配阶段实现 dialect gating**（推荐）
   - `TryMatch(span, dialect, out tag, out matched_len)`
   - 若“最长匹配”属于 `IsVsFilterMod` 且 dialect != vsfiltermod，则 **直接匹配失败**（unknown），不回退到短前缀 tag。
2. scanner 层提供 options：
   - `AssOverrideTagScanner(payload, payloadAbsoluteStartByte, lineBytes, options)`
3. `AssEventTextParser.ParseLine*` 与 `AssOverrideTagValidator` 共享同一 scanner/匹配策略，保证一致行为。

> 现实世界兼容性说明：VSFilter/libass 都是“前缀匹配”，所以当不认识 `\rnd.../\blend...` 时，可能会被解析成 `\r` 或 `\b` 等更短 tag（例如 `\blend1` 可能会把 bold 关掉）。  
> 我们的目标是**不让 VSFilterMod 标签在关闭 mod_mode 时悄悄改变语义**，因此需要做“前缀冲突保护”：
> - 若禁用 mod tags，但输入看起来像 mod tag（例如 `blend/frs/fsvp/fshp/movevc/...`），则应优先作为 unknown 报错/告警，而不是回退到短前缀标准 tag。
> - 但要允许“确实合法的短前缀 tag”继续工作：例如 `\rStyleName`、`\fnFontName` 这类本来就允许以字母开头的参数。
>
> 推荐规则（实现层面）：在选定候选 tag 后，根据该 tag 的 `value_kind`/形态做一次 **参数形状校验**（param-leading char check）。  
> - 数值类（int/double/byte/bool）：param 以字母开头时不应命中（避免 `\blend` → `\b`）。  
> - bytes 类（`\r`/`\fn`）：允许字母开头（所以 `\rnd10` 在非 mod_mode 下仍可按渲染器语义解析为 `\r` + `nd10`，同时可额外提示“疑似 VSFilterMod tag”）。

### 3.2 validator 中 mod-warning 的统一输出（必须）
- 当解析到 `IsVsFilterMod` tag：
  - 统一输出 warning：`ass.override.vsfiltermodTag`（message 含 tag 名）
  - 未来可加 strictness：strict 下可升级为 error

### 3.3 颜色/alpha 的“兼容解析 + 非规范告警 + 规范化建议”
- 解析层保持兼容（尽量贴近 VSFilter 的 `wcstol` 截断语义）
- 诊断层输出：
  - invalid：非法字符/非十六进制等
  - non-canonical：位数过多、大小写、缺少 `&H...&` 等
  - normalize 建议：给出 canonical 输出（用于 quick-fix/optimizer）

### 3.4 `renderer_profile` + `strictness`（libass vs VSFilter）
原则：
- **不要为 libass/vsfilter 各写一套解析器**；override/tag 的 **词法/结构解析** 保持统一（同一套 `AssEventTextRead`/scanner/parser）。
- 通过两个正交开关控制“行为差异”：
  - `renderer_profile`：描述“目标渲染器”的语义/范围/钳制差异（例如 libass 对 `\be/\blur` 的上限、某些 tag 的 reset 语义等）
  - `strictness`：描述诊断与兼容策略（Info/Warning/Error 分级、是否启用“前缀冲突保护”、是否将非规范输入视为错误等）

建议的组合（实现时以 API/枚举体现）：
- `AssTextDialect`：控制“哪些 tags 会被识别”（`ass` / `vsfilter` / `vsfiltermod`）
- `AssRendererProfile`：控制“识别后如何验证/解释”（例如 `vsfilter_2_39`、`libass_0_17_4`）
- `AssValidationStrictness`：控制“诊断强度/容错”（例如 `compat`/`normal`/`strict`）

关于 libass 版本：
- libass 的具体钳制/容错会随 release 变动，因此 **profile 名必须带版本号**（例如 `libass_0_17_4`）。
- 文档与代码实现应同步维护该版本号，并在升级 profile 时更新对应的测试用例（见 §9）。

### 3.5 坐标/几何数据类型（可选）
建议：
- parser/validator 层保持 `out double x, out double y` 等“平铺输出”，避免引入 `Vector2` 这类依赖与精度/装箱问题（AOT/跨平台更稳）。
- 算法层（AutomationBridge motion/perspective 等）如需可读性，可引入轻量 struct：
  - `AssPointD { double x; double y; }` / `AssRectI { int x1,y1,x2,y2; }`
  - 不要求 SIMD；热点通常在扫描/编辑而不是两三个 double 的运算。

---

## 4. 测试与验收策略

### 4.1 单元测试（必须）
- `AssEventTextParser`：tag 分割、matched_len、函数 tag 括号解析、`\t(...)` 嵌套深度
- `AssOverrideTagValidator`：
  - range/enum/mask
  - color/alpha 的 invalid/non-canonical/normalize
  - function tags：`pos/move/org/clip/fade/fad/t` + VSFilterMod 新增函数
- dialect gating：
  - 非 mod_mode：mod tag -> unknown（且不被拆成短 tag）
  - mod_mode：mod tag -> recognized + mod-warning
  - 特例：`\r`/`\fn` 等 bytes-tag 前缀在关闭 mod_mode 时仍按“合法短 tag”解析，但应额外产出“疑似 mod tag”提示（可选，受 strictness 控制）

### 4.2 性能回归（建议）
- 以真实字幕文件（`src/Test/test_files/*.ass` + `_ref`）做基准：
  - 每行 parse 次数（目标：1）
  - 分配（目标：0 或接近 0；编辑仅一次 buffer）
  - 时间（对比 refactor 前后）

---

## 5. VSFilterMod（mod_mode）Tag 清单与解析形状（基于代码 + Wiki）

> 来源：
> - VSFilterMod Wiki：`New-Tags` 页面  
> - VSFilterMod 代码：`CRenderedTextSubtitle::ParseSSATag`（`src/subtitles/RTS.cpp`）

### 5.1 需要新增到 `AssTag` 的 VSFilterMod tags（初始清单）

基础（代码中 `_VSMOD` 分支明确出现）：
- `\ortho<0|1>`（bool/int，0/1）
- `\z<arg>`（double；VSFilterMod 内部会缩放）
- `\xblur<arg>`、`\yblur<arg>`（double >= 0）
- `\frs<angle>`（double；VSFilterMod 内部会缩放）
- `\fsvp<leading>`、`\fshp<indents>`（double；VSFilterMod 内部会缩放）
- `\blend<mode>`（enum：数值或关键字；代码含 `over/add/sub/mult/scr/diff/rsub/isub`）
- `\distort(u1,v1,u2,v2,u3,v3)`（function：>=6 doubles）
- `\jitter(left,right,up,down,period[,seed])`（function：4-6 ints/doubles）
- `\mover(x1,y1,x2,y2,angle1,angle2,radius1,radius2[,t1,t2])`（function：8 或 10）
- `\moves3(x1,y1,x2,y2,x3,y3[,t1,t2])`（function：6 或 8）
- `\moves4(x1,y1,x2,y2,x3,y3,x4,y4[,t1,t2])`（function：8 或 10）
- `\movevc(x1,y1[,x2,y2[,t1,t2]])`（function：2/4/6）
- `\rndx<arg>`、`\rndy<arg>`、`\rndz<arg>`、`\rnd<arg>`、`\rnds<seed>`（double/int；seed 在代码中按 hex 读）
- `\lua(method, args...)`（function；并且存在“直接调用同名 tag 作为 Lua 函数”的扩展行为）

渐变/贴图（以 “1..4” 通道区分）：
- `\1img(path[,xoffset,yoffset])` … `\4img(...)`（function）
- `\1vc(c1,c2,c3,c4)` … `\4vc(...)`（function；4 colors）
- `\1va(a1,a2,a3,a4)` … `\4va(...)`（function；4 alphas）

> 注：以上清单以“代码明确出现”为准；Wiki 可能还有额外条目，最终以代码为准补齐。

### 5.2 mod_mode 下对“已有 tag”的 overload（需要考虑）
VSFilterMod 不仅新增 tags，还扩展了部分已有 tags 的参数形式（仅举关键项）：
- `\pos(x,y,z)`（代码出现 3 参数分支）
- `\fsc<scale>`（VSFilterMod 扩展：`\fsc` 在标准渲染器中是 reset，但在 VSFilterMod 中可带数值，等价于同时设置 `\fscx` 与 `\fscy`）
- `\org` 在 mod 分支出现多参数/带时间的 effect 写法（需按代码确认是否纳入解析器/validator）

建议的验证策略：
- 标准模式：`\fsc` 不带参是 reset；带参时 **不按 scale 生效**（与 VSFilter/libass 一致），但建议产出 `ass.override.nonStandardPayload`（Info/Warning）。
- mod_mode：`\fsc<scale>` 按 scale 生效，同时产出 `ass.override.vsfiltermodOverload`（Warning）。

实现约束：
- 这些 overload 必须受 dialect gating 控制：仅在 `vsfiltermod` dialect / `mod_mode` 下接受。

---

### 5.3 mod-warning 规范（必须）
- 诊断码：`ass.override.vsfiltermodTag`
- 默认 severity：Warning
- message 形态：`VSFilterMod override tag: \\<name> (enabled by mod_mode)`

---

## 6. 代码落点（实施时的文件/模块建议）

### 6.1 Spec/Generator
- `src/SubtitleParse/AssTypes/AssTags.cs`
- `src/SubtitleParse/AssTypes/AssTagSpecAttribute.cs`（扩展约束字段）
- `src/SubtitleParse.TagGenerator/AssTagRegistryGenerator.cs`（生成更多表：enum/keyword/special_rule/spec struct）

### 6.2 Parser/Scanner（dialect gating）
- `src/SubtitleParse/AssText/AssOverrideTagScanner.cs`
- `src/SubtitleParse/AssTypes/AssTags.cs`（`AssTagRegistry.TryMatch` 增加 dialect 参数或 overload）
- `src/SubtitleParse/AssText/AssEventTextParser.cs`（parse options 贯通）

### 6.3 Validator / Analyzer / Completion
- `src/SubtitleParse/AssText/AssOverrideTagValidator.cs`
- `src/SubtitleParse.Language/AssOverrideAnalyzer.cs`
- `src/SubtitleParse.Language/AssOverrideTextCompletionProvider.cs`
- `src/Ikkoku/CommandLine/CheckCmd.cs`

### 6.4 Optimizer / Editor（去冗余）
- `src/SubtitleParse/AssText/AssEventTextEdit.cs`
- `src/SubtitleParse/AssText/AssSubtitleParseTagEditor.cs`
- （新）`src/SubtitleParse/AssText/AssOverrideTagOptimizer.cs`（建议新增，纯表驱动）

---

## 7. 风险与对策

- tag 过多导致维护成本上升  
  对策：把“特殊规则”抽象为 `special_rule` + 表驱动；新增 tag 只改 spec。
- dialect gating 影响兼容性（例如旧字幕里把 `\rnd...` 当作 `\r` style name）  
  对策：明确规则：当存在更长的（即使被禁用的）mod tag 命中时，非 mod_mode 视为 unknown；必要时提供 `compat` strictness 允许回退。
- 解析/校验/优化多处入口不同步  
  对策：所有入口统一走 `AssEventTextRead + AssOverrideTagScanner + AssOverrideTagValidator`，并用单元测试锁行为。

---

## 8. API 入口补齐（ReadOnlySpan<char>）

现状：
- `AssEventTextRead.Parse(ReadOnlySpan<char>)` 已存在（内部用 `ArrayPool<byte>` 编码到 UTF-8，低 GC）。

计划补齐：
- 为 validator/editor 提供对 `ReadOnlySpan<char>` 的便捷入口（避免上层重复写 `Parse + Dispose`）：
  - `AssOverrideTagValidator.ValidateText(ReadOnlySpan<char> text, ...)`
  - `AssSubtitleParseTagEditor.*` 增加接收 `ReadOnlySpan<char>` 的 overload（内部调用 `AssEventTextRead.Parse`）

说明：
- 不建议再做一套“char 直接解析 override/tag”的实现，以免维护两套括号/函数 tag 规则；char 输入应通过一次 UTF-8 编码进入同一套 `AssText` 解析器。

---

## 9. 参考版本与来源（实现时需同步维护）

- libass：建议以 `libass 0.17.4` 作为初始 profile（发布于 2025-06-07；profile 名写成 `libass_0_17_4`），后续升级需更新 profile 名与相关测试。
- VSFilter：建议以 `xy-VSFilter` 的**最新 release**作为参考实现（不固定 commit/版本号）：
  - `https://github.com/pinterf/xy-VSFilter`
  - 行为参考点：`src/subtitles/RTS.cpp`（`InitCmdMap` + `ParseSSATag`/`CreateSubFromSSATag`）
  - 交叉验证：对比 guliverkli2 系列的 `RTS.cpp`，其识别的 override tag 集合与 `InitCmdMap` 一致（便于解释“现实世界字幕”的兼容行为）。
  - MPC-HC 的 VSFilter 行为实现主要在 `src/Subtitles`（`RTS.cpp`/`RTS.h` 等），可作为另一份“现实世界实现”的交叉参考：
    - `https://github.com/clsid2/mpc-hc/tree/develop/src/Subtitles`
  - `src/filters/transform/VSFilter/` 更偏向集成/接口（例如 `IDirectVobSub.h`），不作为 override tag 行为差异来源：
    - `https://github.com/clsid2/mpc-hc/tree/develop/src/filters/transform/VSFilter/`
- VSFilterMod：以 Wiki（New-Tags）+ 代码（`RTS.cpp`）为准；建议在实现时记录一次性抓取的 commit hash，避免 Wiki 与代码漂移导致测试不稳定。
- 对标库（上层 API 参考）：`ass_tag_analyzer`：`https://github.com/moi15moi/ass_tag_analyzer`（用于校验“解析输出形态 + 操作能力”是否足够通用）。
