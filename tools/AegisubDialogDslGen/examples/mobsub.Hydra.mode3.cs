using static Mobsub.Tools.AegisubDialogDslGen.DialogDsl;
using static Mobsub.Tools.AegisubDialogDslGen.HydraDialogDsl;

// Mobsub.Hydra mode-3-ish dialog (C# DSL source for generation).
// Notes:
// - Lua(...) values are emitted as raw Lua (no quotes).
// - This script references helpers/constants that exist in `src/SimpleTools/AutomationBridge/examples/mobsub.Hydra.lua`.

var h = Hydra();

Dialog(
    10,
    Row(
        Label(Lua("('Hydra v' .. tostring(script_version))")),
        h.Check("fn_on", @"\fn"),
        h.Edit("fn", w: 5, hint: "Font name (optional)."),
        Label("Special:"),
        h.DropDownStr("special", itemsLua: "SPECIAL_ITEMS", defaultValue: "sort_tags", w: 2)),
    Row(
        h.Check("c_on", @"\c"),
        h.Color("c"),
        h.Check("bord_on", @"\bord"),
        h.Float("bord", 0, w: 2),
        h.Check("xbord_on", @"\xbord"),
        h.Float("xbord", 0, w: 2),
        h.Check("alpha_on", @"\alpha"),
        h.Alpha("alpha")),
    Row(
        h.Check("c2_on", @"\2c"),
        h.Color("c2"),
        h.Check("shad_on", @"\shad"),
        h.Float("shad", 0, w: 2),
        h.Check("ybord_on", @"\ybord"),
        h.Float("ybord", 0, w: 2),
        h.Check("a1_on", @"\1a"),
        h.Alpha("a1")),
    Row(
        h.Check("c3_on", @"\3c"),
        h.Color("c3"),
        h.Check("blur_on", @"\blur"),
        h.Float("blur", 0.5, w: 2),
        h.Check("xshad_on", @"\xshad"),
        h.Float("xshad", 0, w: 2),
        h.Check("a2_on", @"\2a"),
        h.Alpha("a2")),
    Row(
        h.Check("c4_on", @"\4c"),
        h.Color("c4"),
        h.Check("be_on", @"\be"),
        h.Float("be", 1, w: 2),
        h.Check("yshad_on", @"\yshad"),
        h.Float("yshad", 0, w: 2),
        h.Check("a3_on", @"\3a"),
        h.Alpha("a3")),
    Row(
        h.Check("r_on", @"\r"),
        h.Edit("r_style", hint: "Optional style name."),
        h.Check("fs_on", @"\fs"),
        h.Float("fs", 50, w: 2),
        h.Check("fsp_on", @"\fsp"),
        h.Float("fsp", 0, w: 2),
        h.Check("a4_on", @"\4a"),
        h.Alpha("a4")),
    Row(
        h.Check("an_on", @"\an"),
        h.DropDownIntToString("an", new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" }, defaultValue: 7),
        h.Check("fscx_on", @"\fscx"),
        h.Float("fscx", 100, w: 2),
        h.Check("fscy_on", @"\fscy"),
        h.Float("fscy", 100, w: 2)),
    Row(
        h.Check("q_on", @"\q"),
        h.DropDownIntToString("q", new[] { "0", "1", "2", "3" }, defaultValue: 2),
        h.Check("frz_on", @"\frz"),
        h.Float("frz", 0, w: 2),
        h.Check("frx_on", @"\frx"),
        h.Float("frx", 0, w: 2)),
    Row(
        h.Check("b_on", @"\b"),
        h.Int("b", 1),
        h.Check("fry_on", @"\fry"),
        h.Float("fry", 0, w: 2),
        h.Check("fax_on", @"\fax"),
        h.Float("fax", 0, w: 2)),
    Row(
        h.Check("i_on", @"\i"),
        h.Check("u_on", @"\u"),
        h.Check("s_on", @"\s"),
        h.Check("p_on", @"\p"),
        h.Int("p", 0),
        h.Check("fay_on", @"\fay"),
        h.Float("fay", 0, w: 2)),
    Row(
        Label("Extra:"),
        h.Edit("extra", hint: "Optional raw tags payload (no braces).", w: 5)),
    Row(
        Label("Gradient:", w: 2),
        h.GKind(w: 2),
        Label("Stripe:"),
        h.GFloat("g_stripe", 2, w: 2),
        Label("Acc:"),
        h.GFloat("g_accel", 1, w: 2)),
    Row(
        h.GCheck("g_centered", "Ctr", hint: "Centered gradient."),
        h.GCheck("g_hsl", "HSL", field: "use_hsl"),
        h.GCheck("g_short", "Short", field: "short_rotation", hint: "Short rotation path."),
        Spacer(1),
        Label("CharGrp:"),
        h.GInt("g_char_group", 1, field: "char_group", min: 1, w: 2),
        h.GCheck("g_byline_last", "Last", field: "by_line_use_last", hint: "ByLine: use last line as end values when possible."),
        h.GCheck("g_auto_clip", "Clip", field: "auto_clip", w: 2, hint: "Vertical/horizontal only. Auto-insert rectangular \\clip from outbox."))
)
