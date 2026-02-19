-- mobsub.Hydra.lua
-- Port of HYDRA (mode 3) + Script Cleanup's "remove specified tags" to Mobsub.AutomationBridge (.NET).
--
-- Setup (classic Aegisub LuaJIT):
--   1) Copy `mobsub_bridge.lua`, `mobsub_bridge_gen.lua`, and `MessagePack.lua` into `automation/include/`
--      (and ensure a `json` module is available for config).
--   2) Make `Mobsub.AutomationBridge.dll` (NativeAOT) loadable.

script_name        = "Mobsub.Hydra"
script_description = "HYDRA mode 3: tag tools + gradients (bridge)"
script_author      = "Mobsub"
script_version     = "0.0.5"
script_namespace   = "mobsub.Hydra"

local mobsub = require("mobsub_bridge").load("Mobsub.AutomationBridge.dll")

local CONFIG_FILE = "mobsub.hydra.json"

local DEFAULT_SORT_ORDER = [[\r\an\q\blur\be\fn\b\i\u\s\frz\fs\fscx\fscy\fad\fade\c\2c\3c\4c\alpha\1a\2a\3a\4a\bord\xbord\ybord\shad\xshad\yshad\fsp\frx\fry\fax\fay\org\pos\move\clip\iclip\p]]

local STATE = {
  v = 3,
  ui = {
    -- Common tags (mode 3-ish).
    c_on = false, c = "#FFFFFF00", -- \c
    c2_on = false, c2 = "#FFFFFF00", -- \2c
    c3_on = false, c3 = "#FFFFFF00", -- \3c
    c4_on = false, c4 = "#FFFFFF00", -- \4c

    alpha_on = false, alpha = "00", -- \alpha
    a1_on = false, a1 = "00", -- \1a
    a2_on = false, a2 = "00", -- \2a
    a3_on = false, a3 = "00", -- \3a
    a4_on = false, a4 = "00", -- \4a

    an_on = false, an = 7, -- \an

    bord_on = false, bord = 0,
    shad_on = false, shad = 0,
    fs_on = false, fs = 50,
    fsp_on = false, fsp = 0,
    blur_on = false, blur = 0.5,
    be_on = false, be = 1,
    fscx_on = false, fscx = 100,
    fscy_on = false, fscy = 100,

    xbord_on = false, xbord = 0,
    ybord_on = false, ybord = 0,
    xshad_on = false, xshad = 0,
    yshad_on = false, yshad = 0,
    frx_on = false, frx = 0,
    fry_on = false, fry = 0,
    frz_on = false, frz = 0,
    fax_on = false, fax = 0,
    fay_on = false, fay = 0,

    extra = "",

    special = "sort_tags",

    -- More tags from Hydra's sort order.
    r_on = false, r_style = "", -- \r or \rStyleName
    q_on = false, q = 2, -- \q0..3
    fn_on = false, fn = "", -- \fnFont Name
    b_on = false, b = 1, -- \b weight / 0 off / 1 on
    i_on = false, -- \i1
    u_on = false, -- \u1
    s_on = false, -- \s1
    p_on = false, p = 0, -- \pN
  },
  gradient = {
    kind = 2, -- 0=vertical,1=horizontal,2=by_char,3=by_line
    stripe = 2,
    accel = 1,
    centered = false,
    use_hsl = false,
    short_rotation = false,
    char_group = 1,
    by_line_use_last = false,
    auto_clip = true,
  },
  sort = {
    order = DEFAULT_SORT_ORDER,
  },
}

local STATE_LOADED = false

local function load_state_once()
  if STATE_LOADED then
    return
  end
  STATE_LOADED = true

  local cfg = mobsub.read_config(CONFIG_FILE)
  if type(cfg) ~= "table" then
    return
  end

  -- Migration / defaults across versions.
  local cfg_v = tonumber(cfg.v) or 0
  if cfg_v < 3 then
    cfg.gradient = type(cfg.gradient) == "table" and cfg.gradient or {}
    -- v2 defaulted to false; make vertical/horizontal gradients usable without manually adding \\clip.
    cfg.gradient.auto_clip = true
  end

  -- Migrate legacy config keys.
  if type(cfg.tags) == "string" and type(cfg.ui) ~= "table" then
    cfg.ui = cfg.ui or {}
    cfg.ui.extra = cfg.tags
  end

  if type(cfg.ui) == "table" then
    for k, v in pairs(cfg.ui) do
      STATE.ui[k] = v
    end
  end
  if type(cfg.gradient) == "table" then
    for k, v in pairs(cfg.gradient) do
      STATE.gradient[k] = v
    end
  end
  if type(cfg.sort) == "table" then
    for k, v in pairs(cfg.sort) do
      STATE.sort[k] = v
    end
  elseif type(cfg.sort_order) == "string" then
    STATE.sort.order = cfg.sort_order
  end
end

local function save_state()
  mobsub.write_config(CONFIG_FILE, STATE)
end

local function can_run(_, selected_lines)
  if type(selected_lines) ~= "table" or #selected_lines == 0 then
    return false, "You must have lines selected to use this macro."
  end
  return true
end

local function invoke_and_apply(method, subtitles, selected_lines, lines, args)
  local code, resp = mobsub.invoke_method(method, nil, lines, args)
  if type(resp) ~= "table" or not resp[1] then
    mobsub.show_error(code, resp)
    return false
  end
  mobsub.apply_patch(subtitles, resp[4])
  return true
end

