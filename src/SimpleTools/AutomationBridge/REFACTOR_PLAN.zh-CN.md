# Mobsub.AutomationBridge 重构与迁移方案（更新）

日期：2026-02-09  
范围：`src/SimpleTools/AutomationBridge`（已合并 `src/SimpleTools/AssNextGen.Core`；必要时联动 `src/SubtitleParse`）

## 0. 目标与约束

### 目标
- 让经典 Aegisub（C++/LuaJIT）侧 Lua 脚本尽量“变薄”：只负责收集选区/组装 request/应用 patch。
- 将 `_ref/automation` 中常用脚本的**核心逻辑**（解析/算法/批处理）迁移到 .NET（NativeAOT）侧，以便复用、测试、性能可控。
- 让项目结构与分层清晰：**ABI/协议层**、**通用适配层**、**脚本/功能算法层**边界明确，便于持续扩展更多方法。
- 为未来 Aegisub.Next（托管内置模块）预留“同形 API”：尽量让 handler 既能被 MsgPack/FFI 调用，也能被托管直接调用。

### 约束（必须保留）
- ABI 稳定：`mobsub_abi_version` / `mobsub_invoke` / `mobsub_free` 不轻易变化。
- request/response 以 blob 承载（当前：`MSB1` envelope + MessagePack）；JSON 仅作为调试/配置用途，不再作为常规传输格式。
- NativeAOT：避免依赖反射/动态加载；尽量静态注册方法表。
- 性能与可读性兼顾：不要为了“抽象”引入过度间接层；同时避免 `Bridge.cs` 继续膨胀。

### 当前进度（2026-02-13）
- 已完成目录与分层重组：`Abi/Protocol/Dispatch/Common/Scripts/Ae/Core` 等目录。
- 已合并 `AssNextGen.Core` 到同一 csproj，算法统一到 `Mobsub.AutomationBridge.Core.*`（不再使用 `Mobsub.AssNextGen.Core.*`）。
- 已落地 `BridgeDispatcher` + `BridgeMethodCatalog` 静态方法表；导出 ABI 保持 `mobsub_abi_version/mobsub_invoke/mobsub_free`。
- 已引入并强制要求 `MSB1` envelope（4 bytes magic + MessagePack payload）；JSON 仅用于脚本侧配置/调试输出。
- 已将旧 `AssNextGen.Core.Tests` 的关键测试迁移到 `src/Test`（MSTest），并移除旧 NUnit 测试工程源文件，避免双套测试框架并存。
- 下一步：以 `_ref/automation` 为基准迁移常用脚本（先 Aegisub-Motion：去掉 trim/encoding，合并 Fix motion），并把示例脚本放入 `examples/`。

## 1. 现状问题（为什么需要拆层）

当前 `Bridge.cs` 同时承担：
- 协议解码/校验（schema/call/args/lines/resolution）
- 方法分发（call kind/type -> handler）
- 各脚本算法实现（motion/perspective/drawing）
- 若干“通用工具”实现（样式表解析、数字解析、tag 字符串拼装等）

典型痛点：
- 单文件过大，代码导航困难；新增一个方法会继续堆积重复的参数校验/解析逻辑。
- “通用适配”和“算法”混在一起：同类 tag 编辑逻辑分散在不同 handler 中，难以复用和测试。
- 部分字符串扫描解析（如 `TryParseMove`/`TryParseTagNumber`）与 `Mobsub.SubtitleParse` 的 AST 解析并存，重复且容易不一致。

## 2. 推荐的分层与职责（核心设计）

建议把 `Mobsub.AutomationBridge` 视为“宿主 + 适配层 + 算法层”的**单一工程**（先合并，后拆分），通过**目录与 namespace**把边界做硬：
- 宿主/协议/适配：`Mobsub.AutomationBridge.*`
- 算法：使用独立 namespace（例如 `Mobsub.AutomationBridge.Core.*`），避免 glue 侵入算法代码；将来需要时再拆回独立 csproj。

### Layer A：ABI/导出层（LuaJIT FFI 直接调用）
职责：稳定 ABI、内存分配/释放、异常兜底、把 `byte*+len` 转给上层 dispatcher。

