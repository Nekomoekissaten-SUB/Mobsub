-- mobsub.Aegisub-Motion.lua
-- Aegisub-Motion (Bridge): keep Lua thin; delegate parsing/algorithms/batch logic to Mobsub.AutomationBridge (NativeAOT).

script_name        = "Mobsub.Aegisub-Motion"
script_description = "Apply AE Keyframe / shake_shape_data motion like Aegisub-Motion, but with a thin Lua bridge."
script_author      = "MIR"
script_version     = "0.0.4"
script_namespace   = "mobsub.Aegisub-Motion"

local clipboard = require("aegisub.clipboard")
local m = require("mobsub_bridge").load("Mobsub.AutomationBridge.dll")
-- NOTE: Copy `mobsub_bridge.lua`, `mobsub_bridge_gen.lua`, and `MessagePack.lua` into `automation/include`
-- (and ensure a `json` module is available for config/extradata).

local CONFIG_FILE = "mobsub.aegisub-motion.json"

local STATE_LOADED = false
local function can_run(_, selected)
  if not aegisub.frame_from_ms(0) then
    return false, "You must have a video loaded to run this macro."
  end
  if #selected == 0 then
    return false, "You must have lines selected to use this macro."
  end
  return true
end

local function maybe_motion_from_clipboard(bridge)
  if jit and jit.os == "Linux" then
    return ""
  end

  local ok, text = pcall(clipboard.get)
  if not ok or type(text) ~= "string" then
    return ""
  end

  if bridge.is_ae_keyframe_data(text) or bridge.is_shake_shape_data(text) then
    return text
  end

  return ""
end

local STATE_MAIN = {}
local STATE_CLIP = {}

local function load_state_once()
  if STATE_LOADED then
    return
  end
  STATE_LOADED = true

  local cfg = m.read_config(CONFIG_FILE)
  if type(cfg) ~= "table" then
    return
  end

  local function migrate_keys(t, map)
    if type(t) ~= "table" then
      return
    end
    for old, new in pairs(map) do
      if t[new] == nil and t[old] ~= nil then
        t[new] = t[old]
      end
    end
  end

  migrate_keys(cfg.main, {
    xPosition = "x_position",
    yPosition = "y_position",
    absPos = "abs_pos",
    xScale = "x_scale",
    blurScale = "blur_scale",
    zRotation = "z_rotation",
    startFrame = "start_frame",
    clipOnly = "clip_only",
    rectClip = "rect_clip",
    vectClip = "vect_clip",
    rcToVc = "rc_to_vc",
    killTrans = "kill_trans",
    round = "round_decimals",
    linearMode = "linear_mode",
    segmentPosEps = "segment_pos_eps",
    posErrorMode = "pos_error_mode",
  })
  migrate_keys(cfg.clip, {
    xPosition = "x_position",
    yPosition = "y_position",
    xScale = "x_scale",
    zRotation = "z_rotation",
    rectClip = "rect_clip",
    vectClip = "vect_clip",
    rcToVc = "rc_to_vc",
    startFrame = "start_frame",
  })

  if type(cfg.main) == "table" then
    STATE_MAIN = cfg.main
  end
  if type(cfg.clip) == "table" then
    STATE_CLIP = cfg.clip
  end
end

local function save_state(main_cfg, clip_cfg)
  local function pick(src, keys)
    local out = {}
    for _, k in ipairs(keys) do
      if src[k] ~= nil then
        out[k] = src[k]
      end
    end
    return out
  end

  local main_keys = {
    "fix", "diff", "round_decimals",
    "x_position", "y_position", "origin", "abs_pos",
    "x_scale", "border", "shadow", "blur", "blur_scale",
    "z_rotation",
    "relative", "start_frame",
    "linear_mode", "segment_pos_eps", "pos_error_mode",
    "clip_only",
    "rect_clip", "vect_clip", "rc_to_vc",
    "kill_trans",
  }
  local clip_keys = {
    "x_position", "y_position", "x_scale", "z_rotation",
    "rect_clip", "vect_clip", "rc_to_vc",
    "start_frame",
  }

  local payload = {
    v = 1,
    main = pick(main_cfg or {}, main_keys),
    clip = pick(clip_cfg or {}, clip_keys),
  }

  m.write_config(CONFIG_FILE, payload)
