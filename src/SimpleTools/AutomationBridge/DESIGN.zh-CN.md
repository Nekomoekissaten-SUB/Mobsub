# Mobsub.AutomationBridge（经典 Aegisub ↔ .NET）设计说明

目标：让经典 Aegisub（C++/LuaJIT）里的 Lua 脚本尽量“变薄”，把复杂的解析/算法/批处理逻辑迁移到 .NET（NativeAOT）侧实现，同时为未来 Aegisub.Next 的托管内置模块预留一致的 API 形态。

本项目仅解决 **经典版** 的跨边界调用与最小可用的处理器示例（`motion` / `perspective`）。

现阶段（2026-02）：Aegisub 侧示例脚本与流程主要保证 `motion.amo_apply`（Aegisub-Motion bridge 版）可用；`perspective` / `drawing` 相关方法仍在持续优化中（接口与行为可能会有小幅调整）。

## 为什么不是“很多 C API + struct/char**”

LuaJIT FFI 能调用 C API，但当接口扩展成大量 `struct`、`char*`、`char**` 时，常见问题会指数级增长：

- 编码不一致（UTF-16/UTF-8 混用），`ffi.string()`/`Marshal.PtrToString*` 读写错乱
- NUL 终止/长度管理不严谨（越界写、截断）
- 大量小字符串跨边界分配/释放（性能与内存碎片）
- 协议演进困难（加字段就要改 struct，Lua/C# 两边一起改）

因此本设计采用 **固定 ABI 的极小 C API + blob（byte*+len）**：

1) ABI 稳定（导出函数几乎不变）
2) 内容格式可替换（JSON → MessagePack → 自定义二进制，都不影响 ABI）
3) 可选字段与 schema 版本，便于“只传必要数据”

## ABI（导出函数）

- `int mobsub_abi_version()`
- `int mobsub_invoke(uint8_t* req, int reqLen, uint8_t** resp, int* respLen)`
- `void mobsub_free(void* p)`

约定：

- `req/resp` 均为 **blob（byte*+len）**，当前实现为：`MSB1` envelope（4 bytes magic）+ MessagePack payload。
- `req/resp` 必须带 `MSB1` envelope；Bridge 总是返回封包响应（便于 Lua 侧一致解码）。
- `mobsub_invoke` 返回值为错误码；同时返回 `BridgeResponse`（MessagePack 编码，封包请求 -> 封包响应），便于 Lua 打印与诊断。
- `resp` 由 .NET 侧分配（`NativeMemory.Alloc`），Lua 侧用 `mobsub_free` 释放。

## Schema（当前，on-wire）

> 注意：Lua glue（`mobsub_bridge.lua`）对脚本暴露易用 API：  
> `invoke_method(method, context, lines, args)`（推荐 `snake_case` 的 table）。  
> 其中 `method -> call(union)` 的打包逻辑由 `mobsub_bridge_gen.lua` 自动生成；脚本作者仍然可以传 map 风格的 table，
> glue 会在发送前 pack 成固定数组结构（typed arrays + unions）。
>
> 兼容/探测：Lua glue 还提供 `abi_version()`、`list_methods()`、`has_method(name)` 等能力探测 API，便于脚本对旧 DLL 做灰度兼容。

### Request（BridgeRequest）

- 形态：`[schema_version, call]`
- `schema_version`：目前固定为 `1`
- `call`：union 数组：`[kind, payload]`
  - `kind=0`：`ping`：payload `{}`（空数组）
  - `kind=1`：`list_methods`：payload `{}`（空数组）
  - `kind=2`：`motion.amo_apply`：payload `[context, lines, args]`
  - `kind=3`：`perspective.apply_clip_quad`：payload `[context, lines, args]`
  - `kind=4`：`perspective.apply_tags_from_quad`：payload `[context, lines, args]`
  - `kind=5`：`perspective.apply_tags_from_clip_quad`：payload `[lines, args]`
  - `kind=6`：`drawing.optimize_lines`：payload `[lines, args]`

