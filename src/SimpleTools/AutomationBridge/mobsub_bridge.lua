-- mobsub_bridge.lua
-- Minimal Lua glue for calling Mobsub.AutomationBridge (NativeAOT C API).
--
-- Requirements:
--   - LuaJIT (ffi)
--   - lua-MessagePack available as `require("MessagePack")` (for bridge transport)
--   - a JSON module available as `require("json")` (config + extradata only)

local ffi = require("ffi")
local msgpack = require("MessagePack")
local json = require("json")
local bridge_gen = require("mobsub_bridge_gen")

-- Use MessagePack arrays even when they have holes (needed for typed arrays with optional fields).
pcall(function() msgpack.set_array("with_hole") end)

ffi.cdef[[
  int mobsub_abi_version(void);
  void mobsub_free(void* p);
  int mobsub_invoke(const char* req, int reqLen, uint8_t** resp, int* respLen);
]]

local M = {}

local BRIDGE_MAGIC = "MSB1"

local function _wrap_envelope(payload)
  return BRIDGE_MAGIC .. payload
end

local function _unwrap_envelope(s)
  if type(s) ~= "string" then
    return nil, "response is not a string"
  end

  if #s >= 4 and s:sub(1, 4) == BRIDGE_MAGIC then
    return s:sub(5), nil
  end

  return nil, "Missing MSB1 envelope."
end

function M.load(dll_name)
  -- Example: "Mobsub.AutomationBridge.dll"
  M._dll = ffi.load(dll_name)
  return M
end

local function _encode_request(tbl)
  return _wrap_envelope(msgpack.pack(tbl))
end

local function _decode_response(s)
  local payload, env_err = _unwrap_envelope(s)
  if not payload then
    return { false, env_err }
  end

  local ok, obj = pcall(msgpack.unpack, payload)
  if ok and type(obj) == "table" then
    return obj
  end
  return { false, "msgpack decode failed: " .. tostring(obj) }
end

function M.dialog_error(title, message)
  aegisub.dialog.display({
    { class = "label", x = 0, y = 0, width = 70, height = 10, label = tostring(title) .. "\n\n" .. tostring(message) },
  }, { "OK" })
end

function M.invoke(req_tbl)
  if not M._dll then error("mobsub bridge dll not loaded") end

  local req_bytes = _encode_request(req_tbl)
  local req_len = #req_bytes

  local resp_ptr = ffi.new("uint8_t*[1]")
  local resp_len = ffi.new("int[1]")

  local code = M._dll.mobsub_invoke(req_bytes, req_len, resp_ptr, resp_len)
  local out = nil
  if resp_ptr[0] ~= nil and resp_len[0] > 0 then
    local resp_bytes = ffi.string(resp_ptr[0], resp_len[0])
    M._dll.mobsub_free(resp_ptr[0])
    out = _decode_response(resp_bytes)
  end

  return code, out
end

function M.show_error(code, resp)
  local err = (type(resp) == "table" and resp[2]) or "no response"
  M.dialog_error("mobsub error (code=" .. tostring(code) .. ")", err)
end

-- Bridge protocol (unified invoke):
--   [schema_version=1, call]
--
-- make_request (method -> typed union call) is generated from bridge_calls_spec.json (see mobsub_bridge_gen.lua).

local RNG_SEEDED = false
local function _random_hex32()
  if not RNG_SEEDED then
    RNG_SEEDED = true
    math.randomseed(os.time())
    math.random(); math.random(); math.random()
  end

  local t = {}
  for i = 1, 32 do
    t[i] = string.format("%x", math.random(0, 15))
  end
  return table.concat(t)
end

local function _shallow_copy_str_map(src)
  if type(src) ~= "table" then
    return nil
  end
  local out = {}
  for k, v in pairs(src) do
    if type(k) == "string" and type(v) == "string" then
      out[k] = v
    end
  end
  return out
end

local function _ensure_extradata_on_line(line, extra_key)
  if type(line) ~= "table" then
    return false
  end

  extra_key = extra_key or "a-mo"
  local extra = line.extra
  if type(extra) ~= "table" then
    extra = {}
    line.extra = extra
  end

  local existing = extra[extra_key]
  if type(existing) == "string" and existing ~= "" then
    return false
  end

  local uuid = _random_hex32()
  local original_text = ""
  if type(line.text) == "string" then
    original_text = line.text
  end

  extra[extra_key] = json.encode({
    uuid = uuid,
    original_text = original_text,
  })
  return true
end

