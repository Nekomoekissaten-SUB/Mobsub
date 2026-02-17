-- Example macro for classic Aegisub (LuaJIT).
--
-- NOTE (2026-02): `motion` 域仅暴露 `motion.amo_apply`（稳定主接口）。
-- This file keeps perspective/drawing smoke tests.
--
-- Setup:
--   1) Copy `mobsub_bridge.lua`, `mobsub_bridge_gen.lua`, and `MessagePack.lua` to `automation/include/`
--      (and ensure a `json` module is available for config/extradata).
--   2) Copy `Mobsub.AutomationBridge.dll` (native AOT dll) somewhere loadable.
--   3) Adjust `dll_name` below.

local mobsub = require("mobsub_bridge").load("Mobsub.AutomationBridge.dll")

script_name = "mobsub_bridge_examples"
script_description = "Examples for Mobsub.AutomationBridge"
script_author = "repo"
script_version = "0.0.1"

local function read_all(path)
  local f = assert(io.open(path, "rb"))
  local s = f:read("*a")
  f:close()
  return s
end

local function get_context_with_script_resolution(subtitles)
  local w, h = mobsub.get_script_resolution(subtitles)
  if not w or not h then
    return nil
  end
  return { script_resolution = { w = w, h = h } }
end

function perspective_apply_clip_quad(subtitles, selected_lines)
  local ae_text = read_all("motion_blender.txt")
  local ctx = get_context_with_script_resolution(subtitles)
  if not ctx then
    aegisub.log("script_resolution not found\n")
    return
  end

  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  local code, resp = mobsub.invoke_method(
    "perspective.apply_clip_quad",
    ctx,
    lines,
    { ae_text = ae_text, effect_group = "CC Power Pin #1" })
  if type(resp) ~= "table" or not resp[1] then
    aegisub.log(string.format("mobsub error (code=%d): %s\n", code, (type(resp) == "table" and resp[2]) or "no response"))
    return
  end

  mobsub.apply_patch(subtitles, resp[4])
end

function perspective_apply_tags_from_quad(subtitles, selected_lines)
  local ae_text = read_all("motion_blender.txt")

  -- NOTE: For text, you should usually pass a per-line width/height computed via Aegisub's text extents.
  -- This example uses a global default size for simplicity.
  local ctx = get_context_with_script_resolution(subtitles)
  if not ctx then
    aegisub.log("script_resolution not found\n")
    return
  end

  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  local code, resp = mobsub.invoke_method(
    "perspective.apply_tags_from_quad",
    ctx,
    lines,
    {
      ae_text = ae_text,
      effect_group = "CC Power Pin #1",
      width = 200,
      height = 50,
      align = 7,
      org_mode = 2,
      layout_scale = 1,
      precision_decimals = 3
    })
  if type(resp) ~= "table" or not resp[1] then
    aegisub.log(string.format("mobsub error (code=%d): %s\n", code, (type(resp) == "table" and resp[2]) or "no response"))
    return
  end

  mobsub.apply_patch(subtitles, resp[4])
end

function perspective_apply_tags_from_clip_quad(subtitles, selected_lines)
  -- Requires each selected line to have a \clip(m ... l ...) quad in the first override block.
  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  local code, resp = mobsub.invoke_method(
    "perspective.apply_tags_from_clip_quad",
    nil,
    lines,
    {
      -- NOTE: For text, you should usually pass a per-line width/height computed via Aegisub's text extents.
      width = 200,
      height = 50,
      align = 7,
      org_mode = 2,
      layout_scale = 1,
      precision_decimals = 3
    })
  if type(resp) ~= "table" or not resp[1] then
    aegisub.log(string.format("mobsub error (code=%d): %s\n", code, (type(resp) == "table" and resp[2]) or "no response"))
    return
  end

  mobsub.apply_patch(subtitles, resp[4])
end

function drawing_optimize_lines(subtitles, selected_lines)
  local lines = mobsub.collect_selected_lines_minimal(subtitles, selected_lines)
  local code, resp = mobsub.invoke_method(
    "drawing.optimize_lines",
    nil,
    lines,
    { curve_tolerance = 0.25, simplify_tolerance = 0.1, precision_decimals = 0 })
  if type(resp) ~= "table" or not resp[1] then
    aegisub.log(string.format("mobsub error (code=%d): %s\n", code, (type(resp) == "table" and resp[2]) or "no response"))
    return
  end

  mobsub.apply_patch(subtitles, resp[4])
end

aegisub.register_macro(script_name .. "/perspective.apply_clip_quad", "Insert \\clip(quad) from AE CC Power Pin", perspective_apply_clip_quad)
aegisub.register_macro(script_name .. "/perspective.apply_tags_from_quad", "Insert \\org/\\pos/\\fr*/\\fs*/\\fa* from AE quad", perspective_apply_tags_from_quad)
aegisub.register_macro(script_name .. "/perspective.apply_tags_from_clip_quad", "Insert \\org/\\pos/\\fr*/\\fs*/\\fa* from existing \\clip quad", perspective_apply_tags_from_clip_quad)
aegisub.register_macro(script_name .. "/drawing.optimize_lines", "Flatten/simplify \\p drawings (m/l only)", drawing_optimize_lines)
