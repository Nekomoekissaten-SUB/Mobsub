# Mobsub

库 | 类型 | 主要功能
--- | --- | ---
SubtitleParse | 类库 | 读取和写入字幕文件
Ikkoku | 控制台应用程序 | 字幕相关的处理工具
ZhConvert | 类库 | 中文繁体化
Test | 控制台应用程序 | 测试用

## SubtitleParse

命名空间：`Mobsub.SubtitleParse`、`Mobsub.AssTypes`

目前支持的字幕类型：ass

### Ass

1. 读取、解析、写入常规 4.00+ 样式版本的 ass 文件，支持的部分：
    - Script Info
    - V4+ Styles（旧版本需要确认，还有个别 format 可能有兼容性问题）
    - Events
    - Fonts
    - Graphics
    - Aegisub Project Garbage
    - Aegisub Extradata
2. 提取 ass 中使用的字体和字形
3. 提取 ass 中使用的特效标签（overide tags）
4. 解析并转化 ass 中的内嵌字体和图片
    - 目前不支持转化字体为 ass 中的内嵌字体，因为还没搞懂使用的name和bold/italic/encoding判断依据

## Ikkoku

### CommandLine

目前支持的字幕类型：ass

#### clean（字幕清理）

1. 移除无用部分：目前是移除 `Fonts`、`Graphics`、`Aegisub Project Garbage`、`Aegisub Extradata`，暂时不支持自定义
2. `Script Info` 移除 `;` 起始的行，可以通过 `--keep-comments` 保留
3. `Script Info` 中的 `Tilte` 值改为 ass 的文件名，不含后缀，暂时不支持自定义
4. `Script Info` 添加 `LayoutResX/Y`，默认与 `PlayResX/Y` 值相同，需要指定 `--add-layoutres` 开启
5. 检查并记录 `Events` 中使用但 `Styles` 中未定义的样式
6. `Events` 中移除 `U+200E`、`U+200F`、`U+200B`，将 `U+00A0` 替换为 `U+0020`
7. `Events` 中清理 aegisub-motion 产生的多余字符
8. 注释并打印 `Events` 中开始时间大于结束时间的行
9. 如果使用 `--drop-unused-styles` 可以删除没有使用的样式，默认不开启
10. 如果使用 `--extract-binaries` 可以将 `Fonts`、`Graphics` 中的数据转回二进制文件，与 ass 输出路径同级目录
11. `preset` 可以指定预设，可以被其他选项覆盖
    - `Basic`: 启用 1，但不会移除 `Aegisub Extradata`
    - `More`：启用 1-8

#### check（字幕检查）

1. 通过 `--tag` 指定检查 VSFilterMod 标签、可能有问题的标签
2. 通过 `--style` 检查使用却未定义的样式，与 `clean` 的 5 检查范围相同
3. 默认检查是否有错误的时间戳、无用的字符，与 `clean` 的 6、8 检查范围相同

#### tpp（字幕时间处理）

多种模式不能同时使用

##### 平移

`--shift-by` 指定要平移的跨度，通过后缀分别，如果平移为帧时需要指定 `--fps`

`--shift-styles` 指定要平移的样式，默认只处理指定的样式，若第一个元素为 `!` 则不处理指定的样式

- 计划支持分时间段（结合章节文件）的平移（分样式的平移考虑功能优化）

##### 转换

根据 `--tcfile` 指定的符合 mkv timestamp v2 的 timecode 文件，将 vfr 字幕转为 cfr 字幕

#### merge（字幕混合拼接）

`--section` 指定要拼接的部分，只支持 Styles、Events

##### 直接拼接

需要指定 `--base`（基础文件）、`--merge`（要拼接的文件）

##### 根据配置拼接

可以在平移后再进行拼接，配置文件见下文的「merge 配置文件」

需要指定 `--config`（配置文件）、`--config-var`（配置文件的变量）

- `--config-var` 可以配置两个变量，第一个为集数（连续多集用 `:` 分割起止集），第二个为语言后缀

#### cjkpp（中日韩字形处理）

构建二进制字典可以使用子命令 `build-dict`。

目前实现的：

1. 基于 OpenCCSharp 类似于 OpenCC 的中文字形词汇转换

    - 忽略 ass style 中包含 JP（不区分大小写）的行（目前不支持处理 `\r` 指定的部分）
    - 转换后字符数量与转换前不同时，ovrride block 会插在原位置，且在控制台打印在文件中的行号和前后文字便于检查

2. 简易配置文件（与 OpenCCSharp 的相同）
    
    - 配置中的相对路径基于配置文件的路径
    - 计划增加一个包含多处理步骤的配置文件，可以针对不同的样式采用不同的处理

计划实现的：

1. 日文假名的全半角转换（基于 nkf？）
2. 修正常用错字，包括且不限于中文中误用的日文汉字

### Others

#### Mkv timestamp

目前只支持解析 v2 格式

#### merge 配置文件

`toml` 文件，有 v1、v2 两种格式

v1 示范样例：[Nekomoekissaten-Storage/Danseur/Subs
/danseur_op.yml](https://github.com/Nekomoekissaten-SUB/Nekomoekissaten-Storage/blob/e97e3f83bebe4ea6f6a02e5b0fe54b59859caea1/Danseur/Subs/danseur_op.yml)

v2 示范样例：[Nekomoekissaten-Storage/Summertime/Subs
/str_effect.yml](https://github.com/Nekomoekissaten-SUB/Nekomoekissaten-Storage/blob/e97e3f83bebe4ea6f6a02e5b0fe54b59859caea1/Summertime/Subs/str_effect.yml)

##### v2

1. `namef`、`tpl_fr`和 `shift_fr` 集数下的同一 key 表示同一文件
2. `shift_fr` 下的 key 代表集数，集数 key 下文件 key 的 value 是一个 int 数组，根据位置具有不同含义：
    
    1. 相对 `tpl_fr` 的偏移量（必有）
    2. 在视频中 `tpl_fr` 帧的帧号，如果这一帧不存在可以根据其他参考点计算偏移量，并记录该点（一般有，值为 -1 跳过解析）
    3. 如果不需要完整的文件合并进去，则填入一个开始需要合并的帧号，前面的部分会被截断（需要时有，值为 -1 跳过解析）

## ZhConvert

未定未来是否会支持[繁化姬](https://zhconvert.org/)

### OpenCCSharpUtils

基于受 [OpenCC](https://github.com/BYVoid/OpenCC) 启发由 CXuesong 开发的 [OpenCCSharp](https://github.com/CXuesong/OpenCCSharp)（License: Apache-2.0）。

1. 文本字典需要转换为二进制格式 `tris`，文本格式与 OpenCC 的文本相同，二进制与 OpenCC 的 `ocd` / `ocd2` 不兼容
2. 配置文件 `json` 定义转换使用的字典，每组 `Dictionaries` 有先后之分，内部顺序无区别
    - 与 OpenCC 的 json 不兼容，暂未支持 json 的转换