建议命名/位置：
- `Abi/Exports.cs`（或保留根目录 `Exports.cs`，但逻辑归类为 Abi）

### Layer B：协议/分发层（Bridge Dispatcher）
职责：
- Codec 解码/编码（当前 MessagePack）
- SchemaVersion/call 校验（v1 typed union）
- call -> handler 的静态映射（不依赖反射）
- 标准化错误码与错误响应（保证 Lua 侧体验一致）
- `list_methods` 聚合

建议命名/位置：
- `Protocol/BridgeModels.cs`（typed models + request/response）
- `Protocol/BridgeCallUnion.Generated.cs`（`IBridgeCall` union kind 列表；与 Lua `CALL_*` 同源）
- `Protocol/BridgeMessagePack.cs`（MessagePack-CSharp serializer options + request/response 编解码）
- `Dispatch/BridgeDispatcher.cs`（`Invoke` 入口）
- `Scripts/BridgeScriptCatalog.Generated.cs`（方法列表 + 映射表 + `list_methods`）
- `Dispatch/BridgeErrorCodes.cs`（错误码）

### Layer C：通用适配层（Common / Adapters）
职责：在“Lua 输入数据”与“算法所需数据结构/ASS 操作”之间做稳定转换，避免每个 handler 重写一遍。

建议拆出：
- `Common/StyleInfo.cs`：`styles` 的强类型结构（request 中直接携带，无需 handler 侧解析 map）
- `Common/AssTagOps.cs`：基于 `Mobsub.SubtitleParse` 的 tag block 编辑/剥离/提取（把现有 `AssSubtitleParseTagEditor`/`AssSubtitleParseTagStripper`/`AssClipQuadExtractor` 归拢到同一处）。
- `Ae/AfterEffectsKeyframes.cs`、`Ae/AeKeyframeDataFixer.cs`：AE 相关解析/修复逻辑（与具体 handler 解耦）。

> 注：这层的目标不是“把所有东西抽象成框架”，而是把重复且容易出错的 glue 逻辑集中。

### Layer D：脚本/功能层（Scripts：按脚本域拆文件）
职责：每个方法只做领域逻辑的 orchestrate：读取 options -> 调用算法 -> 调用 ASS 编辑适配 -> 生成 patch/result。

建议按域拆：
- `Scripts/Motion/*.cs`
  - `MotionAmoApplyHandler.cs`
- `Scripts/Perspective/*.cs`
  - `PerspectiveApplyClipQuadHandler.cs`
  - `PerspectiveApplyTagsFromQuadHandler.cs`
  - `PerspectiveApplyTagsFromClipQuadHandler.cs`
- `Scripts/Drawing/*.cs`
  - `DrawingOptimizeLinesHandler.cs`
- System：建议直接放在 `Scripts/BridgeScriptCatalog.cs`（`ping/list_methods`）。

每个 handler 文件建议包含：
- `const string MethodName = "..."`（或统一放 `BridgeMethods`）
- `BridgeMethodInfo` 描述
- `InvokeResult Handle(BridgeRequest, List<string>)`（或 `BridgeResponse`）
- 方法私有的 `Options`（`readonly record struct`/`sealed record` 均可）

## 3. partial 类 vs “每个 handler 一个类”的取舍

本次重构建议 **一步到位采用显式 Dispatcher + Handler**（不做 partial 过渡）。

原因：
- 你明确希望“重构一步到位”，且当前尚未进入 Git 管理阶段，可以承受一次性结构调整。
- handler 未来要同时服务 JSON/FFI 与托管直调（Next），强边界结构更可持续。

## 4. 关于 `AssNextGen.Core` 是否要合并

结论：**已合并**。`AssNextGen.Core` 代码已并入 `Mobsub.AutomationBridge` 的同一个 csproj，并统一到 `Mobsub.AutomationBridge.Core.*` namespace。

理由：
- 目前没有其它项目引用 Core，拆成独立 csproj 的收益暂时不大，反而增加跨项目引用、发布与组织成本。
- 合并后仍可通过 namespace + folder 隔离，保证算法层“看起来像独立库”，将来需要时再拆出来（结构迁移成本可控）。