说明：
- 文本字段（`text_utf8`、`main_data`/`clip_data` 等）以 **UTF-8 bytes（原样 Lua string）** 传入；Lua 的 MessagePack 默认编码为 `str`，C# 侧解码同时支持 `str/bin`。
- `method` 字符串只存在于 Lua glue 的便捷 API；on-wire request 不再携带 method string（减少分配与解码分支）。

### Response（BridgeResponse）

- 形态：`[ok, error, logs, patch, result, methods]`
- `patch`：`[ops]`，其中每个 op 为 union：`[kind, payload]`（一步到位不留过渡）
  - `kind=0`：`set_text`：`[0, [index, text_utf8]]`
  - `kind=1`：`splice_template`：`[1, [index, delete_count, templates, inserts]]`
    - `template_id=0` 表示“以 `subtitles[index]`（删除前的原行）作为模板行”
    - `templates`：可选的“内联模板行”数组（id 从 1 开始）
    - `inserts`：输出行列表；每行只携带 `template_id + start_time + end_time + text_utf8`

说明：
- `splice_template` 的目标是让拆分/插入场景只回传变化字段（通常是 time/text），其余字段都继承模板，避免回传完整行结构。
- `width/height/align/raw` 属于输入 hint/调试字段：可出现在 request 的 `lines`（供算法使用），但 **不属于 patch 的可写字段**。

## 新增方法流程（方法分发 / v2-only）

1) C# 协议模型：在 `Protocol/BridgeModels.cs`（以及需要时 `Common/StyleInfo.cs`）新增/更新 on-wire record/enum（`[MessagePackObject]` + `[Key(n)]`；Key 只允许 append，禁止改号）。
2) Spec（calls）：在 `Protocol/bridge_calls_spec.json` 增加/更新 call（`kind/method/call_type/description`）。
3) 生成：运行 `tools/AutomationBridgeProtocolGen`，更新：
   - `Protocol/BridgeCallUnion.Generated.cs`（`IBridgeCall` 的 union kind 表）
   - `Scripts/BridgeScriptCatalog.Generated.cs`（方法表 + `list_methods`）
   - `mobsub_bridge_gen.lua`（Lua pack + `make_request`）
4) C#：实现新的 handler（参考 `Scripts/*/*Handler.cs`），输入为某个 `*Call` record，输出 `BridgeResponse`（通常带 `patch`）。
5) Lua：脚本侧仍调用 `mobsub.invoke_method("your.method", ctx, lines, args)`（map 风格）；实际打包由 `mobsub_bridge_gen.lua` 负责。
6) 测试：在 `src/Test/AutomationBridgeProtocolTests.cs` 补单测（decode + dispatcher→handler→patch 行为）。

## Lua pack 生成规则（C# model 驱动）

`mobsub_bridge_gen.lua` 由两部分输入生成：
- `Protocol/bridge_calls_spec.json`：提供 method→kind 映射与 method 列表（决定 `CALL_*` 常量与 `_make_call` 分支）。
- 已编译的 C# MessagePack 模型：字段顺序/类型由 `[Key(n)]` 决定；Lua pack 的行为由 `Common/LuaGenAttributes.cs` 的属性控制（例如 `LuaPackMode/LuaKey/LuaAltKeys/LuaDefault/LuaMin/LuaEmptyStringAsNil`）。

说明：生成器会先写入 C# 侧的 `IBridgeCall` union 与 method 表，然后 build `Mobsub.AutomationBridge` 并反射模型来生成 Lua pack 代码（避免维护一份大 JSON 的字段 spec）。

### 规则摘要

- **key 映射**：默认 `C# 属性名 -> snake_case`；可用 `[LuaKey(\"...\")]` 覆盖；`[LuaAltKeys(\"a\",\"b\")]` 作为兼容/别名输入。
- **默认值**：
  - 优先读取 `[LuaDefault(...)]`；
  - 否则读取 C# record 主构造参数的可选默认值；
  - 其余按类型默认（0/false/\"\"/nil/{}）。