end

local function build_main_dialog(cfg)
  local function b(name, default)
    if cfg[name] == nil then return default end
    return cfg[name] and true or false
  end

  local function i(name, default)
    local v = cfg[name]
    if v == nil then return default end
    v = tonumber(v)
    return v ~= nil and v or default
  end

  local function s(name, default)
    local v = cfg[name]
    if v == nil then return default end
    return tostring(v)
  end

  return {
    { class = "label", x = 0, y = 0, width = 10, height = 1, label = "Paste data or enter a filepath." },
    { class = "textbox", x = 0, y = 1, width = 10, height = 4, name = "data", value = s("data", ""), hint = "AE Keyframe Data or shake_shape_data 4.0, or a file path." },

    { class = "checkbox", x = 0, y = 5, width = 2, height = 1, name = "fix", label = "&Fix AE", value = b("fix", false), hint = "Fix AE TSR Keyframe Data before applying." },
    { class = "label", x = 2, y = 5, width = 1, height = 1, label = "Th:" },
    { class = "floatedit", x = 3, y = 5, width = 2, height = 1, name = "diff", value = i("diff", 0.2), step = 0.05, min = 0, max = 1000 },
    { class = "label", x = 5, y = 5, width = 1, height = 1, label = "Rd:" },
    { class = "intedit", x = 6, y = 5, width = 1, height = 1, name = "round_decimals", value = i("round_decimals", 2), min = 0, max = 6 },

    { class = "checkbox", x = 0, y = 6, width = 1, height = 1, name = "x_position", label = "&x", value = b("x_position", true) },
    { class = "checkbox", x = 1, y = 6, width = 1, height = 1, name = "y_position", label = "&y", value = b("y_position", true) },
    { class = "checkbox", x = 2, y = 6, width = 2, height = 1, name = "origin", label = "&Origin", value = b("origin", false), hint = "Move \\org along with position." },
    { class = "checkbox", x = 4, y = 6, width = 2, height = 1, name = "abs_pos", label = "Absolut&e", value = b("abs_pos", false), hint = "Use tracking position as absolute (ignore line offset)." },

    { class = "checkbox", x = 0, y = 7, width = 2, height = 1, name = "x_scale", label = "&Scale", value = b("x_scale", true) },
    { class = "checkbox", x = 2, y = 7, width = 2, height = 1, name = "border", label = "&Border", value = b("border", true), hint = "Scale border (requires Scale)." },
    { class = "checkbox", x = 4, y = 7, width = 2, height = 1, name = "shadow", label = "&Shadow", value = b("shadow", true), hint = "Scale shadow (requires Scale)." },

    { class = "checkbox", x = 0, y = 8, width = 3, height = 1, name = "z_rotation", label = "&Rotation", value = b("z_rotation", false) },
    { class = "checkbox", x = 4, y = 8, width = 2, height = 1, name = "blur", label = "Bl&ur", value = b("blur", true), hint = "Scale blur (requires Scale; does not scale \\be)." },
    { class = "floatedit", x = 7, y = 8, width = 3, height = 1, name = "blur_scale", value = i("blur_scale", 1.0), step = 0.01, min = 0, max = 10 },

    { class = "checkbox", x = 0, y = 9, width = 3, height = 1, name = "rect_clip", label = "Rect C&lip", value = b("rect_clip", true) },
    { class = "checkbox", x = 3, y = 9, width = 3, height = 1, name = "vect_clip", label = "&Vect Clip", value = b("vect_clip", true) },
    { class = "checkbox", x = 6, y = 9, width = 4, height = 1, name = "rc_to_vc", label = "Rect -> Vect", value = b("rc_to_vc", false), hint = "Convert rectangular clips to vector clips." },

    { class = "checkbox", x = 0, y = 10, width = 10, height = 1, name = "kill_trans", label = "Interpolate &transforms", value = b("kill_trans", true), hint = "Try to interpolate transform values instead of only shifting times." },

    { class = "checkbox", x = 0, y = 11, width = 3, height = 1, name = "clip_only", label = "&Clip Only", value = b("clip_only", false), hint = "Only apply main data to clips in the line." },
    { class = "checkbox", x = 4, y = 11, width = 3, height = 1, name = "relative", label = "Relat&ive", value = b("relative", true), hint = "Start frame relative to selection start (not video start)." },
    { class = "intedit", x = 7, y = 11, width = 3, height = 1, name = "start_frame", value = i("start_frame", 1), hint = "Start frame (relative: 1..N, -1=last; absolute: video frame)." },

    { class = "label", x = 0, y = 12, width = 2, height = 1, label = "Linear:" },
    { class = "dropdown", x = 2, y = 12, width = 8, height = 1, name = "linear_mode", items = { "auto_linear_pos", "auto_segment_pos", "force_nonlinear", "force_linear" }, value = s("linear_mode", "auto_linear_pos"), hint = "Output strategy. Default auto_linear_pos." },

    { class = "label", x = 0, y = 13, width = 2, height = 1, label = "Seg eps:" },
    { class = "floatedit", x = 2, y = 13, width = 3, height = 1, name = "segment_pos_eps", value = i("segment_pos_eps", 0.0), step = 0.1, min = 0, max = 1000, hint = "auto_segment_pos only. <=0 => auto (min(resX,resY)/1000)." },
    { class = "label", x = 5, y = 13, width = 2, height = 1, label = "Err:" },
    { class = "dropdown", x = 7, y = 13, width = 3, height = 1, name = "pos_error_mode", items = { "full", "ignore_scale_rot" }, value = s("pos_error_mode", "full"), hint = "Error model for auto_* decisions (pos only)." },
  }