合并方式（落地）：
- 把 `src/SimpleTools/AssNextGen.Core/**.cs` 移入 `src/SimpleTools/AutomationBridge/Core/**.cs`
- 算法代码统一为 `namespace Mobsub.AutomationBridge.Core.*`（便于在同一工程内分层）
- 移除 `AutomationBridge` 对 `AssNextGen.Core` 的 `ProjectReference`，改为同项目内直接编译
- 已将旧 `AssNextGen.Core.Tests` 的关键用例迁移到 `src/Test`（MSTest + FluentAssertions），旧 NUnit 测试工程不再保留源码。

## 5. `SubtitleParse` 的定位与建议调整

定位：`src/SubtitleParse` 是**字幕解析与编辑的权威实现**（尤其是 ASS tag block 的语义解析），Bridge 层不要重复写“字符串启发式解析”。

建议（可选的库侧增强）：
- 将当前 Bridge 内的以下能力沉到 `Mobsub.SubtitleParse.AssUtils`：
  - “在第一个 override block 中插入/替换 tags，并按 predicate 删除旧 tags”
  - “移除所有 override block 中满足 predicate 的 tags”
  - “从 clip drawing 提取 quad 的通用工具”（或把它作为 `AssTagFunctionValue` 的辅助解析）
- 提供一个更通用的 API 形态（示意）：
  - `AssOverrideBlockEditor.InsertOrReplaceInFirstBlock(lineUtf8, insertUtf8, shouldRemoveTag)`
  - `AssOverrideBlockEditor.RemoveTagsInAllBlocks(lineUtf8, shouldRemoveTag)`
  - 返回 `byte[]`/`ArrayBufferWriter<byte>` 以减少 string 往返（Bridge 再统一 UTF-8 -> string 或直接保持 UTF-8）

这样可以：
- 降低 Bridge 侧重复代码
- 避免未来多个工具项目各自实现一套 tag 编辑逻辑

## 6. 方法/协议的演进策略（避免破坏脚本生态）

### 方法命名约定
- 继续使用 `domain.verb_object`：
  - `motion.*`、`perspective.*`、`drawing.*`
  - 未来可扩展 `tags.*`（纯 tag 清理/改写）、`text.*`（字符串/Unicode 处理）、`ass.*`（脚本级别处理）

### Options 强类型化（减少 handler 内重复）
为每个方法定义 options，并集中解析/校验：
- 示例：`MotionApplyTsrOptions` 从 args 读取 `selection_start_frame/total_frames/reference_frame/apply_*`…
- 解析逻辑统一走 `ArgsReader`，在错误时输出一致的字段路径（`payload.args.xxx`）。

### Patch 语义保持稳定
保持 Lua 侧 `apply_patch` 简单：
- 以 `set_text` 为主（最稳）
- 一步到位新增 `splice_template`：用“模板行 + 插入 delta”表达拆分/插入，避免回传完整行结构
- 彻底移除 `set_line` 与旧 `splice(lines=BridgeLine[])`（不留过渡）

`splice_template` 设计要点（通用，供 motion/ruby/批处理共用）：
- 目标：让“拆分成 N 行”的场景只回传每行变化的 `time/text/layer`，其余字段全部继承模板，显著降低 payload 与 Lua 侧样板。
- 模板（template）：
  - 默认模板：`template_id=0` 表示“以当前 `subtitles[index]`（被替换/参考的原行）作为模板行”。这覆盖 motion 的绝大多数情况。
  - 自定义模板：op 内可携带 `templates[]`（若干“内联模板行”），用于生成和原行无关的多行（例如 ruby/注音脚本可能需要不同的 style/margin/actor）。
  - 模板行只包含“可写的 ASS 行字段”（class/layer/style/actor/effect/margin/comment/extra 等）。**不包含也不写回** `width/height/align/raw` 这类 “hint/调试字段”。
- 插入 delta（insert）：每个输出行只携带：`template_id + start_time + end_time + text_utf8 (+ layer 可选)`。
- 应用顺序：生成 patch ops 时建议按 `index` 降序（保持现有 motion 的策略），避免 `splice_template` 引起的索引漂移；Lua `apply_patch` 也以该约定实现。

### 编码格式（当前：MessagePack）