- **类型模式**：`[LuaPackMode]`：
  - `Default`：非 table 输入也会 pack（使用默认值）。
  - `Nilable`：非 table 输入直接返回 `nil`（常用于数组元素/可空输入）。
  - `Strict`：非 table 或校验失败返回 `nil`；numeric 可配合 `[LuaMin]` 做最小值校验。
- **UTF-8 bytes**：`ReadOnlyMemory<byte>` 对应 Lua string；可用 `[LuaEmptyStringAsNil]` 把 `\"\"` 转成 `nil`（例如 `clip_data`）。
- **容器**：
  - `T[]`：按 `ipairs` 逐元素 pack。
  - `Dictionary<string,string>`：过滤非 string 键值。
  - `Dictionary<string,T>`：过滤非 string/空 key，逐 value pack。

> 这些 Lua 生成属性只影响 `mobsub_bridge_gen.lua` 的打包逻辑，不影响 C# 的 MessagePack 模型本身。

## 目前提供的示例方法

> 说明：`motion` 域仅保留 `motion.amo_apply` 作为稳定主接口；Fix motion 通过 `fix` 选项内置。

### `motion.amo_apply`

- 用途：Aegisub-Motion（`a-mo`）的 bridge 版 Apply（不含 trim/encoding）。
- 行为概览（C# 侧主要链路）：
  - 参数解析：读取 `main_data`/`clip_data`（UTF-8 bytes）、选区帧信息（`selection_start_frame/total_frames/frame_ms`）、样式表（`styles`），以及 `main/clip/fix` 选项。
  - 可选 Fix：当 `fix.enabled=true` 且数据看起来是 AE Keyframe Data 时，对 `Position/Scale/Rotation` 做阈值平滑（移植自 `z_fix_motion.lua`）。
  - 数据解析：将 `main_data/clip_data`（UTF-8 bytes）解析为 `AmoData`（TSR 或其它类型；空数据视为 None）。
  - 逐行预处理（`AmoLinePreprocessor`）：
    - 必要时补 `\pos` / 缺失的 scale/border/shadow/rotation 标签
    - token 化 `\t(...)`，避免在 transform 内部误改
    - 需要时把 `\fad` 粗暴改写为 alpha transform（贴近 a-mo 行为）
    - clip 规范化（可选 rect->vect）
    - extradata：Lua 侧在调用前通过 `ensure_extradata` 写入 `extra["a-mo"] = { uuid, original_text }`，patch 的模板继承会保留该字段（用于 Revert）
  - 逐行应用（`AmoMotionApplier`）：
    - 非 `dialogue` / comment / 缺帧的行直接跳过（不产生 patch）
    - 将行的绝对帧区间映射到 tracking 相对帧区间
    - 输出策略由 `main.linear_mode` 控制（默认 `auto_linear_pos`）：
      - `force_nonlinear`：按帧拆成多行（每帧一行，时间来自 `frame_ms`），并对每帧重写 `\pos/\org/scale/rot/clip`。
      - `force_linear/auto_linear_pos`：满足 a-mo 兼容约束时走线性输出（单行 `\move + \t`），否则回退 per-frame。
      - `auto_segment_pos`：满足约束时走自适应分段线性（每段一行 `\move + \t`；误差阈值可由 `segment_pos_eps` 控制），否则回退 per-frame。
    - 合并相邻且文本完全相同的拆分行（减少行数）
- 输入参数（`payload.args`）：
  - `main_data`：主 motion 数据（AE Keyframe Data 或 shake_shape_data 或文件内容；以 UTF-8 bytes 传输）
  - `clip_data`：可选，单独用于 `\clip` 追踪（以 UTF-8 bytes 传输）
  - `selection_start_frame` / `total_frames` / `frame_ms`：必填（`frame_ms` 长度必须为 `total_frames+1`）
  - `styles`：可选，样式表（用于缺失标签时的默认值）
  - `main` / `clip`：选项对象（仅支持 `snake_case` 键名，例如 `x_position`、`start_frame`、`rc_to_vc`）
    - `main.linear_mode`：输出策略（默认 `auto_linear_pos`）
    - `main.segment_pos_eps`：`auto_segment_pos` 误差阈值（像素；`<=0` 表示使用自适应默认 `min(resX,resY)/1000`）
    - `main.pos_error_mode`：误差模式（默认 `full`；可选 `ignore_scale_rot`）
  - `fix`：可选对象（仅支持 `snake_case`：`apply_main` / `apply_clip` / `round_decimals`）
