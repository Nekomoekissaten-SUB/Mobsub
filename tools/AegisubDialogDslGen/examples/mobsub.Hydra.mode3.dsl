# Mobsub.Hydra mode-3-ish dialog (source for generation)
# Intended to be turned into a Lua `dlg` table via AegisubDialogDslGen.
# Notes:
# - Values starting with '@' are emitted as raw Lua (no quotes).
# - This file references helpers/constants that exist in `src/SimpleTools/AutomationBridge/examples/mobsub.Hydra.lua`.

columns=10

row:
  label label=@('Hydra v' .. tostring(script_version))
  checkbox name=fn_on label=\fn value=@(to_bool(ui.fn_on, false))
  edit name=fn value=@(to_string(ui.fn, '')) hint="Font name (optional)." w=5
  label "Special:"
  dropdown name=special items=@SPECIAL_ITEMS value=@(to_string(ui.special, 'sort_tags')) w=2

row:
  checkbox name=c_on label=\c value=@(to_bool(ui.c_on, false))
  coloralpha name=c value=@(normalize_coloralpha_picker_value(ui.c))
  checkbox name=bord_on label=\bord value=@(to_bool(ui.bord_on, false))
  floatedit name=bord value=@(to_num(ui.bord, 0)) w=2
  checkbox name=xbord_on label=\xbord value=@(to_bool(ui.xbord_on, false))
  floatedit name=xbord value=@(to_num(ui.xbord, 0)) w=2
  checkbox name=alpha_on label=\alpha value=@(to_bool(ui.alpha_on, false))
  dropdown name=alpha items=@ALPHA_HEX value=@(hex2(to_string(ui.alpha, '00')))

row:
  checkbox name=c2_on label=\2c value=@(to_bool(ui.c2_on, false))
  coloralpha name=c2 value=@(normalize_coloralpha_picker_value(ui.c2))
  checkbox name=shad_on label=\shad value=@(to_bool(ui.shad_on, false))
  floatedit name=shad value=@(to_num(ui.shad, 0)) w=2
  checkbox name=ybord_on label=\ybord value=@(to_bool(ui.ybord_on, false))
  floatedit name=ybord value=@(to_num(ui.ybord, 0)) w=2
  checkbox name=a1_on label=\1a value=@(to_bool(ui.a1_on, false))
  dropdown name=a1 items=@ALPHA_HEX value=@(hex2(to_string(ui.a1, '00')))

row:
  checkbox name=c3_on label=\3c value=@(to_bool(ui.c3_on, false))
  coloralpha name=c3 value=@(normalize_coloralpha_picker_value(ui.c3))
  checkbox name=blur_on label=\blur value=@(to_bool(ui.blur_on, false))
  floatedit name=blur value=@(to_num(ui.blur, 0.5)) w=2
  checkbox name=xshad_on label=\xshad value=@(to_bool(ui.xshad_on, false))
  floatedit name=xshad value=@(to_num(ui.xshad, 0)) w=2
  checkbox name=a2_on label=\2a value=@(to_bool(ui.a2_on, false))
  dropdown name=a2 items=@ALPHA_HEX value=@(hex2(to_string(ui.a2, '00')))

row:
  checkbox name=c4_on label=\4c value=@(to_bool(ui.c4_on, false))
  coloralpha name=c4 value=@(normalize_coloralpha_picker_value(ui.c4))
  checkbox name=be_on label=\be value=@(to_bool(ui.be_on, false))
  floatedit name=be value=@(to_num(ui.be, 1)) w=2
  checkbox name=yshad_on label=\yshad value=@(to_bool(ui.yshad_on, false))
  floatedit name=yshad value=@(to_num(ui.yshad, 0)) w=2
  checkbox name=a3_on label=\3a value=@(to_bool(ui.a3_on, false))
  dropdown name=a3 items=@ALPHA_HEX value=@(hex2(to_string(ui.a3, '00')))