当前 bridge 使用“封包模式（envelope）”作为默认与主格式：
- 请求：固定 magic `MSB1`（4 bytes）+ MessagePack payload
- 响应：与请求保持一致（封包请求 -> 封包响应），避免 Lua 侧难以判定。

说明：
- 不通过“首字节猜测”判别编码；以 magic 为准。
- `.NET` 侧会优先走 MessagePack；JSON 不再作为常规传输 codec（仅保留给调试/配置等用途）。

## 7. 重构落地的里程碑（不改行为优先）

本次按“一步到位”的目标组织交付，但仍建议内部按阶段推进以降低风险：

### Phase 1：合并 Core + 重组目录/命名空间
- 合并 `AssNextGen.Core` 到 `AutomationBridge`（迁移到 `Mobsub.AutomationBridge.Core.*` namespace）
- 将 `AutomationBridge` 重组为 `Abi/Protocol/Dispatch/Common/Scripts/Ae/Core` 等目录

### Phase 2：Dispatcher + Scripts 重写落地（行为保持）
- 用静态方法表/字典完成 method 分发（无反射）
- 各方法拆到 `Scripts/*`，共享逻辑进入 `Common/*`
- 现有 method 名、返回结构、错误码保持不变

### Phase 3：引入 envelope + codec 抽象（MessagePack）
- 增加 envelope 解析/生成
- MessagePack 为主；JSON 仅用于调试/输出（Lua 侧转码/落盘）

### Phase 4：补齐测试与回归（重点）
- 算法类测试：已迁移 `AssNextGen.Core.Tests` 的关键用例到 `src/Test`，后续新增用例统一放在 `src/Test`。
- Bridge 侧测试：补“协议/错误/patch 生成”的基础覆盖（特别是 envelope 分支）

### Phase 5：脚本迁移（以 Aegisub 侧“薄脚本”为目标）
- 先迁移 `_ref/automation/autoload/a-mo.Aegisub-Motion.moon`：只保留 Apply/Revert（不带 trim/encoding），并将 AE/算法/批处理尽量下沉到 Bridge（NativeAOT）侧。
- 合并 `_ref/automation/autoload/z_fix_motion.lua`：Fix motion 算法内置到 `motion.amo_apply` 的 `fix` 选项中，Lua 侧只保留 UI/粘贴/复制。
- 提供可直接拷贝的示例脚本，放入 `src/SimpleTools/AutomationBridge/examples/`（以 `examples/mobsub.Aegisub-Motion.lua` 为主）。

## 8. 性能与可维护性准则（写法约束）
- NativeAOT 下避免反射与动态：method 表使用静态数组/字典注册。
- 避免 LINQ；优先 `for` 循环与预分配容量（`new List<T>(lines.Count)`）。
- 处理 ASS 文本时尽量以 UTF-8 byte 流为主，减少 `string` 拼接往返（必要时再落到 string）。
- 复用 `Mobsub.SubtitleParse` 的 pooled parser，避免重复解析。
- 算法代码与 glue 代码用 namespace 隔离（即使在同一 csproj 内也要保持“可拆分性”）。

## 9. 预期的目录结构（示意）

```
src/SimpleTools/AutomationBridge/
  Abi/
    Exports.cs
  Protocol/
    BridgeEnvelope.cs
    BridgeModels.cs
    BridgeCallUnion.Generated.cs
    bridge_calls_spec.json
    BridgeInvokeRequestV2.cs
    BridgeMessagePack.cs
    Utf8BytesReadOnlyMemoryFormatter.cs
  Dispatch/
    BridgeDispatcher.cs
    BridgeErrorCodes.cs
    BridgeHandlerResult.cs
  Common/
    StyleInfo.cs
    LuaGenAttributes.cs
  Scripts/
    BridgeScriptCatalog.cs
    BridgeScriptCatalog.Generated.cs
    Abstractions/
      BridgeCallHandler.cs
    Motion/
      MotionAmoApplyHandler.cs
    Perspective/
      PerspectiveApplyClipQuadHandler.cs
      PerspectiveApplyTagsFromQuadHandler.cs
      PerspectiveApplyTagsFromClipQuadHandler.cs
    Drawing/
      DrawingOptimizeLinesHandler.cs
  Ae/
    AfterEffectsKeyframes.cs
    AeKeyframeDataFixer.cs
  Core/
    (Mobsub.AutomationBridge.Core.*)
```