- 输出：`patch.ops`（主要为 `splice_template`），用于替换/插入拆分后的行。
- Revert：Lua 侧可用 `revert_by_extradata(subtitles, selected, "a-mo")` 合并同 uuid 的拆分行并恢复 `originalText`。

### `perspective.apply_clip_quad`

- 解析 AE `Effects ... CC Power Pin-0002/0003/0005/0004`（常见）或 `0004..0007`（兼容）四个角点（同一组 effect group）
- 生成 `\\clip(m x1 y1 l x2 y2 l x3 y3 l x4 y4 l x1 y1)` 并写入 override block（同时移除已有 `\\clip(...)`）
- 标签定位/移除基于 `Mobsub.SubtitleParse`（避免用字符串启发式替换 `\\clip`）

### `perspective.apply_tags_from_quad`

- 输入同上：AE CC Power Pin 的四个角点（quad）
- 在每行的第一个 override block 中插入/替换一组透视标签：
  - `\\an`、`\\org(...)`、`\\pos(...)`、`\\frz`、`\\frx`、`\\fry`、`\\fscx`、`\\fscy`、`\\fax`、`\\fay`
- 数学逻辑是对 `arch.Perspective.tagsFromQuad` 的 C# 移植（保证脚本生态的可对照性）。
- 标签定位/移除基于 `Mobsub.SubtitleParse`（会同时清理 `\\fr`/`\\a`/`\\fsc` 等等易遗漏别名/组合标签）

参数（`payload.args`）：

- `ae_text`：必填
- `effect_group`、`frame`：同 `apply_clip_quad`
- `width` / `height`：可选（全局默认值）；若未提供则要求每行传 `line.width` / `line.height`
- `align`：可选（全局默认值，缺省 `7`）
- `org_mode`：可选（缺省 `2`）
  - `1/0`：保持传入的 `origin_x/y`
  - `2`：将 `\\org` 设为 quad 的对角线交点（推荐默认）
  - `3`：尝试选择 `\\org` 以让 `\\fax` 尽量接近 0（高级）
- `layout_scale`：可选（缺省 `1`）；用于匹配 PlayRes/LayoutRes 的缩放模型
- `precision_decimals`：可选（缺省 `3`）
- `origin_x` / `origin_y`：可选（当 `org_mode=1/0` 时使用）

### `perspective.apply_tags_from_clip_quad`

- 从每行第一个 override block 的 `\\clip(m ... l ...)` 提取四边形（取前 4 个点作为 quad）
- 使用同一套 `PerspectiveTagsSolver` 解算并插入透视标签（与 `apply_tags_from_quad` 的输出一致/可对照）
- 不依赖 AE 数据（不需要 `ae_text` / `effect_group` / `frame`），适合把 `\\clip` 当作“手动/外部跟踪 quad 容器”

参数（`payload.args`）：

- `width` / `height` / `align` / `org_mode` / `layout_scale` / `precision_decimals` / `origin_x` / `origin_y`：同 `apply_tags_from_quad`

说明：这比 `\\clip` 更接近“真正的透视排版”，但仍依赖 ASS 渲染器对 3D 旋转/投影的实现（vsfilter/libass 的差异需要实测）。对于“任意四边形的精确形变（真正的 projective warp）”，更适合后续做 **绘图点集的 homography 变换**（或先文字转绘图再 warp）。

### `drawing.optimize_lines`

用途：对选中行中的 `\\p` 绘图进行“扁平化 + 可选缩点”，以达到：

- **vsfilter/libass 一致性更好**：输出只包含 `m/l`（不含 `b`），避免不同渲染器对贝塞尔 flatten 的差异
- **更短的绘图字符串**：可选 RDP（按误差阈值缩点）

实现细节：