-- Ensure extradata is written to subtitles (needed for revert_by_extradata).
function M.ensure_extradata(subtitles, selected_lines, extra_key)
  if type(subtitles) ~= "table" or type(selected_lines) ~= "table" then
    return 0
  end

  local changed = 0
  for _, idx in ipairs(selected_lines) do
    local line = subtitles[idx]
    if type(line) == "table" then
      if _ensure_extradata_on_line(line, extra_key) then
        changed = changed + 1
      end
      subtitles[idx] = line
    end
  end
  return changed
end

M.make_request = bridge_gen.make_request

function M.invoke_method(method, context, lines, args, schema_version)
  return M.invoke(M.make_request(method, context, lines, args, schema_version))
end

local _cached_methods_raw = nil
local _cached_method_set = nil

local function _build_method_set(methods)
  local set = {}
  if type(methods) ~= "table" then
    return set
  end

  for _, m in ipairs(methods) do
    if type(m) == "table" then
      local name = m[1]
      if type(name) == "string" and name ~= "" then
        set[name] = true
      end
    end
  end

  return set
end

function M.abi_version()
  if not M._dll then error("mobsub bridge dll not loaded") end
  return M._dll.mobsub_abi_version()
end

-- Returns: methods|nil, code, resp
function M.list_methods(schema_version)
  local code, resp = M.invoke_method("list_methods", nil, nil, nil, schema_version)
  if code ~= 0 or type(resp) ~= "table" or resp[1] ~= true then
    return nil, code, resp
  end

  return resp[6], code, resp
end

-- Refresh cached method list from DLL.
-- Returns: method_set|nil, code, resp
function M.refresh_methods(schema_version)
  local methods, code, resp = M.list_methods(schema_version)
  if methods == nil then
    _cached_methods_raw = nil
    _cached_method_set = nil
    return nil, code, resp
  end

  _cached_methods_raw = methods
  _cached_method_set = _build_method_set(methods)
  return _cached_method_set, code, resp
end

function M.has_method(name, schema_version)
  if type(name) ~= "string" or name == "" then
    return false
  end

  if _cached_method_set == nil then
    M.refresh_methods(schema_version)
  end

  return _cached_method_set ~= nil and _cached_method_set[name] == true
end

function M.require_method(name, schema_version)
  if not M.has_method(name, schema_version) then
    error("mobsub: method not supported by DLL: " .. tostring(name))
  end
end
-- Best-effort script resolution {w,h}.
-- Prefer LayoutRes, then PlayRes; finally try subtitles.script_resolution() when available.
function M.get_script_resolution(subtitles)
  local x, y = M.get_layoutres(subtitles)
  if x and y then
    return x, y
  end

  x, y = M.get_playres(subtitles)
  if x and y then
    return x, y
  end

  if subtitles and type(subtitles.script_resolution) == "function" then
    local ok, w, h = pcall(subtitles.script_resolution)
    if ok and type(w) == "number" and type(h) == "number" then
      return w, h
    end
    ok, w, h = pcall(subtitles.script_resolution, subtitles)
    if ok and type(w) == "number" and type(h) == "number" then
      return w, h
    end
  end

  return nil, nil
end

local function _copy_bridge_line_minimal(src, idx, opts)
  -- Keep fields that Aegisub will preserve on inserted lines (and what handlers commonly need).
  local line = {
    index = idx,
    class = src.class,
    layer = src.layer,
    start_time = src.start_time,
    end_time = src.end_time,
    start_frame = src.start_frame,
    end_frame = src.end_frame,
    style = src.style,
    actor = src.actor,
    margin_l = src.margin_l,
    margin_r = src.margin_r,
    margin_t = src.margin_t,
    effect = src.effect,
    comment = src.comment,
    extra = _shallow_copy_str_map(src.extra),
    width = src.width,
    height = src.height,
    align = src.align,
  }

  if type(src.text) == "string" then
    -- Send UTF-8 bytes (raw Lua string) via MessagePack.
    -- `text` may be omitted; handlers should prefer `text_utf8` when present.
    line.text_utf8 = src.text
  else
    line.text = src.text
  end

  if opts and opts.include_raw then
    line.raw = src.raw
  end

  return line
end