local function trim_ascii_ws(s)
  if type(s) ~= "string" then
    return ""
  end
  return (s:gsub("^%s+", ""):gsub("%s+$", ""))
end

local function get_sort_order_from_config()
  local cfg = mobsub.read_config(CONFIG_FILE)
  if type(cfg) ~= "table" then
    return DEFAULT_SORT_ORDER
  end

  local v = nil
  if type(cfg.sort) == "table" and type(cfg.sort.order) == "string" then
    v = cfg.sort.order
  elseif type(cfg.sort_order) == "string" then
    v = cfg.sort_order
  end

  v = trim_ascii_ws(v)
  if v == "" then
    return DEFAULT_SORT_ORDER
  end
  return v
end

local function insert_into_first_block(text, payload)
  if type(text) ~= "string" or payload == "" then
    return text
  end
  if text:sub(1, 2) == "{\\" then
    local close = text:find("}", 3, true)
    if close then
      return text:sub(1, close - 1) .. payload .. text:sub(close)
    end
  end
  return "{" .. payload .. "}" .. text
end

local function has_rect_clip(text)
  if type(text) ~= "string" then
    return false
  end
  return text:match("\\i?clip%(([%d%.%-]+),([%d%.%-]+),([%d%.%-]+),([%d%.%-]+)")
end

local function get_script_resolution(subtitles)
  local w, h = mobsub.get_script_resolution(subtitles)
  if not w or not h then
    return nil, nil
  end
  return w, h
end

local function collect_style_records(subtitles)
  local styles = {}
  for i = 1, #subtitles do
    local line = subtitles[i]
    if line.class == "style" then
      styles[line.name] = line
    elseif line.class == "dialogue" then
      break
    end
  end
  return styles
end

local function parse_pos(text)
  local x, y = text:match("\\pos%(([%d%.%-]+),([%d%.%-]+)%)")
  if x then
    return tonumber(x), tonumber(y)
  end
  local mx, my = text:match("\\move%(([%d%.%-]+),([%d%.%-]+),")
  if mx then
    return tonumber(mx), tonumber(my)
  end
  return nil, nil
end

local function parse_an(text)
  local an = text:match("\\an([1-9])")
  return an and tonumber(an) or nil
end

local function compute_default_pos(line, styles, res_x, res_y, an)
  local st = styles[line.style]
  local ml = tonumber(line.margin_l) or 0
  local mr = tonumber(line.margin_r) or 0
  local mv = tonumber(line.margin_t) or 0
  if st then
    if ml == 0 then ml = tonumber(st.margin_l) or 0 end
    if mr == 0 then mr = tonumber(st.margin_r) or 0 end
    if mv == 0 then mv = tonumber(st.margin_t) or 0 end
  end

  local x
  if an == 1 or an == 4 or an == 7 then
    x = ml
  elseif an == 2 or an == 5 or an == 8 then
    x = res_x / 2
  else
    x = res_x - mr
  end

  local y
  if an == 7 or an == 8 or an == 9 then
    y = mv
  elseif an == 4 or an == 5 or an == 6 then
    y = res_y / 2
  else
    y = res_y - mv
  end

  return x, y
end

local function compute_outbox_clip(line, styles, res_x, res_y)
  local text = line.text_utf8 or line.text
  if type(text) ~= "string" or text == "" then
    return nil
  end

  local an = parse_an(text)
  if not an then
    local st = styles[line.style]
    an = st and tonumber(st.align) or 7
  end

  local x, y = parse_pos(text)
  if not x or not y then
    x, y = compute_default_pos(line, styles, res_x, res_y, an)
  end

  local st = styles[line.style]
  if type(st) ~= "table" then
    return nil
  end

  local w, h = aegisub.text_extents(st, text)
  w = tonumber(w) or 0
  h = tonumber(h) or 0
  if w <= 0 or h <= 0 then
    return nil
  end

  local x1, x2
  if an == 1 or an == 4 or an == 7 then
    x1, x2 = x, x + w
  elseif an == 2 or an == 5 or an == 8 then
    x1, x2 = x - w / 2, x + w / 2
  else
    x1, x2 = x - w, x
  end

  local y1, y2
  if an == 7 or an == 8 or an == 9 then
    y1, y2 = y, y + h
  elseif an == 4 or an == 5 or an == 6 then
    y1, y2 = y - h / 2, y + h / 2
  else
    y1, y2 = y - h, y
  end

  -- Expand by outline/shadow/blur to approximate rendered outbox for vertical/horizontal gradients.
  -- `text_extents` does not reliably account for these across builds.
  local close = nil
  if text:sub(1, 2) == "{\\" then
    close = text:find("}", 3, true)
  end
  local block = close and text:sub(1, close) or ""

  local function last_num(tag)
    if block == "" then
      return nil
    end
    local v = block:match(".*\\" .. tag .. "([%d%.%-]+)")
    if v == nil then
      return nil
    end
    return tonumber(v)
  end

  local outline = tonumber(st.outline) or 0
  if outline < 0 then outline = 0 end

  local bord = last_num("bord")
  local xbord = last_num("xbord")
  local ybord = last_num("ybord")

  local bx = xbord
  if bx == nil then bx = bord end
  if bx == nil then bx = outline end
  bx = tonumber(bx) or 0
  if bx < 0 then bx = 0 end

  local by = ybord
  if by == nil then by = bord end
  if by == nil then by = outline end
  by = tonumber(by) or 0
  if by < 0 then by = 0 end

  local style_shad = tonumber(st.shadow) or 0

  local shad = last_num("shad")
  local xshad = last_num("xshad")
  local yshad = last_num("yshad")

  local sx = xshad
  if sx == nil then sx = shad end
  if sx == nil then sx = style_shad end
  sx = tonumber(sx) or 0

  local sy = yshad
  if sy == nil then sy = shad end
  if sy == nil then sy = style_shad end
  sy = tonumber(sy) or 0

  local blur = last_num("blur") or 0
  blur = tonumber(blur) or 0
  if blur < 0 then blur = 0 end
  local be = last_num("be") or 0
  be = tonumber(be) or 0
  if be < 0 then be = 0 end

  local pad = blur + be
  local pad_x = bx + pad
  local pad_y = by + pad

  x1 = x1 - pad_x
  x2 = x2 + pad_x
  y1 = y1 - pad_y
  y2 = y2 + pad_y

  local sx1 = x1 + sx
  local sx2 = x2 + sx
  local sy1 = y1 + sy
  local sy2 = y2 + sy
  if sx1 < x1 then x1 = sx1 end
  if sx2 > x2 then x2 = sx2 end
  if sy1 < y1 then y1 = sy1 end
  if sy2 > y2 then y2 = sy2 end

  return string.format("\\clip(%d,%d,%d,%d)",
    math.floor(x1 + 0.0),
    math.floor(y1 + 0.0),
    math.ceil(x2 + 0.0),
    math.ceil(y2 + 0.0))