## 10. 优化方案（P0-P2）

> 说明：P0-P2 是在当前“协议已切到 MSB1 + MessagePack、Lua 尽量薄”的基础上继续做的性能/可维护性优化。  
> 目标不是再发明一套格式，而是把“跨边界交互”收敛为 **一套通用协议 + 一套通用 patch 语义 + 一套权威 ASS tag AST/编辑/优化实现**。

### P0：传输与协议完全收敛（只有一种 on-wire）
- on-wire 只保留：`MSB1` envelope + MessagePack（request/response 都一致）。
- 文本/大字段统一：`*_utf8`（UTF-8 bytes）；协议层尽量不出现 `string`（除日志/错误）。
- JSON 仅用于 Lua 侧配置/extra/debug 输出；不再作为常规传输 codec。
- 验收：Lua 侧无需关心 codec 分支；Bridge 侧无需兼容多种“请求格式猜测”。

当前进度（2026-02-13）：
- [x] Bridge 侧强制要求 `MSB1` envelope；缺失时返回 `ErrDecode`（不再兼容“裸 MessagePack”）。
- [x] Bridge 响应总是封包（Lua 侧统一解码路径）。
- [x] Lua 请求编码固定封包（`_encode_request`）。
- [x] Lua 响应解码已严格要求 `MSB1` envelope（移除“无封包输入”的 fallback）。
- [~] `*_utf8` 已用于 motion 相关大字段（`text_utf8/main_data_utf8/clip_data_utf8`）；perspective 的 `ae_text` 等仍为 string，后续可按需迁移为 `*_utf8`。

### P1：patch 语义收敛为 `set_text` + `splice_template`（一步到位）
- 删除 `set_line` 与旧 `splice(lines=BridgeLine[])`；不留过渡。
- 新增 `splice_template`：
  - 只回传每个输出行的最小 delta（通常 `start_time/end_time/text_utf8/(layer 可选)`）。
  - 其它可写字段通过模板继承（默认模板=被替换的原行；必要时用内联模板支持 ruby/注音等“生成与原行无关”的插入）。
  - `width/height/align/raw` 属于 request hint/调试字段：可以作为输入参与算法，但不属于 patch 可写内容。
- 验收：motion 拆分时 response payload 显著减小；Lua `apply_patch` 逻辑仍保持简单（模板复制 + delta 覆盖）。

当前进度（2026-02-13）：
- [x] patch ops 已收敛为 typed union：`set_text` + `splice_template`。
- [x] Lua `apply_patch` 已支持 `splice_template`（模板继承 + delta 覆盖；按 index 降序应用）。
- [x] motion handler 已按“最小 delta”生成 `splice_template`（仅 `start_time/end_time/text_utf8`；必要时回退 `set_text`）。

### P2：ASS tag 处理核心下沉到 `SubtitleParse`（Bridge 只 orchestrate）

设计原则：
- 不再在 AutomationBridge 内维护“字符串扫描 + 手写插入/删除/替换”的第二套 override 解析逻辑。
- `src/SubtitleParse/AssText` 的 `AssEventTextRead/AssTagSpan/AssTagValue/AssEventTextEdit/AssTokenizedText` 视为轻量 AST/typed token 层（低分配），并作为所有工具的真源。

交付物（SubtitleParse 侧新增/整理 API）：
- 统一的 override/tag 重写入口（供 motion/perspective/drawing/未来脚本复用）：
  - 以一次 parse 的 `AssEventTextRead`/`AssEventTextEdit` 为基础，支持：
    - “在第一个 override block 中插入/替换 tags，并按 predicate 删除旧 tags”
    - “替换指定 tag（含 function tag）的 payload，且能递归重写 `\\t(...)` 内 payload”
    - “删除空 override block（`{}`）”
- 统一的 tag 优化器（去冗余/同值不输出）：`AssOverrideTagOptimizer`
  - handler 在“勾选但结果未变”时不生成冗余 tag（表驱动，复用 tag spec/typed value）。