row:
  checkbox name=r_on label=\r value=@(to_bool(ui.r_on, false))
  edit name=r_style value=@(to_string(ui.r_style, '')) hint="Optional style name."
  checkbox name=fs_on label=\fs value=@(to_bool(ui.fs_on, false))
  floatedit name=fs value=@(to_num(ui.fs, 50)) w=2
  checkbox name=fsp_on label=\fsp value=@(to_bool(ui.fsp_on, false))
  floatedit name=fsp value=@(to_num(ui.fsp, 0)) w=2
  checkbox name=a4_on label=\4a value=@(to_bool(ui.a4_on, false))
  dropdown name=a4 items=@ALPHA_HEX value=@(hex2(to_string(ui.a4, '00')))

row:
  checkbox name=an_on label=\an value=@(to_bool(ui.an_on, false))
  dropdown name=an items=["1","2","3","4","5","6","7","8","9"] value=@(tostring(to_int(ui.an, 7)))
  checkbox name=fscx_on label=\fscx value=@(to_bool(ui.fscx_on, false))
  floatedit name=fscx value=@(to_num(ui.fscx, 100)) w=2
  checkbox name=fscy_on label=\fscy value=@(to_bool(ui.fscy_on, false))
  floatedit name=fscy value=@(to_num(ui.fscy, 100)) w=2

row:
  checkbox name=q_on label=\q value=@(to_bool(ui.q_on, false))
  dropdown name=q items=["0","1","2","3"] value=@(tostring(to_int(ui.q, 2)))
  checkbox name=frz_on label=\frz value=@(to_bool(ui.frz_on, false))
  floatedit name=frz value=@(to_num(ui.frz, 0)) w=2
  checkbox name=frx_on label=\frx value=@(to_bool(ui.frx_on, false))
  floatedit name=frx value=@(to_num(ui.frx, 0)) w=2

row:
  checkbox name=b_on label=\b value=@(to_bool(ui.b_on, false))
  intedit name=b value=@(to_int(ui.b, 1))
  checkbox name=fry_on label=\fry value=@(to_bool(ui.fry_on, false))
  floatedit name=fry value=@(to_num(ui.fry, 0)) w=2
  checkbox name=fax_on label=\fax value=@(to_bool(ui.fax_on, false))
  floatedit name=fax value=@(to_num(ui.fax, 0)) w=2

row:
  checkbox name=i_on label=\i value=@(to_bool(ui.i_on, false))
  checkbox name=u_on label=\u value=@(to_bool(ui.u_on, false))
  checkbox name=s_on label=\s value=@(to_bool(ui.s_on, false))
  checkbox name=p_on label=\p value=@(to_bool(ui.p_on, false))
  intedit name=p value=@(to_int(ui.p, 0))
  checkbox name=fay_on label=\fay value=@(to_bool(ui.fay_on, false))
  floatedit name=fay value=@(to_num(ui.fay, 0)) w=2

row:
  label "Extra:"
  edit name=extra value=@(to_string(ui.extra, '')) hint="Optional raw tags payload (no braces)." w=5

row:
  label "Gradient:" w=2
  dropdown name=g_kind items=@GRAD_KINDS value=@(GRAD_KINDS[(to_int(g.kind, 0) + 1)] or 'vertical') w=2
  label "Stripe:"
  floatedit name=g_stripe value=@(to_num(g.stripe, 2)) w=2
  label "Acc:"
  floatedit name=g_accel value=@(to_num(g.accel, 1)) w=2

row:
  checkbox name=g_centered label=Ctr value=@(to_bool(g.centered, false)) hint="Centered gradient."
  checkbox name=g_hsl label=HSL value=@(to_bool(g.use_hsl, false))
  checkbox name=g_short label=Short value=@(to_bool(g.short_rotation, false)) hint="Short rotation path."
  spacer 1
  label "CharGrp:"
  intedit name=g_char_group value=@(to_int(g.char_group, 1)) min=1 w=2
  checkbox name=g_byline_last label=Last value=@(to_bool(g.by_line_use_last, false)) hint="ByLine: use last line as end values when possible."
  checkbox name=g_auto_clip label=Clip value=@(to_bool(g.auto_clip, false)) hint="Vertical/horizontal only. Auto-insert rectangular \\clip from outbox." w=2