end

local ALPHA_HEX = { "00","10","20","30","40","50","60","70","80","90","A0","B0","C0","D0","E0","F0","F8","FF" }
local GRAD_KINDS = { "vertical", "horizontal", "by_char", "by_line" }
local SPECIAL_ITEMS = { "sort_tags", "convert_clip" }

local function to_bool(v, default)
  if v == nil then
    return default and true or false
  end
  return v and true or false
end

local function to_num(v, default)
  local n = tonumber(v)
  if n == nil then
    return default
  end
  if n ~= n or n == math.huge or n == -math.huge then
    return default
  end
  return n
end

local function to_int(v, default)
  local n = tonumber(v)
  if n == nil then
    return default
  end
  if n ~= n or n == math.huge or n == -math.huge then
    return default
  end
  n = math.floor(n + 0.0)
  return n
end

local function to_string(v, default)
  if v == nil then
    return default
  end
  return tostring(v)
end

local function hex2(s)
  if type(s) ~= "string" then
    return "00"
  end
  s = s:upper():gsub("[^0-9A-F]", "")
  if #s == 1 then
    return "0" .. s
  end
  if #s >= 2 then
    return s:sub(1, 2)
  end
  return "00"
end

local function rgba_to_ass_color(color)
  if type(color) ~= "string" then
    return nil
  end
  -- Aegisub colour controls return "#RRGGBB" (color) or "#RRGGBBAA" (coloralpha).
  local r, g, b = color:match("^#(%x%x)(%x%x)(%x%x)")
  if r then
    return "&H" .. b .. g .. r .. "&"
  end
  -- Allow already-ASS colours (best-effort).
  if color:match("^&H%x%x%x%x%x%x&$") then
    return color:upper()
  end
  return nil
end

local function parse_style_color(style_color)
  if type(style_color) ~= "string" then
    return nil, nil
  end

  -- Style colours are typically "&HAABBGGRR" (no trailing '&'), but accept various forms.
  local s = style_color:upper():gsub("%s+", "")
  s = s:gsub("&$", "")
  s = s:gsub("^&H", ""):gsub("^H", "")
  s = s:gsub("[^0-9A-F]", "")
  if #s == 0 then
    return nil, nil
  end

  if #s > 8 then
    s = s:sub(-8)
  end

  if #s == 6 then
    return s, "00"
  end
  if #s == 8 then
    return s:sub(3, 8), s:sub(1, 2)
  end

  return nil, nil
end

local function normalize_coloralpha_picker_value(s)
  if type(s) ~= "string" then
    return "#FFFFFF00"
  end

  local r, g, b, a = s:match("^#(%x%x)(%x%x)(%x%x)(%x%x)$")
  if r then
    return ("#" .. r .. g .. b .. a):upper()
  end

  r, g, b = s:match("^#(%x%x)(%x%x)(%x%x)$")
  if r then
    return ("#" .. r .. g .. b .. "00"):upper()
  end

  return "#FFFFFF00"
end

local function get_first_override_block(text)
  if type(text) ~= "string" then
    return ""
  end
  if text:sub(1, 2) ~= "{\\" then
    return ""
  end
  local close = text:find("}", 3, true)
  if not close then
    return ""
  end
  return text:sub(1, close)
end