AutomationBridge 侧改动原则：
- motion/perspective/drawing 的 handler 与算法只负责“算出目标值/策略”，所有 ASS override 编辑由 SubtitleParse API 完成。
- motion 的热路径目标：每行最多 parse 一次；一个 `AssEventTextEdit` 内完成多处替换，避免重复遍历。

验收：
- AutomationBridge 内部的 tag 编辑样板显著减少（减少维护面）；行为边界（尤其 `\\t(...)`）与 SubtitleParse 统一；性能回归通过测试/基准。

当前进度（2026-02-13）：
- [x] `SubtitleParse` 已提供通用重写入口：`AssOverrideTagRewriter`（支持递归改写 `\\t(...)` payload + 移除空 `{}`）。
- [x] motion 多处 tag 改写已迁移到 `RewriteKnownTags`（单次 parse 覆盖一类 tag，减少重复遍历/样板）。
- [x] 增加 transform payload 的最大递归深度限制（避免极端输入导致栈溢出）。
- [x] `AssOverrideTagOptimizer`（“同值不输出/去冗余”）已落地（并覆盖 `\\t(...)` payload）。
- [x] motion 热路径已收敛为“每行最多 parse 一次”（linear/nonlinear 都改为单次 `AssEventTextEdit.Parse` 批量改写）。

## 11. 架构审查后续计划（2026-02-13）

本节不讨论 `motion/perspective/drawing` 的具体算法细节，仅针对 **AutomationBridge 作为“经典 Aegisub ↔ .NET（NativeAOT）桥”本体**，在“高性能、易用、易扩展、分层清晰”上的进一步巩固与演进。

### 11.1 目标与约束

目标：
- 统一“方法层”目录名为 `Scripts/`（不再使用/维护 `Handlers/` 作为方法层目录）。
- Lua 脚本继续保持“薄”：收集必要字段 → invoke → apply patch；复杂解析/算法/批处理尽量下沉到 .NET。
- 尽可能解耦：Lua 脚本/GUI 层、交互层（Bridge/协议/分发）、C# 核心算法层职责清晰、依赖方向单向。
- 后续维护以“核心算法层”为主：交互层尽量稳定，且尽可能由协议 spec 自动生成。

约束（必须保留）：
- ABI 稳定：`mobsub_abi_version/mobsub_invoke/mobsub_free` 不轻易变化。
- Bridge ↔ Lua 交互 **只保留**：`MSB1` envelope + MessagePack（request/response 对称；不再引入 JSON 作为传输 codec）。
- NativeAOT：避免反射/动态加载；方法表静态注册（或生成）。
- 向后兼容优先：Lua 侧 API 尽量不破坏；patch 语义保持稳定（`set_text` + `splice_template`）。

推荐的“分层/依赖”目标形态：
- Lua 脚本/GUI 层：`mobsub_bridge.lua` + `examples/*`（只负责收集/调用/应用 patch；不承载算法）。
- 交互层（Interop）：`Abi/Protocol/Dispatch/Scripts`（协议模型、编解码、method 分发、handler 编排；尽量自动生成）。
- 核心算法层（Core）：`Core/*`（算法与 ASS/解析工具；不依赖 `Protocol/Dispatch/Lua`）。

### 11.2 里程碑（P3-P7）

#### P3：结构一致性与可维护性（低风险）
- [x] 目录/命名收敛：统一“方法层”目录名为 `Scripts/`，并同步更新文档中的路径示例（移除 `Handlers/*` 的陈述）。
- [x] 清理冗余：删除长期为空的目录（或补齐用途说明），避免误导后续开发者。
- [x] `BridgeInvokeAdapter`/Catalog 相关代码拆分为 `partial` 多文件（按 domain 或按 method 分组），降低单文件膨胀与冲突概率。

验收：
- 文档/目录结构与实际代码一致；新增 method 的常规改动面清晰可控。

#### P4：MsgPack-only（Lua 仍易用，on-wire 强类型/可生成）

目标：Bridge ↔ Lua 交互只保留 MessagePack（封包 `MSB1` + payload），并让 on-wire payload 的结构可由 spec 驱动生成。

