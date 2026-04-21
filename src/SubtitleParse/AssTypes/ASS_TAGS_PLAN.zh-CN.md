# AssTag / AssTagSpec（source generator）计划与进度

日期：2026-02-11  
范围：`src/SubtitleParse/AssTypes/AssTags.cs`、`src/SubtitleParse.TagGenerator/AssTagRegistryGenerator.cs`、（联动）`src/SubtitleParse/AssText/*`

> 本文用于跟踪 “ASS override tags 的 spec/generator 体系” 的进度与待办，便于观察是否已满足：统一解析、统一校验、统一优化/编辑、AOT 友好、高性能。

---

## 1. 重要说明：tag 编写顺序是否影响匹配？

结论：**不需要按前缀长度排序**（例如不必把 `qm`/`q`、`fscx`/`fsc` 这类“长短前缀关系”特意排顺序）。

原因：
- `AssTagRegistry.TryMatch(...)` 是 generator 生成的 **trie 匹配**，匹配过程中会记录最后一次命中的 terminal，因此天然得到“**最长命中**”。  
  - 输入 `fscx`：会先命中 `fsc`，继续向下命中 `fscx`，最终返回 `fscx`。
- `AssTag` enum 成员的书写顺序不参与匹配逻辑；generator 只要求：
  - tag `name` **唯一**且 **ASCII**；
  - `[AssTagSpec]` 字段齐全（`name/value_kind/tag_kind/function_kind` 等）。

例外（需要显式考虑的不是“顺序”，而是 **dialect gating + 前缀冲突保护**）：
- 当 VSFilterMod 关闭时，像 `\\blend1` 这种 tag 不能被误拆成 `\\b` + `lend1`。  
  - 目前做法：registry 在 match 时会同时返回 `gated_matched_length`（被 dialect gate 掉的更长命中长度），scanner 仅在“确实存在更长 gated 命中”时才拒绝短前缀命中，避免破坏 `\\iabc` 等“无效值按 0 处理”的兼容行为。

---

## 2. 当前状态（已完成）

- [x] `AssTextOptions`（dialect/profile/strictness）贯通 parser/scanner/validator
- [x] VSFilterMod tags：加入 `AssTag`，并用 `AssTagKind.IsVsFilterMod` 标注；仅 `mod_mode` 识别
- [x] 前缀冲突保护：仅当存在更长 gated 命中时才拒绝短命中（例如 `\\blend1`）
- [x] mod tag 诊断：识别到 mod tag 输出 `ass.override.vsfiltermodTag`
- [x] overload：
  - [x] `\\pos(x,y,z)`：仅 `mod_mode` 视为有效（`ass.override.vsfiltermodOverload`）
  - [x] `\\fsc<scale>`：非 mod 模式提示 ignored（`ass.override.nonStandardPayload`）；mod 模式启用并告警（`ass.override.vsfiltermodOverload`）
- [x] VSFilterMod 函数 tags 基础形状校验（distort/jitter/mover/moves3/moves4/movevc/vc/va/lua/img）
- [x] 单元测试覆盖 dialect gating / prefix-conflict / overload

---

## 3. 待办（按优先级）

### P0：完善 spec + generator 的“完整约束/特殊规则”
- [x] 为所有 tag 补齐并统一约束（range/enum/keyword/特殊规则）到 `AssTagSpec` + generator 表驱动
  - [x] `BlendMode`：关键字集（`over/add/sub/mult/scr/diff/rsub/isub`）与数值模式的合法性（VSFilterMod）
  - [x] `rnds`：seed 的解析规则（代码按 hex 读）与诊断
  - [x] `\\1vc..\\4vc` / `\\1va..\\4va`：颜色/alpha 的规范化与诊断策略（允许非 canonical；输出 normalize 建议）
  - [x] `\\lua(...)`：形状 + “method/args” 的最小约束（至少非空）

### P0：补齐 “完整 ASS tag 集合”
- [x] 以 “能支撑 analyzer/optimizer/completion/未来 renderer” 为验收，补齐 `AssTag` 与 `[AssTagSpec]` 覆盖面（VSFilter baseline + VSFilterMod 扩展）
  - [x] 逐项对照 VSFilter / libass / VSFilterMod 代码清单，补齐缺失项（含 legacy/obsolete replacement）
  - [x] 对每个 tag 标注 `tag_kind`（render-first/latest/animateable/ignored 等）

### P1：dialect/profile/strictness 的行为矩阵测试
- [x] 统一整理行为矩阵（vsfilter / libass_0_17_4 / vsfiltermod × compat/normal/strict）
- [x] 将关键差异固化为测试用例（避免后续 refactor 回归）

### P1：把更多逻辑“从手写 validator 迁移到表驱动”
- [x] generator 生成更多元数据（keyword blob、special_rule id）
- [x] validator 仅保留：
  - [x] function tag 的结构解析
  - [x] 少量 truly-special 的兼容逻辑（例如 `\\a` legacy align mapping、`\\t` nested payload）

### P2：对外 API（token 层 + typed 层）
- [x] 增加/整理 token/visitor API：unknown/invalid 也能枚举（为统计/重写/格式化服务）
  - [x] `AssOverrideTagScanner` / `AssOverrideTagToken`（override payload 级别扫描）
  - [x] `AssEventTextRead.TryCreateTagBlockScanner(...)` / `TryGetFirstOverrideTagScanner(...)`（复用 parse options）
- [x] typed tag 层与诊断信息的绑定（给优化器/规范化工具直接复用）
  - [x] `AssOverrideTagValueParser`（token -> `AssTagValue`）
  - [x] `AssTagValue.TryGet<T>`（便于 typed value 读取）
  - [x] `AssOverrideTagValidator.ValidateOverrideBlocks(AssEventTextRead, ...)`（减少样板并复用 options）

---

## 4. 关联文档

- 总体重构计划：`src/SubtitleParse/AssText/REFACTOR_PLAN.zh-CN.md`
- 设计概览：`src/SubtitleParse/AssText/DESIGN.zh-CN.md`