- 是否为绘图行（`\\p`）使用 `Mobsub.SubtitleParse` 的 tag 解析判断（而不是手写扫描）

参数（`payload.args`）：

- `curve_tolerance`：曲线离散化容差（默认 `0.25`）
- `simplify_tolerance`：RDP 缩点容差（默认 `0.1`；设为 `0` 可关闭缩点）
- `precision_decimals`：坐标小数位数（默认 `0`，推荐用整数坐标提高一致性）

### `list_methods`

用于脚本侧探测 DLL 支持哪些方法（便于灰度/兼容旧版本）。成功时 `response[6]` 返回方法列表。

## Lua 侧用法（示例）

将 `mobsub_bridge.lua` 与 `mobsub_bridge_gen.lua` 放到 `automation/include`（或你自己的 include 路径），并确保 `MessagePack.lua` 与 `json` 模块可用；脚本中：

- `local mobsub = require("mobsub_bridge").load("Mobsub.AutomationBridge.dll")`
- `local w, h = mobsub.get_script_resolution(subtitles)`
- `local ctx = { script_resolution = { w = w, h = h } }`
- `local lines = mobsub.collect_selected_lines_with_frames_minimal(subtitles, selected_lines)`（或用 `collect_selected_lines`）
- `local code, resp = mobsub.invoke_method("motion.amo_apply", ctx, lines, { main_data=..., selection_start_frame=..., total_frames=..., frame_ms=..., main={ x_position=true, ... } })`
- `mobsub.apply_patch(subtitles, resp[4])`

约定：`payload.args` 的键名使用 `snake_case`（与其它 handler 参数保持一致）。

示例脚本（可直接拷贝到 Aegisub 的 `automation/autoload`，并确保 `mobsub_bridge.lua` + `mobsub_bridge_gen.lua` 在 `automation/include`）：

- `examples/mobsub.Aegisub-Motion.lua`：Aegisub-Motion 的 bridge 版（Apply/Revert/Fix motion；不含 trim/encoding）
- `examples/test_macro.lua`：开发/冒烟测试用的调用示例（不建议当成最终脚本使用）

## 构建（win-x64）

在仓库根目录执行（PowerShell）：

`Set-Location src\\SimpleTools\\AutomationBridge; dotnet publish -c Release -r win-x64`

## Debug 与踩坑总结（2026-02）

### Lua 侧调试（推荐工作流）

- 在脚本里用行首开关控制（例如 `local DEBUG=false`），发布时保持关闭。
- `mobsub_bridge.lua` 支持将请求前的 `method/context/lines/args` 以“可读化+截断”的 JSON 输出（避免二进制/超长文本写爆文件）：
  - `mobsub.DEBUG_JSON=true`
  - `mobsub.DEBUG_JSON_FILE="mobsub.*.last_call.json"`
- 当 `code=2 (ErrDecode)` 时，Lua glue 会在错误信息中附带 request/payload 的前缀 hex，便于快速确认请求是否为 `[schema_version, call]` 结构。

### MessagePack 反序列化失败（Unexpected msgpack code 0x90 / fixarray）

现象：Aegisub 弹窗 `mobsub error (code=2)`，提示 `Unexpected msgpack code 144 (fixarray) encountered.`

原因：Lua 侧把“空 table”编码成 `[]`（`fixarray(0)`），但 C# 模型字段类型是 map（例如 `Dictionary<string,string>`），期望 msgpack map，导致 `BridgeRequest` 反序列化失败。

处理：
- Lua pack 时 **空 dict 一律转 `nil`**（不要传 `{}`）。
- `mobsub_bridge_gen.lua` 是生成文件，不应手改；修复应落在生成器 `tools/AutomationBridgeProtocolGen`，并重新生成：
  - `dotnet run --project tools/AutomationBridgeProtocolGen/AutomationBridgeProtocolGen.csproj`

### Aegisub 的 `subtitles`/`line.extra` 不是普通 Lua table

现象：调用了 `ensure_extradata()` 但 `changed=0`，并且 dump/request 里看不到 `extra["a-mo"]`。