end

local function build_clip_dialog(cfg)
  local function b(name, default)
    if cfg[name] == nil then return default end
    return cfg[name] and true or false
  end

  local function i(name, default)
    local v = cfg[name]
    if v == nil then return default end
    v = tonumber(v)
    return v ~= nil and v or default
  end

  local function s(name, default)
    local v = cfg[name]
    if v == nil then return default end
    return tostring(v)
  end

  return {
    { class = "label", x = 0, y = 0, width = 10, height = 1, label = "This stuff is for clips." },
    { class = "textbox", x = 0, y = 1, width = 4, height = 20, name = "data", value = s("data", ""), hint = "AE Keyframe Data or shake_shape_data 4.0, or a file path." },

    { class = "label", x = 0, y = 5, width = 5, height = 1, label = "Data to be applied:" },
    { class = "checkbox", x = 0, y = 6, width = 1, height = 1, name = "x_position", label = "&x", value = b("x_position", true) },
    { class = "checkbox", x = 1, y = 6, width = 1, height = 1, name = "y_position", label = "&y", value = b("y_position", true) },
    { class = "checkbox", x = 0, y = 7, width = 2, height = 1, name = "x_scale", label = "&Scale", value = b("x_scale", true) },
    { class = "checkbox", x = 0, y = 8, width = 3, height = 1, name = "z_rotation", label = "&Rotation", value = b("z_rotation", false) },

    { class = "checkbox", x = 0, y = 10, width = 3, height = 1, name = "rect_clip", label = "Rect C&lip", value = b("rect_clip", true) },
    { class = "checkbox", x = 3, y = 10, width = 3, height = 1, name = "vect_clip", label = "&Vect Clip", value = b("vect_clip", true) },
    { class = "checkbox", x = 6, y = 10, width = 4, height = 1, name = "rc_to_vc", label = "Rect -> Vect", value = b("rc_to_vc", false) },

    { class = "label", x = 7, y = 5, width = 3, height = 1, label = "Start Frame:" },
    { class = "intedit", x = 7, y = 6, width = 3, height = 1, name = "start_frame", value = i("start_frame", 1), hint = "Relative: 1..N, -1=last; absolute: video frame (uses main Relative)." },
  }
end