function M.collect_selected_lines_minimal(subtitles, selected_lines, opts)
  local out = {}
  for _, idx in ipairs(selected_lines) do
    local src = subtitles[idx]
    out[#out + 1] = _copy_bridge_line_minimal(src, idx, opts)
  end
  return out
end

function M.collect_selected_lines_with_frames_minimal(subtitles, selected_lines, opts)
  local out = M.collect_selected_lines_minimal(subtitles, selected_lines, opts)
  for _, l in ipairs(out) do
    if l.start_time and l.end_time then
      l.start_frame = aegisub.frame_from_ms(l.start_time)
      l.end_frame = aegisub.frame_from_ms(l.end_time)
    end
  end
  return out
end

function M.collect_selected_lines(subtitles, selected_lines)
  local out = {}
  for _, idx in ipairs(selected_lines) do
    local src = subtitles[idx]
    local line = {}
    for k, v in pairs(src) do
      line[k] = v
    end
    line.index = idx
    out[#out + 1] = line
  end
  return out
end

function M.collect_selected_lines_with_frames(subtitles, selected_lines)
  local out = M.collect_selected_lines(subtitles, selected_lines)
  for _, l in ipairs(out) do
    if l.start_time and l.end_time then
      l.start_frame = aegisub.frame_from_ms(l.start_time)
      l.end_frame = aegisub.frame_from_ms(l.end_time)
    end
  end
  return out
end

function M.get_playres(subtitles)
  local x, y
  for i = 1, #subtitles do
    local line = subtitles[i]
    if line.class == "info" then
      if line.key == "PlayResX" then x = tonumber(line.value) end
      if line.key == "PlayResY" then y = tonumber(line.value) end
    elseif line.class == "dialogue" then
      break
    end
  end
  return x, y
end

function M.get_layoutres(subtitles)
  local x, y
  for i = 1, #subtitles do
    local line = subtitles[i]
    if line.class == "info" then
      if line.key == "LayoutResX" then x = tonumber(line.value) end
      if line.key == "LayoutResY" then y = tonumber(line.value) end
    elseif line.class == "dialogue" then
      break
    end
  end
  return x, y
end

function M.collect_styles(subtitles)
  local styles = {}
  for i = 1, #subtitles do
    local line = subtitles[i]
    if line.class == "style" then
      styles[line.name] = {
        align = tonumber(line.align) or 7,
        margin_l = tonumber(line.margin_l) or 0,
        margin_r = tonumber(line.margin_r) or 0,
        margin_t = tonumber(line.margin_t) or 0,
        scale_x = tonumber(line.scale_x) or 100,
        scale_y = tonumber(line.scale_y) or tonumber(line.scale_x) or 100,
        outline = tonumber(line.outline) or 0,
        shadow = tonumber(line.shadow) or 0,
        angle = tonumber(line.angle) or 0,
      }
    elseif line.class == "dialogue" then
      break
    end
  end
  return styles
end

local function _collect_styles_from_r_tags(text, needed)
  if type(text) ~= "string" or text == "" then
    return
  end

  local i = 1
  while true do
    local b = text:find("{", i, true)
    if not b then
      break
    end

    local e = text:find("}", b + 1, true)
    if not e then
      break
    end

    local block = text:sub(b + 1, e - 1)
    local p = 1
    while true do
      local rpos = block:find("\\r", p, true)
      if not rpos then
        break
      end

      local name_start = rpos + 2
      if name_start <= #block then
        local ch = block:sub(name_start, name_start)
        -- "\r" alone means reset to current line style.
        if ch ~= "\\" and ch ~= "" then
          local next_slash = block:find("\\", name_start, true)
          local name = next_slash and block:sub(name_start, next_slash - 1) or block:sub(name_start)
          if type(name) == "string" and name ~= "" then
            needed[name] = true
          end
        end
      end

      p = rpos + 2
    end

    i = e + 1
  end
end

-- Collect only the style definitions actually referenced by the given bridge lines.
-- This reduces request payload size when the script has many styles.
function M.collect_styles_used(subtitles, bridge_lines)
  local needed = {}
  if bridge_lines then
    for _, l in ipairs(bridge_lines) do
      local name = l.style
      if type(name) == "string" and name ~= "" then
        needed[name] = true
      end

      _collect_styles_from_r_tags(l.text_utf8 or l.text, needed)
    end
  end

  if not next(needed) then
    return {}
  end

  local styles = {}
  for i = 1, #subtitles do
    local line = subtitles[i]
    if line.class == "style" then
      local name = line.name
      if needed[name] then
        styles[name] = {
          align = tonumber(line.align) or 7,
          margin_l = tonumber(line.margin_l) or 0,
          margin_r = tonumber(line.margin_r) or 0,
          margin_t = tonumber(line.margin_t) or 0,
          scale_x = tonumber(line.scale_x) or 100,
          scale_y = tonumber(line.scale_y) or tonumber(line.scale_x) or 100,
          outline = tonumber(line.outline) or 0,
          shadow = tonumber(line.shadow) or 0,
          angle = tonumber(line.angle) or 0,
        }
      end
    elseif line.class == "dialogue" then
      break
    end
  end
  return styles
end

function M.read_all_file(path)
  if type(path) ~= "string" or path == "" then
    return nil
  end
  local f = io.open(path, "rb")
  if not f then
    return nil
  end
  local c = f:read("*a")
  f:close()
  return c
end

function M.get_config_paths(filename)
  local sep = package.config:sub(1, 1)
  local out = {}

  -- Prefer Aegisub data root (typically next to aegisub.exe in portable setups).
  local ok_data_root, data_root = pcall(aegisub.decode_path, "?data")
  if ok_data_root and type(data_root) == "string" and data_root ~= "" then
    out[#out + 1] = data_root .. sep .. filename
  end

  local ok_data, data_dir = pcall(aegisub.decode_path, "?data/config")
  if ok_data and type(data_dir) == "string" and data_dir ~= "" then
    local p = data_dir .. sep .. filename
    if #out == 0 or out[1] ~= p then
      out[#out + 1] = p
    end
  end

  local ok_user, user_dir = pcall(aegisub.decode_path, "?user/config")
  if ok_user and type(user_dir) == "string" and user_dir ~= "" then
    local p = user_dir .. sep .. filename
    if #out == 0 or out[1] ~= p then
      out[#out + 1] = p
    end
  end

  return out
end

function M.read_config(filename)
  local paths = M.get_config_paths(filename)
  for _, path in ipairs(paths) do
    local s = M.read_all_file(path)
    if type(s) == "string" and s ~= "" then
      local ok, obj = pcall(json.decode, s)
      if ok and type(obj) == "table" then
        return obj, path
      end
    end
  end
  return nil, nil
end

function M.write_config(filename, obj)
  if type(filename) ~= "string" or filename == "" then
    return false, nil
  end
  if type(obj) ~= "table" then
    return false, nil
  end

  local s = json.encode(obj)
  local paths = M.get_config_paths(filename)
  for _, path in ipairs(paths) do
    local f = io.open(path, "wb")
    if f then
      f:write(s)
      f:close()
      return true, path
    end
  end

  return false, nil
end

function M.is_ae_keyframe_data(s)
  return type(s) == "string" and s:match("^[%s\239\187\191]*Adobe After Effects 6%.0 Keyframe Data") ~= nil
end

function M.is_shake_shape_data(s)
  return type(s) == "string" and s:match("^[%s\239\187\191]*shake_shape_data 4%.0") ~= nil
end

function M.read_text_or_file(input)
  if type(input) ~= "string" then
    return nil
  end
  if M.is_ae_keyframe_data(input) or M.is_shake_shape_data(input) then
    return input
  end
  return M.read_all_file(input)
end

function M.collect_frame_ms(selection_start_frame, total_frames)
  if selection_start_frame == nil or selection_start_frame < 0 or total_frames == nil or total_frames <= 0 then
    return nil
  end
  local arr = {}
  for f = selection_start_frame, selection_start_frame + total_frames do
    arr[#arr + 1] = aegisub.ms_from_frame(f)
  end
  return arr
end

function M.get_selection_frame_range(lines)
  if type(lines) ~= "table" or #lines == 0 then
    return nil
  end

  local min_start = nil
  local max_end = nil

  for _, l in ipairs(lines) do
    local sf = l and l.start_frame
    local ef = l and l.end_frame
    if sf ~= nil then
      sf = tonumber(sf)
      if sf ~= nil then
        if min_start == nil or sf < min_start then min_start = sf end
      end
    end
    if ef ~= nil then
      ef = tonumber(ef)
      if ef ~= nil then
        if max_end == nil or ef > max_end then max_end = ef end
      end
    end
  end

  if min_start == nil or max_end == nil or max_end <= min_start then
    return nil
  end

  return min_start, max_end, (max_end - min_start)
end

local function _copy_table(src)
  if type(src) ~= "table" then
    return {}
  end
  local out = {}
  for k, v in pairs(src) do
    out[k] = v
  end
  return out
end

local function _bridge_line_template_to_sub_line(tpl)
  -- BridgeLineTemplate (wire): [class, layer, comment, style, actor, effect, margin_l, margin_r, margin_t, extra]
  if type(tpl) ~= "table" then
    return {}
  end
  return {
    class = tpl[1],
    layer = tpl[2],
    comment = tpl[3],
    style = tpl[4],
    actor = tpl[5],
    effect = tpl[6],
    margin_l = tpl[7],
    margin_r = tpl[8],
    margin_t = tpl[9],
    extra = tpl[10],
  }
end

local function _delete_range(subtitles, index, count)
  count = tonumber(count) or 0
  if count <= 0 then
    return
  end

  local del = {}
  for i = 0, count - 1 do
    del[#del + 1] = index + i
  end
  subtitles.delete(del)
end

local function _splice_template(subtitles, index, delete_count, templates, inserts)
  index = tonumber(index)
  if index == nil then
    return
  end

  -- template_id=0 refers to subtitles[index] (before deletion).
  local base_template = _copy_table(subtitles[index])

  local out_lines = {}
  if type(inserts) == "table" then
    for _, ins in ipairs(inserts) do
      if type(ins) == "table" then
        local template_id = tonumber(ins[1]) or 0
        local start_time = tonumber(ins[2]) or 0
        local end_time = tonumber(ins[3]) or start_time
        local text_utf8 = ins[4]

        local line
        if template_id == 0 then
          line = _copy_table(base_template)
        else
          local tpl = type(templates) == "table" and templates[template_id] or nil
          if type(tpl) == "table" then
            line = _bridge_line_template_to_sub_line(tpl)
          else
            line = _copy_table(base_template)
          end
        end

        if type(line.class) ~= "string" or line.class == "" then
          line.class = "dialogue"
        end

        line.start_time = start_time
        line.end_time = end_time
        line.text = type(text_utf8) == "string" and text_utf8 or ""

        out_lines[#out_lines + 1] = line
      end
    end
  end

  _delete_range(subtitles, index, delete_count)

  if #out_lines > 0 then
    local at = index
    for i = 1, #out_lines do
      subtitles.insert(at, out_lines[i])
      at = at + 1
    end
  end
end

function M.apply_patch(subtitles, patch)
  if type(patch) ~= "table" then return end

  local ops = patch[1]
  if type(ops) ~= "table" then return end

  for _, op in ipairs(ops) do
    if type(op) == "table" then
      local kind = op[1]
      local payload = op[2]

      -- BridgePatchOp (wire, union): [kind, payload]
      -- kind=0: BridgeSetTextPatchOp payload: [index, text_utf8]
      -- kind=1: BridgeSpliceTemplatePatchOp payload: [index, delete_count, templates, inserts]
      if kind == 0 and type(payload) == "table" then
        local index = tonumber(payload[1])
        local text_utf8 = payload[2]
        if index ~= nil then
          local line = subtitles[index]
          if type(text_utf8) == "string" then
            line.text = text_utf8
          end
          subtitles[index] = line
        end
      elseif kind == 1 and type(payload) == "table" then
        _splice_template(subtitles, payload[1], payload[2], payload[3], payload[4])
      end
    end
  end
end

function M.revert_by_extradata(subtitles, selected_lines, extra_key)
  extra_key = extra_key or "a-mo"

  local uuids = {}
  for _, idx in ipairs(selected_lines) do
    local line = subtitles[idx]
    if type(line.extra) == "table" and type(line.extra[extra_key]) == "string" then
      local ok, data = pcall(json.decode, line.extra[extra_key])
      if ok and type(data) == "table" and type(data.uuid) == "string" then
        uuids[data.uuid] = true
      end
    end
  end

  if not next(uuids) then
    return 0
  end

  local keep = {}
  local to_delete = {}

  for i = 1, #subtitles do
    local line = subtitles[i]
    if type(line.extra) == "table" and type(line.extra[extra_key]) == "string" then
      local ok, data = pcall(json.decode, line.extra[extra_key])
      if ok and type(data) == "table" and type(data.uuid) == "string" and uuids[data.uuid] then
        local uuid = data.uuid
        local original_text = data.originalText or data.original_text

        local k = keep[uuid]
        if not k then
          keep[uuid] = {
            index = i,
            start_time = line.start_time,
            end_time = line.end_time,
            original_text = original_text,
          }
        else
          if line.start_time < k.start_time then k.start_time = line.start_time end
          if line.end_time > k.end_time then k.end_time = line.end_time end
          if i < k.index then
            to_delete[#to_delete + 1] = k.index
            k.index = i
          else
            to_delete[#to_delete + 1] = i
          end
        end
      end
    end
  end

  local changed = 0
  for _, k in pairs(keep) do
    local idx = k.index
    local line = subtitles[idx]
    if type(k.original_text) == "string" then
      line.text = k.original_text
    end
    line.start_time = k.start_time
    line.end_time = k.end_time
    if type(line.extra) == "table" then
      line.extra[extra_key] = nil
    end
    subtitles[idx] = line
    changed = changed + 1
  end

  if #to_delete > 0 then
    subtitles.delete(to_delete)
  end

  return changed
end

return M