计划：
- [x] 规范化传输：request/response 均为 `MSB1` envelope + MessagePack（不再引入/恢复 JSON 传输或“裸 MessagePack”猜测）。
- [x] Lua 侧保持“写法易用”：脚本作者仍传入 map table（`snake_case`），但 glue 在发送前 **自动 pack** 成“固定数组结构”（with_hole）。
- [x] Lua glue 拆分为两层：`mobsub_bridge.lua`（恒定/手写，负责 FFI/封包/patch）+ `mobsub_bridge_gen.lua`（自动生成，负责协议打包与 method→call 映射）。
- [x] method→kind 映射由 `bridge_calls_spec.json` 统一定义，并由 `tools/AutomationBridgeProtocolGen` 生成到 `Protocol/BridgeCallUnion.Generated.cs` 与 `mobsub_bridge_gen.lua`（避免手写漂移）。

验收：
- 交互协议只剩一种：`MSB1` + MessagePack；Lua 侧调用方式不变（仍是 map 风格）。
- on-wire 的 `context/lines/args` 变为数组后，C# 侧可直接使用 source-generated formatter 反序列化（减少 map-key 分配与手写解码）。

#### P5：交互层自动生成（Spec single-source）

目标：交互层（method kind、method 列表/分发、Lua pack 代码）尽可能从 `bridge_calls_spec.json` + C# 模型自动生成；人工维护聚焦在 Core 算法与 handler 的业务编排。

计划：
- [x] on-wire 模型以 C# 为权威：`Protocol/BridgeModels.cs` / `Common/StyleInfo.cs`（`[Key(n)]` 保持 append-only）。
- [x] calls spec 仅维护 calls：`Protocol/bridge_calls_spec.json`（`kind/method/call_type/description`）。
- [x] 生成 C#：`Protocol/BridgeCallUnion.Generated.cs`（`IBridgeCall` union kind）+ `Scripts/BridgeScriptCatalog.Generated.cs`（方法表 + `list_methods`）。
- [x] 生成 Lua：`mobsub_bridge_gen.lua`（calls spec + 反射 C# 模型，生成 pack + `make_request`）。

验收：
- 新增/调整协议字段时，优先改 C# on-wire 模型（必要时更新 calls spec）→ 运行生成器 → 只补少量手写 glue（尽量不改多处 switch/重复解码代码）。

#### P6：Core/Interop 解耦（核心算法可独立维护）
- [x] Core 算法层不依赖 `Protocol/Dispatch/Scripts/Lua`（不直接使用 `BridgeLine`/patch op 等交互 DTO）。
- [x] handler 负责“协议模型 ↔ Core 输入/输出模型”的映射，并把 Core 结果转换为 patch（`set_text/splice_template`）。
- [x] 协议层（`Protocol/*Generated.cs`）不再引用 Core 类型（将可复用 options/models 迁移为协议内类型或单独 contracts 层）。

验收：
- Core 算法层可在脱离 Bridge 交互的前提下单测/复用（仅依赖必要的解析/数学库）。
- 交互层改动（协议/patch）不会反向污染 Core 代码结构。

#### P7：维护体验（新增 method 的最小改动面）
- [x] 新增 method 的“必改点”收敛为：calls spec（方法+kind） + C# on-wire 模型（call+args） + handler（算法编排） + 测试；其余由生成器产出。
- [x] 为脚本侧提供 `list_methods` 与必要的能力探测，方便灰度与兼容旧 DLL。

### 11.3 测试与回归
- [x] 为每个新增/改动的解码分支补单测：packed（数组）形态、缺字段/错类型、超长字段、未知 key。
- [x] 增加“桥接层”回归测试：request → dispatcher → handler → patch（至少覆盖 `set_text` 与 `splice_template`）。
- [x] （可选）补最小基准：对比优化前后的分配/耗时（可用简单 Stopwatch+GC 计数，不强制引入 BenchmarkDotNet）。

### 11.4 风险与回滚策略
- 如果 packed 形态出现兼容问题，优先在 Lua glue 侧修复/回滚 pack 逻辑（脚本作者写法保持不变）。
- 生成器输出必须可重复（deterministic）；生成失败/冲突时允许临时回退到上一版生成产物，但不得手改生成文件。
- Core/Interop 解耦建议分阶段做：先新增 Core 输入/输出模型与映射，稳定后再逐步迁移依赖，避免一次性大重构影响现有方法。