local function ensure_gradient_start_tags(lines, subtitles, res, styles)
  if type(lines) ~= "table" or #lines == 0 or type(res) ~= "table" then
    return
  end

  local need_styles =
    res.c_on or res.c2_on or res.c3_on or res.c4_on or
    res.alpha_on or res.a1_on or res.a2_on or res.a3_on or res.a4_on or
    res.bord_on or res.shad_on or res.fs_on or res.fsp_on or res.fscx_on or res.fscy_on or
    res.frz_on or
    res.an_on

  if need_styles and styles == nil then
    styles = collect_style_records(subtitles)
  end

  for _, l in ipairs(lines) do
    local text = l.text_utf8
    if type(text) ~= "string" or text == "" then
      goto continue
    end

    local block = get_first_override_block(text)
    local st = (styles and l.style) and styles[l.style] or nil

    local parts = {}

    local function has_plain(needle)
      return block ~= "" and block:find(needle, 1, true) ~= nil
    end

    local function has_num(tag)
      if block == "" then
        return false
      end
      return block:match("\\" .. tag .. "[%d%.%-]") ~= nil
    end

    local function add_num(tag, n)
      if n == nil then
        return
      end
      n = tonumber(n)
      if n == nil then
        return
      end
      parts[#parts + 1] = "\\" .. tag .. tostring(n)
    end

    local function add_color(tag, style_color)
      local bgr, _ = parse_style_color(style_color)
      if not bgr then
        return
      end
      parts[#parts + 1] = "\\" .. tag .. "&H" .. bgr .. "&"
    end

    local function add_alpha(tag, style_color)
      local _, a = parse_style_color(style_color)
      if not a then
        return
      end
      parts[#parts + 1] = "\\" .. tag .. "&H" .. a .. "&"
    end

    -- Colours (use style defaults when missing).
    if res.c_on and not (has_plain("\\c&H") or has_plain("\\1c&H")) then
      add_color("c", st and st.color1 or nil)
    end
    if res.c2_on and not has_plain("\\2c&H") then
      add_color("2c", st and st.color2 or nil)
    end
    if res.c3_on and not has_plain("\\3c&H") then
      add_color("3c", st and st.color3 or nil)
    end
    if res.c4_on and not has_plain("\\4c&H") then
      add_color("4c", st and st.color4 or nil)
    end

    -- Alphas (derive from style colour alpha; best-effort).
    if res.alpha_on and not has_plain("\\alpha&H") then
      add_alpha("alpha", st and st.color1 or nil)
    end
    if res.a1_on and not has_plain("\\1a&H") then
      add_alpha("1a", st and st.color1 or nil)
    end
    if res.a2_on and not has_plain("\\2a&H") then
      add_alpha("2a", st and st.color2 or nil)
    end
    if res.a3_on and not has_plain("\\3a&H") then
      add_alpha("3a", st and st.color3 or nil)
    end
    if res.a4_on and not has_plain("\\4a&H") then
      add_alpha("4a", st and st.color4 or nil)
    end

    -- Common numeric defaults from style (others default to 0 and are fine as-is).
    if res.bord_on and not has_num("bord") then
      add_num("bord", st and st.outline or 0)
    end
    if res.xbord_on and not has_num("xbord") then
      add_num("xbord", 0)
    end
    if res.ybord_on and not has_num("ybord") then
      add_num("ybord", 0)
    end
    if res.shad_on and not has_num("shad") then
      add_num("shad", st and st.shadow or 0)
    end
    if res.xshad_on and not has_num("xshad") then
      add_num("xshad", 0)
    end
    if res.yshad_on and not has_num("yshad") then
      add_num("yshad", 0)
    end
    if res.fs_on and not has_num("fs") then
      add_num("fs", st and st.fontsize or nil)
    end
    if res.fsp_on and not has_num("fsp") then
      add_num("fsp", st and st.spacing or 0)
    end
    if res.blur_on and not has_num("blur") then
      add_num("blur", 0)
    end
    if res.be_on and not has_num("be") then
      add_num("be", 0)
    end
    if res.fscx_on and not has_num("fscx") then
      add_num("fscx", st and st.scale_x or 100)
    end
    if res.fscy_on and not has_num("fscy") then
      add_num("fscy", st and st.scale_y or 100)
    end
    if res.frz_on and not has_num("frz") then
      add_num("frz", st and st.angle or 0)
    end
    if res.frx_on and not has_num("frx") then
      add_num("frx", 0)
    end
    if res.fry_on and not has_num("fry") then
      add_num("fry", 0)
    end
    if res.fax_on and not has_num("fax") then
      add_num("fax", 0)
    end
    if res.fay_on and not has_num("fay") then
      add_num("fay", 0)
    end
    if res.an_on and block ~= "" and not block:match("\\an[1-9]") then
      local an = st and st.align or nil
      if an ~= nil then
        an = tonumber(an)
        if an and an >= 1 and an <= 9 then
          parts[#parts + 1] = "\\an" .. tostring(math.floor(an + 0.5))
        end
      end
    end

    if #parts > 0 then
      l.text_utf8 = insert_into_first_block(text, table.concat(parts))
    end

    ::continue::
  end
end

local function build_tags_payload(res)
  local parts = {}

  local ui = res or {}

  local function add_number(on_key, val_key, tag, default)
    if not ui[on_key] then
      return
    end
    local v = to_num(ui[val_key], default)
    parts[#parts + 1] = "\\" .. tag .. tostring(v)
  end

  local function add_color(on_key, val_key, tag)
    if not ui[on_key] then
      return
    end
    local c = rgba_to_ass_color(ui[val_key])
    if not c then
      return
    end
    parts[#parts + 1] = "\\" .. tag .. c
  end

  local function add_alpha(on_key, val_key, tag)
    if not ui[on_key] then
      return
    end
    local a = hex2(ui[val_key])
    parts[#parts + 1] = "\\" .. tag .. "&H" .. a .. "&"
  end

  local function add_bytes(on_key, val_key, tag)
    if not ui[on_key] then
      return
    end
    local s = trim_ascii_ws(to_string(ui[val_key], ""))
    parts[#parts + 1] = "\\" .. tag .. s
  end

  local function add_bool1(on_key, tag)
    if not ui[on_key] then
      return
    end
    parts[#parts + 1] = "\\" .. tag .. "1"
  end

  -- Hydra-ish order (subset).
  if ui.r_on then
    local rs = trim_ascii_ws(to_string(ui.r_style, ""))
    parts[#parts + 1] = "\\r" .. rs
  end

  if ui.an_on then
    local an = to_int(ui.an, 7)
    if an < 1 then an = 1 end
    if an > 9 then an = 9 end
    parts[#parts + 1] = "\\an" .. tostring(an)
  end

  if ui.q_on then
    local q = to_int(ui.q, 2)
    if q < 0 then q = 0 end
    if q > 3 then q = 3 end
    parts[#parts + 1] = "\\q" .. tostring(q)
  end

  add_number("blur_on", "blur", "blur", 0.5)
  add_number("be_on", "be", "be", 1)

  add_bytes("fn_on", "fn", "fn")
  add_number("b_on", "b", "b", 1)
  add_bool1("i_on", "i")
  add_bool1("u_on", "u")
  add_bool1("s_on", "s")

  add_number("frz_on", "frz", "frz", 0)
  add_number("fs_on", "fs", "fs", 50)
  add_number("fscx_on", "fscx", "fscx", 100)
  add_number("fscy_on", "fscy", "fscy", 100)

  add_color("c_on", "c", "c")
  add_color("c2_on", "c2", "2c")
  add_color("c3_on", "c3", "3c")
  add_color("c4_on", "c4", "4c")

  add_alpha("alpha_on", "alpha", "alpha")
  add_alpha("a1_on", "a1", "1a")
  add_alpha("a2_on", "a2", "2a")
  add_alpha("a3_on", "a3", "3a")
  add_alpha("a4_on", "a4", "4a")

  add_number("bord_on", "bord", "bord", 0)
  add_number("xbord_on", "xbord", "xbord", 0)
  add_number("ybord_on", "ybord", "ybord", 0)

  add_number("shad_on", "shad", "shad", 0)
  add_number("xshad_on", "xshad", "xshad", 0)
  add_number("yshad_on", "yshad", "yshad", 0)

  add_number("fsp_on", "fsp", "fsp", 0)
  add_number("frx_on", "frx", "frx", 0)
  add_number("fry_on", "fry", "fry", 0)
  add_number("fax_on", "fax", "fax", 0)
  add_number("fay_on", "fay", "fay", 0)

  if ui.p_on then
    local p = to_int(ui.p, 0)
    if p < 0 then p = 0 end
    parts[#parts + 1] = "\\p" .. tostring(p)
  end

  local extra = trim_ascii_ws(to_string(ui.extra, ""))
  if extra ~= "" then
    parts[#parts + 1] = extra
  end

  return table.concat(parts)
end

local function build_mode3_dialog()
  local ui = STATE.ui
  local g = STATE.gradient

  local dlg = {
    -- Match HYDRA mode 3 column style (x=0..9) to avoid wx sizing quirks.
    { x = 0, y = 0, class = "label", label = "Hydra v" .. tostring(script_version) },
    { x = 1, y = 0, class = "checkbox", name = "fn_on", label = "\\fn", value = to_bool(ui.fn_on, false) },
    { x = 2, y = 0, width = 5, class = "edit", name = "fn", value = to_string(ui.fn, ""), hint = "Font name (optional)." },
    { x = 7, y = 0, class = "label", label = "Special:" },
    { x = 8, y = 0, width = 2, class = "dropdown", name = "special", items = SPECIAL_ITEMS, value = to_string(ui.special, "sort_tags") },

    -- Colours + common tags.
    { x = 0, y = 1, class = "checkbox", name = "c_on", label = "\\c", value = to_bool(ui.c_on, false) },
    { x = 1, y = 1, class = "coloralpha", name = "c", value = normalize_coloralpha_picker_value(ui.c) },
    { x = 2, y = 1, class = "checkbox", name = "bord_on", label = "\\bord", value = to_bool(ui.bord_on, false) },
    { x = 3, y = 1, width = 2, class = "floatedit", name = "bord", value = to_num(ui.bord, 0) },
    { x = 5, y = 1, class = "checkbox", name = "xbord_on", label = "\\xbord", value = to_bool(ui.xbord_on, false) },
    { x = 6, y = 1, width = 2, class = "floatedit", name = "xbord", value = to_num(ui.xbord, 0) },
    { x = 8, y = 1, class = "checkbox", name = "alpha_on", label = "\\alpha", value = to_bool(ui.alpha_on, false) },
    { x = 9, y = 1, class = "dropdown", name = "alpha", items = ALPHA_HEX, value = hex2(to_string(ui.alpha, "00")) },

    { x = 0, y = 2, class = "checkbox", name = "c2_on", label = "\\2c", value = to_bool(ui.c2_on, false) },
    { x = 1, y = 2, class = "coloralpha", name = "c2", value = normalize_coloralpha_picker_value(ui.c2) },
    { x = 2, y = 2, class = "checkbox", name = "shad_on", label = "\\shad", value = to_bool(ui.shad_on, false) },
    { x = 3, y = 2, width = 2, class = "floatedit", name = "shad", value = to_num(ui.shad, 0) },
    { x = 5, y = 2, class = "checkbox", name = "ybord_on", label = "\\ybord", value = to_bool(ui.ybord_on, false) },
    { x = 6, y = 2, width = 2, class = "floatedit", name = "ybord", value = to_num(ui.ybord, 0) },
    { x = 8, y = 2, class = "checkbox", name = "a1_on", label = "\\1a", value = to_bool(ui.a1_on, false) },
    { x = 9, y = 2, class = "dropdown", name = "a1", items = ALPHA_HEX, value = hex2(to_string(ui.a1, "00")) },

    { x = 0, y = 3, class = "checkbox", name = "c3_on", label = "\\3c", value = to_bool(ui.c3_on, false) },
    { x = 1, y = 3, class = "coloralpha", name = "c3", value = normalize_coloralpha_picker_value(ui.c3) },
    { x = 2, y = 3, class = "checkbox", name = "blur_on", label = "\\blur", value = to_bool(ui.blur_on, false) },
    { x = 3, y = 3, width = 2, class = "floatedit", name = "blur", value = to_num(ui.blur, 0.5) },
    { x = 5, y = 3, class = "checkbox", name = "xshad_on", label = "\\xshad", value = to_bool(ui.xshad_on, false) },
    { x = 6, y = 3, width = 2, class = "floatedit", name = "xshad", value = to_num(ui.xshad, 0) },
    { x = 8, y = 3, class = "checkbox", name = "a2_on", label = "\\2a", value = to_bool(ui.a2_on, false) },
    { x = 9, y = 3, class = "dropdown", name = "a2", items = ALPHA_HEX, value = hex2(to_string(ui.a2, "00")) },

    { x = 0, y = 4, class = "checkbox", name = "c4_on", label = "\\4c", value = to_bool(ui.c4_on, false) },
    { x = 1, y = 4, class = "coloralpha", name = "c4", value = normalize_coloralpha_picker_value(ui.c4) },
    { x = 2, y = 4, class = "checkbox", name = "be_on", label = "\\be", value = to_bool(ui.be_on, false) },
    { x = 3, y = 4, width = 2, class = "floatedit", name = "be", value = to_num(ui.be, 1) },
    { x = 5, y = 4, class = "checkbox", name = "yshad_on", label = "\\yshad", value = to_bool(ui.yshad_on, false) },
    { x = 6, y = 4, width = 2, class = "floatedit", name = "yshad", value = to_num(ui.yshad, 0) },
    { x = 8, y = 4, class = "checkbox", name = "a3_on", label = "\\3a", value = to_bool(ui.a3_on, false) },
    { x = 9, y = 4, class = "dropdown", name = "a3", items = ALPHA_HEX, value = hex2(to_string(ui.a3, "00")) },

    { x = 2, y = 5, class = "checkbox", name = "fs_on", label = "\\fs", value = to_bool(ui.fs_on, false) },
    { x = 3, y = 5, width = 2, class = "floatedit", name = "fs", value = to_num(ui.fs, 50) },
    { x = 5, y = 5, class = "checkbox", name = "fsp_on", label = "\\fsp", value = to_bool(ui.fsp_on, false) },
    { x = 6, y = 5, width = 2, class = "floatedit", name = "fsp", value = to_num(ui.fsp, 0) },
    { x = 8, y = 5, class = "checkbox", name = "a4_on", label = "\\4a", value = to_bool(ui.a4_on, false) },
    { x = 9, y = 5, class = "dropdown", name = "a4", items = ALPHA_HEX, value = hex2(to_string(ui.a4, "00")) },

    { x = 0, y = 5, class = "checkbox", name = "r_on", label = "\\r", value = to_bool(ui.r_on, false) },
    { x = 1, y = 5, class = "edit", name = "r_style", value = to_string(ui.r_style, ""), hint = "Optional style name." },

    { x = 0, y = 6, class = "checkbox", name = "an_on", label = "\\an", value = to_bool(ui.an_on, false) },
    { x = 1, y = 6, class = "dropdown", name = "an", items = { "1","2","3","4","5","6","7","8","9" }, value = tostring(to_int(ui.an, 7)) },
    { x = 2, y = 6, class = "checkbox", name = "fscx_on", label = "\\fscx", value = to_bool(ui.fscx_on, false) },
    { x = 3, y = 6, width = 2, class = "floatedit", name = "fscx", value = to_num(ui.fscx, 100) },
    { x = 5, y = 6, class = "checkbox", name = "fscy_on", label = "\\fscy", value = to_bool(ui.fscy_on, false) },
    { x = 6, y = 6, width = 2, class = "floatedit", name = "fscy", value = to_num(ui.fscy, 100) },

    { x = 2, y = 7, class = "checkbox", name = "frz_on", label = "\\frz", value = to_bool(ui.frz_on, false) },
    { x = 3, y = 7, width = 2, class = "floatedit", name = "frz", value = to_num(ui.frz, 0) },
    { x = 5, y = 7, class = "checkbox", name = "frx_on", label = "\\frx", value = to_bool(ui.frx_on, false) },
    { x = 6, y = 7, width = 2, class = "floatedit", name = "frx", value = to_num(ui.frx, 0) },
    { x = 0, y = 7, class = "checkbox", name = "q_on", label = "\\q", value = to_bool(ui.q_on, false) },
    { x = 1, y = 7, class = "dropdown", name = "q", items = { "0","1","2","3" }, value = tostring(to_int(ui.q, 2)) },

    { x = 2, y = 8, class = "checkbox", name = "fry_on", label = "\\fry", value = to_bool(ui.fry_on, false) },
    { x = 3, y = 8, width = 2, class = "floatedit", name = "fry", value = to_num(ui.fry, 0) },
    { x = 5, y = 8, class = "checkbox", name = "fax_on", label = "\\fax", value = to_bool(ui.fax_on, false) },
    { x = 6, y = 8, width = 2, class = "floatedit", name = "fax", value = to_num(ui.fax, 0) },
    { x = 0, y = 8, class = "checkbox", name = "b_on", label = "\\b", value = to_bool(ui.b_on, false) },
    { x = 1, y = 8, class = "intedit", name = "b", value = to_int(ui.b, 1) },

    { x = 5, y = 9, class = "checkbox", name = "fay_on", label = "\\fay", value = to_bool(ui.fay_on, false) },
    { x = 6, y = 9, width = 2, class = "floatedit", name = "fay", value = to_num(ui.fay, 0) },
    { x = 0, y = 9, class = "checkbox", name = "i_on", label = "\\i", value = to_bool(ui.i_on, false) },
    { x = 1, y = 9, class = "checkbox", name = "u_on", label = "\\u", value = to_bool(ui.u_on, false) },
    { x = 2, y = 9, class = "checkbox", name = "s_on", label = "\\s", value = to_bool(ui.s_on, false) },
    { x = 3, y = 9, class = "checkbox", name = "p_on", label = "\\p", value = to_bool(ui.p_on, false) },
    { x = 4, y = 9, class = "intedit", name = "p", value = to_int(ui.p, 0) },

    -- Extra tags.
    { x = 0, y = 10, class = "label", label = "Extra:" },
    { x = 1, y = 10, width = 5, class = "edit", name = "extra", value = to_string(ui.extra, ""), hint = "Optional raw tags payload (no braces)." },

    -- Gradient settings.
    { x = 0, y = 11, width = 2, class = "label", label = "Gradient:" },
    { x = 2, y = 11, width = 2, class = "dropdown", name = "g_kind", items = GRAD_KINDS, value = GRAD_KINDS[(to_int(g.kind, 0) + 1)] or "vertical" },
    { x = 4, y = 11, class = "label", label = "Stripe:" },
    { x = 5, y = 11, width = 2, class = "floatedit", name = "g_stripe", value = to_num(g.stripe, 2) },
    { x = 7, y = 11, class = "label", label = "Acc:" },
    { x = 8, y = 11, width = 2, class = "floatedit", name = "g_accel", value = to_num(g.accel, 1) },

    { x = 0, y = 12, class = "checkbox", name = "g_centered", label = "Ctr", value = to_bool(g.centered, false), hint = "Centered gradient." },
    { x = 1, y = 12, class = "checkbox", name = "g_hsl", label = "HSL", value = to_bool(g.use_hsl, false) },
    { x = 2, y = 12, class = "checkbox", name = "g_short", label = "Short", value = to_bool(g.short_rotation, false), hint = "Short rotation path." },
    { x = 4, y = 12, class = "label", label = "CharGrp:" },
    { x = 5, y = 12, width = 2, class = "intedit", name = "g_char_group", value = to_int(g.char_group, 1), min = 1 },
    { x = 7, y = 12, class = "checkbox", name = "g_byline_last", label = "Last", value = to_bool(g.by_line_use_last, false), hint = "ByLine: use last line as end values when possible." },
    { x = 8, y = 12, width = 2, class = "checkbox", name = "g_auto_clip", label = "Clip", value = to_bool(g.auto_clip, false), hint = "Vertical/horizontal only. Auto-insert rectangular \\clip from outbox." },
  }

  -- Make width/height explicit everywhere (helps avoid wx sizing quirks on some builds).
  for i = 1, #dlg do
    local c = dlg[i]
    if c.width == nil then c.width = 1 end
    if c.height == nil then c.height = 1 end
  end

  return dlg
end

local function update_state_from_res(res)
  if type(res) ~= "table" then
    return
  end

  local ui = STATE.ui
  local g = STATE.gradient

  local function put_bool(k)
    ui[k] = res[k] and true or false
  end
  local function put_num(k, default)
    ui[k] = to_num(res[k], default)
  end
  local function put_str(k, default)
    ui[k] = to_string(res[k], default)
  end
  local function put_int(k, default)
    ui[k] = to_int(res[k], default)
  end

  put_bool("c_on"); put_str("c", "#FFFFFF00")
  put_bool("c2_on"); put_str("c2", "#FFFFFF00")
  put_bool("c3_on"); put_str("c3", "#FFFFFF00")
  put_bool("c4_on"); put_str("c4", "#FFFFFF00")

  put_bool("alpha_on"); put_str("alpha", "00")
  put_bool("a1_on"); put_str("a1", "00")
  put_bool("a2_on"); put_str("a2", "00")
  put_bool("a3_on"); put_str("a3", "00")
  put_bool("a4_on"); put_str("a4", "00")

  put_bool("r_on"); put_str("r_style", "")
  put_bool("an_on"); put_int("an", 7)
  put_bool("q_on"); put_int("q", 2)

  put_bool("bord_on"); put_num("bord", 0)
  put_bool("shad_on"); put_num("shad", 0)
  put_bool("fs_on"); put_num("fs", 50)
  put_bool("fsp_on"); put_num("fsp", 0)
  put_bool("blur_on"); put_num("blur", 0.5)
  put_bool("be_on"); put_num("be", 1)
  put_bool("fn_on"); put_str("fn", "")
  put_bool("b_on"); put_int("b", 1)
  put_bool("i_on")
  put_bool("u_on")
  put_bool("s_on")
  put_bool("fscx_on"); put_num("fscx", 100)
  put_bool("fscy_on"); put_num("fscy", 100)

  put_bool("xbord_on"); put_num("xbord", 0)
  put_bool("ybord_on"); put_num("ybord", 0)
  put_bool("xshad_on"); put_num("xshad", 0)
  put_bool("yshad_on"); put_num("yshad", 0)
  put_bool("frx_on"); put_num("frx", 0)
  put_bool("fry_on"); put_num("fry", 0)
  put_bool("frz_on"); put_num("frz", 0)
  put_bool("fax_on"); put_num("fax", 0)
  put_bool("fay_on"); put_num("fay", 0)
  put_bool("p_on"); put_int("p", 0)

  put_str("extra", "")
  put_str("special", "sort_tags")

  local kind_str = to_string(res.g_kind, "vertical")
  local kind_num = 0
  for i, k in ipairs(GRAD_KINDS) do
    if k == kind_str then
      kind_num = i - 1
      break
    end
  end

  g.kind = kind_num
  g.stripe = to_num(res.g_stripe, 2)
  if g.stripe <= 0 then g.stripe = 0.001 end
  g.accel = to_num(res.g_accel, 1)
  if g.accel <= 0 then g.accel = 0.001 end
  g.centered = res.g_centered and true or false
  g.use_hsl = res.g_hsl and true or false
  g.short_rotation = res.g_short and true or false
  g.char_group = to_int(res.g_char_group, 1)
  if g.char_group < 1 then g.char_group = 1 end
  g.by_line_use_last = res.g_byline_last and true or false
  g.auto_clip = res.g_auto_clip and true or false
end

local function hydra_mode3(subtitles, selected_lines)
  load_state_once()

  local dlg = build_mode3_dialog()
  local buttons = { "Add", "Remove (First)", "Remove (All)", "Gradient", "Special", "Cancel" }
  local btn, res = aegisub.dialog.display(dlg, buttons, { ok = "Add", cancel = "Cancel" })
  if btn == "Cancel" then
    return
  end

  update_state_from_res(res)
  save_state()

  if btn == "Special" then
    local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
    local which = to_string(res.special, "sort_tags")
    if which == "convert_clip" then
      invoke_and_apply("hydra.convert_clip", subtitles, selected_lines, lines, nil)
    else
      local order = to_string(STATE.sort and STATE.sort.order, "")
      order = trim_ascii_ws(order)
      if order == "" then
        order = DEFAULT_SORT_ORDER
      end
      invoke_and_apply("hydra.sort_tags", subtitles, selected_lines, lines, { order = order })
    end
    return
  end

  local tags = build_tags_payload(res)
  if tags == "" then
    mobsub.dialog_error("Mobsub.Hydra", "No tags selected.")
    return
  end

  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)

  if btn == "Add" then
    invoke_and_apply("hydra.add_tags", subtitles, selected_lines, lines, { tags = tags })
    return
  end
  if btn == "Remove (First)" then
    invoke_and_apply("hydra.remove_tags", subtitles, selected_lines, lines, { tags = tags, scope = 0 })
    return
  end
  if btn == "Remove (All)" then
    invoke_and_apply("hydra.remove_tags", subtitles, selected_lines, lines, { tags = tags, scope = 1 })
    return
  end

  if btn ~= "Gradient" then
    return
  end

  local kind_num = STATE.gradient.kind
  local styles = nil

  if STATE.gradient.auto_clip and (kind_num == 0 or kind_num == 1) then
    local res_x, res_y = get_script_resolution(subtitles)
    if not res_x or not res_y then
      mobsub.dialog_error("Mobsub.Hydra", "script_resolution not found; cannot auto-clip.")
      return
    end
    styles = collect_style_records(subtitles)

    for _, l in ipairs(lines) do
      if type(l.text_utf8) == "string" and not has_rect_clip(l.text_utf8) then
        local clip = compute_outbox_clip(l, styles, res_x, res_y)
        if clip then
          l.text_utf8 = insert_into_first_block(l.text_utf8, clip)
        end
      end
    end
  end

  ensure_gradient_start_tags(lines, subtitles, res, styles)

  invoke_and_apply("hydra.gradient", subtitles, selected_lines, lines, {
    tags = tags,
    kind = kind_num,
    stripe = STATE.gradient.stripe,
    accel = STATE.gradient.accel,
    centered = STATE.gradient.centered,
    use_hsl = STATE.gradient.use_hsl,
    short_rotation = STATE.gradient.short_rotation,
    char_group = STATE.gradient.char_group,
    by_line_use_last = STATE.gradient.by_line_use_last,
  })
end

local function hydra_sort_tags(subtitles, selected_lines)
  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  invoke_and_apply("hydra.sort_tags", subtitles, selected_lines, lines, { order = get_sort_order_from_config() })
end

local function hydra_convert_clip(subtitles, selected_lines)
  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  invoke_and_apply("hydra.convert_clip", subtitles, selected_lines, lines, nil)
end

aegisub.register_macro(script_name .. "/Mode 3", "HYDRA mode 3: tags/gradients + specials (sort/clip)", hydra_mode3, can_run)
aegisub.register_macro(script_name .. "/Sort Tags", "Sort tags using HYDRA order", hydra_sort_tags, can_run)
aegisub.register_macro(script_name .. "/Convert Clip", "Convert \\clip <-> \\iclip", hydra_convert_clip, can_run)