local function apply(subtitles, selected)
  load_state_once()

  local sub_res_x, sub_res_y = m.get_script_resolution(subtitles)
  if not sub_res_x or not sub_res_y then
    m.dialog_error("Apply failed", "LayoutResX/LayoutResY/PlayResX/PlayResY not found in script info.")
    return
  end

  local lines = m.collect_selected_lines_with_frames_minimal(subtitles, selected)
  local selection_start_frame, _, total_frames = m.get_selection_frame_range(lines)
  if selection_start_frame == nil or total_frames == nil or total_frames <= 0 then
    m.dialog_error("Apply failed", "Failed to infer selection frame range (need valid start_time/end_time and video loaded).")
    return
  end

  local frame_ms = m.collect_frame_ms(selection_start_frame, total_frames)
  if not frame_ms then
    m.dialog_error("Apply failed", "Failed to collect frame timestamps (frame_ms).")
    return
  end

  local styles = m.collect_styles_used(subtitles, lines)
  if styles and not next(styles) then
    styles = nil
  end

  local pp = aegisub.project_properties() or {}
  local current_frame = tonumber(pp.video_position) or selection_start_frame

  local default_rel = current_frame - selection_start_frame + 1
  if default_rel < 1 then default_rel = 1 end
  if default_rel > total_frames then default_rel = total_frames end

  local defaults = {
    main = {
      data = maybe_motion_from_clipboard(m),
      fix = false,
      diff = 0.2,
      round_decimals = 2,
      x_position = true,
      y_position = true,
      origin = false,
      abs_pos = false,
      x_scale = true,
      border = true,
      shadow = true,
      blur = true,
      blur_scale = 1.0,
      z_rotation = false,
      relative = true,
      start_frame = default_rel,
      linear_mode = "auto_linear_pos",
      segment_pos_eps = 0.0,
      pos_error_mode = "full",
      clip_only = false,
      rect_clip = true,
      vect_clip = true,
      rc_to_vc = false,
      kill_trans = true,
    },
    clip = {
      data = "",
      x_position = true,
      y_position = true,
      x_scale = true,
      z_rotation = false,
      rect_clip = true,
      vect_clip = true,
      rc_to_vc = false,
      start_frame = default_rel,
    },
  }

  local cfg = {
    main = {},
    clip = {},
  }

  for k, v in pairs(defaults.main) do cfg.main[k] = v end
  for k, v in pairs(defaults.clip) do cfg.clip[k] = v end
  for k, v in pairs(STATE_MAIN) do cfg.main[k] = v end
  for k, v in pairs(STATE_CLIP) do cfg.clip[k] = v end

  if cfg.main.relative then
    if cfg.main.start_frame == nil then cfg.main.start_frame = default_rel end
    if cfg.clip.start_frame == nil then cfg.clip.start_frame = default_rel end
  else
    if cfg.main.start_frame == nil then cfg.main.start_frame = current_frame end
    if cfg.clip.start_frame == nil then cfg.clip.start_frame = current_frame end
  end

  local buttons = {
    main = {
      list = { "&Go", "Track &\\clip separately", "&Quit" },
      namedList = { ok = "&Go", clip = "Track &\\clip separately", cancel = "&Quit" },
    },
    clip = {
      list = { "&Go", "&Back", "&Quit" },
      namedList = { ok = "&Go", back = "&Back", cancel = "&Quit" },
    },
  }

  local current = "main"
  local opened_clip = false

  while true do
    local dlg = (current == "main") and build_main_dialog(cfg.main) or build_clip_dialog(cfg.clip)
    local btn, out = aegisub.dialog.display(dlg, buttons[current].list, buttons[current].namedList)

    if btn == false then
      aegisub.cancel()
      return
    end

    cfg[current] = out or {}

    if current == "main" then
      if btn == buttons.main.namedList.cancel then
        aegisub.cancel()
        return
      end
      if btn == buttons.main.namedList.clip then
        opened_clip = true
        current = "clip"
      else
        break
      end
    else
      if btn == buttons.clip.namedList.cancel then
        aegisub.cancel()
        return
      end
      if btn == buttons.clip.namedList.back then
        current = "main"
      else
        break
      end
    end
  end

  STATE_MAIN = cfg.main
  STATE_CLIP = cfg.clip

  save_state(cfg.main, cfg.clip)

  local main_data = m.read_text_or_file(cfg.main.data or "") or ""

  local clip_data = nil
  if opened_clip then
    clip_data = m.read_text_or_file(cfg.clip.data or "")
    if clip_data == "" then
      clip_data = nil
    end
  end

  local has_clip_data = (clip_data ~= nil and clip_data ~= "")

  if (main_data == nil or main_data == "") and (clip_data == nil or clip_data == "") then
    m.dialog_error("Apply failed", "No motion data provided.\nPaste data or provide a file path.")
    return
  end

  -- Write extradata to subtitles so the Revert macro can locate/merge lines.
  m.ensure_extradata(subtitles, selected, "a-mo")

  local context = {
    script_resolution = { w = sub_res_x, h = sub_res_y },
  }

  local LINEAR_MODE = {
    force_nonlinear = 0,
    force_linear = 1,
    auto_linear_pos = 2,
    auto_segment_pos = 3,
  }

  local POS_ERROR_MODE = {
    full = 0,
    ignore_scale_rot = 1,
  }

  local linear_mode = tostring(cfg.main.linear_mode or "auto_linear_pos")
  if linear_mode == "" then
    linear_mode = "auto_linear_pos"
  end
  local linear_mode_code = LINEAR_MODE[linear_mode] or 2
  local pos_error_mode = tostring(cfg.main.pos_error_mode or "full")
  local pos_error_mode_code = POS_ERROR_MODE[pos_error_mode] or 0
  local segment_pos_eps = tonumber(cfg.main.segment_pos_eps) or 0.0

  -- Prefer snake_case in args for consistency.
  local args = {
    main_data = main_data,
    clip_data = clip_data,
    selection_start_frame = selection_start_frame,
    total_frames = total_frames,
    frame_ms = frame_ms,
    styles = styles,
    fix = cfg.main.fix and {
      enabled = true,
      diff = tonumber(cfg.main.diff) or 0.2,
      round_decimals = tonumber(cfg.main.round_decimals) or 2,
      apply_main = true,
      apply_clip = has_clip_data and true or false,
    } or nil,
    main = {
      x_position = cfg.main.x_position and true or false,
      y_position = cfg.main.y_position and true or false,
      origin = cfg.main.origin and true or false,
      abs_pos = cfg.main.abs_pos and true or false,
      x_scale = cfg.main.x_scale and true or false,
      border = cfg.main.border and true or false,
      shadow = cfg.main.shadow and true or false,
      blur = cfg.main.blur and true or false,
      blur_scale = tonumber(cfg.main.blur_scale) or 1.0,
      z_rotation = cfg.main.z_rotation and true or false,
      relative = cfg.main.relative and true or false,
      start_frame = tonumber(cfg.main.start_frame) or 1,
      -- v2 options (new DLLs)
      linear_mode = linear_mode_code,
      segment_pos_eps = segment_pos_eps,
      pos_error_mode = pos_error_mode_code,
      clip_only = cfg.main.clip_only and true or false,
      rect_clip = cfg.main.rect_clip and true or false,
      vect_clip = cfg.main.vect_clip and true or false,
      rc_to_vc = cfg.main.rc_to_vc and true or false,
      kill_trans = cfg.main.kill_trans and true or false,
    },
    clip = has_clip_data and {
      x_position = cfg.clip.x_position and true or false,
      y_position = cfg.clip.y_position and true or false,
      x_scale = cfg.clip.x_scale and true or false,
      z_rotation = cfg.clip.z_rotation and true or false,
      rect_clip = cfg.clip.rect_clip and true or false,
      vect_clip = cfg.clip.vect_clip and true or false,
      rc_to_vc = cfg.clip.rc_to_vc and true or false,
      start_frame = tonumber(cfg.clip.start_frame) or 1,
    } or nil,
  }

  local code, resp = m.invoke_method("motion.amo_apply", context, lines, args)
  if type(resp) ~= "table" or not resp[1] then
    m.show_error(code, resp)
    return
  end

  m.apply_patch(subtitles, resp[4])

  local logs = resp[3]
  if type(logs) == "table" and #logs > 0 then
    aegisub.debug.out(0, table.concat(logs, "\n") .. "\n")
  end
end

local function revert(subtitles, selected)
  local changed = m.revert_by_extradata(subtitles, selected, "a-mo")
  -- aegisub.debug.out(0, "revert changed: " .. tostring(changed) .. "\n")
end

aegisub.register_macro(script_name .. "/Apply", "Apply motion data like Aegisub-Motion (Bridge; no trim/encoding).", apply, can_run)
aegisub.register_macro(script_name .. "/Revert", "Revert lines using extradata written by the bridge (key: a-mo).", revert)