原因（常见于部分 Aegisub 构建）：
- `subtitles` 是 `userdata`（不是 table）；Lua glue 若用 `type(subtitles) ~= "table"` 守卫会直接 return。
- `line.extra` 可能是 `userdata/proxy`；不能通过“替换为新 table”的方式写入，否则会断开 Aegisub 内部 extradata 机制。

处理建议：
- glue 侧把 `subtitles` 视为 `table|userdata`，并用 `pcall` 保护索引读写。
- extradata 写入策略：优先在原 `extra` 上写入（`extra[key]=value`），失败再 fallback 为 table。
- `collect_selected_lines_*()` 是快照拷贝；如果要在请求里看到新写入的 `extra`，需要 `ensure_extradata()` 后重新 collect 一次 `lines`。

### `frame_ms`/时间字段的整数化

- C# 协议里 `frame_ms` 是 `int[]`（毫秒）；在 VFR 或某些环境下 `aegisub.ms_from_frame()` 可能返回非整数，Lua 侧需要在打包前做四舍五入到 int，否则会造成 decode/handler 行为不一致。

### `start_frame` 的相对/绝对语义（对齐 a-mo）

- `relative=true`：`start_frame` 是选区内相对帧（`1..total_frames`，`-1=last`）。
- `relative=false`：`start_frame` 是视频绝对帧号，会换算为选区相对帧：`start_frame - selection_start_frame + 1`。
- 常见 `code=1 (ErrBadArgs)`：`Out-of-range absolute start_frame (before selection)` 表示绝对模式下 `start_frame` 在选区之前（playhead 不在选区内也会导致默认值落在选区外）。

### “Track \\clip separately” 打开 clip 对话框崩溃/布局异常

原因：控件坐标/尺寸不匹配导致重叠（Aegisub UI 侧可能直接崩溃）。

处理：clip dialog 的 data 输入框建议保持与 a-mo 一致的尺寸（例如 `width=10,height=4`），避免覆盖后续控件。

输出的原生 DLL 通常位于：

`src\\SimpleTools\\AutomationBridge\\bin\\Release\\net10.0\\win-x64\\publish\\Mobsub.AutomationBridge.dll`

## 后续演进建议（两边都跑的最终形态）

- 经典版：继续使用本 ABI + blob request/patch
- Next：在 `Aegisub.Automation.LuaJit` 内置同名 Lua 模块（`package.preload["mobsub"]`），直接调用托管 handler（无 FFI）
- 统一的“请求/响应/patch”模型，让 Lua 生态脚本只写一份

## Codec（MessagePack，当前默认）

现阶段 bridge 的主要开销通常来自：`payload.lines`（大量文本与行字段）与 `frame_ms`（按帧时间戳数组）。因此优先的优化仍然是：Lua 侧只收集必要字段、只传用到的样式、避免传空参数、减少无意义 patch。

当前实现选择 MessagePack 作为主格式（封包 + MessagePack payload）：

- Lua 侧：使用 `_ref/automation/include/MessagePack.lua`（纯 Lua）提供 `MessagePack.pack/unpack`。
- C# 侧：使用 MessagePack-CSharp（source generator）+ 少量自定义 formatter（AOT 友好，无反射依赖）。
- 文本字段：优先传 `text_utf8` 与 `main_data/clip_data`（Lua 字符串原样传输），避免 JSON/base64 与重复转码；patch 侧同样支持 `text_utf8`。
- JSON：仅用于脚本侧配置/ASS extradata（以及必要时的调试输出），不再作为常规传输 codec。

## 后续优化（P0-P2）

- P0：只保留一种 on-wire：`MSB1` + MessagePack；JSON 不参与传输，仅用于 Lua 配置/调试。
- P1：patch 收敛为 `set_text` + `splice_template`（模板行 + 最小 delta），不再回传完整行结构；一步到位移除 `set_line` 与旧 `splice`。
- P2：ASS override tag 的解析/编辑/优化以 `src/SubtitleParse/AssText` 为权威实现，Bridge 侧只 orchestrate，避免维护第二套字符串扫描逻辑。