## 12. Motion 输出策略优化（2026-02-14）

本节基于前述架构分层已落地的前提，对 `motion.amo_apply` 的**输出策略**做进一步演进：在保持“Lua 脚本薄”的同时，降低 per-frame 产线数、提升性能与可用性，并为后续维护把复杂度尽量收敛到 Core 算法层。

### 12.1 背景与目标

现状（2026-02-14）：
- 输出策略由 `main.linear_mode` 控制：`force_nonlinear` 为 per-frame；其余模式尝试线性/分段线性，不满足约束则回退 per-frame。
- per-frame 输出在长段 tracking 上会生成大量行；即便做了“相邻同文本合并”，仍可能偏重。

目标：
- 默认更偏向高性能与可读输出：能线性就线性，不能就分段线性，仍不行再回退 per-frame。
- 交互层只扩展少量 options 字段；其余行为尽量由 Core 算法内部决定。

明确不做（先暂缓）：
- `tags` 级别的“改写 tags/改写 block”专项优化（另开 domain 或 method）。
- 多行并行/批量输出策略（先不做并行生成与合并策略）。

### 12.2 新增 options（协议 & Lua）

新增（挂在 `args.main` 下，保持 snake_case）：
- `linear_mode`：输出策略（枚举/整数）。**默认：`auto_linear_pos`**。
  - `force_nonlinear`：强制 per-frame（现有行为）。
  - `force_linear`：强制线性（若遇到不支持的行/约束则回退 per-frame）。
  - `auto_linear_pos`：仅基于 `pos` 路径误差判断是否可走线性；不可则回退 per-frame。
  - `auto_segment_pos`：基于 `pos` 路径误差进行**自适应分段线性**（RDP 类分段）；每段输出一行 `\\move+\\t`；若分段效果不佳则回退 per-frame。
- `segment_pos_eps`：`auto_segment_pos` 的误差阈值（像素）。`<=0` 表示使用自适应默认值：`min(resX,resY)/1000`。
- `pos_error_mode`：误差计算模式（枚举/整数）。默认 `full`。
  - `full`：误差基于完整 `PositionMath`（包含 scale/rot 对 pos 的影响）。
  - `ignore_scale_rot`：误差仅看平移（忽略 scale/rot 对 pos 的影响）。

### 12.3 选择/回退规则（Core）

线性/分段线性仍遵循 a-mo 兼容约束（避免已知不支持场景）：
- 当 `main.origin=true` 且行内已存在 `\\org` 时，不走线性/分段线性（回退 per-frame）。
- 当存在 clip data 且行内含 clip（`\\clip/\\iclip`）需要重写时，不走线性/分段线性（回退 per-frame）。

`auto_*` 的判定只基于 `pos`（不检查 `scale/rot/clip` 的拟合误差），优先性能与稳定实现。

### 12.4 基准与验收

计划：
- [x] 协议扩展：更新 `bridge_calls_spec.json`（calls）与 C# on-wire 模型，并运行 `tools/AutomationBridgeProtocolGen` 同步生成 C# union/method 表与 Lua pack 代码。
- [x] Core 落地：在 `AmoMotionApplier` 增加 `auto_linear_pos/auto_segment_pos` 的判定与分段输出。
- [x] Lua 示例更新：`examples/mobsub.Aegisub-Motion.lua` UI 改为 `linear_mode`，默认 `auto_linear_pos`。
- [x] 回归测试：补齐 `src/Test` 覆盖四种模式的最小用例（至少覆盖：auto 走 set_text；force_nonlinear 走 splice_template；auto_segment_pos 能减少行数）。
- [x] 手动 benchmark：新增/扩展 `AutomationBridgeBenchmarks`，对比 per-frame vs auto/segment 在长 tracking 上的耗时与分配。

验收：
- 默认（`auto_linear_pos`）在“线性 tracking”上输出单行 `set_text`（或单行 `splice_template`），不再无谓 per-frame。
- `auto_segment_pos` 在“明显非线性 pos”上能显著减少输出行数（相较 per-frame），且误差受阈值控制。
- 与现有 per-frame 行为相比，强制模式 `force_nonlinear` 可保持旧行为用于对照与回退。
